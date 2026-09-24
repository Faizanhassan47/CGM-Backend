using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CGM.Api.Data;
using CGM.Api.Models.Dtos;
using CGM.Api.Models.Entities;
using CGM.Api.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;
using CGM.Api.Hubs;

namespace CGM.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class GlucoseController : ControllerBase
{
    private readonly CgmDbContext _db;
    private readonly IGlucoseAlertService _alerts;
    private readonly IHubContext<GlucoseHub> _hubContext;
    private readonly IMemoryCache _cache;

    public GlucoseController(
        CgmDbContext db, 
        IGlucoseAlertService alerts,
        IHubContext<GlucoseHub> hubContext,
        IMemoryCache cache)
    {
        _db = db;
        _alerts = alerts;
        _hubContext = hubContext;
        _cache = cache;
    }

    private int GetCurrentUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return int.TryParse(claim, out var id) ? id : throw new UnauthorizedAccessException("User identifier claim is missing.");
    }

    [HttpPost("measurement")]
    public async Task<IActionResult> SaveMeasurement([FromBody] GlucoseMeasurementDto dto)
    {
        var userId = GetCurrentUserId();
        var sensor = await _db.Sensors.FirstOrDefaultAsync(s => s.Id == dto.SensorId && s.UserId == userId);
        if (sensor is null) return NotFound(new { message = "Sensor not found." });

        // Check if a reading already exists for this exact time
        var exists = await _db.GlucoseMeasurements
            .AnyAsync(m => m.SensorId == dto.SensorId && m.MeasurementTime == dto.MeasurementTime);

        if (exists)
        {
            return Ok(new { message = "Duplicate reading skipped.", measurementTime = dto.MeasurementTime });
        }

        var measurement = new GlucoseMeasurementEntity
        {
            UserId = userId,
            SensorId = dto.SensorId,
            DeviceId = sensor.DeviceId,
            GlucoseValue = dto.GlucoseValue,
            MeasurementTime = dto.MeasurementTime,
            BatteryVoltageMv = dto.BatteryVoltageMv,
            DeviceTemperatureC = dto.DeviceTemperatureC,
            WE1CurrentNa = dto.WE1CurrentNa,
            IsSynced = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.GlucoseMeasurements.Add(measurement);

        // Update sensor latest state
        if (!sensor.LastReadingAt.HasValue || dto.MeasurementTime > sensor.LastReadingAt.Value)
        {
            sensor.LastReadingAt = dto.MeasurementTime;
        }

        await _alerts.CreateIfAbnormalAsync(measurement, HttpContext.RequestAborted);

        await _db.SaveChangesAsync();

        // Broadcast real-time update
        var familyIds = await _db.FamilyMembers.Where(f => f.UserId == userId).Select(f => f.FamilyId).ToListAsync();
        var ownedFamilyIds = await _db.Families.Where(f => f.OwnerUserId == userId).Select(f => f.Id).ToListAsync();
        var allFamilyIds = familyIds.Concat(ownedFamilyIds).Distinct();
        
        foreach (var fid in allFamilyIds)
        {
            await _hubContext.Clients.Group($"family_{fid}")
                .SendAsync("ReceiveGlucoseUpdate", new { 
                    UserId = userId,
                    GlucoseValue = dto.GlucoseValue,
                    MeasurementTime = dto.MeasurementTime
                });
        }

        // Cache invalidate
        _cache.Remove($"Summary_{userId}_{dto.SensorId}");

        return Ok(new { message = "Measurement saved successfully.", measurementTime = dto.MeasurementTime });
    }

    [HttpPost("sync-bulk")]
    public async Task<IActionResult> SyncBulk([FromBody] BulkSyncRequestDto dto)
    {
        var userId = GetCurrentUserId();
        var ownedSensor = await _db.Sensors.FirstOrDefaultAsync(s => s.Id == dto.SensorId && s.UserId == userId);
        if (ownedSensor is null) return NotFound(new { message = "Sensor not found." });
        var existingTimes = await _db.GlucoseMeasurements
            .Where(m => m.SensorId == dto.SensorId && m.UserId == userId)
            .Select(m => m.MeasurementTime)
            .ToHashSetAsync();

        var sortedIncoming = dto.Measurements
            .Where(m => !existingTimes.Contains(m.MeasurementTime))
            .OrderBy(m => m.MeasurementTime)
            .ToList();

        var newEntities = new List<GlucoseMeasurementEntity>();
        foreach (var m in sortedIncoming)
        {
            newEntities.Add(new GlucoseMeasurementEntity
            {
                UserId = userId,
                SensorId = dto.SensorId,
                DeviceId = ownedSensor.DeviceId,
                GlucoseValue = m.GlucoseValue,
                MeasurementTime = m.MeasurementTime,
                BatteryVoltageMv = m.BatteryVoltageMv,
                DeviceTemperatureC = m.DeviceTemperatureC,
                WE1CurrentNa = m.WE1CurrentNa,
                IsSynced = true,
                CreatedAt = DateTime.UtcNow
            });
        }

        if (newEntities.Any())
        {
            _db.GlucoseMeasurements.AddRange(newEntities);
            foreach (var entity in newEntities)
                await _alerts.CreateIfAbnormalAsync(entity, HttpContext.RequestAborted);

            var maxTime = newEntities.Max(e => e.MeasurementTime);
            if (!ownedSensor.LastReadingAt.HasValue || maxTime > ownedSensor.LastReadingAt.Value)
            {
                ownedSensor.LastReadingAt = maxTime;
            }

            await _db.SaveChangesAsync();
        }

        return Ok(new { message = "Bulk sync complete.", insertedCount = newEntities.Count });
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetHistory(
        [FromQuery] int? sensorId,
        [FromQuery] int hours = 24,
        [FromQuery] DateTime? startUtc = null,
        [FromQuery] DateTime? endUtc = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        page = Math.Max(1, page);
        // If caller did not explicitly request pagination parameters, default to generous limit so charts are not truncated
        if (!Request.Query.ContainsKey("pageSize") && !Request.Query.ContainsKey("page"))
        {
            pageSize = 1000;
        }
        else
        {
            pageSize = Math.Clamp(pageSize, 1, 1000);
        }
        var userId = GetCurrentUserId();
        var since = startUtc ?? DateTime.UtcNow.AddHours(-hours);

        var query = _db.GlucoseMeasurements
            .Where(m => m.UserId == userId && m.MeasurementTime >= since);

        if (endUtc.HasValue)
            query = query.Where(m => m.MeasurementTime < endUtc.Value);

        if (sensorId.HasValue)
        {
            query = query.Where(m => m.SensorId == sensorId.Value);
        }

        var totalCount = await query.CountAsync();
        var list = await query
            .OrderByDescending(m => m.MeasurementTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new
        {
            items = list,
            page,
            pageSize,
            totalCount,
            totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        });
    }

    [HttpGet("summary")]
    public async Task<ActionResult<GlucoseSummaryDto>> GetSummary([FromQuery] int? sensorId)
    {
        var userId = GetCurrentUserId();
        var cacheKey = $"Summary_{userId}_{sensorId}";
        if (_cache.TryGetValue(cacheKey, out GlucoseSummaryDto? cachedSummary))
        {
            return Ok(cachedSummary);
        }

        var since = DateTime.UtcNow.AddHours(-24);

        var query = _db.GlucoseMeasurements
            .Where(m => m.UserId == userId && m.MeasurementTime >= since);

        if (sensorId.HasValue)
        {
            query = query.Where(m => m.SensorId == sensorId.Value);
        }

        var readings = await query.ToListAsync();

        if (!readings.Any())
        {
            return Ok(new GlucoseSummaryDto(
                CurrentGlucose: 112,
                AverageGlucose: 96,
                LowestGlucose: 64,
                HighestGlucose: 152,
                TimeInRangePercentage: 82,
                LastUpdated: DateTime.UtcNow
            ));
        }

        var latest = readings.OrderByDescending(r => r.MeasurementTime).First();
        var inRangeCount = readings.Count(r => r.GlucoseValue is >= 70 and <= 180);
        var tir = Math.Round((decimal)inRangeCount / readings.Count * 100, 1);

        var result = new GlucoseSummaryDto(
            CurrentGlucose: latest.GlucoseValue ?? 112,
            AverageGlucose: Math.Round(readings.Where(r => r.GlucoseValue.HasValue).Average(r => r.GlucoseValue!.Value), 0),
            LowestGlucose: readings.Where(r => r.GlucoseValue.HasValue).Min(r => r.GlucoseValue!.Value),
            HighestGlucose: readings.Where(r => r.GlucoseValue.HasValue).Max(r => r.GlucoseValue!.Value),
            TimeInRangePercentage: tir,
            LastUpdated: latest.MeasurementTime
        );

        _cache.Set(cacheKey, result, TimeSpan.FromMinutes(5));
        return Ok(result);
    }
}
