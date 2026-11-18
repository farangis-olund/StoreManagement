using Infrastructure.Dtos;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;

namespace PresentationWpf.Documents;

public sealed class StoreExchangeReportDocument(IReadOnlyList<StoreExchangeReportRow> rows, string shop, DateTime asOf) : IDocument
{
    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(1, Unit.Centimetre);
            page.DefaultTextStyle(x => x.FontSize(10));
            page.Header().Column(header =>
            {
                header.Spacing(6);
                header.Item().Row(row =>
                {
                    row.RelativeItem().Text($"Магазин: {shop}");
                    row.ConstantItem(185).AlignRight().Text($"Данные на {asOf:dd.MM.yyyy HH:mm}");
                });
                header.Item().AlignCenter().Text("ОБ ОБМЕНЕ ТОВАРОВ МАГАЗИНАМИ").FontSize(14).Bold();
                header.Item().Text("Все товары. Приход и расход - текущие незакрытые обмены со всеми магазинами.").FontSize(9);
                header.Item().Text("Остаток = количество на складе - приход + расход.").FontSize(9);
                header.Item().LineHorizontal(.6f);
            });
            page.Content().PaddingTop(10).Column(column =>
            {
                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(32);
                        c.RelativeColumn(1.7f);
                        for (var i = 0; i < 4; i++) c.RelativeColumn();
                    });
                    table.Header(header =>
                    {
                        foreach (var title in new[] { "№", "Артикул", "Кол-во на складе", "Кол-во приходов", "Кол-во расходов", "Остаток" })
                            header.Cell().Background(Colors.Grey.Lighten3).Element(Cell).AlignCenter().Text(title).Bold();
                    });
                    foreach (var row in rows)
                    {
                        table.Cell().Element(Cell).AlignRight().Text(row.Number.ToString());
                        table.Cell().Element(Cell).Text(row.ArticleNumber);
                        foreach (var quantity in new[] { row.WarehouseQuantity, row.IncomingQuantity, row.OutgoingQuantity, row.RemainingQuantity })
                            table.Cell().Element(Cell).AlignRight().Text(Number(quantity));
                    }
                    if (rows.Count == 0) table.Cell().ColumnSpan(6).Element(Cell).Text("Нет товаров.");
                    table.Cell().ColumnSpan(2).Background(Colors.Grey.Lighten3).Element(Cell).Text("Итого").Bold();
                    foreach (var sum in new[] { rows.Sum(r => r.WarehouseQuantity), rows.Sum(r => r.IncomingQuantity),
                        rows.Sum(r => r.OutgoingQuantity), rows.Sum(r => r.RemainingQuantity) })
                        table.Cell().Background(Colors.Grey.Lighten3).Element(Cell).AlignRight().Text(Number(sum)).Bold();
                });
                column.Item().PaddingTop(8).Text($"Всего артикулов: {rows.Count}").FontSize(9);
            });
            page.Footer().AlignCenter().Text(text =>
            {
                text.Span("Стр. "); text.CurrentPageNumber(); text.Span(" / "); text.TotalPages();
            });
        });
    }

    private static string Number(long value) => value.ToString("N0", CultureInfo.GetCultureInfo("ru-RU"));
    private static IContainer Cell(IContainer c) => c.Border(.4f).BorderColor(Colors.Grey.Lighten1).Padding(5).AlignMiddle();
}
