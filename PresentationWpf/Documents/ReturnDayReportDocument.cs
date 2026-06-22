using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Infrastructure.Dtos;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PresentationWpf.Documents
{
    public class ReturnDayReportDocument : IDocument
    {
        private readonly List<ReturnDayReportRowDto> _rows;
        private readonly DateTime _reportDate;
        private readonly string _shopName;
        private readonly decimal _totalReturn;
        private readonly string _title;

        public ReturnDayReportDocument(
            IEnumerable<ReturnDayReportRowDto> rows,
            DateTime reportDate,
            string shopName,
            decimal totalReturn,
            string title = "ОТЧЕТ О ВОЗВРАТАХ ЗА ДЕНЬ")
        {
            _rows = rows.ToList();
            _reportDate = reportDate;
            _shopName = shopName;
            _totalReturn = totalReturn;
            _title = title;
        }

        public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

        public void Compose(IDocumentContainer container)
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.2f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(9));

                // ================= HEADER =================
                page.Header().Column(header =>
                {
                    header.Spacing(4);

                    header.Item().Row(row =>
                    {
                        row.RelativeItem().Text(_title).Bold().FontSize(14);

                        row.ConstantItem(140)
                            .AlignRight()
                            .Text($"{_reportDate:dd.MM.yyyy}")
                            .FontSize(10);
                    });

                    if (!string.IsNullOrWhiteSpace(_shopName))
                    {
                        header.Item().Row(row =>
                        {
                            row.ConstantItem(80).Text("Магазин:").SemiBold();
                            row.RelativeItem().Text(_shopName);
                        });
                    }

                    header.Item().PaddingTop(3).LineHorizontal(0.7f);
                });

                // ================= CONTENT =================
                page.Content().PaddingTop(8).Column(content =>
                {
                    content.Spacing(8);

                    // ===== TABLE =====
                    content.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(1.1f);  // Место
                            columns.RelativeColumn(1.5f);  // Артикул
                            columns.RelativeColumn(3.8f);  // Наименование
                            columns.RelativeColumn(1.6f);  // Бренд
                            columns.RelativeColumn(1.1f);  // Кол-во
                        });

                        static IContainer CellStyle(IContainer container) =>
                            container
                                .Border(0.4f)
                                .BorderColor(Colors.Grey.Lighten1)
                                .PaddingVertical(4)
                                .PaddingHorizontal(4);

                        static IContainer HeaderCellStyle(IContainer container) =>
                            CellStyle(container)
                                .Background(Colors.Grey.Lighten3);

                        // ===== HEADER =====
                        table.Header(header =>
                        {
                            header.Cell().Element(HeaderCellStyle).AlignCenter().Text("Место").Bold();
                            header.Cell().Element(HeaderCellStyle).AlignCenter().Text("Артикул").Bold();
                            header.Cell().Element(HeaderCellStyle).AlignCenter().Text("Наименование").Bold();
                            header.Cell().Element(HeaderCellStyle).AlignCenter().Text("Бренд").Bold();
                            header.Cell().Element(HeaderCellStyle).AlignCenter().Text("Кол-во").Bold();
                        });

                        // ===== ROWS =====
                        foreach (var row in _rows)
                        {
                            table.Cell().Element(CellStyle).Text(row.Place);
                            table.Cell().Element(CellStyle).Text(row.ArticleNumber);
                            table.Cell().Element(CellStyle).Text(row.ProductName);
                            table.Cell().Element(CellStyle).Text(row.BrandName);
                            table.Cell().Element(CellStyle).AlignRight().Text($"{row.Quantity:N2}");
                        }

                        // ===== EMPTY =====
                        if (_rows.Count == 0)
                        {
                            table.Cell().ColumnSpan(5)
                                .Element(CellStyle)
                                .AlignCenter()
                                .Text("Нет данных");
                        }
                    });

                    // ===== TOTAL =====
                    content.Item().AlignRight().Width(200).Border(0.6f).Padding(5).Row(row =>
                    {
                        row.RelativeItem().Text("Итого:").Bold();
                        row.ConstantItem(80).AlignRight().Text($"{_totalReturn:N2}").Bold();
                    });
                });

                // ================= FOOTER =================
                page.Footer().PaddingTop(10).AlignCenter().Text(x =>
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