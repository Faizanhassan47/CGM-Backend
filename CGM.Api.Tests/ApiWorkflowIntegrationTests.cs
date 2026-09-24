using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CGM.Api.Data;
using CGM.Api.Models.Entities;
using Microsoft.Extensions.DependencyInjection;
namespace CGM.Api.Tests;
public sealed class ApiWorkflowIntegrationTests: IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory; private readonly HttpClient _client;
    public ApiWorkflowIntegrationTests(ApiFactory factory){_factory=factory;_client=factory.CreateClient();}
    [Fact] public async Task Registration_returns_contract_and_stateless_refresh_token()
    {
        var email=$"patient-{Guid.NewGuid():N}@example.com";
        var response=await _client.PostAsJsonAsync("/api/auth/register",new{FullName="Patient",Email=email,Password="StrongPassword1!",PhoneNumber=(string?)null,PreferredGlucoseUnit="mg/dL"});
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetProperty("success").GetBoolean());Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("accessToken").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("refreshToken").GetString()));
        using var scope=_factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<CgmDbContext>();
        Assert.True(db.Users.Any(x=>x.Email==email));
    }
    [Fact] public async Task Invalid_login_returns_no_token()
    {
        var response=await _client.PostAsJsonAsync("/api/auth/login",new{Email="missing@example.com",Password="wrong",DeviceId="test-device"});
        Assert.Equal(HttpStatusCode.Unauthorized,response.StatusCode);
    }
    [Fact] public async Task Protected_device_contract_rejects_anonymous_request()
    {
        using var anonymous=_factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized,(await anonymous.GetAsync("/api/devices")).StatusCode);
    }
    [Fact] public async Task Health_contract_reports_api_database_and_queue_without_connection_details()
    {
        var response=await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        var payload=await response.Content.ReadAsStringAsync();
        using var json=JsonDocument.Parse(payload);
        Assert.Equal("healthy",json.RootElement.GetProperty("api").GetString());
        Assert.True(json.RootElement.TryGetProperty("database",out _));
        Assert.True(json.RootElement.TryGetProperty("queue",out _));
        Assert.DoesNotContain("Server=",payload,StringComparison.OrdinalIgnoreCase);
    }
    [Fact] public async Task Measurement_upload_links_user_sensor_device_and_queues_alert()
    {
        var (token,userId)=await RegisterAndToken();using(var scope=_factory.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<CgmDbContext>();
            var device=new CgmDeviceEntity{UserId=userId,DeviceName="CGM",ConnectionStatus="Connected"};db.CgmDevices.Add(device);await db.SaveChangesAsync();
            db.Sensors.Add(new SensorEntity{Id=700+userId,UserId=userId,DeviceId=device.Id,Status="Active"});await db.SaveChangesAsync();}
        var result=await PostAuthorized("/api/glucose/measurement",token,new{SensorId=700+userId,GlucoseValue=60,MeasurementTime=DateTime.UtcNow});
        Assert.Equal(HttpStatusCode.OK,result.StatusCode);using var verify=_factory.Services.CreateScope();var data=verify.ServiceProvider.GetRequiredService<CgmDbContext>();
        var reading=data.GlucoseMeasurements.Single(x=>x.UserId==userId);Assert.NotNull(reading.DeviceId);Assert.True(data.AlertQueue.Any(q=>q.Alert.UserId==userId));
    }
    [Fact] public async Task Bulk_sync_is_idempotent_and_returns_insert_count_contract()
    {
        var (token,userId)=await RegisterAndToken();var sensorId=1700+userId;
        using(var scope=_factory.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<CgmDbContext>();
            var device=new CgmDeviceEntity{UserId=userId,DeviceName="CGM",ConnectionStatus="Connected"};db.CgmDevices.Add(device);await db.SaveChangesAsync();
            db.Sensors.Add(new SensorEntity{Id=sensorId,UserId=userId,DeviceId=device.Id,Status="Active"});await db.SaveChangesAsync();}
        var body=new{SensorId=sensorId,Measurements=new[]{new{SensorId=sensorId,GlucoseValue=110,MeasurementTime=DateTime.UtcNow},new{SensorId=sensorId,GlucoseValue=120,MeasurementTime=DateTime.UtcNow.AddMinutes(5)}}};
        var first=await PostAuthorized("/api/glucose/sync-bulk",token,body);var second=await PostAuthorized("/api/glucose/sync-bulk",token,body);
        Assert.Equal(HttpStatusCode.OK,first.StatusCode);Assert.Equal(HttpStatusCode.OK,second.StatusCode);
        using var json=JsonDocument.Parse(await second.Content.ReadAsStringAsync());Assert.Equal(0,json.RootElement.GetProperty("insertedCount").GetInt32());
    }
    private async Task<HttpResponseMessage> PostAuthorized(string path,string token,object body){using var request=new HttpRequestMessage(HttpMethod.Post,path){Content=JsonContent.Create(body)};request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);return await _client.SendAsync(request);}
    private async Task<(string Token,int UserId)> RegisterAndToken(){var email=$"flow-{Guid.NewGuid():N}@example.com";var r=await _client.PostAsJsonAsync("/api/auth/register",new{FullName="Flow",Email=email,Password="StrongPassword1!",PreferredGlucoseUnit="mg/dL"});using var j=JsonDocument.Parse(await r.Content.ReadAsStringAsync());return(j.RootElement.GetProperty("accessToken").GetString()!,j.RootElement.GetProperty("user").GetProperty("id").GetInt32());}
}
