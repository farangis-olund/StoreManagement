using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Infrastructure.Dtos;

namespace PresentationWpf.Documents;

public sealed class ReconciliationReportDocument : IDocument
{
    private readonly Customer _customer;
    private readonly CustomerFinanceInfo _info;
    private readonly string _orgName;
    private readonly DateTime _lastOrderDate;

    public ReconciliationReportDocument(
        Customer customer,
        CustomerFinanceInfo info,
        DateTime lastOrderDate,
        string orgName)
    {
        _customer = customer;
        _info = info;
        _lastOrderDate = lastOrderDate;
        _orgName = orgName;
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
                content.Item().Table(t =>
                {
                    t.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(90);
                        c.RelativeColumn();
                        c.ConstantColumn(90);
                        c.RelativeColumn();
                    });

                    Info(t, "ФИО:", _customer.FullName);
                    Info(t, "Обл.:", _customer.Region);
                    Info(t, "Город:", _customer.City);
                    Info(t, "Адрес:", _customer.Address);
                    Info(t, "Контакты:", _customer.MobilePhone);
                });

                // ===== TITLE =====
                content.Item()
                    .AlignCenter()
                    .PaddingTop(10)
                    .Text("АКТ СВЕРКИ")
                    .FontSize(16)
                    .Bold();

                // ===== DATE TABLE (CENTERED) =====
                content.Item().PaddingTop(10).Row(row =>
                {
                    row.RelativeItem();
                    row.ConstantItem(420).Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(105);
                            c.ConstantColumn(105);
                            c.ConstantColumn(105);
                            c.ConstantColumn(105);
                        });

                        Header(t, "Дата");
                        Header(t, "Продажа");
                        Header(t, "Платеж");
                        Header(t, "Возврат");

                        Cell(t, _lastOrderDate.ToString("dd.MM.yyyy"));
                        Cell(t, _info.TotalSales.ToString("N2"));
                        Cell(t, (_info.PreviousPayments + _info.CurrentPayment).ToString("N2"));
                        Cell(t, _info.TotalReturns.ToString("N2"));
                    });
                    row.RelativeItem();
                });

                // ===== TOTALS TABLE (CENTERED) =====
                content.Item().PaddingTop(14).Row(row =>
                {
                    row.RelativeItem();
                    row.ConstantItem(320).Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn();
                            c.ConstantColumn(120);
                        });

                        Sum(t, "Продажа", _info.TotalSales);
                        Sum(t, "Платежи", _info.PreviousPayments + _info.CurrentPayment);
                        Sum(t, "Возврат", _info.TotalReturns);
                        Sum(t, "Задолжность", _info.CustomerDebt);
                        SumBold(t, "ОСТАТОК", _info.Balance);
                    });
                    row.RelativeItem();
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
        t.Cell().Text("");
        t.Cell().Text("");
    }

    private static void Header(TableDescriptor t, string text)
    {
        t.Cell().Element(c => c
            .Border(0.5f)
            .Background(Colors.Grey.Lighten3)
            .Padding(4)
            .AlignCenter()
            .AlignMiddle()
            .Text(text)
            .Bold());
    }

    private static void Cell(TableDescriptor t, string text)
    {
        t.Cell().Element(c => c
            .Border(0.5f)
            .Padding(4)
            .AlignCenter()
            .AlignMiddle()
            .Text(text));
    }

    private static void Sum(TableDescriptor t, string label, decimal value)
    {
        t.Cell().Text(label);
        t.Cell().AlignRight().Text(value.ToString("N2"));
    }

    private static void SumBold(TableDescriptor t, string label, decimal value)
    {
        t.Cell().Text(label).Bold();
        t.Cell().AlignRight().Text(value.ToString("N2")).Bold();
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
