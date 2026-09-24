using System.Net.Http.Headers;
using System.Net.Http.Json;
using CGM.Api.Models.Entities;
namespace CGM.Api.Services;
public interface IExternalNotificationSender { Task<bool> SendAsync(string channel,string address,AlertEntity alert,CancellationToken ct); }
public sealed class ExternalNotificationSender(IHttpClientFactory clients,IConfiguration config,ILogger<ExternalNotificationSender> logger):IExternalNotificationSender
{
    public async Task<bool> SendAsync(string channel,string address,AlertEntity alert,CancellationToken ct)
    {
        var url=config[$"Notifications:{channel}:WebhookUrl"]??Environment.GetEnvironmentVariable($"{channel.ToUpperInvariant()}_NOTIFICATION_WEBHOOK");
        if(string.IsNullOrWhiteSpace(url)){logger.LogWarning("{Channel} notification provider is not configured",channel);return false;}
        using var request=new HttpRequestMessage(HttpMethod.Post,url){Content=JsonContent.Create(new{destination=address,alertId=alert.Id,title=alert.Title,message=alert.Message,severity=alert.Severity})};
        var key=config[$"Notifications:{channel}:ApiKey"]??Environment.GetEnvironmentVariable($"{channel.ToUpperInvariant()}_NOTIFICATION_API_KEY");
        if(!string.IsNullOrWhiteSpace(key))request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",key);
        using var response=await clients.CreateClient("notifications").SendAsync(request,ct);return response.IsSuccessStatusCode;
    }
}
