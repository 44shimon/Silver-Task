namespace Silver_Task.Server.Models.DTOs.Admin
{
    public class AdminStatsDto
    {
        public int TotalUsers { get; set; }

        public int ActiveUsers { get; set; }

        /// <summary>Excludes archived projects, matching what "Total Projects" means everywhere
        /// else in the app (the sidebar/project list default).</summary>
        public int TotalProjects { get; set; }

        public int TotalTasks { get; set; }

        /// <summary>Not Complete and not Cancelled — same definition used by the My Tasks summary.</summary>
        public int OpenTasks { get; set; }

        public int CompletedTasks { get; set; }

        /// <summary>Phase 37 — completed today, server UTC day (admin-facing system stat, not a
        /// per-user timezone-sensitive figure like the dashboard's own widgets).</summary>
        public int CompletedToday { get; set; }

        public int OverdueTasks { get; set; }
    }
}
