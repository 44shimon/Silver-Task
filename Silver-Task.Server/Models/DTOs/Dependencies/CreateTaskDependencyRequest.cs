using System.ComponentModel.DataAnnotations;
using Silver_Task.Server.Common;

namespace Silver_Task.Server.Models.DTOs.Dependencies
{
    public class CreateTaskDependencyRequest
    {
        [Required]
        public Guid DependsOnTaskId { get; set; }

        /// <summary>One of DependencyTypes.All — null/omitted defaults to FinishToStart (the
        /// spec's own stated default), validated against the whitelist in
        /// TaskDependencyService.CreateAsync, never trusted as free text beyond that check.</summary>
        public string? DependencyType { get; set; }

        public string ResolvedDependencyType => string.IsNullOrWhiteSpace(DependencyType) ? DependencyTypes.FinishToStart : DependencyType;
    }
}
