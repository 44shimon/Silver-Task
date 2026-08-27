namespace Silver_Task.Server.Models.Entities.Enums
{
    /// <summary>Fixed operator set (Phase 35 spec) — deliberately closed, no "custom expression"
    /// escape hatch, so a condition can never smuggle in arbitrary logic (see the spec's own "no
    /// script execution" requirement). Before/After behave identically to
    /// GreaterThan/LessThan but are offered as separate, clearer options for date fields in the
    /// builder UI.</summary>
    public enum AutomationConditionOperator
    {
        Equals,
        NotEquals,
        Contains,
        NotContains,
        IsEmpty,
        IsNotEmpty,
        GreaterThan,
        LessThan,
        GreaterThanOrEqual,
        LessThanOrEqual,
        Before,
        After
    }
}
