using PresentationWpf.ViewModels;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PresentationWpf.Documents;

public sealed class SalesDetailInfoReportDocument : IDocument
{
    private readonly List<SalesSummaryRow> _rows;
    private readonly string _orgName;

    public SalesDetailInfoReportDocument(
        List<SalesSummaryRow> rows,
        string orgName)
    {
        _rows = rows;
        _orgName = orgName;
    }

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A3.Landscape());
            page.Margin(20);
            page.DefaultTextStyle(x => x.FontSize(9));

            // HEADER
            page.Header().Column(col =>
            {
                col.Item().Row(row =>
                {
                    row.RelativeItem().Text(_orgName);
                    row.ConstantItem(200)
                        .AlignRight()
                        .Text($"Дата: {DateTime.Today:dd.MM.yyyy}");
                });

                col.Item().AlignCenter()
                    .PaddingTop(5)
                    .Text("ДЕТАЛЬНАЯ ИНФОРМАЦИЯ ПО КЛИЕНТАМ")
                    .FontSize(13)
                    .Bold();
            });

            // CONTENT TABLE
            page.Content().PaddingTop(10).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(60);   // Код
                    columns.RelativeColumn(2);    // ФИО
                    columns.RelativeColumn(2);    // Адрес
                    columns.ConstantColumn(80);   // Дата
                    columns.ConstantColumn(80);   // Продажа
                    columns.ConstantColumn(80);   // Возврат
                    columns.ConstantColumn(90);   // Задолженность
                    columns.ConstantColumn(80);   // Платежи
                    columns.ConstantColumn(90);   // Остаток
                });

                table.Header(header =>
                {
                    HeaderCell(header, "Код");
                    HeaderCell(header, "ФИО");
                    HeaderCell(header, "Адрес");
                    HeaderCell(header, "ПоследнДатаЗакупа");
                    HeaderCell(header, "Продажа");
                    HeaderCell(header, "Возврат");
                    HeaderCell(header, "Задолженность");
                    HeaderCell(header, "Платежи");
                    HeaderCell(header, "Остаток");
                });

                foreach (var row in _rows)
                {
                    BodyCell(table, row.ClientCode);
                    BodyCell(table, row.ClientName);
                    BodyCell(table, row.Address);
                    BodyCell(table, row.MaxDate?.ToString("dd.MM.yyyy") ?? "");
                    BodyCell(table, row.Sales.ToString("N2"), true);
                    BodyCell(table, row.Returns.ToString("N2"), true);
                    BodyCell(table, row.Debt.ToString("N2"), true);
                    BodyCell(table, row.Payments.ToString("N2"), true);
                    BodyCell(table, row.Balance.ToString("N2"), true);
                }
            });

            // FOOTER TOTAL
            page.Footer().Column(col =>
            {
                var totalSales = _rows.Sum(x => x.Sales);
                var totalBalance = _rows.Sum(x => x.Balance);

                col.Item().AlignRight().Text(text =>
                {
                    text.Span("ИТОГО Продажа: ").Bold();
                    text.Span(totalSales.ToString("N2")).Bold();
                });

                col.Item().AlignRight().Text(text =>
                {
                    text.Span("ИТОГО Остаток: ").Bold();
                    text.Span(totalBalance.ToString("N2")).Bold();
                });

                col.Item().AlignCenter().Text(text =>
                {
                    text.Span("Стр. ");
                    text.CurrentPageNumber();
                    text.Span(" / ");
                    text.TotalPages();
                });
            });
        });
    }

    private static void HeaderCell(TableCellDescriptor header, string text)
    {
        header.Cell().Element(container =>
        {
            container
                .Border(0.5f)
                .BorderColor(Colors.Grey.Medium)
                .Background(Colors.Grey.Lighten3)
                .Padding(3)
                .Text(text)
                .Bold();
        });
    }

    private static void BodyCell(
        TableDescriptor table,
        string text,
        bool alignRight = false)
    {
        table.Cell().Element(cell =>
        {
            var styled = cell
                .Border(0.5f)
                .BorderColor(Colors.Grey.Lighten2)
                .Padding(4)
                .AlignMiddle();

            styled = alignRight
                ? styled.AlignRight()
                : styled.AlignLeft();

            styled.Text(text ?? "");
        });
    }
}