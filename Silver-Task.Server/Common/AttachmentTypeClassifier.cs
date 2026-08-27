namespace Silver_Task.Server.Common
{
    /// <summary>Buckets a MIME type into the same pdf/image/spreadsheet/document/archive/other
    /// categories AttachmentService's own project-files type filter already uses (Phase 33/34) —
    /// extracted here as a small reusable helper purely so AutomationService's "File.FileType"
    /// condition field can reuse the exact same categorization rather than a third copy of this
    /// logic (the frontend's utils/attachmentType.ts is the second).</summary>
    public static class AttachmentTypeClassifier
    {
        private static readonly HashSet<string> SpreadsheetTypes =
        [
            "text/csv", "application/vnd.ms-excel", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
        ];

        private static readonly HashSet<string> DocumentTypes =
        [
            "text/plain", "application/msword", "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
        ];

        private static readonly HashSet<string> ArchiveTypes = ["application/zip", "application/x-zip-compressed"];

        public static string Classify(string mimeType)
        {
            if (mimeType == "application/pdf") return "pdf";
            if (mimeType.StartsWith("image/", StringComparison.Ordinal)) return "image";
            if (SpreadsheetTypes.Contains(mimeType)) return "spreadsheet";
            if (DocumentTypes.Contains(mimeType)) return "document";
            if (ArchiveTypes.Contains(mimeType)) return "archive";
            return "other";
        }
    }
}
