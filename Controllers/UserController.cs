using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using StripUserIntegration.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;

[ApiController]
[Route("api/[controller]")]
public class UserController : Controller
{
    private readonly IConfiguration _configuration;
    private readonly StripeUserDbContext _dbContext;
    public UserController(IConfiguration configuration, StripeUserDbContext dbContext)
    {
        _configuration = configuration;
        _dbContext = dbContext;
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public IActionResult Login(StripUserIntegration.Models.LoginRequest model)
    {
        if (model.Username == "admin" &&
            model.Password == "12345")
        {
            var token = GenerateJwtToken(model.Username);

            return Ok(new
            {
                token = token
            });
        }

        return Unauthorized();
    }

    private string GenerateJwtToken(string username)
    {
        var jwtSettings = _configuration.GetSection("Jwt");

        // generate once, store value in config/secret store
        byte[] keyBytes = RandomNumberGenerator.GetBytes(32); // 256-bit
        string base64Key = Convert.ToBase64String(keyBytes);

        // Use at runtime (appsettings: Jwt:Key is base64Key)
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Key"]));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            expires: DateTime.Now.AddMinutes(60),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}