using CGM.Api.Data;
using CGM.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace CGM.Api.Controllers;
[ApiController,Route("api/[controller]")]
public sealed class HealthController(CgmDbContext db,OperationalMetrics metrics):ControllerBase
{
    [HttpGet("/health")]
    [HttpGet]
    public async Task<IActionResult> Health(CancellationToken ct)
    {
        try
        {
            if(!await db.Database.CanConnectAsync(ct))return StatusCode(503,new{api="healthy",database="unhealthy",queue="unknown",timestamp=DateTimeOffset.UtcNow});
            var failed=await db.AlertQueue.CountAsync(x=>x.Status=="Failed",ct);
            var pending=await db.AlertQueue.CountAsync(x=>x.Status=="Pending"||x.Status=="Processing",ct);
            var heartbeat=metrics.WorkerHeartbeatUtc;
            var queueHealth=failed>100||heartbeat.HasValue&&heartbeat<DateTime.UtcNow.AddMinutes(-2)?"degraded":"healthy";
            return Ok(new{api="healthy",database="healthy",queue=queueHealth,pendingAlerts=pending,failedAlerts=failed,notificationWorkerHeartbeatUtc=heartbeat,timestamp=DateTimeOffset.UtcNow});
        }
        catch(Exception){return StatusCode(503,new{api="healthy",database="unhealthy",queue="unknown",timestamp=DateTimeOffset.UtcNow});}
    }
    [HttpGet("/metrics")]
    public IActionResult Metrics()=>Ok(metrics.Snapshot());
}
