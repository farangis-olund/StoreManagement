using PresentationWpf.Services;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PresentationWpf.Documents;

public sealed class WarehousePlaceImportErrorsDocument(WarehousePlaceImportSnapshot snapshot, string shop) : IDocument
{
    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container) => container.Page(page =>
    {
        page.Size(PageSizes.A4);
        page.Margin(1, Unit.Centimetre);
        page.DefaultTextStyle(t => t.FontSize(10));
        page.Header().Column(header =>
        {
            header.Spacing(5);
            header.Item().Row(row =>
            {
                row.RelativeItem().Text($"Магазин: {shop}");
                row.ConstantItem(180).AlignRight().Text($"Импорт: {snapshot.ImportedAt:dd.MM.yyyy HH:mm}");
            });
            header.Item().AlignCenter().Text("ОШИБКИ ИМПОРТА МЕСТ НА СКЛАДЕ").FontSize(14).Bold();
            header.Item().Text($"Файл: {snapshot.FileName}").FontSize(9);
            header.Item().Text(snapshot.Summary).FontSize(9);
            header.Item().Text("Строки ниже не загружены. Исправьте артикулы или места и выполните импорт повторно.").FontSize(9);
            header.Item().LineHorizontal(.6f);
        });
        page.Content().PaddingTop(10).Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(48);
                columns.RelativeColumn(1.5f);
                columns.RelativeColumn();
                columns.RelativeColumn(2);
            });
            table.Header(header =>
            {
                foreach (var title in new[] { "Строка Excel", "Артикул", "Место", "Причина" })
                    header.Cell().Background(Colors.Grey.Lighten3).Element(Cell).AlignCenter().Text(title).Bold();
            });
            foreach (var issue in snapshot.Issues)
            {
                table.Cell().Element(Cell).AlignRight().Text(issue.RowNumber.ToString());
                table.Cell().Element(Cell).Text(issue.ArticleNumber);
                table.Cell().Element(Cell).Text(issue.WarehousePlace);
                table.Cell().Element(Cell).Text(issue.Reason);
            }
            if (snapshot.Issues.Count == 0) table.Cell().ColumnSpan(4).Element(Cell).Text("Ошибок импорта нет.");
        });
        page.Footer().AlignCenter().Text(text =>
        {
            text.Span("Стр. "); text.CurrentPageNumber(); text.Span(" / "); text.TotalPages();
        });
    });

    private static IContainer Cell(IContainer container) => container.Border(.4f).BorderColor(Colors.Grey.Lighten1).Padding(5).AlignMiddle();
}
