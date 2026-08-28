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
        After,

        /// <summary>Phase 43 — Text filter operators (Saved Views). Added alongside Between
        /// rather than as a Saved-View-only enum so this stays the one shared operator vocabulary
        /// across Automation conditions, Custom Field conditional-visibility, and Saved View
        /// filters (see CustomFieldConditionEvaluator's own doc comment).</summary>
        StartsWith,
        EndsWith,

        /// <summary>Phase 43 — inclusive range comparison (Number/Currency/Date Saved View
        /// filters). The range's upper bound is carried out-of-band (SavedViewFilterConditionDto.
        /// ValueTo) since AutomationCondition/CustomField's own single ConditionValue string can't
        /// hold two values — see SavedViewFilterEngine for where ValueTo is consumed.</summary>
        Between
    }
}
