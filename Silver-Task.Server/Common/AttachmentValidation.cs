namespace Silver_Task.Server.Common
{
    /// <summary>
    /// Static, stateless file-upload validation shared by every AttachmentService upload path
    /// (Project/Task/Comment) — extension allow-list membership, declared Content-Type
    /// consistency, and magic-byte sniffing for the types where it's cheap and reliable. None of
    /// this trusts the client-supplied extension or Content-Type alone (per the explicit "never
    /// trust the file extension alone" requirement) — the strongest signal available for a given
    /// file type is used, and a mismatch anywhere is rejected.
    /// </summary>
    public static class AttachmentValidation
    {
        /// <summary>Extension -> acceptable declared Content-Type(s). DOCX/XLSX are themselves
        /// ZIP containers, so browsers/OSes sometimes report them as "application/zip" — accepted
        /// alongside their proper OOXML type rather than rejected as a mismatch.</summary>
        public static readonly IReadOnlyDictionary<string, string[]> ExtensionContentTypes =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                [".pdf"] = ["application/pdf"],
                [".doc"] = ["application/msword"],
                [".docx"] = ["application/vnd.openxmlformats-officedocument.wordprocessingml.document", "application/zip"],
                [".xls"] = ["application/vnd.ms-excel"],
                [".xlsx"] = ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "application/zip"],
                [".csv"] = ["text/csv", "application/vnd.ms-excel", "text/plain"],
                [".txt"] = ["text/plain"],
                [".jpg"] = ["image/jpeg"],
                [".jpeg"] = ["image/jpeg"],
                [".png"] = ["image/png"],
                [".gif"] = ["image/gif"],
                [".webp"] = ["image/webp"],
                [".zip"] = ["application/zip", "application/x-zip-compressed"],
            };

        // Magic-byte prefixes for the types where sniffing is cheap and reliable. Deliberately
        // not attempted for .doc/.xls (OLE2 container, shared by many legacy formats) or
        // .csv/.txt (plain text has no signature) — extension + Content-Type consistency is the
        // practical ceiling for those without a much heavier parsing dependency.
        private static readonly Dictionary<string, byte[][]> MagicBytes = new(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = [[0x25, 0x50, 0x44, 0x46]], // %PDF
            [".png"] = [[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]],
            [".jpg"] = [[0xFF, 0xD8, 0xFF]],
            [".jpeg"] = [[0xFF, 0xD8, 0xFF]],
            [".gif"] = [[0x47, 0x49, 0x46, 0x38, 0x37, 0x61], [0x47, 0x49, 0x46, 0x38, 0x39, 0x61]], // GIF87a / GIF89a
            [".zip"] = [[0x50, 0x4B, 0x03, 0x04], [0x50, 0x4B, 0x05, 0x06]],
            [".docx"] = [[0x50, 0x4B, 0x03, 0x04]],
            [".xlsx"] = [[0x50, 0x4B, 0x03, 0x04]],
            // RIFF....WEBP — the first 4 bytes are the generic RIFF container signature; bytes
            // 8-11 ("WEBP") are what actually distinguish it, checked separately below.
            [".webp"] = [[0x52, 0x49, 0x46, 0x46]],
        };

        public static IReadOnlyList<string> ParseAllowedExtensions(string settingValue) =>
            settingValue.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        public static bool IsExtensionAllowed(string extension, IReadOnlyList<string> allowedExtensions) =>
            allowedExtensions.Any(allowed => string.Equals(allowed, extension, StringComparison.OrdinalIgnoreCase));

        public static bool IsContentTypeConsistent(string extension, string? declaredContentType)
        {
            if (string.IsNullOrWhiteSpace(declaredContentType))
            {
                // Some OS/browser combinations omit Content-Type for less common extensions
                // (e.g. .csv on some platforms) — absence isn't itself suspicious the way an
                // outright mismatch is; the extension allow-list and magic-byte check (where one
                // exists for this extension) remain the authoritative gates either way.
                return true;
            }
            return ExtensionContentTypes.TryGetValue(extension, out var expected) &&
                expected.Any(t => string.Equals(t, declaredContentType, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>True if no signature check applies to this extension (nothing to contradict)
        /// or the header bytes match one of the extension's known signatures.</summary>
        public static bool IsSignatureConsistent(string extension, ReadOnlySpan<byte> header)
        {
            if (!MagicBytes.TryGetValue(extension, out var signatures))
            {
                return true;
            }

            foreach (var signature in signatures)
            {
                if (header.Length >= signature.Length && header[..signature.Length].SequenceEqual(signature))
                {
                    if (!string.Equals(extension, ".webp", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                    // WEBP: RIFF prefix alone is shared with other RIFF-based formats (e.g. WAV/AVI) —
                    // also require the "WEBP" tag at bytes 8-11 before accepting it as a real match.
                    if (header.Length >= 12 && header.Slice(8, 4).SequenceEqual("WEBP"u8))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
