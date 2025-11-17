using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using System.Text;
using System.Text.Json;

namespace ThaiBev.Controllers
{
    [ApiController]
    [Route("/api/[controller]")]
    public class userController : ControllerBase
    {
        private readonly IWebHostEnvironment _env;
        private static readonly object _fileLock = new();

        public userController(IWebHostEnvironment env)
        {
            _env = env;
        }

        public record UserRecord(int userID, string username, string passwordHash, DateTime createAt, string createBy);
        public record LoginRequest(string username, string password);


        private string JsonPath => Path.Combine(_env.ContentRootPath, "Data", "user.json");

        [HttpPost("login")]
        public ActionResult<string> Login([FromBody] LoginRequest request)
        {
            var users = LoadUsers();
            if (users is null) return NotFound("UserName or Password not correct!!");

            var user = users.FirstOrDefault(u => string.Equals(u.username, request.username, StringComparison.OrdinalIgnoreCase));
            if (user is null) return Unauthorized();

            if (user.passwordHash == $"PLAINTEXT:{request.password}")
                return Ok(new { username = user.username });
            return Unauthorized();
        }

        private List<UserRecord>? LoadUsers()
        {
            if (!System.IO.File.Exists(JsonPath)) return null;
            var json = System.IO.File.ReadAllText(JsonPath);
            return JsonSerializer.Deserialize<List<UserRecord>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<UserRecord>();
        }

    }
}
