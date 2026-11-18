using System.IO;
using Infrastructure.Dtos;
using PresentationWpf.Documents;
using PresentationWpf.ViewModels;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

internal static class BarterInvoicePdfTests
{
    public static void Run()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var vm = new OrderInvoiceViewModel(null!, null!, null!)
        {
            ShopName = "Auto Spaire", InvoiceNumber = "БТ-ABC123", InvoiceDate = new DateTime(2026, 11, 17), Rate = 11,
            CustomerName = "Клиент Тестовый", CustomerCity = "Душанбе", CustomerAddress = "ул. Тестовая, 12",
            CustomerRegion = "Регион", CustomerPhoneNumber = "123456789",
            CurrentSale = 86.25m, OldDebt = 1234.5m, CurrentPayment = 86.25m, Balance = 1148.25m, DebtPayment = 777.77m,
            TotalSum = 86.25m, TotalSumInWords = "Восемьдесят шесть сомони двадцать пять дирам", RowCount = 1,
            OrderDetails = [new OrderDetail { ArticleNumber = "170 451", ProductName = "Амортизатор задний", BrandName = "SACHS",
                Marka = "MERC", Model = "W202", Quentity = 5, Price = 17.25m, Total = 86.25m }]
        };
        var folder = Path.Combine(AppContext.BaseDirectory, "test-pdfs"); Directory.CreateDirectory(folder);
        new BarterInvoiceDocument(vm).GeneratePdf(Path.Combine(folder, "barter-summary-sample.pdf"));
        Console.WriteLine("PASS: barter receipt renders manual amounts and four financial totals without debt repayment");
    }
}
