using PresentationWpf.ViewModels;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PresentationWpf.Documents;

public sealed class InactiveWarehouseReportDocument : IDocument
{
    private readonly InactiveWarehouseProductsReportViewModel _vm;
    private readonly string _orgName;

    public InactiveWarehouseReportDocument(
        InactiveWarehouseProductsReportViewModel vm,
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
            page.Size(PageSizes.A4);
            page.Margin(0.8f, Unit.Centimetre);
            page.DefaultTextStyle(x => x.FontSize(10));

            // ================= HEADER =================

            page.Header().Column(header =>
            {
                header.Item().AlignLeft().Text(_orgName).FontSize(10);

                header.Item().AlignRight()
                    .Text($"Дата печати: {DateTime.Today:dd.MM.yyyy}")
                    .FontSize(10);
            });


            // ================= CONTENT =================

            page.Content().Column(content =>
            {
                content.Spacing(6);

                // -------- TITLE --------

                content.Item()
                    .AlignCenter()
                    .Text("НЕАКТИВНЫЕ ТОВАРЫ СКЛАДА")
                    .FontSize(14)
                    .Bold();


                // -------- PERIOD --------

                string from = _vm.FromDate?.ToString("dd.MM.yyyy") ?? "—";
                string to = _vm.ToDate?.ToString("dd.MM.yyyy") ?? "—";

                content.Item()
                    .AlignCenter()
                    .Text($"Период: {from} — {to}")
                    .FontSize(10);


                // -------- TABLE --------

                content.Item().PaddingTop(6).Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(110); // Артикул
                        c.RelativeColumn(3);   // Наименование
                        c.RelativeColumn(2);   // Бренд
                        c.RelativeColumn(2);   // Марка
                        c.RelativeColumn(2);   // Модель
                        c.ConstantColumn(50);  // Кол
                    });

                    // ===== HEADER =====

                    table.Header(header =>
                    {
                        HeaderCell(header, "Артикул");
                        HeaderCell(header, "Наименование");
                        HeaderCell(header, "Бренд");
                        HeaderCell(header, "Марка");
                        HeaderCell(header, "Модель");
                        HeaderCell(header, "Кол");
                    });

                    // ===== ROWS =====

                    foreach (var row in _vm.Items)
                    {
                        BodyCell(table, row.ArticleNumber);
                        BodyCell(table, row.ProductName);
                        BodyCell(table, row.BrandName);
                        BodyCell(table, row.Marka);
                        BodyCell(table, row.Model);
                        BodyCell(table, row.Quentity.ToString(), true);
                    }
                });


                // -------- RECORD COUNT --------

                content.Item()
                    .PaddingTop(6)
                    .Text($"Всего позиций: {_vm.Items.Count}")
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
            .FontColor(Colors.Grey.Darken3)
            .ClampLines(1);
    }
}