using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Silver_Task.Server.Services
{
    public enum ReportExportFormat
    {
        Csv,
        Excel,
        Pdf
    }

    /// <summary>A single generic tabular exporter every report endpoint shares — callers flatten
    /// whatever report DTO they have into headers + string rows first (see ReportsController),
    /// so this never needs to know about any specific report shape. CSV is hand-rolled (no
    /// dependency needed for delimited text); Excel uses ClosedXML (MIT-licensed); PDF uses
    /// QuestPDF, whose free Community license is revenue-gated (free for organizations/individuals
    /// under $1M USD annual gross revenue, or non-profit/personal/educational use) — see this
    /// project's Phase 38 final report for that disclosed licensing caveat. Kept behind this one
    /// interface specifically so swapping the PDF engine later, if ever needed, is a contained
    /// change (same "swappable behind an interface" precedent as IAttachmentService's storage
    /// backend).</summary>
    public interface IReportExportService
    {
        byte[] Export(ReportExportFormat format, string title, IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string>> rows);

        string GetContentType(ReportExportFormat format);

        string GetFileExtension(ReportExportFormat format);
    }

    public class ReportExportService : IReportExportService
    {
        public byte[] Export(ReportExportFormat format, string title, IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string>> rows) => format switch
        {
            ReportExportFormat.Csv => ExportCsv(headers, rows),
            ReportExportFormat.Excel => ExportExcel(title, headers, rows),
            ReportExportFormat.Pdf => ExportPdf(title, headers, rows),
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };

        public string GetContentType(ReportExportFormat format) => format switch
        {
            ReportExportFormat.Csv => "text/csv",
            ReportExportFormat.Excel => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ReportExportFormat.Pdf => "application/pdf",
            _ => "application/octet-stream"
        };

        public string GetFileExtension(ReportExportFormat format) => format switch
        {
            ReportExportFormat.Csv => "csv",
            ReportExportFormat.Excel => "xlsx",
            ReportExportFormat.Pdf => "pdf",
            _ => "bin"
        };

        private static byte[] ExportCsv(IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string>> rows)
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(',', headers.Select(EscapeCsvField)));
            foreach (var row in rows)
            {
                sb.AppendLine(string.Join(',', row.Select(EscapeCsvField)));
            }
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetBytes(sb.ToString());
        }

        private static string EscapeCsvField(string field)
        {
            if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
            {
                return $"\"{field.Replace("\"", "\"\"")}\"";
            }
            return field;
        }

        private static byte[] ExportExcel(string title, IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string>> rows)
        {
            using var workbook = new XLWorkbook();
            var sheetName = title.Length > 31 ? title[..31] : title;
            var sheet = workbook.Worksheets.Add(string.IsNullOrWhiteSpace(sheetName) ? "Report" : sheetName);

            for (var col = 0; col < headers.Count; col++)
            {
                var cell = sheet.Cell(1, col + 1);
                cell.Value = headers[col];
                cell.Style.Font.Bold = true;
            }

            for (var row = 0; row < rows.Count; row++)
            {
                for (var col = 0; col < rows[row].Count; col++)
                {
                    sheet.Cell(row + 2, col + 1).Value = rows[row][col];
                }
            }

            sheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        private static byte[] ExportPdf(string title, IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string>> rows)
        {
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(30);
                    page.DefaultTextStyle(t => t.FontSize(9));

                    page.Header().Text(title).FontSize(16).Bold();

                    page.Content().PaddingTop(10).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            foreach (var _ in headers)
                            {
                                columns.RelativeColumn();
                            }
                        });

                        table.Header(header =>
                        {
                            foreach (var h in headers)
                            {
                                header.Cell().Border(1).Padding(4).Background(Colors.Grey.Lighten3).Text(h).Bold();
                            }
                        });

                        foreach (var row in rows)
                        {
                            foreach (var value in row)
                            {
                                table.Cell().Border(1).Padding(4).Text(value);
                            }
                        }
                    });

                    page.Footer().AlignCenter().Text(x =>
                    {
                        x.Span("Generated ");
                        x.Span(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture));
                    });
                });
            });

            return document.GeneratePdf();
        }
    }
}
