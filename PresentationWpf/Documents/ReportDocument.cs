using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Data;
using PresentationWpf.ViewModels;

namespace PresentationWpf.Documents
{
    public class ReportDocument : IDocument
    {
        private readonly ReportViewModel _vm;

        public ReportDocument(ReportViewModel vm)
        {
            _vm = vm;
        }

        public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

        public void Compose(IDocumentContainer container)
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(0.5f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(10));

                // ===== HEADER =====
                page.Header().Column(header =>
                {
                    header.Item().Row(row =>
                    {
                        // Left side: store name
                        row.RelativeItem().Text(text =>
                        {
                            text.Span("Магазин: ").FontSize(10);
                            text.Line($"{_vm.OrganizationName}");
                        });

                        // Right side: date
                        row.ConstantItem(200).AlignRight().Column(col =>
                        {
                            col.Item().Text($"Дата отчёта: {_vm.SelectedDate:dd.MM.yyyy}");
                        });
                    });

                    header.Item().LineHorizontal(0.01f);
                });

                // ===== CONTENT =====
                page.Content().Column(content =>
                {
                    content.Spacing(6);

                    // --- Title ---
                    content.Item().AlignCenter().PaddingTop(6)
                        .Text(text =>
                        {
                            text.Span(_vm.Title.ToUpper()).FontSize(12).Bold();
                        });

                    // --- Table ---
                    var dt = _vm.Table;
                    content.Item().PaddingTop(8).Table(table =>
                    {
                        // define columns dynamically
                        table.ColumnsDefinition(cols =>
                        {
                            foreach (DataColumn col in dt.Columns)
                                cols.RelativeColumn(1);
                        });

                        // Table header (repeats on pages)
                        table.Header(header =>
                        {
                            foreach (DataColumn col in dt.Columns)
                            {
                                header.Cell()
                                    .Border(0.25f)
                                    .BorderColor(Colors.Grey.Medium)
                                    .Background(Colors.Grey.Lighten3)
                                    .Padding(2)
                                    .MinHeight(14)
                                    .AlignMiddle()
                                    .Text(col.ColumnName)
                                    .Bold()
                                    .FontSize(10)
                                    .FontColor(Colors.Grey.Darken3);
                            }
                        });

                        // --- Data rows (no wrapping, trimmed) ---
                        foreach (DataRow row in dt.Rows)
                        {
                            foreach (var cell in row.ItemArray)
                            {
                                string value = cell?.ToString() ?? "";
                                var trimmed = Trim(value, 16);

                                void Cell(string val)
                                {
                                    var cellItem = table.Cell()
                                        .Border(0.25f)
                                        .BorderColor(Colors.Grey.Medium)
                                        .Padding(2)
                                        .MinHeight(14)
                                        .AlignMiddle();
                                    var txt = cellItem.Text(val ?? string.Empty)
                                        .FontSize(10)
                                        .FontColor(Colors.Black);
                                    txt.AlignLeft(); // consistent alignment
                                }

                                Cell(trimmed);
                            }
                        }
                    });

                    // --- Summary ---
                    content.Item().PaddingTop(3).Text(
                        $"Всего записей: {_vm.Table.Rows.Count}"
                    ).FontSize(10).Bold();
                });

                // ===== FOOTER =====
                page.Footer().Column(col =>
                {
                    col.Item().Height(25);
                    col.Item().AlignCenter().Text(x =>
                    {
                        x.Span("Стр. ");
                        x.CurrentPageNumber();
                        x.Span(" / ");
                        x.TotalPages();
                    });
                });
            });
        }

        // --- Helpers ---
        private static string Trim(string value, int max)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "";
            return value.Length <= max ? value : value.Substring(0, max - 3);
        }
    }
}
