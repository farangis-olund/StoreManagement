using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using PresentationWpf.ViewModels;


public sealed class InactivesReportDocument : IDocument
{
    private readonly SalesSummaryViewModel _vm;
    private readonly string _orgName;

    public InactivesReportDocument(
        SalesSummaryViewModel vm,
        string orgName)
    {
        _vm = vm;
        _orgName = orgName;
    }

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape()); // Landscape safer for table
            page.Margin(1, Unit.Centimetre);
            page.DefaultTextStyle(x => x.FontSize(10));

            // ================= HEADER =================
            page.Header().Column(header =>
            {
                header.Spacing(2);

                header.Item().Row(row =>
                {
                    row.RelativeItem().Text(_orgName).Bold();
                    row.ConstantItem(200)
                        .AlignRight()
                        .Text($"Дата: {DateTime.Today:dd.MM.yyyy}");
                });

                header.Item()
                    .AlignCenter()
                    .Text("СПИСОК ДОЛЖНИКОВ")
                    .FontSize(14)
                    .Bold();

                // 🔥 PERIOD HANDLING
                if (_vm.UsePeriod)
                {
                    header.Item()
                        .AlignCenter()
                        .Text($"Период: {_vm.PeriodFrom:dd.MM.yyyy} - {_vm.PeriodTo:dd.MM.yyyy}")
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
            page.Content().Column(content =>
            {
                content.Spacing(6);

                content.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(60);   // Код
                        c.RelativeColumn(2);    // ФИО
                        c.ConstantColumn(110);  // Телефон
                        c.RelativeColumn(3);    // Адрес
                        c.ConstantColumn(100);  // Ограничение
                        c.ConstantColumn(100);  // Остаток
                    });

                    // ===== HEADER =====
                    HeaderCell(table, "Код");
                    HeaderCell(table, "ФИО");
                    HeaderCell(table, "Телефон");
                    HeaderCell(table, "Адрес");
                    HeaderCell(table, "Ограничение");
                    HeaderCell(table, "Остаток");

                    // ===== ROWS =====
                    foreach (var row in _vm.InactiveClients)
                    {
                        BodyCell(table, row.ClientCode);
                        BodyCell(table, row.ClientName);
                        BodyCell(table, row.Phone);
                        BodyCell(table, row.Address);
                        BodyCell(table, row.Restriction.ToString("N2"), true);
                        BodyCell(table, row.Balance.ToString("N2"), true);
                    }
                });

                // ---- TOTAL ----
                var total = _vm.InactiveClients.Sum(x => x.Balance);

                content.Item()
                    .AlignRight()
                    .Text(t =>
                    {
                        t.Span("ИТОГО: ").Bold();
                        t.Span(total.ToString("N2")).Bold();
                    });
            });

            // ================= FOOTER =================
            page.Footer()
                .AlignCenter()
                .Text(t =>
                {
                    t.Span("Стр. ");
                    t.CurrentPageNumber();
                    t.Span(" / ");
                    t.TotalPages();
                });
        });
    }

    // ================= HELPERS =================

    private static void HeaderCell(TableDescriptor table, string text)
    {
        table.Cell().Element(cell =>
        {
            cell.Border(0.5f)
                .BorderColor(Colors.Grey.Medium)
                .Background(Colors.Grey.Lighten3)
                .Padding(4)
                .Text(text ?? "")
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