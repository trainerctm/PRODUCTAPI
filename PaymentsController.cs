using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Stripe;
using Stripe.Checkout;
using ProductApi.Data;
using ProductApi.Models;
using Microsoft.EntityFrameworkCore;

namespace ProductApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentsController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly AppDbContext _context;

        public PaymentsController(IConfiguration config, AppDbContext context)
        {
            _config = config;
            _context = context;

            // Set your Stripe secret key
            StripeConfiguration.ApiKey = _config["Stripe:SecretKey"];
        }

        // 1) CREATE A CHECKOUT SESSION AND ORDER
        // POST /api/payments/create-checkout-session
        [HttpPost("create-checkout-session")]
        [Authorize]
        public IActionResult CreateCheckoutSession([FromBody] PaymentRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            // 1) Get the user's current cart items
            var cartItems = _context.CartItems
                .Where(ci => ci.UserId == userId)
                .ToList();

            if (cartItems.Count == 0)
            {
                return BadRequest("Cart is empty.");
            }

            // 2) Create the new Order
            var order = new Order
            {
                UserId = userId,
                Status = "Awaiting Payment",
                CreatedAt = DateTime.UtcNow
            };

            // 3) Build OrderItems from the cart
            var orderItems = cartItems.Select(ci => new OrderItem
            {
                ProductId = ci.ProductId,
                Quantity = ci.Quantity,
                UnitPrice = ci.UnitPrice
            }).ToList();

            // 4) Attach them to the order
            order.OrderItems = orderItems;

            _context.Orders.Add(order);
            _context.SaveChanges();

            // (B) Convert the decimal amount to cents
            long amountInCents = (long)(request.Amount * 100);

            // (C) Build the Stripe session
            var options = new SessionCreateOptions
            {
                PaymentMethodTypes = new List<string> { "card" },
                LineItems = new List<SessionLineItemOptions>
        {
            new SessionLineItemOptions
            {
                PriceData = new SessionLineItemPriceDataOptions
                {
                    UnitAmount = amountInCents,
                    Currency = request.Currency,
                    ProductData = new SessionLineItemPriceDataProductDataOptions
                    {
                        Name = "E-Commerce Purchase"
                    }
                },
                Quantity = 1
            }
        },
                Mode = "payment",
                SuccessUrl = "https://localhost:7090/payment-success",
                CancelUrl = "https://localhost:7090/payment-cancel",
                ClientReferenceId = order.Id.ToString()
            };

            var service = new SessionService();
            var session = service.Create(options);

            // (E) Store the Stripe session ID in the order
            order.StripeSessionId = session.Id;
            _context.Orders.Update(order);
            _context.SaveChanges();

            return Ok(new PaymentResponse { PaymentUrl = session.Url });
        }


        // 2) WEBHOOK ENDPOINT
        // POST /api/payments/webhook
        [HttpPost("webhook")]
        [AllowAnonymous]
        public async Task<IActionResult> StripeWebhook()
        {
            var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
            var stripeSignature = Request.Headers["Stripe-Signature"];

            Event stripeEvent;
            try
            {
                // Verify the event signature
                stripeEvent = EventUtility.ConstructEvent(
                    json,
                    stripeSignature,
                    _config["Stripe:WebhookSecret"]
                );
            }
            catch (Exception e)
            {
                return BadRequest($"Webhook Error: {e.Message}");
            }

            // Compare string literals for event type
            if (stripeEvent.Type == "checkout.session.completed")
            {
                // Payment is successful
                var session = stripeEvent.Data.Object as Session;

                // Retrieve the order ID from ClientReferenceId
                if (session.ClientReferenceId != null)
                {
                    if (int.TryParse(session.ClientReferenceId, out int orderId))
                    {
                        // Load the order, including any OrderItems
                        var order = await _context.Orders
                            .Include(o => o.OrderItems)
                            .FirstOrDefaultAsync(o => o.Id == orderId);

                        // Verify the session matches
                        if (order != null && order.StripeSessionId == session.Id)
                        {
                            // 1) Set the order status to "Paid"
                            order.Status = "Paid";

                            // 2) Remove items from the cart (if they still exist)
                            //    This ensures the user won't see them in the cart anymore
                            var userCart = _context.CartItems
                                .Where(ci => ci.UserId == order.UserId)
                                .ToList();
                            _context.CartItems.RemoveRange(userCart);

                            // 3) Reduce product stock
                            //    If you stored order items in DB, loop them:
                            foreach (var item in order.OrderItems)
                            {
                                var product = await _context.Products.FindAsync(item.ProductId);
                                if (product != null)
                                {
                                    // Reduce the stock by the quantity purchased
                                    product.Stock = Math.Max(0, product.Stock - item.Quantity);
                                }
                            }

                            await _context.SaveChangesAsync();
                        }
                    }
                }
            }
            else if (stripeEvent.Type == "payment_intent.payment_failed")
            {
                // Payment failed
                // (Optional) handle a failed payment, e.g. mark order "Failed"
            }

            return Ok();
        }
    

        [HttpPost("create-checkout-session-nowebhook")]
        [Authorize]
        public IActionResult CreateCheckoutSessionNoWebhook([FromBody] PaymentRequest request)
        {
            // 1) Identify the user
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            // 2) Optionally create an order with "Awaiting Payment" 
            var order = new Order
            {
                UserId = userId,
                Status = "Awaiting Payment",
                CreatedAt = DateTime.UtcNow,
               
            };
            _context.Orders.Add(order);
            _context.SaveChanges();

            // 3) Build a Stripe session for the total
            long amountInCents = (long)(request.Amount * 100);

            var options = new SessionCreateOptions
            {
                PaymentMethodTypes = new List<string> { "card" },
                LineItems = new List<SessionLineItemOptions>
        {
            new SessionLineItemOptions
            {
                PriceData = new SessionLineItemPriceDataOptions
                {
                    UnitAmount = amountInCents,
                    Currency = request.Currency,
                    ProductData = new SessionLineItemPriceDataProductDataOptions
                    {
                        Name = "E-Commerce Purchase (No Webhook)"
                    }
                },
                Quantity = 1
            }
        },
                Mode = "payment",
                // 4) Instead of a webhook, rely on success redirect
                SuccessUrl = $"https://localhost:7090/payment-success?method=stripeclient&orderId={order.Id}",
                CancelUrl = "https://localhost:7090/payment-cancel?method=stripeclient"
            };

            var service = new SessionService();
            var session = service.Create(options);

            // 5) Save the Stripe Session ID
            order.StripeSessionId = session.Id;
            _context.Orders.Update(order);
            _context.SaveChanges();

            // Return the Checkout URL
            return Ok(new PaymentResponse { PaymentUrl = session.Url });
        }
    }

    // HELPER CLASSES FOR REQUEST/RESPONSE
    public class PaymentRequest
    {
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "usd";
    }

    public class PaymentResponse
    {
        public string PaymentUrl { get; set; } = string.Empty;
    }
}
