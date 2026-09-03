using System.Collections.Concurrent;

namespace Silver_Task.Server.Services
{
    /// <summary>Phase 62 — a lightweight, in-memory defense against scanning/guessing on the
    /// X-Api-Key header, in the same spirit as Phase 59's login rate limiter but a genuinely
    /// different shape: an API key is a ~256-bit random secret (ApiKeyService.GenerateKey), not a
    /// human-memorable password, so it isn't practically brute-forceable the way a password is —
    /// a login-style lockout would be the wrong tool. What this *does* guard against is a leaked
    /// partial key, a scripted scan of the endpoint, or plain misconfiguration hammering the
    /// server: repeated invalid attempts from one IP get a cheap, immediate reject (no hashing or
    /// DB query) rather than being rate-limited via the ASP.NET RateLimiter middleware, which
    /// would throttle a whole endpoint's traffic — successes included — and could hurt a
    /// legitimate high-volume integration (an n8n workflow firing many valid requests). Only
    /// *failed* attempts count here.
    ///
    /// Same singleton/ConcurrentDictionary shape as IWorkerHeartbeatRegistry/ISlowOperationTracker
    /// (Phases 58/60) — must be shared across every request, which each run in their own scope.
    /// Threshold/window are passed in by the caller (ApiKeyAuthenticationHandler, which resolves
    /// Security:ApiKeyFailureLimit:* per request) rather than read here, matching how
    /// SlowOperationActionFilter — not SlowOperationTracker itself — owns its own threshold.</summary>
    public interface IApiKeyFailureTracker
    {
        void RecordFailure(string clientIp);

        bool IsBlocked(string clientIp, int maxFailures, TimeSpan window);

        int GetRecentFailureCount(TimeSpan window);
    }

    public class ApiKeyFailureTracker : IApiKeyFailureTracker
    {
        // A generous, fixed internal retention independent of any caller's window — just needs to
        // cover any reasonable configured window so trimming doesn't discard data a caller still
        // wants to count.
        private static readonly TimeSpan Retention = TimeSpan.FromHours(1);

        // Caps the number of distinct source IPs tracked at once — a simple, deliberate safety
        // valve against unbounded memory growth from a flood of spoofed/rotating source IPs. This
        // tracker is defense-in-depth, not the primary control (that's the key's own entropy), so
        // failing open on *tracking* a new IP once this cap is hit — rather than growing forever —
        // is the right tradeoff; IPs already being tracked keep working normally.
        private const int MaxTrackedIps = 10_000;

        private readonly ConcurrentDictionary<string, ConcurrentQueue<DateTime>> _failuresByIp = new();

        public void RecordFailure(string clientIp)
        {
            var queue = _failuresByIp.GetOrAdd(clientIp, _ =>
            {
                if (_failuresByIp.Count >= MaxTrackedIps)
                {
                    return null!;
                }
                return new ConcurrentQueue<DateTime>();
            });
            if (queue is null)
            {
                return;
            }

            queue.Enqueue(DateTime.UtcNow);
            Trim(queue);
        }

        public bool IsBlocked(string clientIp, int maxFailures, TimeSpan window)
        {
            if (!_failuresByIp.TryGetValue(clientIp, out var queue))
            {
                return false;
            }

            Trim(queue);
            var since = DateTime.UtcNow - window;
            return queue.Count(t => t >= since) >= maxFailures;
        }

        public int GetRecentFailureCount(TimeSpan window)
        {
            var since = DateTime.UtcNow - window;
            var count = 0;
            foreach (var queue in _failuresByIp.Values)
            {
                count += queue.Count(t => t >= since);
            }
            return count;
        }

        private static void Trim(ConcurrentQueue<DateTime> queue)
        {
            var cutoff = DateTime.UtcNow - Retention;
            while (queue.TryPeek(out var oldest) && oldest < cutoff)
            {
                queue.TryDequeue(out _);
            }
        }
    }
}
