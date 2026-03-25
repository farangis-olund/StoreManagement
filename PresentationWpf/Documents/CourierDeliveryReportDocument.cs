using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;


namespace PresentationWpf.Documents
{
    public class CourierDeliveryReportDocument : IDocument
    {
        private readonly string _orgName;
        private readonly string _courierName;
        private readonly List<CourierDeliveryLine> _lines;
        private readonly string _title;
        private readonly DateTime? _date;

        public CourierDeliveryReportDocument(
            string orgName,
            string courierName,
            List<CourierDeliveryLine> lines,
            string title,
            DateTime? date = null)
        {
            _orgName = orgName;
            _courierName = courierName;
            _lines = lines;
            _title = title;
            _date = date;
        }

        public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

        public void Compose(IDocumentContainer container)
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(1f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(10));

                // ===== HEADER =====
                page.Header().Column(header =>
                {
                    header.Item().Row(row =>
                    {
                        row.RelativeItem().Text($"Магазин: {_orgName}");
                        row.ConstantItem(200).AlignRight().Text(_date.HasValue
                            ? $"Дата: {_date:dd.MM.yyyy}"
                            : $"Отчёт сформирован: {DateTime.Now:dd.MM.yyyy HH:mm}");
                    });

                    header.Item().LineHorizontal(0.01f);
                });

                // ===== CONTENT =====
                page.Content().Column(content =>
                {
                    content.Spacing(6);

                    // --- Title ---
                    content.Item().AlignCenter()
                        .PaddingTop(10)
                        .Text(_title.ToUpper())
                        .Bold()
                        .FontSize(12);

                    // --- Courier ---
                    content.Item().AlignCenter()
                        .PaddingBottom(8)
                        .Text($"Доставщик: {_courierName}")
                        .FontSize(10)
                        .FontColor(Colors.Grey.Darken2);

                    // --- Table ---
                    content.Item().PaddingTop(8).Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(0.8f); // Дата
                            cols.RelativeColumn(0.9f); // Код кл.
                            cols.RelativeColumn(1.3f); // ФИО
                            cols.RelativeColumn(1.3f); // Адрес
                            cols.RelativeColumn(1.0f); // Тел
                            cols.RelativeColumn(1.3f); // Накладная
                            cols.RelativeColumn(1.0f); // Сумма
                            cols.RelativeColumn(1.0f); // Сумма (СМН)
                            cols.RelativeColumn(1.0f); // Платеж
                        });

                        string[] headers = new[]
                        {
                            "Дата", "Код кл.", "ФИО", "Адрес","Тел", "Накладная", "Сумма", "Платеж(СМН)", "Платеж"
                        };

                        table.Header(header =>
                        {
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
                                    .FontSize(10);
                            }
                        });

                        foreach (var line in _lines)
                        {
                            void Cell(string text, bool right = false)
                            {
                                var cell = table.Cell()
                                    .Border(0.25f)
                                    .BorderColor(Colors.Grey.Medium)
                                    .Padding(3)
                                    .AlignMiddle();
                                var t = cell.Text(text ?? string.Empty).FontSize(10);
                                if (right) t.AlignRight();
                            }

                            Cell(line.Date.ToString("dd.MM.yy"));
                            Cell(line.CustomerId);
                            Cell(Trim(line.FullName, 18));
                            Cell(Trim(line.Address, 18));
                            Cell(Trim(line.Phone, 10));
                            Cell(line.Invoice);
                            Cell(line.Sale.ToString("N2"), right: true);
                            Cell(line.Amount.ToString("N2"), right: true);
                            Cell(line.Payment.ToString("N2"), right: true);
                        }
                    });

                    // --- Totals ---
                    content.Item().PaddingTop(6).Text(t =>
                    {
                        t.Span($"Всего клиентов: {_lines.Count}   ");
                        t.Span($"Общая сумма: {_lines.Sum(x => x.Sale):N2}   ");
                        t.Span($"Платеж(СМН) итого: {_lines.Sum(x => x.Amount):N2}   ");
                        t.Span($"Платеж итого: {_lines.Sum(x => x.Payment):N2}");
                    });
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

        private static string Trim(string value, int max)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "";
            return value.Length <= max ? value : value.Substring(0, max - 1);
        }
    }

    public class CourierDeliveryLine
    {
        public DateTime Date { get; set; }
        public string CustomerId { get; set; } = "";
        public string FullName { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Address { get; set; } = "";
        public string City { get; set; } = "";
        public string Invoice { get; set; } = "";
        public decimal Sale { get; set; }
        public decimal Amount { get; set; }
        public decimal Payment { get; set; }
    }
}
