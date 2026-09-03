namespace Silver_Task.Server.Models.Common
{
    /// <summary>Phase 61 — the one canonical pagination envelope for the public v1 API
    /// (Controllers/V1/*), replacing the ad-hoc per-controller shapes the internal API had
    /// accumulated (EmailDeliveryPageDto, ProjectsController.GetFiles's inline anonymous shape).
    /// Internal endpoints keep their existing shapes unchanged — this is additive, not a
    /// "replace working internal APIs" change.</summary>
    public class PagedResult<T>
    {
        public required IReadOnlyList<T> Items { get; set; }

        public int Page { get; set; }

        public int PageSize { get; set; }

        public int TotalCount { get; set; }

        public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 0;
    }
}
