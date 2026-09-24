using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CGM.Api.Data;
using CGM.Api.Models.Dtos;
using CGM.Api.Models.Entities;

namespace CGM.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class DevicesController : ControllerBase
{
    private readonly CgmDbContext _db;

    public DevicesController(CgmDbContext db)
    {
        _db = db;
    }

    private int GetCurrentUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return int.TryParse(claim, out var id)
            ? id
            : throw new UnauthorizedAccessException("User identifier claim is missing.");
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<DeviceInfoDto>> GetDevice(int id)
    {
        var userId = GetCurrentUserId();
        var device = await _db.CgmDevices.FirstOrDefaultAsync(d => d.Id == id);
        if (device is null) return NotFound();
        if (device.UserId != userId) return Forbid();

        return Ok(ToDto(device));
    }

    [HttpGet]
    public async Task<ActionResult<List<DeviceInfoDto>>> GetDevices()
    {
        var userId = GetCurrentUserId();
        var devices = await _db.CgmDevices
            .Where(d => d.UserId == userId && d.IsActive)
            .Select(d => new DeviceInfoDto(
                d.Id,
                d.DeviceName ?? "CGM Device",
                d.DeviceType ?? "Disposable",
                d.DeviceModel ?? "G-AA",
                d.SerialNumber,
                d.BleDeviceName,
                d.FirmwareVersion,
                d.BatteryVoltageMv,
                d.BatteryPercentage,
                d.ConnectionStatus,
                d.LastConnectedAt
            ))
            .ToListAsync();

        return Ok(devices);
    }

    [HttpPost("register")]
    public async Task<ActionResult<DeviceInfoDto>> RegisterDevice([FromBody] RegisterDeviceRequestDto request)
    {
        var userId = GetCurrentUserId();

        // Check if serial number already registered
        CgmDeviceEntity? device = null;
        if (!string.IsNullOrEmpty(request.SerialNumber))
        {
            device = await _db.CgmDevices.FirstOrDefaultAsync(d => d.SerialNumber == request.SerialNumber);
            if (device is not null && device.UserId != userId)
                return Forbid();
        }

        if (device == null)
        {
            device = new CgmDeviceEntity
            {
                UserId = userId,
                DeviceName = request.DeviceName,
                DeviceType = request.DeviceType,
                DeviceModel = request.DeviceModel,
                SerialNumber = request.SerialNumber,
                BleDeviceName = request.BleDeviceName,
                FirmwareVersion = request.FirmwareVersion,
                BatteryVoltageMv = request.BatteryVoltageMv,
                ConnectionStatus = "Connected",
                LastConnectedAt = DateTime.UtcNow,
                LastCommunicationAt = DateTime.UtcNow,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            _db.CgmDevices.Add(device);
        }
        else
        {
            device.UserId = userId;
            device.DeviceName = request.DeviceName;
            device.DeviceType = request.DeviceType;
            device.DeviceModel = request.DeviceModel;
            device.BleDeviceName = request.BleDeviceName;
            device.FirmwareVersion = request.FirmwareVersion;
            device.BatteryVoltageMv = request.BatteryVoltageMv;
            device.ConnectionStatus = "Connected";
            device.LastConnectedAt = DateTime.UtcNow;
            device.LastCommunicationAt = DateTime.UtcNow;
            device.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        return Ok(new DeviceInfoDto(
            device.Id,
            device.DeviceName ?? "CGM Device",
            device.DeviceType ?? "Disposable",
            device.DeviceModel ?? "G-AA",
            device.SerialNumber,
            device.BleDeviceName,
            device.FirmwareVersion,
            device.BatteryVoltageMv,
            device.BatteryPercentage,
            device.ConnectionStatus,
            device.LastConnectedAt
        ));
    }

    [HttpPut("{id:int}")]
    [HttpPut("{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateDeviceStatusDto request)
    {
        var userId = GetCurrentUserId();
        var device = await _db.CgmDevices.FirstOrDefaultAsync(d => d.Id == id);
        if (device is null) return NotFound();
        if (device.UserId != userId) return Forbid();

        device.ConnectionStatus = request.ConnectionStatus;
        if (request.BatteryVoltageMv.HasValue) device.BatteryVoltageMv = request.BatteryVoltageMv;
        if (request.BatteryPercentage.HasValue) device.BatteryPercentage = request.BatteryPercentage;
        device.LastCommunicationAt = DateTime.UtcNow;
        device.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(new { message = "Status updated." });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> RemoveDevice(int id)
    {
        var userId = GetCurrentUserId();
        var device = await _db.CgmDevices.FirstOrDefaultAsync(d => d.Id == id);
        if (device is null) return NotFound();
        if (device.UserId != userId) return Forbid();

        device.IsActive = false;
        device.ConnectionStatus = "Disconnected";
        device.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static DeviceInfoDto ToDto(CgmDeviceEntity device) => new(
        device.Id,
        device.DeviceName ?? "CGM Device",
        device.DeviceType ?? "Disposable",
        device.DeviceModel ?? "G-AA",
        device.SerialNumber,
        device.BleDeviceName,
        device.FirmwareVersion,
        device.BatteryVoltageMv,
        device.BatteryPercentage,
        device.ConnectionStatus,
        device.LastConnectedAt);
}
