using PresentationWpf.ViewModels;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PresentationWpf.Documents;

public sealed class WarehousePlaceReportDocument : IDocument
{
    private readonly WarehousePlaceReportViewModel _vm;
    private readonly string _orgName;

    public WarehousePlaceReportDocument(
        WarehousePlaceReportViewModel vm,
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
                header.Spacing(2);

                header.Item().Row(row =>
                {
                    row.RelativeItem()
                        .AlignLeft()
                        .Text(_orgName)
                        .FontSize(10);

                    row.RelativeItem()
                        .AlignRight()
                        .Text($"Дата печати: {DateTime.Today:dd.MM.yyyy}")
                        .FontSize(10);
                });
            });

            // ================= CONTENT =================

            page.Content().Column(content =>
            {
                content.Spacing(6);

                // -------- TITLE --------

                content.Item()
                    .AlignCenter()
                    .Text("ОТЧЁТ О МЕСТЕ НА СКЛАДЕ")
                    .FontSize(14)
                    .Bold();

                // -------- FILTERS --------

                var section = string.IsNullOrWhiteSpace(_vm.SelectedSection) || _vm.SelectedSection == "Все"
                    ? "Все"
                    : _vm.SelectedSection;

                var row = _vm.SelectedRow <= 0
                    ? "Все"
                    : _vm.SelectedRow.ToString();

                var place = _vm.SelectedPlace <= 0
                    ? "Все"
                    : _vm.SelectedPlace.ToString();

                var quantityMode = _vm.IncludeZeroQuantity
                    ? "включая нулевое количество"
                    : "только количество > 0";

                content.Item()
                    .AlignCenter()
                    .Text($"Сектор: {section}   Ряд: {row}   Место: {place}")
                    .FontSize(10);

                content.Item()
                    .AlignCenter()
                    .Text($"Режим: {quantityMode}")
                    .FontSize(10);

                // -------- TABLE --------

                content.Item()
                    .PaddingTop(6)
                    .Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(55); // Место
                            c.ConstantColumn(95); // Артикул
                            c.RelativeColumn(3);  // Наименование
                            c.RelativeColumn(2);  // Бренд
                            c.RelativeColumn(2);  // Марка
                            c.RelativeColumn(3);  // Модель
                            c.ConstantColumn(45); // Кол-во
                            c.ConstantColumn(30); // Исп

                        });

                        // ===== HEADER =====

                        table.Header(header =>
                        {
                            HeaderCell(header, "Место");
                            HeaderCell(header, "Артикул");
                            HeaderCell(header, "Наименование");
                            HeaderCell(header, "Бренд");
                            HeaderCell(header, "Марка");
                            HeaderCell(header, "Модель");
                            HeaderCell(header, "Кол-во");
                            HeaderCell(header, "Исп");


                        });

                        // ===== ROWS =====

                        foreach (var rowItem in _vm.Items)
                        {
                            BodyCell(table, rowItem.PlaceCode);
                            BodyCell(table, rowItem.Article);
                            BodyCell(table, rowItem.Name);
                            BodyCell(table, rowItem.Brand);
                            BodyCell(table, rowItem.Mark);
                            BodyCell(table, rowItem.Model);
                            BodyCell(table, rowItem.Quantity.ToString(), true);
                            BodyCell(table, "");
                        }
                    });

                // -------- TOTAL --------

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
            .MinHeight(16)
            .AlignMiddle()
            .Text(text)
            .Bold()
            .FontSize(10)
            .FontColor(Colors.Grey.Darken3);
    }

    private static void BodyCell(
        TableDescriptor table,
        string? text,
        bool alignRight = false)
    {
        var cell = table.Cell()
            .Border(0.25f)
            .BorderColor(Colors.Grey.Medium)
            .Padding(2)
            .MinHeight(15)
            .AlignMiddle();

        if (alignRight)
            cell.AlignRight();

        cell.Text(text ?? string.Empty)
            .FontSize(9)
            .FontColor(Colors.Grey.Darken3);
    }
}