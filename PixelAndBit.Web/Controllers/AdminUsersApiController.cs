using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PixelAndBit.Infrastructure.Data;

namespace PixelAndBit.Web.Controllers;

/// <summary>
/// JSON API for admin: registered users (no passwords). Requires Admin role + auth cookie.
/// </summary>
[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminUsersApiController : ControllerBase
{
    private readonly PixelBitDbContext _db;

    public AdminUsersApiController(PixelBitDbContext db) => _db = db;

    [HttpGet("users")]
    [Produces("application/json")]
    public async Task<ActionResult<IReadOnlyList<AdminUserRowDto>>> GetUsers(CancellationToken cancellationToken)
    {
        var list = await _db.Users
            .AsNoTracking()
            .OrderBy(u => u.Email)
            .Select(u => new AdminUserRowDto(
                u.Id,
                u.Email,
                u.UserName,
                u.EmailConfirmed,
                u.PhoneNumber,
                u.LockoutEnd,
                u.TwoFactorEnabled))
            .ToListAsync(cancellationToken);

        return Ok(list);
    }
}

public record AdminUserRowDto(
    string Id,
    string? Email,
    string? UserName,
    bool EmailConfirmed,
    string? PhoneNumber,
    DateTimeOffset? LockoutEnd,
    bool TwoFactorEnabled);
