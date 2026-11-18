using System.IO;
using PresentationWpf.Documents;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

internal static class DeliveryReportPdfTests
{
    public static void Run()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var folder = Path.Combine(AppContext.BaseDirectory, "test-pdfs");
        Directory.CreateDirectory(folder);
        List<CourierDeliveryLine> lines =
        [
            new() { Date = new(2026, 11, 12), CustomerId = "N0001", FullName = "Клиент Первый", Address = "ул. Ленина, 2",
                Phone = "324234", Invoice = "СЧ-3H3KLC", ReceiptNumber = "ПЛ-3H3KLC", Sale = 514.8m, Amount = 440, Payment = 40 },
            new() { Date = new(2026, 11, 12), CustomerId = "N0002", FullName = "Клиент Второй", Address = "ул. С.Айни, 4",
                Phone = "3432423", ReceiptNumber = "ПЛ-ABC123", Amount = 550, Payment = 50 },
            new() { Date = new(2026, 11, 10), CustomerId = "N0003", FullName = "Клиент Третий", Address = "ул. Ленина, 3",
                Phone = "435435345", Invoice = "СЧ-ZYX987", Sale = 1956.24m },
            new() { Date = new(2026, 11, 10), CustomerId = "N0001", FullName = "Клиент Первый", Address = "ул. Ленина, 2",
                Phone = "324234", Invoice = "СЧ-3H3KLC", ReceiptNumber = "ПЛ-00000Z", Amount = 220, Payment = 20 }
        ];
        new CourierDeliveryReportDocument("Opel", "Доставщик1", lines, "Список неоплаченных доставок")
            .GeneratePdf(Path.Combine(folder, "courier-unpaid-sample.pdf"));
        var many = Enumerable.Range(1, 160).Select(i => new CourierDeliveryLine
        {
            Date = new(2026, 11, 12), CustomerId = $"N{i:0000}", FullName = "Проверка нескольких страниц",
            Address = "Очень длинный адрес доставки", Phone = "1234567890", Invoice = $"СЧ-{i:000000}",
            ReceiptNumber = $"ПЛ-{i:000000}", Sale = i, Amount = i * 11, Payment = i
        }).ToList();
        new CourierDeliveryReportDocument("Opel", "Доставщик1", many, "Список неоплаченных доставок")
            .GeneratePdf(Path.Combine(folder, "courier-unpaid-multipage.pdf"));
        new DeliveryNoteDocument("Opel", "Доставщик1", new(2026, 11, 12), 11, "СЧ-3H3KLC",
            lines.Select(l => new DeliveryNoteLine { CustomerId = l.CustomerId, City = "Душанбе", Address = l.Address, CustomerName = l.FullName,
                Phone = l.Phone, InvoiceNumber = l.Invoice, ReceiptNumber = l.ReceiptNumber }).ToList())
            .GeneratePdf(Path.Combine(folder, "delivery-note-sample.pdf"));
        Console.WriteLine("PASS: courier report and transport note render with separate invoice/receipt columns, standalone payments, invoice-only rows and multiple pages");
    }
}
