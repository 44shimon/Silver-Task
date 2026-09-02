using System.Collections.Concurrent;

namespace Silver_Task.Server.Services
{
    /// <summary>One request that took at least Diagnostics:SlowOperationThresholdMs to complete.
    /// Operation is "{Controller}.{Action}" — never the full URL (which could carry a route-bound
    /// GUID or query string) and never the request body — a low-cardinality, safe label, matching
    /// the spec's own explicit "do not log ... sensitive request bodies."</summary>
    public sealed record SlowOperation(string Operation, long DurationMs, DateTime RecordedAtUtc);

    /// <summary>Phase 60 — a shared, queryable "what's been slow recently" signal, fed by
    /// SlowOperationActionFilter and surfaced on GET /api/admin/diagnostics
    /// (recentSlowOperations). Deliberately a bounded ring buffer, not an unbounded log — this is
    /// a lightweight in-memory diagnostic aid for "is something wrong right now," not a
    /// replacement for real request logging/APM (see docs/performance.md's own "what this phase
    /// deliberately did not do").</summary>
    public interface ISlowOperationTracker
    {
        void Record(string operation, long durationMs);

        IReadOnlyList<SlowOperation> GetRecent();
    }

    /// <summary>Singleton (see Program.cs) — must be shared across every request, which each run
    /// in their own scope. ConcurrentQueue because requests record concurrently with admin
    /// requests reading GetRecent().</summary>
    public class SlowOperationTracker : ISlowOperationTracker
    {
        private const int MaxEntries = 50;
        private readonly ConcurrentQueue<SlowOperation> _recent = new();

        public void Record(string operation, long durationMs)
        {
            _recent.Enqueue(new SlowOperation(operation, durationMs, DateTime.UtcNow));
            while (_recent.Count > MaxEntries && _recent.TryDequeue(out _))
            {
                // Trim down to MaxEntries — oldest first, since ConcurrentQueue is FIFO.
            }
        }

        public IReadOnlyList<SlowOperation> GetRecent() => [.. _recent];
    }
}
