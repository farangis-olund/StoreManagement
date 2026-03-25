using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using PresentationWpf.ViewModels;
using System;
using System.Linq;

namespace PresentationWpf.Documents
{
    public class InvoiceDocument : IDocument
    {
        private readonly OrderInvoiceViewModel _vm;

        public InvoiceDocument(OrderInvoiceViewModel vm)
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
                            col.Item().Text($"Дата: {_vm.InvoiceDate:dd.MM.yyyy}");
                            col.Item().Text($"Курс валюты: {_vm.Rate:N2}");
                        });
                    });

                    header.Item().LineHorizontal(0.01f);
                });

                // ===== CONTENT =====
                page.Content().Column(content =>
                {
                    content.Spacing(6);

                    // --- Client info ---
                    // --- Customer Info Section ---
                    content.Item().Column(info =>
                    {
                        info.Spacing(2);

                        // First line: Name + Region
                        info.Item().Row(row =>
                        {
                            row.RelativeItem(7)
                                .Text($"ФИО клиента: {Trim(_vm.CustomerName, 40)}");

                            row.RelativeItem(3)
                                .AlignRight()
                                .Text($"Обл: {Trim(_vm.CustomerRegion, 30)}");
                        });

                        // Second line: City + Address
                        info.Item().Row(row =>
                        {
                            row.RelativeItem(5)
                                .Text($"Город: {Trim(_vm.CustomerCity, 40)}");

                            row.RelativeItem(5)
                                .AlignRight()
                                .Text($"Адрес: {Trim(_vm.CustomerAddress, 40)}");
                        });

                        info.Item().Row(row =>
                        {
                            // Left side: contacts
                            row.RelativeItem(7)
                                .Text($"Контакты: {Trim(_vm.CustomerPhoneNumber, 40)}");

                            // Right side: route
                            var territory = Trim(_vm.CustomerTerritory ?? "", 30);
                            var courier = Trim(_vm.CourierId ?? "", 10);
                            var storekeeper = Trim(_vm.StorekeeperId ?? "", 10);
                            var route = string.Join(" - ", new[] { territory, courier, storekeeper }
                                .Where(x => !string.IsNullOrWhiteSpace(x)));

                            row.RelativeItem(3)
                                .AlignRight()
                                .Text($"Маршрут: {route}");
                        });

                    });


                    content.Item().LineHorizontal(0.01f);

                    // --- Invoice title ---
                    content.Item().AlignCenter().PaddingTop(6)
                           .Text(text =>
                           {
                               text.Span("Товарный чек № ").FontSize(12);
                               text.Span(_vm.InvoiceNumber).Bold();
                           });

                    // --- Table of items ---
                    content.Item().PaddingTop(8).Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(2f); // Артикул
                            cols.RelativeColumn(2f); // Наиме
                            cols.RelativeColumn(1.2f); // Бренд
                            cols.RelativeColumn(1.3f); // Марка
                            cols.RelativeColumn(2f);   // Модель
                            cols.RelativeColumn(0.8f); // Кол
                            cols.RelativeColumn(1f);   // Цена
                            cols.RelativeColumn(1f);   // Сумма
                            cols.RelativeColumn(1f);   // Место
                        });

                        string[] headers =
                        {
                            "Артикул", "Наименование", "Бренд", "Марка", "Модель",
                            "Кол", "Цена", "Сумма", "Место"
                        };

                        // ✅ Table header that repeats on every page
                        table.Header(header =>
                        {
                            foreach (var h in headers)
                                header.Cell()
                                 .Border(0.25f)                                     // thin physical line
                                 .BorderColor(Colors.Grey.Medium)                 // visually lighter
                                 .Background(Colors.Grey.Lighten3)                  // soft background for header
                                 .Padding(2)
                                 .MinHeight(14)
                                 .AlignMiddle()
                                 .Text(h)
                                 .Bold()
                                 .FontSize(10)
                                 .FontColor(Colors.Grey.Darken3);                   // subtle contrast
                        });

                        // ✅ Data rows (auto-paginate)
                        foreach (var item in _vm.OrderDetails)
                        {
                            var art = Trim(GetProp(item, "ArticleNumber"), 18);
                            var name = Trim(GetProp(item, "ProductName"), 16);
                            var brand = Trim(GetProp(item, "BrandName"), 7);
                            var marka = Trim(GetProp(item, "Marka"), 10);
                            var model = Trim(GetProp(item, "Model"), 15);
                            var qty = Convert.ToDecimal(item.GetType().GetProperty("Quentity")?.GetValue(item) ?? 0);
                            var price = Convert.ToDecimal(item.GetType().GetProperty("Price")?.GetValue(item) ?? 0);
                            var total = qty * price;
                            var warehousePlace = Trim(GetProp(item, "WarehousePlace"), 5);
                            void Cell(string value, bool center = false, bool right = false)
                            {
                                var cell = table.Cell()
                                    .Border(0.25f)
                                    .BorderColor(Colors.Grey.Medium)
                                    .Padding(2)
                                    .MinHeight(14)
                                    .AlignMiddle();
                                var text = cell.Text(value ?? string.Empty);
                                if (center) text.AlignCenter();
                                else if (right) text.AlignRight();
                            }

                            Cell(art);
                            Cell(name);
                            Cell(brand);
                            Cell(marka);
                            Cell(model);
                            Cell(qty.ToString("0.##"), center: true);
                            Cell(price.ToString("0.00"), right: true);
                            Cell(total.ToString("0.00"), right: true);
                            Cell(warehousePlace, center: true);
                        }
                    });

                    // --- Totals under table ---
                    content.Item().PaddingTop(3).Text(
                        $"Всего наименований: {_vm.RowCount}   Итого: {_vm.TotalSum:N2}   {_vm.TotalSumInWords}"
                    ).FontSize(10);

                    // --- Summary box ---
                    content.Item().PaddingTop(6).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(1);
                            c.RelativeColumn(1);
                            c.RelativeColumn(1);
                            c.RelativeColumn(1);
                            c.RelativeColumn(1);
                        });

                        string[] summaryHeaders =
                        {
                            "Итоги продаж", "Старый долг", "ПогДолг", "Оплачено", "Остаток"
                        };

                        table.Header(header =>
                        {
                            foreach (var h in summaryHeaders)
                                header.Cell()
                                .Border(0.25f)                                     // thin physical line
                                .BorderColor(Colors.Grey.Medium)                 // visually lighter
                                .Background(Colors.Grey.Lighten3)                  // soft background for header
                                .Padding(2)
                                .MinHeight(14)
                                .AlignMiddle()
                                .Text(h)
                                .Bold()
                                .FontSize(10)
                                .FontColor(Colors.Grey.Darken3);                   // subtle contrast
                        });

                        void Cell(string value) => table.Cell()
                        .Border(0.25f)                                 // thin physical border
                        .BorderColor(Colors.Grey.Medium)             // soft gray (looks thinner)
                        .AlignCenter()
                        .MinHeight(14)
                        .AlignMiddle()
                        .PaddingVertical(2)
                        .PaddingHorizontal(3)
                        .Text(value ?? string.Empty)                   // draw text once
                        .FontSize(10)
                        .FontColor(Colors.Grey.Darken3);

                        Cell($"{_vm.CurrentSale:N2}");
                        Cell($"{_vm.OldDebt:N2}");
                        Cell($"{_vm.DebtPayment:N2}");
                        Cell($"{_vm.CurrentPayment:N2}");
                        Cell($"{_vm.Balance:N2}");
                    });

                    // --- Signature section ---
                    content.Item().PaddingTop(20).Row(row =>
                    {
                        row.RelativeItem().Text("Разрешено ____________");
                        row.RelativeItem().Text("Отпустил ____________");
                        row.RelativeItem().Text("Получил ____________");
                    });
                });

                
                page.Footer().Column(col =>
                {
                    col.Item().Height(25); // adds guaranteed empty space before footer
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
       

        // --- Helpers ---
        private static string Trim(string value, int max)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "";
            return value.Length <= max ? value : value.Substring(0, max - 1);
        }

        private static string GetProp(object item, string name)
        {
            return item.GetType().GetProperty(name)?.GetValue(item)?.ToString() ?? "";
        }
    }
}
