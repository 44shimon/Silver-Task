namespace Silver_Task.Server.Common.Automation
{
    /// <summary>Ambient "how deep in an automation chain is the current call" tracker (Phase 35
    /// loop protection) — implemented with AsyncLocal, the same ambient-context technique
    /// ASP.NET Core itself uses for HttpContext, so that a service like TaskService can dispatch
    /// an event without needing a "chain depth" parameter threaded through every method signature
    /// it (and everything it calls) already has. AutomationService wraps each action's execution
    /// in EnterChain(depth + 1) before calling into TaskService/CommentService/etc.; any event
    /// those calls raise as a normal part of their own logic is automatically stamped with that
    /// depth by AutomationDispatcher. A genuine user-initiated request never calls EnterChain, so
    /// CurrentChainDepth is 0 for it by default.</summary>
    public static class AutomationExecutionContext
    {
        private static readonly AsyncLocal<int> ChainDepthLocal = new();

        public static int CurrentChainDepth => ChainDepthLocal.Value;

        public static IDisposable EnterChain(int depth)
        {
            var previous = ChainDepthLocal.Value;
            ChainDepthLocal.Value = depth;
            return new ChainScope(previous);
        }

        private sealed class ChainScope(int previousDepth) : IDisposable
        {
            public void Dispose() => ChainDepthLocal.Value = previousDepth;
        }
    }
}
