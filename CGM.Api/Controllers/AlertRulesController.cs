using System.Security.Claims;
using CGM.Api.Data;
using CGM.Api.Models.Dtos;
using CGM.Api.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CGM.Api.Controllers;

[ApiController, Authorize, Route("api/alert-rules")]
public sealed class AlertRulesController(CgmDbContext db) : ControllerBase
{
    private int UserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id)
        ? id : throw new UnauthorizedAccessException();

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) => Ok(await db.AlertRules
        .Where(r => r.UserId == UserId).OrderBy(r => r.AlertType).ToListAsync(ct));

    [HttpPost]
    public async Task<IActionResult> Create(SaveAlertRuleRequest request, CancellationToken ct)
    {
        if (request.MinimumValue.HasValue && request.MaximumValue.HasValue && request.MinimumValue > request.MaximumValue)
            return BadRequest(new { message = "Minimum value cannot exceed maximum value." });
        var rule = new AlertRuleEntity { UserId=UserId, AlertType=request.AlertType.Trim(), MinimumValue=request.MinimumValue,
            MaximumValue=request.MaximumValue, DurationMinutes=request.DurationMinutes, Enabled=request.Enabled };
        db.AlertRules.Add(rule);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id=rule.Id }, rule);
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, SaveAlertRuleRequest request, CancellationToken ct)
    {
        var rule = await db.AlertRules.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (rule is null) return NotFound();
        if (rule.UserId != UserId) return Forbid();
        if (request.MinimumValue.HasValue && request.MaximumValue.HasValue && request.MinimumValue > request.MaximumValue)
            return BadRequest(new { message = "Minimum value cannot exceed maximum value." });
        rule.AlertType=request.AlertType.Trim(); rule.MinimumValue=request.MinimumValue; rule.MaximumValue=request.MaximumValue;
        rule.DurationMinutes=request.DurationMinutes; rule.Enabled=request.Enabled;
        await db.SaveChangesAsync(ct);
        return Ok(rule);
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        var rule = await db.AlertRules.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (rule is null) return NotFound();
        if (rule.UserId != UserId) return Forbid();
        db.AlertRules.Remove(rule);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
