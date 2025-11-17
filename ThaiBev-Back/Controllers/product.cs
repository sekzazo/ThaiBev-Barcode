using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using System.Text;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;

namespace ThaiBev.Controllers
{
    [ApiController]
    [Route("/api/[controller]")]
    public class productController : ControllerBase
    {
        private readonly IWebHostEnvironment _env;
        private static readonly object _fileLock = new();

        public productController(IWebHostEnvironment env)
        {
            _env = env;
        }

        public record ProductRecord(int productID, string productName, string productCode, DateTime createAt, string createBy);
        public record CreateProductRequest(string productName, string productCode);
        public record UpdateProductRequest(string? productName, string? productCode);

        private string JsonPath => Path.Combine(_env.ContentRootPath, "Data", "product.json");

        [HttpGet]
        public ActionResult<IEnumerable<object>> GetAll()
        {
            var products = LoadProducts();
            if (products is null) return NotFound("product not found");
            return Ok(products.Select(p => new { p.productID, p.productName, p.productCode, p.createAt, p.createBy }));
        }

        [HttpGet("{id:int}")]
        public ActionResult<object> GetById(int id)
        {
            var products = LoadProducts();
            if (products is null) return NotFound("product.json not found");

            var product = products.FirstOrDefault(p => p.productID == id);
            if (product is null) return NotFound($"Product {id} not found");
            return Ok(product);
        }

        [HttpPost]
        public ActionResult<object> Create([FromBody] CreateProductRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.productName) || string.IsNullOrWhiteSpace(req.productCode))
                return BadRequest("productName and productCode required");

            var products = LoadProducts() ?? new List<ProductRecord>();
            if (products.Any(p => string.Equals(p.productCode, req.productCode, StringComparison.OrdinalIgnoreCase)))
                return Conflict("product already exists");

            int nextId = products.Count == 0 ? 1 : products.Max(p => p.productID) + 1;
            var now = DateTime.UtcNow;
            var newProduct = new ProductRecord(nextId, req.productName, req.productCode, now, "User");
            products.Add(newProduct);
            SaveProducts(products);
            return Created($"/product/{nextId}", new { newProduct.productID, newProduct.productName, newProduct.productCode });
        }

        [HttpPut("{id:int}")]
        public ActionResult<object> Update(int id, [FromBody] UpdateProductRequest req)
        {
            var products = LoadProducts();
            if (products is null) return NotFound($"product {id} not found");

            var idx = products.FindIndex(p => p.productID == id);
            if (idx < 0) return NotFound();

            var current = products[idx];
            var updated = current with
            {
                productName = req.productName ?? current.productName,
                productCode = req.productCode ?? current.productCode
            };

            if (!string.Equals(current.productCode, updated.productCode, StringComparison.OrdinalIgnoreCase) &&
                products.Any(p => p.productID != id && string.Equals(p.productCode, updated.productCode, StringComparison.OrdinalIgnoreCase)))
            {
                return Conflict("product already exists");
            }

            products[idx] = updated;
            SaveProducts(products);
            return Ok(new { updated.productID, updated.productName, updated.productCode });
        }

        [HttpDelete("{id:int}")]
        public IActionResult Delete(int id)
        {
            var products = LoadProducts();
            if (products is null) return NotFound("product.json not found");

            int removed = products.RemoveAll(p => p.productID == id);
            if (removed == 0) return NotFound();
            SaveProducts(products);
            return NoContent();
        }

        private List<ProductRecord>? LoadProducts()
        {
            if (!System.IO.File.Exists(JsonPath)) return null;
            var json = System.IO.File.ReadAllText(JsonPath);
            return JsonSerializer.Deserialize<List<ProductRecord>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<ProductRecord>();
        }

        private void SaveProducts(List<ProductRecord> products)
        {
            lock (_fileLock)
            {
                var json = JsonSerializer.Serialize(products, new JsonSerializerOptions { WriteIndented = true });
                Directory.CreateDirectory(Path.GetDirectoryName(JsonPath)!);
                System.IO.File.WriteAllText(JsonPath, json, Encoding.UTF8);
            }
        }
    }
}