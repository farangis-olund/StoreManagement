using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;

namespace PresentationWpf.Documents
{
    public class DeliveryNoteDocument : IDocument
    {
        private readonly string _orgName;
        private readonly string _courierName;
        private readonly DateTime _date;
        private readonly double _exchangeRate;
        private readonly string _invoiceNumber;
        private readonly List<DeliveryNoteLine> _lines;

        public DeliveryNoteDocument(
            string orgName,
            string courierName,
            DateTime date,
            double exchangeRate,
            string invoiceNumber,
            List<DeliveryNoteLine> lines)
        {
            _orgName = orgName;
            _courierName = courierName;
            _date = date;
            _exchangeRate = exchangeRate;
            _invoiceNumber = invoiceNumber;
            _lines = lines;
        }

        public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

        public void Compose(IDocumentContainer container)
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(10));

                // === HEADER ===
                page.Header().Column(header =>
                {
                    header.Item().AlignLeft().Text(_orgName).Bold().FontSize(12);
                    header.Item().AlignRight().Text($"Дата: {_date:dd.MM.yyyy}").FontSize(10);
                    header.Item().LineHorizontal(1);
                });


                // === CONTENT ===
                page.Content().Column(content =>
                {
                    content.Spacing(8);

                    content.Item().Row(row =>
                    {
                        row.RelativeItem().Text($"ФИО доставщика: {_courierName}");
                        row.ConstantItem(120).AlignRight().Text($"Курс валюты: {_exchangeRate:F2}");
                    });

                    content.Item().AlignCenter().PaddingTop(10).Text(text =>
                    {
                        text.Span("Транспортная накладная").Bold().FontSize(12);
                    });

                    // === TABLE ===
                    content.Item().PaddingTop(10).Table(table =>
                    {
                        // Columns
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(1);   // Город
                            cols.RelativeColumn(2);   // Адрес
                            cols.RelativeColumn(2);   // ФИО получателя
                            cols.RelativeColumn(1.5f); // Тел
                            cols.RelativeColumn(1.2f); // № Накладная
                            cols.RelativeColumn(1.0f); // Место
                        });

                        // Header
                        string[] headers = { "Город", "Адрес", "ФИО получателя", "Тел.", "№ Накладная", "Место" };

                        table.Header(header =>
                        {
                            foreach (var h in headers)
                            {
                                header.Cell()
                                    .Border(0.2f)
                                    .BorderColor(Colors.Grey.Lighten1)   // lighter tone
                                    .Background(Colors.Grey.Lighten4)    // softer background
                                    .PaddingVertical(2)
                                    .PaddingHorizontal(3)
                                    .AlignCenter()
                                    .Text(h)
                                    .Bold()
                                    .FontSize(10)
                                    .FontColor(Colors.Grey.Darken3);
                            }
                        });

                        // Data rows
                        foreach (var line in _lines)
                        {
                            void Cell(string value)
                            {
                                table.Cell()
                                    .Border(0.2f)
                                    .BorderColor(Colors.Grey.Lighten2)  // soft and thin borders
                                    .PaddingVertical(2)
                                    .PaddingHorizontal(3)
                                    .Text(value ?? string.Empty)
                                    .FontSize(10)
                                    .FontColor(Colors.Grey.Darken3);
                            }

                            Cell(line.City);
                            Cell(line.Address);
                            Cell(line.CustomerName);
                            Cell(line.Phone);
                            Cell(line.InvoiceNumber);
                            Cell(""); // 👈 Empty “Место” column
                        }
                    });

                    // === SIGNATURES ===
                    content.Item().PaddingTop(20).Row(row =>
                    {
                        row.RelativeItem().Text("Подпись продавца ______________________");
                        row.ConstantItem(50);
                        row.RelativeItem().Text("Подпись доставщика ______________________");
                    });

                    content.Item().PaddingTop(10)
                        .AlignLeft()
                        .Text($"Дата печати: {DateTime.Now:dd.MM.yyyy HH:mm}")
                        .FontSize(10);
                });

                // === FOOTER ===
                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Стр. ");
                    x.CurrentPageNumber();
                    x.Span(" из ");
                    x.TotalPages();
                });
            });
        }
    }

    public class DeliveryNoteLine
    {
        public string City { get; set; } = "";
        public string Address { get; set; } = "";
        public string CustomerName { get; set; } = "";
        public string Phone { get; set; } = "";
        public string InvoiceNumber { get; set; } = "";
    }
}
