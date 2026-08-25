using System.ComponentModel.DataAnnotations;

namespace Silver_Task.Server.Models.DTOs.Tasks
{
    public class SetTaskSortOrderRequest
    {
        [Required]
        public double SortOrder { get; set; }
    }
}
