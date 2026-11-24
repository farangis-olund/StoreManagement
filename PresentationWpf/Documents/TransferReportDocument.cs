using PresentationWpf.ViewModels;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PresentationWpf.Documents
{
    public class TransferReportDocument : IDocument
    {
        private readonly string _orgInfo;
        private readonly DateTime _date;
        private readonly string _type;
        private readonly List<TransferReportLine> _lines;
        private readonly string _description;
        private readonly string _reportType;
        private readonly string _storeName;
        private readonly bool _groupByStore;

        public TransferReportDocument(
            string orgInfo,
            DateTime date,
            string type,
            List<TransferReportLine> lines,
            string description,
            string reportType,
            string storeName,
            bool groupByStore = false)
        {
            _orgInfo = orgInfo;
            _date = date;
            _type = type;
            _lines = lines.OrderBy(x => x.StoreCode, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Article, StringComparer.OrdinalIgnoreCase).ToList();
            _description = description;
            _reportType = reportType;
            _storeName = storeName;
            _groupByStore = groupByStore;
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
                        row.RelativeItem().Text($"Магазин: {_orgInfo}");
                        row.ConstantItem(200).AlignRight()
                            .Text($"Дата: {_date:dd.MM.yyyy HH:mm}");
                    });

                    header.Item().LineHorizontal(0.01f);
                });

                // ===== CONTENT =====
                page.Content().Column(content =>
                {
                    content.Spacing(6);

                    // --- Report title ---
                    content.Item().AlignCenter()
                        .PaddingTop(10)
                        .Text($"ОТЧЁТ: {_type.ToUpper()}")
                        .Bold()
                        .FontSize(12);

                    // --- Store name ---
                    if (!string.IsNullOrWhiteSpace(_storeName))
                    {
                        content.Item().AlignCenter()
                            .PaddingBottom(6)
                            .Text(_groupByStore ? "Магазины: Все" : $"Операция для магазина: {_storeName}")
                            .FontSize(10)
                            .FontColor(Colors.Grey.Darken2);
                    }

                    // --- Description (if exists) ---
                    if (!string.IsNullOrWhiteSpace(_description))
                    {
                        content.Item().AlignCenter()
                            .PaddingBottom(6)
                            .Text(_description)
                            .FontSize(10)
                            .Italic()
                            .FontColor(Colors.Grey.Darken2);
                    }

                    // --- Table ---
                    var groups = _groupByStore
                        ? _lines.GroupBy(x => x.StoreCode, StringComparer.OrdinalIgnoreCase).Select(g => g.ToList()).ToArray()
                        : new[] { _lines };
                    foreach (var group in groups)
                    {
                    content.Item().PaddingTop(8).Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(1.5f); // Артикул
                            cols.RelativeColumn(2.5f); // Наименование
                            cols.RelativeColumn(1.2f); // Бренд
                            cols.RelativeColumn(1.2f); // Марка
                            cols.RelativeColumn(1.8f); // Модель
                            cols.RelativeColumn(0.8f); // Кол-во/Долг
                            cols.RelativeColumn(1.2f); // Место
                        });

                        string[] headers = _reportType == "view"
                            ? new[] { "Артикул", "Наименование", "Бренд", "Марка", "Модель", "Долг", "Место" }
                            : new[] { "Артикул", "Наименование", "Бренд", "Марка", "Модель", "Кол-во", "Место" };

                        // Header row
                        table.Header(header =>
                        {
                            if (_groupByStore)
                            {
                                var store = group[0];
                                var label = string.IsNullOrWhiteSpace(store.StoreName)
                                    ? store.StoreCode : $"{store.StoreCode} · {store.StoreName}";
                                header.Cell().ColumnSpan(7).Background(Colors.Grey.Lighten2).Padding(5)
                                    .Text($"Магазин: {label}").Bold();
                            }
                            foreach (var h in headers)
                            {
                                header.Cell()
                                    .Border(0.25f)
                                    .BorderColor(Colors.Grey.Medium)
                                    .Background(Colors.Grey.Lighten3)
                                    .Padding(3)
                                    .AlignCenter()
                                    .Text(h)
                                    .Bold()
                                    .FontSize(10)
                                    .FontColor(Colors.Grey.Darken3);
                            }
                        });

                        // Data rows
                        foreach (var line in group)
                        {
                            void Cell(string value, bool center = false)
                            {
                                var cell = table.Cell()
                                    .Border(0.25f)
                                    .BorderColor(Colors.Grey.Medium)
                                    .Padding(3)
                                    .AlignMiddle();
                                var text = cell.Text(value ?? string.Empty).FontSize(10);
                                if (center) text.AlignCenter();
                            }

                            Cell(Trim(line.Article, 15));          // Артикул
                            Cell(Trim(line.ProductName, 23));      // Наименование
                            Cell(Trim(line.Brand, 12));            // Бренд
                            Cell(Trim(line.Marka, 12));            // Марка
                            Cell(Trim(line.Model, 16));            // Модель
                            Cell(line.Quantity.ToString(), center: true);
                            Cell(Trim(line.WarehousePlace, 10), center: true); // Место
                        }
                    });
                    if (_groupByStore)
                        content.Item().AlignRight().Text($"Итого по магазину: {group.Sum(x => (long)x.Quantity)} шт.").Bold();
                    }

                    // --- Totals ---
                    content.Item().PaddingTop(6)
                        .Text($"Всего наименований: {_lines.Count}   Общий объём: {_lines.Sum(x => (long)x.Quantity)}")
                        .Bold()
                        .FontSize(10);
                });

                // ===== FOOTER =====
                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Стр. ");
                    x.CurrentPageNumber();
                    x.Span(" / ");
                    x.TotalPages();
                });
            });
        }

        // --- Helpers ---
        private static string Trim(string value, int max)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "";
            return value.Length <= max ? value : value.Substring(0, max - 1);
        }
    }
}
