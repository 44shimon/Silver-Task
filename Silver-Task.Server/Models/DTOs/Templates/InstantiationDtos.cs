namespace Silver_Task.Server.Models.DTOs.Templates
{
    /// <summary>The wizard's "Project Information" + "Configure Assignments" steps. The project's
    /// owner is always the caller (matching how project creation works everywhere else in this
    /// app — see ProjectService.CreateAsync) — there is no separate "Project Manager" picker;
    /// AssignmentMode.ProjectManager resolves to the caller/owner at instantiation time, which is
    /// this app's own existing "owner is always implicitly the Manager" rule (see
    /// ProjectAccessService), not a new concept.</summary>
    public class CreateProjectFromTemplateRequest
    {
        public Guid TemplateId { get; set; }

        public required string ProjectName { get; set; }

        public string? ProjectDescription { get; set; }

        public required DateOnly StartDate { get; set; }

        /// <summary>Null = use each task's own template default. One of
        /// TemplateAssignmentModes.ProjectManager/Unassigned — a single global override applied
        /// uniformly to every task, rather than a per-task override grid (a disclosed scope
        /// simplification — see the Phase 40 final report). SpecificUser is intentionally not a
        /// valid override value here (there is no single user that would make sense for every
        /// task at once); per-task SpecificUser assignments always come from the template itself.</summary>
        public string? AssignmentOverride { get; set; }
    }

    public class CreateTaskFromTemplateRequest
    {
        public Guid TemplateId { get; set; }

        public Guid ProjectId { get; set; }

        /// <summary>The anchor date StartOffsetDays/DueOffsetDays are relative to — defaults to
        /// today (in the caller's own timezone) when omitted, since a standalone task template has
        /// no project start date to anchor to.</summary>
        public DateOnly? StartDateOverride { get; set; }
    }

    public class TemplateScheduleItemDto
    {
        public Guid TemplateTaskId { get; set; }

        public required string Title { get; set; }

        public DateOnly? ComputedStartDate { get; set; }

        public DateOnly? ComputedDueDate { get; set; }
    }

    /// <summary>The wizard's Preview step (spec #10/#54) — read-only, no writes. Warnings (spec
    /// #80) flag schedules where a Finish-to-Start/Start-to-Start prerequisite's own computed date
    /// falls AFTER its dependent's — never silently changed, just surfaced so the user can go back
    /// and adjust offsets before creating anything.</summary>
    public class ProjectTemplatePreviewDto
    {
        public required string TemplateName { get; set; }

        public int TaskCount { get; set; }

        public int SubtaskCount { get; set; }

        public int DependencyCount { get; set; }

        public int? EstimatedDurationDays { get; set; }

        public required List<TemplateScheduleItemDto> Schedule { get; set; }

        public required List<string> Warnings { get; set; }
    }
}
