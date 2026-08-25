using System.ComponentModel.DataAnnotations;

namespace Silver_Task.Server.Models.DTOs.CustomFields
{
    public class CustomFieldOptionRequest
    {
        [Required, StringLength(200, MinimumLength = 1)]
        public required string Value { get; set; }

        /// <summary>Omit to leave unchanged — lets a single PUT rename, reorder, and/or
        /// enable/disable an option in one call.</summary>
        public int? SortOrder { get; set; }

        public bool? IsActive { get; set; }
    }
}
