using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using PresentationWpf.ViewModels;
using System;

namespace PresentationWpf.Documents
{
    public class ReturnInvoiceDocument : IDocument
    {
        private readonly ReturnInvoiceViewModel _vm;

        public ReturnInvoiceDocument(ReturnInvoiceViewModel vm)
        {
            _vm = vm;
        }

        public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

        public void Compose(IDocumentContainer container)
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(0.5f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(10));

                // ===== HEADER =====
                page.Header().Column(header =>
                {
                    header.Item().Row(row =>
                    {
                        row.RelativeItem().Text(text =>
                        {
                            text.Span("Магазин: ").FontSize(10);
                            text.Line($"{_vm.ShopName}");
                        });

                        row.ConstantItem(200).AlignRight().Column(col =>
                        {
                            col.Item().Text($"Дата: {_vm.Date:dd.MM.yyyy}");
                        });
                    });

                    header.Item().LineHorizontal(0.01f);
                });

                // ===== CONTENT =====
                page.Content().Column(content =>
                {
                    content.Spacing(6);

                    // --- Client Info ---
                    content.Item().Column(info =>
                    {
                        info.Spacing(2);

                        // First row: customer name + invoice/order number
                        info.Item().Row(row =>
                        {
                            row.RelativeItem(7)
                                .Text($"ФИО клиента: {Trim(_vm.CustomerName, 40)}");
                            row.RelativeItem(3)
                                .AlignRight()
                                .Text($"Накладная №: {Trim(_vm.OrderNumber, 25)}");
                        });

                        // Second row: customer ID
                        info.Item().Row(row =>
                        {
                            row.RelativeItem(7)
                                .Text($"Код клиента: {Trim(_vm.CustomerId, 30)}");
                            row.RelativeItem(3)
                                .AlignRight()
                                .Text($"Возврат №: {Trim(_vm.InvoiceNumber, 20)}");
                        });

                        // Third row: payment method
                        info.Item().Row(row =>
                        {
                            row.RelativeItem(10)
                                .Text($"Способ возврата: {Trim(_vm.RefundMethod, 30)}");
                        });
                    });

                    content.Item().LineHorizontal(0.01f);

                    // --- Title ---
                    content.Item().AlignCenter().PaddingTop(6)
                           .Text(text =>
                           {
                               text.Span("ВОЗВРАТНЫЙ ЧЕК").FontSize(12).Bold();
                           });

                    // --- Table ---
                    content.Item().PaddingTop(8).Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(2f); // Артикул
                            cols.RelativeColumn(2f); // Наименование
                            cols.RelativeColumn(1.2f); // Бренд
                            cols.RelativeColumn(1.3f); // Марка
                            cols.RelativeColumn(2f);   // Модель
                            cols.RelativeColumn(0.8f); // Кол
                            cols.RelativeColumn(1f);   // Цена
                            cols.RelativeColumn(1f);   // Сумма
                        });

                        string[] headers =
                        {
                            "Артикул", "Наименование", "Бренд", "Марка", "Модель",
                            "Кол", "Цена", "Сумма"
                        };

                        // Header (repeat on pages)
                        table.Header(header =>
                        {
                            foreach (var h in headers)
                                header.Cell()
                                 .Border(0.25f)
                                 .BorderColor(Colors.Grey.Medium)
                                 .Background(Colors.Grey.Lighten3)
                                 .Padding(2)
                                 .MinHeight(14)
                                 .AlignMiddle()
                                 .Text(h)
                                 .Bold()
                                 .FontSize(10)
                                 .FontColor(Colors.Grey.Darken3);
                        });

                        // Data rows
                        foreach (var item in _vm.Lines)
                        {
                            var art = Trim(item.Article, 18);
                            var name = Trim(item.Name, 18);
                            var brand = Trim(item.Brand, 8);
                            var marka = Trim(item.Marka, 10);
                            var model = Trim(item.Model, 15);
                            var qty = item.Quantity;
                            var price = item.Price;
                            var total = item.Total;

                            void Cell(string value, bool center = false, bool right = false)
                            {
                                var cell = table.Cell()
                                    .Border(0.25f)
                                    .BorderColor(Colors.Grey.Medium)
                                    .Padding(2)
                                    .MinHeight(14)
                                    .AlignMiddle();
                                var text = cell.Text(value ?? string.Empty)
                                    .FontSize(10)
                                    .FontColor(Colors.Black);
                                if (center) text.AlignCenter();
                                else if (right) text.AlignRight();
                            }

                            Cell(art);
                            Cell(name);
                            Cell(brand);
                            Cell(marka);
                            Cell(model);
                            Cell(qty.ToString("0"), center: true);
                            Cell(price.ToString("0.00"), right: true);
                            Cell(total.ToString("0.00"), right: true);
                        }
                    });

                    // --- Totals ---
                    content.Item().PaddingTop(3).Text(
                        $"Всего наименований: {_vm.TotalItems}   Итого: {_vm.TotalAmount:N2}   ({_vm.TotalAmountWords})"
                    ).FontSize(10);

                    // --- Summary box ---
                    content.Item().PaddingTop(6).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(1);
                            c.RelativeColumn(1);
                            c.RelativeColumn(1);
                        });

                        string[] summaryHeaders =
                        {
                            "Старый долг", "Возврат", "Остаток"
                        };

                        table.Header(header =>
                        {
                            foreach (var h in summaryHeaders)
                                header.Cell()
                                .Border(0.25f)
                                .BorderColor(Colors.Grey.Medium)
                                .Background(Colors.Grey.Lighten3)
                                .Padding(2)
                                .MinHeight(14)
                                .AlignMiddle()
                                .Text(h)
                                .Bold()
                                .FontSize(10)
                                .FontColor(Colors.Grey.Darken3);
                        });

                        void Cell(string value) => table.Cell()
                        .Border(0.25f)
                        .BorderColor(Colors.Grey.Medium)
                        .AlignCenter()
                        .MinHeight(14)
                        .AlignMiddle()
                        .PaddingVertical(2)
                        .PaddingHorizontal(3)
                        .Text(value ?? string.Empty)
                        .FontSize(10)
                        .FontColor(Colors.Grey.Darken3);

                        Cell($"{_vm.OldDebt:N2}");
                        Cell($"{_vm.ReturnedAmount:N2}");
                        Cell($"{_vm.RemainingDebt:N2}");
                    });

                    // --- Payment note ---
                    content.Item().PaddingTop(6).Text(_vm.RefundNote)
                        .Italic()
                        .FontSize(10)
                        .FontColor(Colors.Grey.Darken2);

                    // --- Signatures ---
                    content.Item().PaddingTop(20).Row(row =>
                    {
                        row.RelativeItem().Text("Разрешено ____________");
                        row.RelativeItem().Text("Отпустил ____________");
                        row.RelativeItem().Text("Получил ____________");
                    });
                });

                // ===== FOOTER =====
                page.Footer().Column(col =>
                {
                    col.Item().Height(25);
                    col.Item().AlignCenter().Text(x =>
                    {
                        x.Span("Стр. ");
                        x.CurrentPageNumber();
                        x.Span(" / ");
                        x.TotalPages();
                    });
                });
            });
        }

        private static string Trim(string value, int max)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "";
            return value.Length <= max ? value : value.Substring(0, max - 1);
        }
    }
}
