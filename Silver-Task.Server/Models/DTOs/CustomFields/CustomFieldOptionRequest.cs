using System.ComponentModel.DataAnnotations;

namespace Silver_Task.Server.Models.DTOs.CustomFields
{
    public class CustomFieldOptionRequest
    {
        [Required, StringLength(200, MinimumLength = 1)]
        public required string Value { get; set; }
    }
}
