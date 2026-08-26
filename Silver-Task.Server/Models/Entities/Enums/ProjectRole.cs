namespace Silver_Task.Server.Models.Entities.Enums
{
    /// <summary>Per-project role (Phase 32) — independent of the member's system-wide UserRole.
    /// A global Manager added to a project isn't automatically able to manage *that* project
    /// unless their ProjectMember row also says Manager; a global Member can be made a project's
    /// Manager the same way. The project owner is always treated as an implicit Manager
    /// (ProjectAccessService bypasses this enum for them directly) but also gets Role=Manager on
    /// their own membership row so project-member lists display it consistently.</summary>
    public enum ProjectRole
    {
        Manager,
        Member,
        Viewer
    }
}
