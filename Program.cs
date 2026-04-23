using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.EntityFrameworkCore;
using StripUserIntegration.Models;
using Stripe;

var builder = WebApplication.CreateBuilder(args);

// JWT Settings
var jwtSettings = builder.Configuration.GetSection("Jwt");

// Add Authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme =
        JwtBearerDefaults.AuthenticationScheme;

    options.DefaultChallengeScheme =
        JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters =
        new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],

            IssuerSigningKey =
                new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwtSettings["Key"]))
        };

    // ✅ Custom Unauthorized JSON Response
    options.Events = new JwtBearerEvents
    {
        OnChallenge = context =>
        {
            context.HandleResponse();

            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";

            var result = System.Text.Json.JsonSerializer.Serialize(
                new
                {
                    status = 401,
                    message = "Unauthorized - Token is missing or invalid"
                });

            return context.Response.WriteAsync(result);
        },

        OnForbidden = context =>
        {
            context.Response.StatusCode = 403;
            context.Response.ContentType = "application/json";

            var result = System.Text.Json.JsonSerializer.Serialize(
                new
                {
                    status = 403,
                    message = "Forbidden - You do not have permission"
                });

            return context.Response.WriteAsync(result);
        }
    };
});

// Authorization
builder.Services.AddAuthorization();

// Controllers
builder.Services.AddControllersWithViews();

// Database
builder.Services.AddDbContext<StripeUserDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

// Stripe Key
StripeConfiguration.ApiKey =
    builder.Configuration["Stripe:SecretKey"];

var app = builder.Build();

// Middleware Order (VERY IMPORTANT)

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting(); // MUST be before Authentication

app.UseAuthentication(); // FIRST

app.UseAuthorization();  // SECOND

// Optional: Auto-create DB
/*
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider
        .GetRequiredService<StripeUserDbContext>();

    db.Database.Migrate();
}
*/

// Routing
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=User}/{action=Create}/{id?}");

app.MapControllers(); // Important for API controllers

app.Run();