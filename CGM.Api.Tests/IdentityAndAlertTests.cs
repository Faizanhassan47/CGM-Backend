using System.IdentityModel.Tokens.Jwt;
using CGM.Api.Data;
using CGM.Api.Models.Entities;
using CGM.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
namespace CGM.Api.Tests;
public sealed class IdentityAndAlertTests
{
    [Fact]
    public void Access_token_is_short_lived_and_contains_role_and_device()
    {
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"JWT_SECRET",new string('x',64)}}).Build();
        var service=new TokenService(config);var user=new User{Id=42,Email="patient@example.com",FullName="Patient",AuthProvider="Email"};
        var (token,expires)=service.GenerateAccessToken(user,"android-1");
        var jwt=new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.InRange(expires-DateTime.UtcNow,TimeSpan.FromMinutes(14),TimeSpan.FromMinutes(16));
        Assert.Contains(jwt.Claims,x=>(x.Type=="role"||x.Type==System.Security.Claims.ClaimTypes.Role)&&x.Value=="Patient");
        Assert.Contains(jwt.Claims,x=>x.Type=="device_id"&&x.Value=="android-1");
    }
    [Fact]
    public void Refresh_token_is_signed_stateless_and_expires_in_seven_days()
    {
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"JWT_SECRET",new string('x',64)}}).Build();
        var service=new TokenService(config);var user=new User{Id=42,Email="patient@example.com",FullName="Patient",AuthProvider="Email",TokenVersion=3};
        var (token,expires)=service.GenerateRefreshToken(user,"android-1");
        var principal=service.ValidateRefreshToken(token);
        Assert.NotNull(principal);
        Assert.InRange(expires-DateTime.UtcNow,TimeSpan.FromDays(6.99),TimeSpan.FromDays(7.01));
        Assert.Equal("refresh",principal!.FindFirst("token_type")?.Value);
        Assert.Equal("3",principal.FindFirst("token_version")?.Value);
    }
    [Fact]
    public async Task Alert_processor_queues_abnormal_measurement_and_suppresses_duplicate()
    {
        await using var db=CreateDb();db.Users.Add(new User{Id=1,Email="p@example.com",FullName="Patient",AuthProvider="Email",IsActive=true});await db.SaveChangesAsync();
        var service=new GlucoseAlertService(db);
        await service.CreateIfAbnormalAsync(new GlucoseMeasurementEntity{UserId=1,SensorId=1,GlucoseValue=55,MeasurementTime=DateTime.UtcNow});
        await db.SaveChangesAsync();
        await service.CreateIfAbnormalAsync(new GlucoseMeasurementEntity{UserId=1,SensorId=1,GlucoseValue=54,MeasurementTime=DateTime.UtcNow.AddMinutes(1)});
        Assert.Single(db.Alerts);Assert.Single(db.AlertQueue);Assert.Single(db.AlertHistory);
    }
    [Fact]
    public void Phase_four_and_five_entities_are_registered()
    {
        using var db=CreateDb();
        Assert.DoesNotContain(db.Model.GetEntityTypes(), entity => entity.GetTableName() == "UserSessions");
        Assert.NotNull(db.Model.FindEntityType(typeof(AlertRuleEntity)));
        Assert.NotNull(db.Model.FindEntityType(typeof(AlertQueueEntity)));
        Assert.NotNull(db.Model.FindEntityType(typeof(AlertDeliveryHistoryEntity)));
    }
    private static CgmDbContext CreateDb()=>new(new DbContextOptionsBuilder<CgmDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}
