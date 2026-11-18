using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Infrastructure.Dtos;
using Infrastructure.Services;
using System.Globalization;

namespace PresentationWpf.Documents;

public sealed class ReconciliationReportDocument : IDocument
{
    private readonly Customer _customer;
    private readonly CustomerFinanceInfo _info;
    private readonly string _orgName;
    private readonly DateTime _lastOrderDate;
    private readonly List<ReconciliationRow> _rows;

    public ReconciliationReportDocument(
        Customer customer,
        CustomerFinanceInfo info,
        DateTime lastOrderDate,
        string orgName,
        IEnumerable<ReconciliationRow> rows)
    {
        _customer = customer;
        _info = info;
        _lastOrderDate = lastOrderDate;
        _orgName = orgName;
        _rows = rows.OrderByDescending(r => r.Date).ToList();
    }

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(1, Unit.Centimetre);
            page.DefaultTextStyle(x => x.FontSize(10));

            // ================= HEADER =================
            page.Header().Column(header =>
            {
                header.Item().Row(row =>
                {
                    row.RelativeItem()
                        .Text(_orgName)
                        .Bold()
                        .FontSize(12);

                    row.ConstantItem(200)
                        .AlignRight()
                        .Text($"Дата: {_lastOrderDate:dd.MM.yyyy}");
                });

                header.Item().PaddingTop(5).LineHorizontal(1);
            });

            // ================= CONTENT =================
            page.Content().Column(content =>
            {
                content.Spacing(12);

                // ===== CLIENT INFO =====
                content.Item().Row(row =>
                {
                    row.RelativeItem().Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(75);
                            c.RelativeColumn();
                        });

                        Info(t, "ФИО:", _customer.FullName);
                        Info(t, "Обл.:", _customer.Region);
                        Info(t, "Город:", _customer.City);
                        Info(t, "Адрес:", _customer.Address);
                        Info(t, "Контакты:", _customer.MobilePhone);
                    });

                    row.ConstantItem(20);
                    row.ConstantItem(220).Column(totals =>
                    {
                        totals.Item().Table(t =>
                        {
                            t.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn();
                                c.ConstantColumn(85);
                            });

                            Sum(t, "Продажа", _info.TotalSales);
                            Sum(t, "Платежи", _info.PreviousPayments + _info.CurrentPayment);
                            Sum(t, "Возврат", _info.TotalReturns);
                            Sum(t, "Бартер", _rows.Sum(r => r.Barter));
                            Sum(t, "Задолженность", _info.CustomerDebt);
                            SumBold(t, "ОСТАТОК", _info.Balance);
                        });
                        totals.Item().PaddingTop(5).Text("Оплата бартера включена в платежи.")
                            .FontSize(8).FontColor(Colors.Grey.Darken2);
                    });
                });

                // ===== TITLE =====
                content.Item()
                    .AlignCenter()
                    .PaddingTop(10)
                    .Text("АКТ СВЕРКИ")
                    .FontSize(16)
                    .Bold();

                // ===== DAILY DETAILS, NEWEST FIRST =====
                content.Item().PaddingTop(10).Table(t =>
                {
                    t.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(85);
                        c.RelativeColumn();
                        c.RelativeColumn();
                        c.RelativeColumn();
                        c.RelativeColumn();
                    });
                    t.Header(header =>
                    {
                        foreach (var title in new[] { "Дата", "Продажа", "Платеж", "Возврат", "Бартер" })
                        {
                            header.Cell().Background(Colors.Grey.Lighten3).Element(CellStyle)
                                .AlignCenter().Text(title).Bold();
                        }
                    });
                    foreach (var detail in _rows)
                    {
                        t.Cell().Element(CellStyle).AlignCenter().Text(detail.Date.ToString("dd.MM.yyyy"));
                        foreach (var amount in new[] { detail.Sales, detail.Payments, detail.Returns, detail.Barter })
                            t.Cell().Element(CellStyle).AlignRight().Text(Money(amount));
                    }
                    if (_rows.Count == 0)
                        t.Cell().ColumnSpan(5).Element(CellStyle).AlignCenter().Text("Нет операций");
                });

                // ===== SIGNATURES (CENTERED) =====
                content.Item().PaddingTop(35).Row(row =>
                {
                    row.RelativeItem();
                    row.ConstantItem(420).Row(sign =>
                    {
                        SignatureBlock(sign.RelativeItem(), "ФИО ПРОДАВЦА");
                        SignatureBlock(sign.RelativeItem(), "ФИО ПОКУПАТЕЛЯ");
                    });
                    row.RelativeItem();
                });
            });

            // ================= FOOTER =================
            page.Footer().AlignCenter().Text(t =>
            {
                t.Span("Стр. ");
                t.CurrentPageNumber();
                t.Span(" / ");
                t.TotalPages();
            });
        });
    }

    // ================= HELPERS =================

    private static void Info(TableDescriptor t, string label, string? value)
    {
        t.Cell().Text(label).Bold();
        t.Cell().Text(value ?? "");
    }

    private static IContainer CellStyle(IContainer container) => container
        .Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(5).AlignMiddle();

    private static string Money(decimal value) => value.ToString("N2", CultureInfo.GetCultureInfo("ru-RU"));

    private static void Sum(TableDescriptor t, string label, decimal value)
    {
        t.Cell().Text(label);
        t.Cell().AlignRight().Text(Money(value));
    }

    private static void SumBold(TableDescriptor t, string label, decimal value)
    {
        t.Cell().Text(label).Bold();
        t.Cell().AlignRight().Text(Money(value)).Bold();
    }

    private static void SignatureBlock(IContainer container, string title)
    {
        container.Column(col =>
        {
            col.Item().AlignCenter().Text(title);
            col.Item().PaddingTop(10).AlignCenter().Text("_________________________");
            col.Item().PaddingTop(10).AlignCenter().Text("ПОДПИСЬ __________");
        });
    }
}
