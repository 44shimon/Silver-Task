using System.Collections.Concurrent;

namespace Silver_Task.Server.Services
{
    /// <summary>One background worker's most recent successful tick — the interval is carried
    /// alongside the timestamp specifically so a consumer (DiagnosticsService) can judge staleness
    /// relative to each worker's own cadence (20 seconds for email delivery vs. 24 hours for
    /// notification retention) rather than a single fixed threshold that would either be far too
    /// strict for slow workers or far too lax for fast ones.</summary>
    public sealed record WorkerHeartbeat(string Name, DateTime LastSuccessUtc, TimeSpan Interval);

    /// <summary>Phase 58 — a shared, queryable "did this worker's last tick actually succeed"
    /// signal. Every interval-driven BackgroundService in this app already catches its own per-tick
    /// exceptions and logs them (never crashes) — but before this, there was no way to tell from
    /// outside the log file whether a worker had quietly stopped making progress. This is
    /// deliberately not a health-check mechanism itself (see DiagnosticsService for the
    /// staleness judgement) — just the raw, most-recent-success facts.</summary>
    public interface IWorkerHeartbeatRegistry
    {
        void ReportSuccess(string workerName, TimeSpan interval);

        IReadOnlyList<WorkerHeartbeat> GetAll();
    }

    /// <summary>Singleton (see Program.cs) — must outlive per-request scopes and be shared across
    /// every background service, which are themselves registered as singletons
    /// (AddHostedService). ConcurrentDictionary because every worker reports from its own
    /// long-running background thread concurrently with admin requests reading GetAll().</summary>
    public class WorkerHeartbeatRegistry : IWorkerHeartbeatRegistry
    {
        private readonly ConcurrentDictionary<string, WorkerHeartbeat> _heartbeats = new();

        public void ReportSuccess(string workerName, TimeSpan interval) =>
            _heartbeats[workerName] = new WorkerHeartbeat(workerName, DateTime.UtcNow, interval);

        public IReadOnlyList<WorkerHeartbeat> GetAll() => [.. _heartbeats.Values];
    }
}
