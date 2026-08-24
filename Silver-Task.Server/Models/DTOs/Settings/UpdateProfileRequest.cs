using System.ComponentModel.DataAnnotations;

namespace Silver_Task.Server.Models.DTOs.Settings
{
    /// <summary>Deliberately has no Role/IsActive field — the self-service profile endpoint
    /// structurally cannot change either, which is a stronger guarantee against privilege
    /// escalation than a runtime check would be (there's simply nothing here to elevate).</summary>
    public class UpdateProfileRequest
    {
        [Required, StringLength(200, MinimumLength = 1)]
        public required string Name { get; set; }

        [Required, EmailAddress, StringLength(320)]
        public required string Email { get; set; }
    }
}
