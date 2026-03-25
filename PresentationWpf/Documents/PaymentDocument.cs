using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using PresentationWpf.ViewModels;
using System;

namespace PresentationWpf.Documents
{
    public class PaymentDocument : IDocument
    {
        private readonly PaymentReceiptViewModel _vm;

        public PaymentDocument(PaymentReceiptViewModel vm)
        {
            _vm = vm;
        }

        public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

        public void Compose(IDocumentContainer container)
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(10));

                // ===== HEADER (identical to invoice style) =====
                page.Header().Column(header =>
                {
                    header.Item().Row(row =>
                    {
                        row.RelativeItem().Text(text =>
                        {
                            text.Span("Магазин: ").SemiBold();
                            text.Line($"{_vm.CompanyName}");
                        });

                        row.RelativeItem().AlignRight().Element(right =>
                        {
                            right.AlignRight().Column(col =>
                            {
                                col.Item().AlignRight().Text($"Дата: {_vm.PaymentDate:dd.MM.yyyy}");
                                col.Item().AlignRight().Text($"Номер квитанции: {_vm.PaymentNumber}");
                                col.Item().AlignRight().Text($"Номер накладной: {_vm.OrderNumber}");
                            });
                        });
                    });

                    header.Item().LineHorizontal(0.3f);
                });

                // ===== CONTENT =====
                page.Content().Column(content =>
                {
                    //content.Spacing(8);

                    // --- Customer info ---
                    content.Item().Column(info =>
                    {
                        //info.Spacing(2);

                        info.Item().Row(row =>
                        {
                            row.RelativeItem(7)
                                .Text($"ФИО клиента: {_vm.CustomerFullName}");
                            row.RelativeItem(3)
                                .AlignRight()
                                .Text($"Код клиента: {_vm.CustomerCode}");
                        });

                        info.Item().Row(row =>
                        {
                            row.RelativeItem(7)
                                .Text($"Адрес: {_vm.CustomerAddress}");
                            row.RelativeItem(3)
                                .AlignRight()
                                .Text($"Телефон: {_vm.CustomerPhone}");
                        });
                    });

                    content.Item().LineHorizontal(0.3f);

                    // --- Title ---
                    content.Item().AlignCenter().PaddingTop(8)
                        .Text(text =>
                        {
                            text.Span("КВИТАНЦИЯ ОБ ОПЛАТЕ").Bold().FontSize(14);
                        });

                    // --- Payment details table ---
                    content.Item().PaddingTop(10).Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(3);
                            cols.RelativeColumn(2);
                        });

                        void RowItem(string label, string value)
                        {
                            table.Cell()
                                .Border(0.25f)
                                .BorderColor(Colors.Grey.Lighten2)
                                .Padding(4)
                                .Text(label)
                                .Bold()
                                .FontSize(10);

                            table.Cell()
                                .Border(0.25f)
                                .BorderColor(Colors.Grey.Lighten2)
                                .Padding(4)
                                .Text(value ?? string.Empty)
                                .AlignRight()
                                .FontSize(10);
                        }

                        RowItem("Задолженность до оплаты:", $"{_vm.Debt:N2}");
                        RowItem("Оплачено:", $"{_vm.Paid:N2}");
                        RowItem("Остаток после оплаты:", $"{_vm.Balance:N2}");
                    });

                    // --- Amount in words ---
                    content.Item().PaddingTop(10)
                        .Text($"Сумма прописью: {_vm.AmountInWords}")
                        .Italic();

                    // --- Signature section ---
                    content.Item().PaddingTop(30).Row(row =>
                    {
                        row.RelativeItem().Text("Разрешено: ______________________");
                        row.RelativeItem().Text("Оплатил(а): ______________________");
                        row.RelativeItem().Text("Получил(а): ______________________");
                    });
                });

                // ===== FOOTER =====
                page.Footer().AlignCenter().TranslateY(15).Text(x =>
                {
                    x.Span("Стр. ");
                    x.CurrentPageNumber();
                    x.Span(" / ");
                    x.TotalPages();
                });
            });
        }
    }
}
