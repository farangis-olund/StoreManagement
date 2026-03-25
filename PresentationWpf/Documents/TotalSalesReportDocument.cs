using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Infrastructure.Dtos;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PresentationWpf.Documents
{
    public class TotalSalesReportDocument : IDocument
    {
        private readonly List<TotalSalesReportRowDto> _rows;
        private readonly DateTime? _fromDate;
        private readonly DateTime? _toDate;
        private readonly string _title;
        private readonly string _shopName;

        public TotalSalesReportDocument(
            IEnumerable<TotalSalesReportRowDto> rows,
            DateTime? fromDate,
            DateTime? toDate,
            string shopName,
            string title = "ОТЧЕТ ПО ОБЩИМ ПОКАЗАТЕЛЯМ")
        {
            _rows = rows.ToList();
            _fromDate = fromDate;
            _toDate = toDate;
            _shopName = shopName;
            _title = title;
        }

        public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

        public void Compose(IDocumentContainer container)
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.2f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(header =>
                {
                    header.Spacing(4);

                    header.Item().Row(row =>
                    {
                        row.RelativeItem().Text(text =>
                        {
                            text.Span("Магазин: ").SemiBold();
                            text.Span(_shopName);
                        });

                        row.ConstantItem(180).AlignRight().Text($"Дата печати: {DateTime.Now:dd.MM.yyyy HH:mm}");
                    });

                    header.Item().AlignCenter().Text(_title)
                        .FontSize(14)
                        .Bold();

                    header.Item().AlignCenter().Text(text =>
                    {
                        text.Span("Период: ").SemiBold();

                        if (_fromDate.HasValue || _toDate.HasValue)
                        {
                            var from = _fromDate?.ToString("dd.MM.yyyy") ?? "...";
                            var to = _toDate?.ToString("dd.MM.yyyy") ?? "...";
                            text.Span($"с {from} по {to}");
                        }
                        else
                        {
                            text.Span("за всё время");
                        }
                    });

                    header.Item().PaddingTop(3).LineHorizontal(0.6f);
                });

                page.Content().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(2.2f);
                        columns.RelativeColumn(1f);
                        columns.RelativeColumn(1f);
                    });

                    static IContainer CellStyle(IContainer container) =>
                        container.Border(0.4f)
                                 .BorderColor(Colors.Grey.Lighten1)
                                 .PaddingVertical(6)
                                 .PaddingHorizontal(6)
                                 .AlignMiddle();

                    static IContainer HeaderCellStyle(IContainer container) =>
                        CellStyle(container)
                            .Background(Colors.Grey.Lighten3);

                    table.Header(header =>
                    {
                        header.Cell().Element(HeaderCellStyle).AlignCenter().Text("Показатель").Bold().FontSize(11);
                        header.Cell().Element(HeaderCellStyle).AlignCenter().Text("EURO").Bold().FontSize(11);
                        header.Cell().Element(HeaderCellStyle).AlignCenter().Text("СМН").Bold().FontSize(11);
                    });

                    foreach (var row in _rows)
                    {
                        var isCashRow = row.Name == "КАССА";

                        table.Cell().Element(CellStyle).AlignCenter().Text(text =>
                        {
                            if (isCashRow) text.Span(row.Name).Bold().FontSize(11);
                            else text.Span(row.Name).FontSize(11);
                        });

                        table.Cell().Element(CellStyle).AlignCenter().Text(text =>
                        {
                            if (isCashRow) text.Span($"{row.Euro:N2}").Bold().FontSize(11);
                            else text.Span($"{row.Euro:N2}").FontSize(11);
                        });

                        table.Cell().Element(CellStyle).AlignCenter().Text(text =>
                        {
                            if (isCashRow) text.Span($"{row.Smn:N2}").Bold().FontSize(11);
                            else text.Span($"{row.Smn:N2}").FontSize(11);
                        });
                    }
                });

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