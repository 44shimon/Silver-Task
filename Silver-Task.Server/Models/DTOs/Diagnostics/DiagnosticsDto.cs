namespace Silver_Task.Server.Models.DTOs.Diagnostics
{
    /// <summary>Phase 58 — backs GET /api/admin/diagnostics. Status fields throughout are one of
    /// "healthy"/"degraded"/"failing" (worker status additionally allows "starting" — no heartbeat
    /// yet, e.g. right after a restart — and "not-applicable" for the one event-driven worker with
    /// no meaningful staleness signal; see DiagnosticsService's own doc comment). The top-level
    /// Status is the worst of every individual status below, so a single field answers "is Silver
    /// Task healthy, degraded, or failing" without the caller needing to inspect every section.</summary>
    public class DiagnosticsDto
    {
        public required string Status { get; set; }

        public required string Version { get; set; }

        public DateTime TimeUtc { get; set; }

        public double UptimeSeconds { get; set; }

        public required DatabaseDiagnosticsDto Database { get; set; }

        public required DiskSpaceDiagnosticsDto DiskSpace { get; set; }

        public required List<WorkerHeartbeatDto> BackgroundWorkers { get; set; }

        /// <summary>Phase 60 — the most recent requests that took at least
        /// Diagnostics:SlowOperationThresholdMs (default 1000ms) to complete, newest first.
        /// Operation is "{Controller}.{Action}" only — never the full URL/query string/request
        /// body. Never affects the top-level Status (a slow endpoint isn't the same signal as a
        /// database/disk/worker problem) — purely informational, for "what's been slow recently."</summary>
        public required List<SlowOperationDto> RecentSlowOperations { get; set; }
    }

    public class DatabaseDiagnosticsDto
    {
        public required string Status { get; set; }

        public bool Reachable { get; set; }

        /// <summary>Null when unreachable — there's no meaningful latency to report for a
        /// connection that never succeeded.</summary>
        public long? LatencyMs { get; set; }
    }

    public class DiskSpaceDiagnosticsDto
    {
        public required string Status { get; set; }

        public required string Path { get; set; }

        /// <summary>All three null when the path's drive could not be read (e.g. permissions) —
        /// reported as "degraded", never "failing": the application can still run, it's only the
        /// visibility into free space that's unavailable.</summary>
        public long? FreeBytes { get; set; }

        public long? TotalBytes { get; set; }

        public double? FreePercent { get; set; }
    }

    public class WorkerHeartbeatDto
    {
        public required string Name { get; set; }

        public required string Status { get; set; }

        public DateTime? LastSuccessfulRunUtc { get; set; }

        public double? IntervalSeconds { get; set; }
    }

    public class SlowOperationDto
    {
        public required string Operation { get; set; }

        public long DurationMs { get; set; }

        public DateTime RecordedAtUtc { get; set; }
    }
}
