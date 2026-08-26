namespace Silver_Task.Server.Models.DTOs.Attachments
{
    /// <summary>Every bulk file action (move/tag/untag/delete/favorite) re-runs the exact same
    /// per-file authorization the single-file endpoint would — one file failing permission/
    /// validation (e.g. it belongs to a project the caller lost access to mid-selection) never
    /// silently succeeds or aborts the rest of the batch; it's reported here instead.</summary>
    public class BulkActionResultDto
    {
        public required List<Guid> SucceededIds { get; set; }

        public required List<BulkActionFailureDto> Failed { get; set; }
    }

    public class BulkActionFailureDto
    {
        public Guid FileId { get; set; }

        public required string Error { get; set; }
    }
}
