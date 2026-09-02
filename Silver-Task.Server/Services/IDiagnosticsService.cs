using System.Diagnostics;
using System.Reflection;
using Silver_Task.Server.Common;
using Silver_Task.Server.Data;
using Silver_Task.Server.Models.DTOs.Diagnostics;

namespace Silver_Task.Server.Services
{
    /// <summary>Phase 58 — computes a point-in-time healthy/degraded/failing snapshot for
    /// GET /api/admin/diagnostics. Deliberately separate from HealthController's existing
    /// anonymous /api/health and /api/health/ready, which stay exactly as they are (external
    /// uptime monitors keep polling those, unauthenticated, binary reachable/unreachable) — this
    /// is the richer, admin-only view: database latency (not just reachability), disk space, and
    /// per-background-worker heartbeats.</summary>
    public interface IDiagnosticsService
    {
        Task<DiagnosticsDto> GetDiagnosticsAsync();
    }

    public class DiagnosticsService(
        AppDbContext db,
        IWorkerHeartbeatRegistry heartbeats,
        IConfiguration configuration,
        IWebHostEnvironment environment) : IDiagnosticsService
    {
        private const string Healthy = "healthy";
        private const string Degraded = "degraded";
        private const string Failing = "failing";
        private const string Starting = "starting";
        private const string NotApplicable = "not-applicable";

        // The 6 interval-driven background services this app registers (see Program.cs) — kept
        // here, not read from the registry, specifically so a worker that hasn't completed its
        // first tick yet (e.g. the first few seconds after a restart — every one of these actually
        // runs once immediately on startup, so this window is normally very short) is reported as
        // "starting" rather than silently missing or falsely "degraded". AutomationQueueBackgroundService
        // is intentionally not listed here — it's purely event-driven (no PeriodicTimer, no fixed
        // interval) and is reported separately as "not-applicable" below.
        private static readonly (string Name, TimeSpan Interval)[] ExpectedWorkers =
        [
            ("due-date-notifications", TimeSpan.FromMinutes(15)),
            ("recurring-task-generation", TimeSpan.FromMinutes(5)),
            ("automation-overdue-check", TimeSpan.FromMinutes(15)),
            ("notification-retention", TimeSpan.FromHours(24)),
            ("email-delivery", TimeSpan.FromSeconds(20)),
            ("digest-scheduler", TimeSpan.FromMinutes(10)),
        ];

        private const string AutomationQueueWorkerName = "automation-queue";

        private static readonly string AppVersion =
            Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
            ?? "unknown";

        private readonly AppDbContext _db = db;
        private readonly IWorkerHeartbeatRegistry _heartbeats = heartbeats;
        private readonly IConfiguration _configuration = configuration;
        private readonly IWebHostEnvironment _environment = environment;

        public async Task<DiagnosticsDto> GetDiagnosticsAsync()
        {
            var database = await CheckDatabaseAsync();
            var diskSpace = CheckDiskSpace();
            var workers = CheckWorkers();

            var overallStatus = WorstOf(database.Status, WorstOf(diskSpace.Status, WorstStatusOf(workers)));

            var uptimeSeconds = (DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime()).TotalSeconds;

            return new DiagnosticsDto
            {
                Status = overallStatus,
                Version = AppVersion,
                TimeUtc = DateTime.UtcNow,
                UptimeSeconds = Math.Max(0, uptimeSeconds),
                Database = database,
                DiskSpace = diskSpace,
                BackgroundWorkers = workers,
            };
        }

        private async Task<DatabaseDiagnosticsDto> CheckDatabaseAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            bool reachable;
            try
            {
                reachable = await _db.Database.CanConnectAsync();
            }
            catch
            {
                // CanConnectAsync already swallows most connectivity failures and returns false,
                // but never trust that alone against every possible provider-level exception.
                reachable = false;
            }
            stopwatch.Stop();

            if (!reachable)
            {
                return new DatabaseDiagnosticsDto { Status = Failing, Reachable = false, LatencyMs = null };
            }

            var latencyMs = stopwatch.ElapsedMilliseconds;
            var degradedThresholdMs = _configuration.GetValue("Diagnostics:DbLatencyDegradedMs", 1000);
            var status = latencyMs > degradedThresholdMs ? Degraded : Healthy;
            return new DatabaseDiagnosticsDto { Status = status, Reachable = true, LatencyMs = latencyMs };
        }

        private DiskSpaceDiagnosticsDto CheckDiskSpace()
        {
            var storageRoot = AttachmentStorageResolver.ResolveStorageRoot(_configuration, _environment);
            try
            {
                Directory.CreateDirectory(storageRoot);
                var drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(storageRoot)) ?? storageRoot);
                var freePercent = drive.TotalSize > 0 ? drive.AvailableFreeSpace * 100.0 / drive.TotalSize : 0;
                var degradedThresholdPercent = _configuration.GetValue("Diagnostics:DiskFreePercentDegraded", 10);
                var status = freePercent < degradedThresholdPercent ? Degraded : Healthy;
                return new DiskSpaceDiagnosticsDto
                {
                    Status = status,
                    Path = storageRoot,
                    FreeBytes = drive.AvailableFreeSpace,
                    TotalBytes = drive.TotalSize,
                    FreePercent = Math.Round(freePercent, 1),
                };
            }
            catch
            {
                // Unreadable drive info (permissions, an unusual filesystem) is a visibility gap,
                // not evidence the app itself is broken — "degraded", never "failing".
                return new DiskSpaceDiagnosticsDto { Status = Degraded, Path = storageRoot, FreeBytes = null, TotalBytes = null, FreePercent = null };
            }
        }

        private List<WorkerHeartbeatDto> CheckWorkers()
        {
            var reported = _heartbeats.GetAll().ToDictionary(h => h.Name);
            var staleMultiplier = _configuration.GetValue("Diagnostics:WorkerStaleMultiplier", 3);
            var now = DateTime.UtcNow;

            var result = new List<WorkerHeartbeatDto>();
            foreach (var (name, expectedInterval) in ExpectedWorkers)
            {
                if (!reported.TryGetValue(name, out var heartbeat))
                {
                    result.Add(new WorkerHeartbeatDto { Name = name, Status = Starting, LastSuccessfulRunUtc = null, IntervalSeconds = expectedInterval.TotalSeconds });
                    continue;
                }
                var staleAfter = TimeSpan.FromTicks(heartbeat.Interval.Ticks * staleMultiplier);
                var status = now - heartbeat.LastSuccessUtc > staleAfter ? Degraded : Healthy;
                result.Add(new WorkerHeartbeatDto { Name = name, Status = status, LastSuccessfulRunUtc = heartbeat.LastSuccessUtc, IntervalSeconds = heartbeat.Interval.TotalSeconds });
            }

            result.Add(new WorkerHeartbeatDto { Name = AutomationQueueWorkerName, Status = NotApplicable, LastSuccessfulRunUtc = null, IntervalSeconds = null });
            return result;
        }

        private static string WorstStatusOf(IEnumerable<WorkerHeartbeatDto> workers)
        {
            var status = Healthy;
            foreach (var worker in workers)
            {
                // "starting" and "not-applicable" never drag the overall status down — neither one
                // is evidence of a problem, just "no signal yet" / "no signal applies here".
                if (worker.Status is Starting or NotApplicable)
                {
                    continue;
                }
                status = WorstOf(status, worker.Status);
            }
            return status;
        }

        private static string WorstOf(string a, string b)
        {
            if (a == Failing || b == Failing)
            {
                return Failing;
            }
            if (a == Degraded || b == Degraded)
            {
                return Degraded;
            }
            return Healthy;
        }
    }
}
