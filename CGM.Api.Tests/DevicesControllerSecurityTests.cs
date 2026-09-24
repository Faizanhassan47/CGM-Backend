using System.Security.Claims;
using CGM.Api.Controllers;
using CGM.Api.Data;
using CGM.Api.Models.Dtos;
using CGM.Api.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CGM.Api.Tests;

public sealed class DevicesControllerSecurityTests
{
    [Fact]
    public void Controller_requires_authorization()
    {
        Assert.NotNull(Attribute.GetCustomAttribute(typeof(DevicesController), typeof(AuthorizeAttribute)));
    }

    [Fact]
    public async Task GetDevice_returns_forbidden_for_another_users_device()
    {
        await using var db = CreateDb();
        db.CgmDevices.Add(Device(id: 8, userId: 2));
        await db.SaveChangesAsync();

        var result = await Controller(db, userId: 1).GetDevice(8);

        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact]
    public async Task UpdateStatus_returns_forbidden_for_another_users_device()
    {
        await using var db = CreateDb();
        db.CgmDevices.Add(Device(id: 8, userId: 2));
        await db.SaveChangesAsync();

        var result = await Controller(db, userId: 1)
            .UpdateStatus(8, new UpdateDeviceStatusDto("Connected", null, null));

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task RemoveDevice_returns_forbidden_for_another_users_device()
    {
        await using var db = CreateDb();
        db.CgmDevices.Add(Device(id: 8, userId: 2));
        await db.SaveChangesAsync();

        var result = await Controller(db, userId: 1).RemoveDevice(8);

        Assert.IsType<ForbidResult>(result);
        Assert.True((await db.CgmDevices.FindAsync(8))!.IsActive);
    }

    [Fact]
    public async Task RegisterDevice_returns_forbidden_when_serial_belongs_to_another_user()
    {
        await using var db = CreateDb();
        db.CgmDevices.Add(Device(id: 8, userId: 2, serial: "CGM-001"));
        await db.SaveChangesAsync();

        var request = new RegisterDeviceRequestDto("Sensor", "Disposable", "G-AA", "CGM-001", null, null, null);
        var result = await Controller(db, userId: 1).RegisterDevice(request);

        Assert.IsType<ForbidResult>(result.Result);
    }

    private static CgmDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<CgmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new CgmDbContext(options);
    }

    private static DevicesController Controller(CgmDbContext db, int userId)
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "test");
        return new DevicesController(db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            }
        };
    }

    private static CgmDeviceEntity Device(int id, int userId, string? serial = null) => new()
    {
        Id = id,
        UserId = userId,
        DeviceName = "Test CGM",
        DeviceType = "Disposable",
        DeviceModel = "G-AA",
        SerialNumber = serial,
        ConnectionStatus = "Disconnected"
    };
}
