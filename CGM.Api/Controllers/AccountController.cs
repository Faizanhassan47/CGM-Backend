using System.Security.Claims;
using CGM.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CGM.Api.Controllers;

[ApiController, Authorize, Route("api/account")]
public sealed class AccountController(CgmDbContext db) : ControllerBase
{
    private int UserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub"), out var id) ? id : throw new UnauthorizedAccessException();

    [HttpPost("logout-all")]
    public async Task<IActionResult> LogoutAll(CancellationToken ct)
    {
        await db.Users.Where(x => x.Id == UserId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.TokenVersion, x => x.TokenVersion + 1), ct);
        return Ok(new { message = "All sessions have been revoked." });
    }
}
