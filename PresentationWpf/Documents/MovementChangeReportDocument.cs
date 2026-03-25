using PresentationWpf.ViewModels;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PresentationWpf.Documents;

public class MovementChangeReportDocument : IDocument
{
    private readonly string _shopName;
    private readonly DateTime _date;
    private readonly IEnumerable<MovementChangeReportItem> _changes;

    public MovementChangeReportDocument(string shopName, DateTime date, IEnumerable<MovementChangeReportItem> changes)
    {
        _shopName = shopName;
        _date = date;
        _changes = changes;
    }

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(0.5f, Unit.Centimetre);
            page.DefaultTextStyle(x => x.FontSize(10));

            // === HEADER ===
            page.Header().Row(row =>
            {
                row.RelativeItem().Text($"Магазин: {_shopName}");
                row.ConstantItem(200).AlignRight().Text($"{_date:dd.MM.yyyy HH:mm}");
            });

            page.Content().Column(content =>
            {
                content.Spacing(6);
                content.Item().AlignCenter().Text("СПИСОК ИЗМЕНЕНИЙ ТОВАРОВ").Bold().FontSize(12);

                // --- TABLE ---
                content.Item().PaddingTop(8).Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(1.5f); // Артикул
                        cols.RelativeColumn(2.5f); // Наименование
                        cols.RelativeColumn(1.2f); // Бренд
                        cols.RelativeColumn(0.8f); // Кол-во в БД
                        cols.RelativeColumn(0.8f); // Приход/Расход
                        cols.RelativeColumn(0.8f); // Итого
                        cols.RelativeColumn(1f); // Место
                    });

                    string[] headers = { "Артикул", "Наименование", "Бренд", "Кол-во", "Прих/Рас", "Итого", "Место" };

                    table.Header(header =>
                    {
                        foreach (var h in headers)
                        {
                            header.Cell().Border(0.25f)
                                .BorderColor(Colors.Grey.Medium)
                                .Background(Colors.Grey.Lighten3)
                                .Padding(2)
                                .AlignMiddle()
                                .Text(h).Bold().FontSize(10).FontColor(Colors.Grey.Darken3);
                        }
                    });

                    foreach (var item in _changes)
                    {
                        void Cell(string value, bool center = false, bool right = false)
                        {
                            var cell = table.Cell()
                                .Border(0.25f)
                                .BorderColor(Colors.Grey.Medium)
                                .Padding(2)
                                .MinHeight(14)
                                .AlignMiddle();
                            var text = cell.Text(Trim(value, 25));
                            if (center) text.AlignCenter();
                            else if (right) text.AlignRight();
                        }

                        Cell(item.Article);
                        Cell(item.ProductName);
                        Cell(item.Brand);
                        Cell(item.QuantityDb.ToString(), center: true);
                        Cell(item.Quantity.ToString(), center: true);
                        Cell(item.Total.ToString(), center: true);
                        Cell(item.WarehousePlace);
                    }
                });

                // --- SUMMARY ---
                var totalItems = _changes.Count();
                var totalQty = _changes.Sum(c => c.Quantity);
                content.Item().PaddingTop(3).Text($"Всего наименований: {totalItems}    Итого кол-во: {totalQty}")
                    .FontSize(10).Bold();
            });

            // FOOTER
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
        return value.Length <= max ? value : value.Substring(0, max - 3);
    }
}
