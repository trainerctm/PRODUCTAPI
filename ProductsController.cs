using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Azure.Storage.Blobs;
using System.IO;
using System.Diagnostics;
using ProductApi.Data;
using ProductApi.Models;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;

namespace ProductApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly TelemetryClient _telemetryClient;

        public ProductsController(AppDbContext context, IConfiguration configuration, TelemetryClient telemetryClient)
        {
            _context = context;
            _configuration = configuration;
            _telemetryClient = telemetryClient;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<IEnumerable<Product>>> GetProducts(
            [FromQuery] string? search,
            [FromQuery] decimal? minPrice,
            [FromQuery] decimal? maxPrice,
            [FromQuery] int? minStock,
            [FromQuery] int? maxStock)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var query = _context.Products.AsQueryable();
                if (!string.IsNullOrEmpty(search))
                    query = query.Where(p => p.Name.Contains(search));
                if (minPrice.HasValue)
                    query = query.Where(p => p.Price >= minPrice.Value);
                if (maxPrice.HasValue)
                    query = query.Where(p => p.Price <= maxPrice.Value);
                if (minStock.HasValue)
                    query = query.Where(p => p.Stock >= minStock.Value);
                if (maxStock.HasValue)
                    query = query.Where(p => p.Stock <= maxStock.Value);

                var products = await query.ToListAsync();
                return Ok(products);
            }
            catch (Exception ex)
            {
                // Log any exception that occurs during the GetProducts operation.
                _telemetryClient.TrackException(ex);
                throw;
            }
            finally
            {
                stopwatch.Stop();
                // Track the duration of the GetProducts operation.
                _telemetryClient.TrackMetric("GetProductsDuration", stopwatch.ElapsedMilliseconds);
            }
        }

        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<ActionResult<Product>> GetProduct(int id)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var product = await _context.Products.FindAsync(id);
                if (product == null)
                {
                    _telemetryClient.TrackEvent("GetProductNotFound", new Dictionary<string, string>
                    {
                        { "ProductId", id.ToString() }
                    });
                    return NotFound();
                }
                return Ok(product);
            }
            catch (Exception ex)
            {
                _telemetryClient.TrackException(ex);
                throw;
            }
            finally
            {
                stopwatch.Stop();
                _telemetryClient.TrackMetric("GetProductDuration", stopwatch.ElapsedMilliseconds);
            }
        }

        [HttpPost]
        [Authorize(Roles = "administrator")]
        public async Task<ActionResult<Product>> CreateProduct(Product product)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                _context.Products.Add(product);
                await _context.SaveChangesAsync();
                
                // Log a custom event for successful product creation.
                _telemetryClient.TrackEvent("ProductCreated", new Dictionary<string, string>
                {
                    { "ProductId", product.Id.ToString() },
                    { "ProductName", product.Name }
                });

                return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, product);
            }
            catch (Exception ex)
            {
                // Log any exceptions encountered during product creation.
                _telemetryClient.TrackException(ex);
                throw;
            }
            finally
            {
                stopwatch.Stop();
                // Track how long the CreateProduct operation took.
                _telemetryClient.TrackMetric("CreateProductDuration", stopwatch.ElapsedMilliseconds);
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "administrator")]
        public async Task<ActionResult> Delete(int id)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var product = await _context.Products.FindAsync(id);
                if (product == null)
                {
                    _telemetryClient.TrackEvent("DeleteProductNotFound", new Dictionary<string, string>
                    {
                        { "ProductId", id.ToString() }
                    });
                    return NotFound();
                }

                _context.Products.Remove(product);
                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception ex)
            {
                _telemetryClient.TrackException(ex);
                throw;
            }
            finally
            {
                stopwatch.Stop();
                _telemetryClient.TrackMetric("DeleteProductDuration", stopwatch.ElapsedMilliseconds);
            }
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "administrator, vendor")]
        public async Task<ActionResult> Update(int id, Product product)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                if (id != product.Id)
                    return BadRequest("ID mismatch");

                _context.Entry(product).State = EntityState.Modified;
                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception ex)
            {
                _telemetryClient.TrackException(ex);
                throw;
            }
            finally
            {
                stopwatch.Stop();
                _telemetryClient.TrackMetric("UpdateProductDuration", stopwatch.ElapsedMilliseconds);
            }
        }

        [HttpPost("{id}/upload-image")]
        [Authorize(Roles = "administrator, vendor")]
        public async Task<ActionResult> UploadImage(int id, IFormFile file)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var product = await _context.Products.FindAsync(id);
                if (product == null)
                {
                    _telemetryClient.TrackEvent("UploadImageFailed", new Dictionary<string, string>
                    {
                        { "Reason", "Product not found" },
                        { "ProductId", id.ToString() }
                    });
                    return NotFound();
                }

                var blobServiceClient = new BlobServiceClient(_configuration.GetConnectionString("AzureBlobStorage"));
                var containerClient = blobServiceClient.GetBlobContainerClient("product-images");
                await containerClient.CreateIfNotExistsAsync();

                string fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
                var blobClient = containerClient.GetBlobClient(fileName);
                using (var stream = file.OpenReadStream())
                {
                    await blobClient.UploadAsync(stream);
                }

                product.ImageUrl = blobClient.Uri.ToString();
                product.ImageFileName = fileName;
                await _context.SaveChangesAsync();

                // Log a custom event for a successful image upload.
                _telemetryClient.TrackEvent("ImageUploaded", new Dictionary<string, string>
                {
                    { "ProductId", id.ToString() },
                    { "ImageFileName", fileName }
                });

                return Ok(new { product.ImageUrl, product.ImageFileName });
            }
            catch (Exception ex)
            {
                // Track the exception if the image upload fails.
                _telemetryClient.TrackException(ex);
                throw;
            }
            finally
            {
                stopwatch.Stop();
                // Track the duration of the image upload process.
                _telemetryClient.TrackMetric("UploadImageDuration", stopwatch.ElapsedMilliseconds);
            }
        }
    }
}
