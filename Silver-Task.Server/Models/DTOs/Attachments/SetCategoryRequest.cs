namespace Silver_Task.Server.Models.DTOs.Attachments
{
    public class SetCategoryRequest
    {
        /// <summary>Null clears the file's category.</summary>
        public Guid? CategoryId { get; set; }
    }
}
