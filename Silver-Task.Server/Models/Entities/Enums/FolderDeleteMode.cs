namespace Silver_Task.Server.Models.Entities.Enums
{
    /// <summary>How a non-empty folder's contents are handled when it's deleted (Phase 34) — the
    /// confirmation dialog's "Move contents to parent folder" / "Delete folder and contents"
    /// choice. Never persisted; this is a request-only value, same category as
    /// RecurrenceEditScope.</summary>
    public enum FolderDeleteMode
    {
        MoveContentsToParent,
        DeleteContents
    }
}
