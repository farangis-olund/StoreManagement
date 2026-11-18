using Infrastructure.Constants;
using Infrastructure.Contexts;
using Infrastructure.Entities;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

internal sealed class BarterReportTests(Func<DatabaseContext> createContext)
{
    public async Task RunAsync()
    {
        await using var db = createContext();
        var prefix = "BR-" + Guid.NewGuid().ToString("N")[..8];
        var firstDay = DateTime.Today.AddDays(-4);
        var barterDay = firstDay.AddDays(1);
        var paymentDay = firstDay.AddDays(2);
        var customer = new CustomerEntity { Id = prefix, FullName = "Reconciliation fixture", Debt = 1000 };
        var other = new CustomerEntity { Id = prefix + "-OTHER", FullName = "Other customer" };
        var user = new UserEntity { UserName = prefix, FirstName = "Report", LastName = "Test", Password = "test" };
        var product = new ProductEntity
        {
            ArticleNumber = prefix, ProductName = "Report fixture", Model = "Test", Marka = "Test",
            Alternative = "", WarehousePlace = "A1/1", Quentity = 100,
            Brand = new BrandEntity { BrandName = prefix }, Group = new GroupEntity { GroupName = prefix }
        };
        OrderEntity Order(string suffix, DateTime date, decimal price, int quantity, double rate, bool barter = false) => new()
        {
            Id = prefix + suffix, Customer = customer, User = user, Date = date, Rate = rate,
            SuminWords = "Test", IsBarter = barter, IsPaid = barter,
            OrderDetails = [new OrderDetailEntity { Product = product, Price = price, Quentity = quantity }]
        };
        var sale = Order("-SALE", firstDay.AddHours(23), 12, 2, 11);
        var firstBarter = Order("-BARTER1", barterDay.AddHours(1), 3.47m, 2, 12, true);
        var secondBarter = Order("-BARTER2", barterDay.AddHours(23), 10, 1, 10, true);
        db.Orders.AddRange(sale, firstBarter, secondBarter);
        db.Payments.AddRange(
            new() { Customer = customer, Order = firstBarter, Date = firstBarter.Date, Amount = 6.94m, Rate = 12, IsDeliveryConfirmed = true },
            new() { Customer = customer, Order = secondBarter, Date = secondBarter.Date, Amount = 10, Rate = 10, IsDeliveryConfirmed = true },
            new() { Customer = customer, Date = paymentDay, Amount = 24, Rate = 13 },
            new() { Customer = other, Date = paymentDay, Amount = 1000, Rate = 2 });
        db.Returns.AddRange(
            new() { Id = prefix + "-BAL", Customer = customer, Date = barterDay, TotalAmount = 5, Rate = 12,
                RefundMethod = RefundMethodConstants.Balance },
            new() { Id = prefix + "-CASH", Customer = customer, Date = paymentDay, TotalAmount = 2, Rate = 13,
                RefundMethod = RefundMethodConstants.Cash });
        await db.SaveChangesAsync();

        var factory = new TestContextFactory(createContext);
        var finance = new CustomerFinanceService(factory);
        var rows = await finance.GetReconciliationAsync(customer.Id);
        Check(rows.Select(r => r.Date).SequenceEqual(new[] { paymentDay, barterDay, firstDay }),
            "Reconciliation must group whole calendar days and sort newest first, including payment-only days.");
        Check(rows[0].Sales == 0 && rows[0].Payments == 24 && rows[0].Returns == 0 && rows[0].Barter == 0,
            "A payment-only day must exclude cash refunds and other customers' transactions.");
        Check(rows[1].Sales == 0 && rows[1].Payments == 16.94m && rows[1].Returns == 5 && rows[1].Barter == 16.94m,
            "The barter column must sum actual manual prices and quantities, without adding barter to sales.");
        var info = await finance.GetFinanceInfoAsync(customer.Id);
        Check(info.TotalSales == rows.Sum(r => r.Sales) && info.TotalReturns == rows.Sum(r => r.Returns) &&
            info.PreviousPayments == rows.Sum(r => r.Payments) && info.Balance == 978.06m &&
            info.CustomerDebt + rows.Sum(r => r.Delta) == info.Balance,
            "Detailed reconciliation and customer totals must agree, counting barter payments once.");
        Check((await finance.GetReconciliationAsync(prefix + "-EMPTY")).Count == 0,
            "A customer without activity must have no invented reconciliation rows.");

        var reports = new ReportService(factory);
        async Task CheckBarter(DateTime? from, DateTime? to, decimal euro, decimal smn)
        {
            var report = await reports.GetFullTotalSalesReportAsync(from, to);
            var barter = report.Single(r => r.Name == "БАРТЕР");
            Check(barter.Euro == euro && barter.Smn == smn, "Barter totals must respect the period and each order's exchange rate.");
        }
        await CheckBarter(barterDay, barterDay, 16.94m, 183.28m);
        await CheckBarter(firstDay, firstDay, 0, 0);
        await CheckBarter(null, barterDay, 16.94m, 183.28m);
        await CheckBarter(barterDay, null, 16.94m, 183.28m);
        await CheckBarter(null, null, 16.94m, 183.28m);
        var total = await reports.GetFullTotalSalesReportAsync(firstDay, paymentDay);
        Check(total.Single(r => r.Name == "ПРОДАЖА").Euro == 24 &&
            total.Single(r => r.Name == "ПЛАТЕЖИ").Euro == 1040.94m &&
            total.Single(r => r.Name == "КАССА").Euro == -2 && total.Single(r => r.Name == "КАССА").Smn == -26,
            "Adding the barter row must preserve ordinary sales, payments and the existing cash calculation.");

        // Preserve the report's existing special rule: Today selects yesterday's orders.
        var yesterdayBarter = Order("-YESTERDAY", DateTime.Today.AddDays(-1).AddHours(23), 7, 1, 11, true);
        var todayBarter = Order("-TODAY", DateTime.Today, 9, 1, 13, true);
        db.Orders.AddRange(yesterdayBarter, todayBarter);
        await db.SaveChangesAsync();
        await CheckBarter(DateTime.Today, DateTime.Today, 7, 77);

        var customerIds = new[] { customer.Id, other.Id };
        await db.Payments.Where(p => customerIds.Contains(p.CustomerId)).ExecuteDeleteAsync();
        await db.Returns.Where(r => customerIds.Contains(r.CustomerId)).ExecuteDeleteAsync();
        await db.OrderDetails.Where(d => customerIds.Contains(d.Order.CustomerId!)).ExecuteDeleteAsync();
        await db.Orders.Where(o => customerIds.Contains(o.CustomerId!)).ExecuteDeleteAsync();
        await db.Customers.Where(c => customerIds.Contains(c.Id)).ExecuteDeleteAsync();
        await db.Products.Where(p => p.ArticleNumber == product.ArticleNumber).ExecuteDeleteAsync();
        await db.Brands.Where(b => b.Id == product.BrandId).ExecuteDeleteAsync();
        await db.Groups.Where(g => g.Id == product.GroupId).ExecuteDeleteAsync();
        await db.Users.Where(u => u.Id == user.Id).ExecuteDeleteAsync();
        Console.WriteLine("PASS: daily reconciliation includes barter, newest dates first, payment-only days, customer isolation and matching balances; summary barter totals respect rates, periods and existing cash/date rules");
    }

    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
