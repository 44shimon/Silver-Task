using System.Threading.Channels;
using Silver_Task.Server.Common.Automation;

namespace Silver_Task.Server.Services
{
    /// <summary>One dispatched domain event, wrapped with the bookkeeping AutomationService needs
    /// but that TaskService/CommentService/etc. shouldn't have to know about: a unique EventId
    /// (duplicate-delivery detection — see AutomationExecution.TriggerEventId) and the chain depth
    /// it inherited from whatever ambient automation context was active when it was dispatched
    /// (loop protection — see AutomationExecutionContext).</summary>
    public record AutomationEventEnvelope(Guid EventId, IAutomationEvent Event, int ChainDepth, DateTime EnqueuedAt);

    /// <summary>The write side — TaskService/CommentService/AttachmentService/ProjectService call
    /// this right after committing a change, without knowing (or caring) whether any automation
    /// actually exists for it. Deliberately fire-and-forget from the caller's perspective: this
    /// only enqueues, it never evaluates or executes anything inline, so a normal user request's
    /// response time is never affected by how many/how expensive the matching automations are —
    /// see AutomationQueueBackgroundService for the actual processing.</summary>
    public interface IAutomationDispatcher
    {
        Task DispatchAsync(IAutomationEvent domainEvent);
    }

    /// <summary>The read side — used only by AutomationQueueBackgroundService.</summary>
    public interface IAutomationEventQueue
    {
        ChannelReader<AutomationEventEnvelope> Reader { get; }
    }

    /// <summary>Singleton (registered once, backing both interfaces above via the same instance —
    /// see Program.cs) wrapping an in-process, unbounded Channel&lt;T&gt; — the lightest possible
    /// reliable producer/consumer queue available in the BCL, appropriate here since automation
    /// processing (a) doesn't need to survive an app restart (an interrupted event is simply not
    /// retried — acceptable for this phase, see the final report's known limitations) and (b) this
    /// app has no existing message-queue infrastructure to reuse instead (Hangfire/Quartz/etc.
    /// would be new infrastructure the spec explicitly says not to introduce unnecessarily).</summary>
    public class AutomationDispatcher(ILogger<AutomationDispatcher> logger) : IAutomationDispatcher, IAutomationEventQueue
    {
        private readonly Channel<AutomationEventEnvelope> _channel = Channel.CreateUnbounded<AutomationEventEnvelope>();
        private readonly ILogger<AutomationDispatcher> _logger = logger;

        public ChannelReader<AutomationEventEnvelope> Reader => _channel.Reader;

        public Task DispatchAsync(IAutomationEvent domainEvent)
        {
            var envelope = new AutomationEventEnvelope(
                Guid.NewGuid(),
                domainEvent,
                AutomationExecutionContext.CurrentChainDepth,
                DateTime.UtcNow);

            if (!_channel.Writer.TryWrite(envelope))
            {
                // Unbounded channel — TryWrite only fails if the channel was completed, which
                // this app never does during normal operation.
                _logger.LogWarning("Automation event queue rejected a {TriggerType} event.", domainEvent.TriggerType);
            }

            return Task.CompletedTask;
        }
    }
}
