using Infrastructure.Constants;
using Infrastructure.Contexts;
using Infrastructure.Entities;
using Infrastructure.Helpers;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

internal sealed class CustomerCoefficientFormulaTests(Func<DatabaseContext> createContext)
{
    public async Task RunAsync()
    {
        await using var db = createContext();
        Check(!await db.RaschetKoefficenta.AnyAsync(), "The fixture must start without global coefficient settings.");
        var prefix = "CF-" + Guid.NewGuid().ToString("N")[..8];
        var contract = DateTime.Today.AddDays(-14);
        var days = DateHelper.DatesBetweenExcludingWeekends(contract, DateTime.Today);
        var manager = new SalesManagerEntity { Id = prefix + "-M", FullName = "Coefficient manager" };
        var otherManager = new SalesManagerEntity { Id = prefix + "-OTHER", FullName = "Other manager" };
        var first = new CustomerEntity
        {
            Id = prefix + "-A", FullName = "Saved purchase and repayment", Debt = 1000, ContractDate = contract,
            DailyPurchaseCoefficient = 13.89, DailyRepaymentCoefficient = 3.47, DailyPlannedPurchaseCoefficient = 0,
            SalesManager = manager, Territory = "North"
        };
        var second = new CustomerEntity
        {
            Id = prefix + "-B", FullName = "Saved planned purchase", ContractDate = contract, Debt = 3000,
            DailyPurchaseCoefficient = 2, DailyPlannedPurchaseCoefficient = 5, SalesManager = manager, Territory = "South"
        };
        var third = new CustomerEntity
        {
            Id = prefix + "-C", FullName = "Other scope", Debt = 10, ContractDate = contract,
            DailyPurchaseCoefficient = 10, DailyPlannedPurchaseCoefficient = 10, SalesManager = otherManager, Territory = "North"
        };
        var missing = new CustomerEntity { Id = prefix + "-D", FullName = "Unset coefficients", Debt = 1000, ContractDate = contract };
        var product = new ProductEntity
        {
            ArticleNumber = prefix, ProductName = "Coefficient fixture", Model = "Test", Marka = "Test",
            Alternative = "", WarehousePlace = "A1/1", Quentity = 100,
            Brand = new BrandEntity { BrandName = prefix }, Group = new GroupEntity { GroupName = prefix }
        };
        var user = new UserEntity { UserName = prefix, FirstName = "Coefficient", LastName = "Test", Password = "test" };
        OrderEntity Order(string suffix, CustomerEntity customer, decimal total, bool official) => new()
        {
            Id = prefix + suffix, Customer = customer, User = user, Date = suffix == "-CURRENT" ? DateTime.Today : contract.AddMonths(-1),
            SuminWords = "Test", OfficialOrder = official,
            OrderDetails = [new OrderDetailEntity { Product = product, Price = total, Quentity = 1 }]
        };
        var current = Order("-CURRENT", first, 20, true);
        var previous = Order("-PREVIOUS", first, 40, false);
        var plannedOrder = Order("-PLAN", second, 10, false);
        plannedOrder.Date = contract.AddHours(1);
        db.Orders.AddRange(current, previous, plannedOrder);
        db.Customers.AddRange(third, missing);
        db.Payments.AddRange(
            new CustomerPaymentEntity { Customer = first, Amount = 100, Date = DateTime.Today.AddDays(-1) },
            new CustomerPaymentEntity { Customer = first, Amount = 30, Date = DateTime.Today },
            new CustomerPaymentEntity { Customer = second, Amount = 4, Date = DateTime.Today.AddDays(-1) });
        db.Returns.AddRange(
            new ReturnEntity { Id = prefix + "-BAL", Customer = first, TotalAmount = 10, RefundMethod = RefundMethodConstants.Balance },
            new ReturnEntity { Id = prefix + "-CASH", Customer = second, TotalAmount = 2, RefundMethod = RefundMethodConstants.Cash });
        await db.SaveChangesAsync();
        var finance = new CustomerFinanceService(new TestContextFactory(createContext));
        async Task<decimal> Required(string id) => (await finance.NeaktivOtEzhigodPogashenieAsync(customerId: id)).SingleOrDefault()?.Shortfall ?? 0;
        async Task<decimal> Planned(string id) => (await finance.NeaktivNeZakupAsync(customerId: id)).SingleOrDefault()?.Shortfall ?? 0;
        async Task CheckSavedValues(decimal purchase, decimal repayment, decimal plan)
        {
            Check(await Required(first.Id) == Math.Round(purchase * days - 20, 0),
                "Required purchase must use the saved coefficient and sales from the contract date through today only.");
            Check(await finance.EzhigodPogashenieAsync(first.Id, current.Id) == Math.Round(repayment * days, 2),
                "Suggested repayment must use the saved daily repayment coefficient.");
            Check(await Planned(second.Id) == Math.Round(plan * days - 10, 0),
                "Planned purchase must subtract period sales without debt, payments or returns.");
            var storedFirst = await db.Customers.AsNoTracking().SingleAsync(c => c.Id == first.Id);
            var storedSecond = await db.Customers.AsNoTracking().SingleAsync(c => c.Id == second.Id);
            Check((decimal)storedFirst.DailyPurchaseCoefficient!.Value == purchase &&
                (decimal)storedFirst.DailyRepaymentCoefficient!.Value == repayment &&
                (decimal)storedSecond.DailyPlannedPurchaseCoefficient!.Value == plan,
                "Reading order formulas must not recalculate or overwrite saved coefficients.");
        }
        await CheckSavedValues(13.89m, 3.47m, 5m);
        Check(await Required(missing.Id) == 0 && await Planned(missing.Id) == 0 &&
            await finance.EzhigodPogashenieAsync(missing.Id, "") == 0, "Unset coefficients must not use a fallback calculation.");

        // Deliberately incompatible global settings must not override saved customer values.
        var settings = new RaschetKoefficentaEntity
        {
            KoefZakupa = 999, KoefZakupaDni = 0, KoefZaplanZakup = 999, KoefZaplanZakupDni = 0,
            KoefEzhPogashOstatokNach1 = 0, KoefEzhPogashOstatokKon1 = 100000, KoefEzhPogashDin1 = 0
        };
        db.RaschetKoefficenta.Add(settings);
        await db.SaveChangesAsync();
        await CheckSavedValues(13.89m, 3.47m, 5m);

        first.DailyPurchaseCoefficient = 66.25;
        first.DailyRepaymentCoefficient = 11.25;
        second.DailyPlannedPurchaseCoefficient = 9.5;
        await db.SaveChangesAsync();
        await CheckSavedValues(66.25m, 11.25m, 9.5m);
        Check((await finance.NeaktivOtEzhigodPogashenieAsync(manager.Id)).Select(c => c.CustomerId).ToHashSet().SetEquals([first.Id, second.Id]),
            "The full manager purchase list must remain available.");
        Check((await finance.NeaktivOtEzhigodPogashenieAsync(manager.Id, "North")).Single().CustomerId == first.Id &&
            (await finance.NeaktivNeZakupAsync(manager.Id, "South")).Single().CustomerId == second.Id,
            "Saved coefficients must not bypass the manager and route filters.");

        // Date boundaries and the zero floor must apply to both purchase indicators.
        first.ContractDate = contract.AddHours(15);
        first.DailyPurchaseCoefficient = first.DailyPlannedPurchaseCoefficient = 10;
        var startDay = Order("-START", first, 3, false);
        startDay.Date = contract.AddHours(1);
        startDay.OrderDetails.Single().Quentity = 2;
        var endDay = Order("-END", first, 7, false);
        endDay.Date = DateTime.Today.AddDays(1).AddTicks(-1);
        endDay.IsBarter = true;
        var futureOrder = Order("-FUTURE", first, 100000, true);
        futureOrder.Date = DateTime.Today.AddDays(1);
        db.Orders.AddRange(startDay, endDay, futureOrder);
        await db.SaveChangesAsync();
        var periodSales = 20 + 3 * 2 + 7;
        Check(await Required(first.Id) == 10 * days - periodSales &&
            await Planned(first.Id) == 10 * days - periodSales,
            "Both indicators must include all sales on the contract day and today, including ordinary/barter orders, and exclude earlier/future orders.");

        var requiredBeforePayments = await Required(first.Id);
        var plannedBeforePayments = await Planned(first.Id);
        first.Debt = 5000;
        var extraPayment = new CustomerPaymentEntity { Customer = first, Amount = 1234, Date = DateTime.Today };
        var extraReturn = new ReturnEntity { Id = prefix + "-EXTRA-RETURN", Customer = first, TotalAmount = 432, RefundMethod = RefundMethodConstants.Balance };
        db.Payments.Add(extraPayment);
        db.Returns.Add(extraReturn);
        await db.SaveChangesAsync();
        Check(await Required(first.Id) == requiredBeforePayments && await Planned(first.Id) == plannedBeforePayments,
            "Changing debt, payments or returns must not affect either purchase indicator.");
        db.Payments.Remove(extraPayment);
        db.Returns.Remove(extraReturn);
        first.Debt = 1000;
        await db.SaveChangesAsync();

        var settlement = Order("-SETTLEMENT", first, 10 * days - periodSales, false);
        settlement.Date = DateTime.Today.AddHours(12);
        db.Orders.Add(settlement);
        await db.SaveChangesAsync();
        Check(await Required(first.Id) == 0 && await Planned(first.Id) == 0,
            "Purchases that meet the target must show zero and disappear from the shortfall lists.");
        settlement.OrderDetails.Single().Price += 1;
        await db.SaveChangesAsync();
        Check(await Required(first.Id) == 0 && await Planned(first.Id) == 0,
            "Purchases above the target must not produce a negative shortfall.");
        db.Orders.Remove(settlement);
        await db.SaveChangesAsync();
        Check(await Required(first.Id) == requiredBeforePayments && await Planned(first.Id) == plannedBeforePayments,
            "Removing an order must recalculate both purchase indicators.");

        first.ContractDate = DateTime.Today.AddHours(15);
        var todayDays = DateTime.Today.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ? 0 : 1;
        var todayExpected = Math.Max(0, 100 * todayDays - 27);
        first.DailyPurchaseCoefficient = first.DailyPlannedPurchaseCoefficient = 100;
        await db.SaveChangesAsync();
        Check(await Required(first.Id) == todayExpected && await Planned(first.Id) == todayExpected,
            "A contract starting today must count today when it is a working day and include all today's sales.");
        Check(DateHelper.DatesBetweenExcludingWeekends(new DateTime(2026, 11, 13), new DateTime(2026, 11, 16)) == 2 &&
            DateHelper.DatesBetweenExcludingWeekends(new DateTime(2026, 11, 14), new DateTime(2026, 11, 15)) == 0,
            "Working-day counting must include both boundaries and exclude Saturday/Sunday.");

        first.ContractDate = null;
        await db.SaveChangesAsync();
        Check(await Required(first.Id) == 0 && await Planned(first.Id) == 0,
            "Neither purchase indicator may calculate without a contract date.");
        first.ContractDate = contract;
        db.Orders.RemoveRange(startDay, endDay, futureOrder);
        await db.SaveChangesAsync();

        foreach (var disabled in new double?[] { null, 0, -1 })
        {
            first.DailyPurchaseCoefficient = disabled;
            first.DailyRepaymentCoefficient = disabled;
            second.DailyPlannedPurchaseCoefficient = disabled;
            await db.SaveChangesAsync();
            Check(await Required(first.Id) == 0 && await finance.EzhigodPogashenieAsync(first.Id, current.Id) == 0 &&
                await Planned(second.Id) == 0, "Disabled coefficients must not fall back to global settings.");
        }

        first.DailyRepaymentCoefficient = 50;
        first.Debt = 85; // Existing cap: balance + today's payments - this order = 15.
        await db.SaveChangesAsync();
        Check(await finance.EzhigodPogashenieAsync(first.Id, current.Id) == 15, "Repayment must remain capped at the existing debt limit.");
        first.Debt = 78;
        first.DailyRepaymentCoefficient = .01;
        await db.SaveChangesAsync();
        Check(await finance.EzhigodPogashenieAsync(first.Id, current.Id) == 8, "The existing full-settlement rule below 10 must remain active.");
        first.Debt = 60;
        await db.SaveChangesAsync();
        Check(await finance.EzhigodPogashenieAsync(first.Id, current.Id) == 0, "Repayment must not become negative.");

        first.Debt = 1000;
        first.ContractDate = DateTime.Today.AddDays(1);
        first.DailyPurchaseCoefficient = 100;
        first.DailyRepaymentCoefficient = 100;
        second.ContractDate = null;
        second.DailyPlannedPurchaseCoefficient = 100;
        await db.SaveChangesAsync();
        Check(await Required(first.Id) == 0 && await finance.EzhigodPogashenieAsync(first.Id, current.Id) == 0 &&
            await Planned(second.Id) == 0, "Future/missing contract-date rules changed.");
        var saved = await db.Customers.AsNoTracking().SingleAsync(c => c.Id == first.Id);
        Check(saved.DailyPurchaseCoefficient == 100 && saved.DailyRepaymentCoefficient == 100,
            "Reading order formulas must not recalculate or overwrite saved coefficients.");

        var customerIds = new[] { first.Id, second.Id, third.Id, missing.Id };
        var orderIds = new[] { current.Id, previous.Id, plannedOrder.Id };
        await db.Payments.Where(p => customerIds.Contains(p.CustomerId)).ExecuteDeleteAsync();
        await db.Returns.Where(r => customerIds.Contains(r.CustomerId)).ExecuteDeleteAsync();
        await db.OrderDetails.Where(d => orderIds.Contains(d.OrderId)).ExecuteDeleteAsync();
        await db.Orders.Where(o => orderIds.Contains(o.Id)).ExecuteDeleteAsync();
        await db.Customers.Where(c => customerIds.Contains(c.Id)).ExecuteDeleteAsync();
        await db.SalesManagers.Where(m => m.Id == manager.Id || m.Id == otherManager.Id).ExecuteDeleteAsync();
        await db.Products.Where(p => p.ArticleNumber == product.ArticleNumber).ExecuteDeleteAsync();
        await db.Brands.Where(b => b.Id == product.BrandId).ExecuteDeleteAsync();
        await db.Groups.Where(g => g.Id == product.GroupId).ExecuteDeleteAsync();
        await db.Users.Where(u => u.Id == user.Id).ExecuteDeleteAsync();
        await db.RaschetKoefficenta.Where(c => c.Id == settings.Id).ExecuteDeleteAsync();
        Console.WriteLine("PASS: purchase indicators use saved coefficients and contract-to-today sales; inclusive dates, weekends, ordinary/barter sales, future orders, debt/payment/return independence, zero floor, order deletion, manager/route lists and repayment caps");
    }

    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
