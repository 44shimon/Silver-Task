namespace Silver_Task.Server.Models.DTOs.Settings
{
    public class SystemSettingDto
    {
        public required string Key { get; set; }

        public required string Section { get; set; }

        public required string Value { get; set; }

        public required string ValueType { get; set; }

        public string? Description { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public string? UpdatedByName { get; set; }
    }
}
