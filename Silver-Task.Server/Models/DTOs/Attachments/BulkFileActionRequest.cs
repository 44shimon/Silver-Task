using System.ComponentModel.DataAnnotations;

namespace Silver_Task.Server.Models.DTOs.Attachments
{
    public class BulkMoveRequest
    {
        [Required, MinLength(1)]
        public required List<Guid> FileIds { get; set; }

        public Guid? FolderId { get; set; }
    }

    public class BulkTagRequest
    {
        [Required, MinLength(1)]
        public required List<Guid> FileIds { get; set; }

        [Required, StringLength(50, MinimumLength = 1)]
        public required string TagName { get; set; }
    }

    public class BulkUntagRequest
    {
        [Required, MinLength(1)]
        public required List<Guid> FileIds { get; set; }

        public Guid TagId { get; set; }
    }

    public class BulkDeleteRequest
    {
        [Required, MinLength(1)]
        public required List<Guid> FileIds { get; set; }
    }

    public class BulkFavoriteRequest
    {
        [Required, MinLength(1)]
        public required List<Guid> FileIds { get; set; }

        public bool Favorite { get; set; }
    }
}
