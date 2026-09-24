using System.Security.Claims;
using CGM.Api.Data;
using CGM.Api.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace CGM.Api.Controllers;
public record SaveNotificationEndpointRequest(string Channel,string Address,bool Enabled=true);
[ApiController,Authorize,Route("api/notification-endpoints")]
public sealed class NotificationEndpointsController(CgmDbContext db):ControllerBase
{
    private int UserId=>int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier)??User.FindFirstValue("sub"),out var id)?id:throw new UnauthorizedAccessException();
    [HttpGet] public async Task<IActionResult> Get(CancellationToken ct)=>Ok(await db.NotificationEndpoints.Where(x=>x.UserId==UserId).ToListAsync(ct));
    [HttpPost] public async Task<IActionResult> Save(SaveNotificationEndpointRequest r,CancellationToken ct)
    {
        var channel=r.Channel.Trim(); if(channel is not ("Push" or "SMS")) return BadRequest(new{message="Channel must be Push or SMS."});
        var endpoint=await db.NotificationEndpoints.FirstOrDefaultAsync(x=>x.UserId==UserId&&x.Channel==channel&&x.Address==r.Address,ct);
        if(endpoint is null){endpoint=new(){UserId=UserId,Channel=channel,Address=r.Address.Trim(),Enabled=r.Enabled};db.NotificationEndpoints.Add(endpoint);}
        else {endpoint.Enabled=r.Enabled;endpoint.UpdatedAt=DateTime.UtcNow;}
        await db.SaveChangesAsync(ct);return Ok(endpoint);
    }
    [HttpDelete("{id:long}")] public async Task<IActionResult> Delete(long id,CancellationToken ct)
    {var endpoint=await db.NotificationEndpoints.FirstOrDefaultAsync(x=>x.Id==id,ct);if(endpoint is null)return NotFound();if(endpoint.UserId!=UserId)return Forbid();db.Remove(endpoint);await db.SaveChangesAsync(ct);return NoContent();}
}
