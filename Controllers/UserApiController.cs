using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using StripUserIntegration.Models;

[Route("api/[controller]")]
[ApiController]
public class UserApiController : ControllerBase
{
    private readonly StripeUserDbContext _context;

    public UserApiController(StripeUserDbContext context)
    {
        _context = context;
    }

    [HttpPost("create")]
    public async Task<IActionResult> Create([FromBody] TblUser user)
    {
        try
        {
            if (user == null)
                return BadRequest();

            await _context.TblUsers.AddAsync(user);
            await _context.SaveChangesAsync();

            return Ok(new { message = "User Record saved", data = user });
        }
        catch (DbUpdateException ex)
        {
            // Check for specific database exception details
            // The exact error number/code depends on the database provider (e.g., SQL Server, PostgreSQL)
            if (IsPrimaryKeyViolation(ex))
            {
                return Conflict(new { message = $"A User with the Email '{user.Email}' already exists.Please try with different Email" });
            }

            // Re-throw if it's a different DbUpdateException or handle other types
            throw;
        }

    }

    // Helper method to check for SQL Server duplicate key error (Error Number 2627 or 2601)
    private bool IsPrimaryKeyViolation(DbUpdateException ex)
    {
        if (ex.InnerException is SqlException sqlEx)
        {
            // SQL Server error numbers for duplicate key violation are 2627 and 2601
            return sqlEx.Number == 2627 || sqlEx.Number == 2601;
        }
        // Add checks for other database types (e.g., PostgreSqlException, MySqlException) as needed
        return false;
    }
}