using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using PresentationWpf.ViewModels;

namespace PresentationWpf.Documents;

public sealed class SalesSummaryReportDocument : IDocument
{
    private readonly SalesSummaryViewModel _vm;
    private readonly string _orgName;
    public SalesSummaryReportDocument(SalesSummaryViewModel vm, string orgName)
    {
        _vm = vm;
        _orgName = orgName;
    }

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(0.8f, Unit.Centimetre);
            page.DefaultTextStyle(x => x.FontSize(10));

            // ================= HEADER =================
            // === HEADER ===
            page.Header().Column(header =>
            {
                header.Item().AlignLeft().Text(_orgName).FontSize(10);
                header.Item().AlignRight().Text($"Дата: {DateTime.Today:dd.MM.yyyy}").FontSize(10);
            });
       

            // ================= CONTENT =================
            page.Content().Column(content =>
            {
                content.Spacing(6);

                // -------- TITLE --------
                content.Item()
                       .AlignCenter()
                       .Text("ИТОГИ ПО ПРОДАЖАМ")
                       .FontSize(14)
                       .Bold();

                // -------- MAIN TABLE (DATAGRID DATA) --------
                content.Item().PaddingTop(6).Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(60);   // Код
                        c.RelativeColumn(2);    // ФИО
                        c.RelativeColumn(3);    // Адрес
                        c.ConstantColumn(100);  // Телефон
                        c.ConstantColumn(90);   // Дата
                        c.ConstantColumn(90);   // Остаток
                    });

                    // ===== HEADER =====
                    table.Header(header =>
                    {
                        HeaderCell(header, "Код");
                        HeaderCell(header, "ФИО");
                        HeaderCell(header, "Адрес");
                        HeaderCell(header, "Телефон");
                        HeaderCell(header, "Дата");
                        HeaderCell(header, "Остаток");
                    });

                    // ===== ROWS =====
                    foreach (var row in _vm.Sales)
                    {
                        BodyCell(table, row.ClientCode);
                        BodyCell(table, row.ClientName);
                        BodyCell(table, row.Address);
                        BodyCell(table, row.Phone);
                        BodyCell(table, row.MaxDate?.ToString("dd.MM.yyyy") ?? "");
                        BodyCell(table, row.Balance.ToString("N2"), alignRight: true);
                    }
                });

                // -------- TOTAL (BOLD, NO TABLE) --------
                var total = _vm.Sales.Sum(x => x.Balance);

                content.Item()
                    .PaddingTop(6)
                    .AlignRight()
                    .Text(text =>
                    {
                        text.Span("ИТОГО: ").Bold();
                        text.Span(total.ToString("N2")).Bold();
                    });
              

                // -------- RECORD COUNT --------
                content.Item().PaddingTop(4)
                    .Text($"Всего клиентов: {_vm.Sales.Count}")
                    .FontSize(9)
                    .FontColor(Colors.Grey.Darken2);
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

    // ================= HELPERS =================

    private static void HeaderCell(TableCellDescriptor header, string text)
    {
        header.Cell()
            .Border(0.25f)
            .BorderColor(Colors.Grey.Medium)
            .Background(Colors.Grey.Lighten3)
            .Padding(2)
            .MinHeight(14)
            .AlignMiddle()
            .Text(text)
            .Bold()
            .FontSize(10)
            .FontColor(Colors.Grey.Darken3);
    }

    private static void BodyCell(
        TableDescriptor table,
        string text,
        bool alignRight = false)
    {
        var cell = table.Cell()
            .Border(0.25f)
            .BorderColor(Colors.Grey.Medium)
            .Padding(2)
            .MinHeight(14)
            .AlignMiddle();

        if (alignRight)
            cell.AlignRight();

        cell.Text(text ?? string.Empty)
            .FontSize(10)
            .FontColor(Colors.Grey.Darken3);
    }
}
