using Infrastructure.Dtos;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PresentationWpf.Documents;

public sealed class AllInactivesReportDocument : IDocument
{
    private readonly List<InactivesSummaryRowDto> _rows;
    private readonly string _orgName;
    private readonly bool _usePeriod;
    private readonly DateTime _from;
    private readonly DateTime _to;

    public AllInactivesReportDocument(
        List<InactivesSummaryRowDto> rows,
        string orgName,
        bool usePeriod,
        DateTime from,
        DateTime to)
    {
        _rows = rows;
        _orgName = orgName;
        _usePeriod = usePeriod;
        _from = from;
        _to = to;
    }

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(20);
            page.DefaultTextStyle(x => x.FontSize(9));

            // ================= HEADER =================
            page.Header().Column(header =>
            {
                header.Item().Row(row =>
                {
                    row.RelativeItem().Text(_orgName).Bold();

                    row.ConstantItem(200)
                       .AlignRight()
                       .Text($"Дата: {DateTime.Today:dd.MM.yyyy}");
                });

                header.Item()
                      .AlignCenter()
                      .PaddingTop(5)
                      .Text("ЗАДОЛЖЕННОСТЬ БЕЗ ОБОРОТА")
                      .FontSize(14)
                      .Bold();

                if (_usePeriod)
                {
                    header.Item()
                          .AlignCenter()
                          .Text($"Период: {_from:dd.MM.yyyy} - {_to:dd.MM.yyyy}")
                          .FontSize(10);
                }
                else
                {
                    header.Item()
                          .AlignCenter()
                          .Text("За весь период")
                          .FontSize(10);
                }
            });

            // ================= CONTENT =================
            page.Content().Column(column =>
            {
                column.Spacing(5);

                column.Item().Element(ComposeTable);

                column.Item()
                      .AlignRight()
                      .Text(text =>
                      {
                          text.Span("ИТОГО: ").Bold();
                          text.Span(_rows.Sum(x => x.Balance).ToString("N2")).Bold();
                      });
            });

            // ================= FOOTER =================
            page.Footer().AlignCenter().Text(text =>
            {
                text.Span("Стр. ");
                text.CurrentPageNumber();
                text.Span(" / ");
                text.TotalPages();
            });
        });
    }

    private void ComposeTable(IContainer container)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(70);   // Код
                columns.RelativeColumn(2);    // ФИО
                columns.RelativeColumn(3);    // Адрес
                columns.ConstantColumn(100);  // Телефон
                columns.ConstantColumn(100);  // Остаток
                columns.ConstantColumn(100);  // Остаток
            });

            table.Header(header =>
            {
                HeaderCell(header, "Код");
                HeaderCell(header, "ФИО");
                HeaderCell(header, "Адрес");
                HeaderCell(header, "Телефон");
                HeaderCell(header, "Ограничение");
                HeaderCell(header, "Остаток");
            });

            foreach (var row in _rows)
            {
                BodyCell(table, row.ClientCode);
                BodyCell(table, row.ClientName);
                BodyCell(table, row.Address);
                BodyCell(table, row.Phone);
                BodyCell(table, row.Restriction.ToString("N2"), true);
                BodyCell(table, row.Balance.ToString("N2"), true);
            }
        });
    }

    private static void HeaderCell(TableCellDescriptor header, string text)
    {
        header.Cell().Element(cell =>
        {
            cell.Border(0.5f)
                .BorderColor(Colors.Grey.Medium)
                .Background(Colors.Grey.Lighten3)
                .Padding(4)
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
