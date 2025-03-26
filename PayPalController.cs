using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ProductApi.Data;
using ProductApi.Models;

namespace ProductApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PayPalController : ControllerBase
    {
        private readonly AppDbContext _context;

        public PayPalController(AppDbContext context)
        {
            _context = context;
        }

        // POST /api/paypal/create-order
        // Creates an order from the user's cart, sets status = "Awaiting Payment"
        // DOES NOT remove cart items yet
        [HttpPost("create-order")]
        [Authorize]
        public async Task<IActionResult> CreatePayPalOrder()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized("User not authenticated.");

            // Get cart items
            var cartItems = await _context.CartItems
                .Where(ci => ci.UserId == userId)
                .ToListAsync();

            if (cartItems.Count == 0)
            {
                return BadRequest("Cart is empty. Cannot create an order.");
            }

            // Create a new order with status "Awaiting Payment"
            var order = new Order
            {
                UserId = userId,
                Status = "Awaiting Payment",
                CreatedAt = DateTime.UtcNow,
                // Build order items from the cart
                OrderItems = cartItems.Select(ci => new OrderItem
                {
                    ProductId = ci.ProductId,
                    Quantity = ci.Quantity,
                    UnitPrice = ci.UnitPrice
                }).ToList()
            };

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            // Return the new order ID to the client
            return Ok(order.Id);
        }
    }
}
