using System.IO;
using Infrastructure.Dtos;
using Infrastructure.Services;
using PresentationWpf.Documents;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

internal static class ReconciliationReportPdfTests
{
    public static void Run()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var folder = Path.Combine(AppContext.BaseDirectory, "test-pdfs");
        Directory.CreateDirectory(folder);
        var customer = new Customer
        {
            Id = "N0011", FullName = "Тестовый клиент", Region = "Хатлон", City = "Фархор",
            Address = "ул. Тестовая, 12", MobilePhone = "324234"
        };
        var info = new CustomerFinanceInfo
        {
            TotalSales = 24, PreviousPayments = 30.94m, TotalReturns = 0, CustomerDebt = 1000, Balance = 993.06m
        };
        // Deliberately pass unsorted rows: the document must retain newest-first ordering.
        List<ReconciliationRow> rows =
        [
            new() { Date = new(2026, 11, 13), Sales = 24 },
            new() { Date = new(2026, 11, 17), Payments = 6.94m, Barter = 6.94m },
            new() { Date = new(2026, 11, 15), Payments = 24 }
        ];
        new ReconciliationReportDocument(customer, info, new(2026, 11, 17), "Opel", rows)
            .GeneratePdf(Path.Combine(folder, "reconciliation-sample.pdf"));
        new ReconciliationReportDocument(customer, new CustomerFinanceInfo { CustomerDebt = 1000, Balance = 1000 },
                new(2026, 11, 17), "Opel", [])
            .GeneratePdf(Path.Combine(folder, "reconciliation-empty.pdf"));
        customer.FullName = "Клиент с длинным наименованием для проверки размещения данных";
        customer.Address = "ул. Очень длинное название улицы, дом 123, корпус 4, помещение 567";
        var many = Enumerable.Range(0, 150).Select(i => new ReconciliationRow
        {
            Date = new DateTime(2026, 11, 17).AddDays(-i), Sales = i + 100,
            Payments = i + 75, Returns = i % 3, Barter = i % 5
        }).ToList();
        var manyInfo = new CustomerFinanceInfo
        {
            CustomerDebt = 1000, TotalSales = many.Sum(r => r.Sales), PreviousPayments = many.Sum(r => r.Payments),
            TotalReturns = many.Sum(r => r.Returns), Balance = 1000 + many.Sum(r => r.Delta)
        };
        new ReconciliationReportDocument(customer, manyInfo, new(2026, 11, 17), "Opel", many)
            .GeneratePdf(Path.Combine(folder, "reconciliation-multipage.pdf"));
        List<TotalSalesReportRowDto> totals =
        [
            new() { Name = "ПРОДАЖА", Euro = 24, Smn = 264 },
            new() { Name = "БАРТЕР", Euro = 6.94m, Smn = 76.34m },
            new() { Name = "ВОЗВРАТ" }, new() { Name = "ПРОШЛОГОДНИЙ ВОЗВРАТ" },
            new() { Name = "ПЛАТЕЖИ", Euro = 30.94m, Smn = 340.34m },
            new() { Name = "ОПЛАТА ДОСТАВЩИКУ", Euro = 24, Smn = 264 },
            new() { Name = "РАСХОДЫ" }, new() { Name = "КАССА", Euro = 24, Smn = 264 }
        ];
        new TotalSalesReportDocument(totals, new(2026, 11, 13), new(2026, 11, 17), "Opel")
            .GeneratePdf(Path.Combine(folder, "total-sales-barter-sample.pdf"));
        Console.WriteLine("PASS: reconciliation renders client-side totals, barter, five dated detail columns, empty activity, long client details and repeated multipage headers; summary report prints barter");
    }
}
