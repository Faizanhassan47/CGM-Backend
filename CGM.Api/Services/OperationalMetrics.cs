using System.Collections.Concurrent;
namespace CGM.Api.Services;
public sealed class OperationalMetrics
{
    private long _requests,_failures,_syncFailures,_notificationFailures,_workerHeartbeatTicks; private readonly ConcurrentQueue<long> _durations=[];
    public void RecordRequest(long ms,bool failed){Interlocked.Increment(ref _requests);if(failed)Interlocked.Increment(ref _failures);_durations.Enqueue(ms);while(_durations.Count>1000)_durations.TryDequeue(out _);}
    public void RecordSyncFailure()=>Interlocked.Increment(ref _syncFailures);
    public void RecordNotificationFailure()=>Interlocked.Increment(ref _notificationFailures);
    public void RecordWorkerHeartbeat()=>Interlocked.Exchange(ref _workerHeartbeatTicks,DateTime.UtcNow.Ticks);
    public DateTime? WorkerHeartbeatUtc { get { var ticks=Interlocked.Read(ref _workerHeartbeatTicks);return ticks==0?null:new DateTime(ticks,DateTimeKind.Utc); } }
    public object Snapshot(){var d=_durations.ToArray();return new{requests=Interlocked.Read(ref _requests),failures=Interlocked.Read(ref _failures),errorPercentage=_requests==0?0:Math.Round(100d*_failures/_requests,2),averageResponseMs=d.Length==0?0:Math.Round(d.Average(),2),syncFailures=Interlocked.Read(ref _syncFailures),notificationFailures=Interlocked.Read(ref _notificationFailures),notificationWorkerHeartbeatUtc=WorkerHeartbeatUtc};}
}
