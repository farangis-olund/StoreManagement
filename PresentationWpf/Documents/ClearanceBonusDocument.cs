using System.Globalization;
using Infrastructure.Dtos;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PresentationWpf.Documents;

public sealed class ClearanceBonusDocument(ClearanceBonusReport report, string shop) : IDocument
{
    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;
    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4); page.Margin(28); page.DefaultTextStyle(x => x.FontSize(10));
            page.Header().Column(column =>
            {
                column.Spacing(6);
                column.Item().Text($"Магазин: {shop}");
                column.Item().AlignCenter().Text("БОНУСЫ ЗА РАСПРОДАЖУ").FontSize(15).Bold();
                column.Item().Text($"Период: {report.FromDate:dd.MM.yyyy} — {report.ToDate:dd.MM.yyyy} · Бонус: {report.Percent:0.##}%");
                column.Item().Text("Продажи без вычета возвратов.").FontSize(9);
            });
            page.Content().PaddingTop(12).Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(2); c.RelativeColumn(1.7f); c.RelativeColumn(); c.RelativeColumn(1.2f); c.RelativeColumn(1.2f);
                });
                table.Header(header =>
                {
                    foreach (var title in new[] { "Продавец", "Артикул", "Кол-во", "Сумма", "Бонус" })
                        header.Cell().Background(Colors.Grey.Lighten3).Element(Cell).Text(title).Bold();
                });
                foreach (var seller in report.Rows.GroupBy(x => new { x.UserId, x.SellerName }))
                {
                    foreach (var row in seller)
                    {
                        table.Cell().Element(Cell).Text(row.SellerName);
                        table.Cell().Element(Cell).Text(row.ArticleNumber);
                        table.Cell().Element(Cell).AlignRight().Text(row.Quantity.ToString("N0", CultureInfo.GetCultureInfo("ru-RU")));
                        table.Cell().Element(Cell).AlignRight().Text(Money(row.Amount));
                        table.Cell().Element(Cell).AlignRight().Text(Money(row.Bonus));
                    }
                    table.Cell().ColumnSpan(2).Background(Colors.Grey.Lighten4).Element(Cell).Text($"Итого: {seller.Key.SellerName}").Bold();
                    table.Cell().Element(Cell).AlignRight().Text(seller.Sum(x => x.Quantity).ToString()).Bold();
                    table.Cell().Element(Cell).AlignRight().Text(Money(seller.Sum(x => x.Amount))).Bold();
                    table.Cell().Element(Cell).AlignRight().Text(Money(seller.Sum(x => x.Bonus))).Bold();
                }
                table.Cell().ColumnSpan(2).Background(Colors.Grey.Lighten3).Element(Cell).Text("Всего").Bold();
                table.Cell().Element(Cell).AlignRight().Text(report.TotalQuantity.ToString()).Bold();
                table.Cell().Element(Cell).AlignRight().Text(Money(report.TotalAmount)).Bold();
                table.Cell().Element(Cell).AlignRight().Text(Money(report.TotalBonus)).Bold();
            });
            page.Footer().AlignCenter().Text(t => { t.Span("Стр. "); t.CurrentPageNumber(); t.Span(" / "); t.TotalPages(); });
        });
    }
    private static string Money(decimal value) => value.ToString("N2", CultureInfo.GetCultureInfo("ru-RU"));
    private static IContainer Cell(IContainer c) => c.Border(.4f).BorderColor(Colors.Grey.Lighten1).Padding(5);
}
