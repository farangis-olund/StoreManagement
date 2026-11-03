using Infrastructure.Constants;
using Infrastructure.Contexts;
using Infrastructure.Entities;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

internal sealed class AvailableRestrictionTests(Func<DatabaseContext> createContext)
{
    public static void CheckCalculation()
    {
        foreach (var (limit, balance, expected) in new (decimal, decimal, decimal)[]
        {
            (2000, 1000, 1000), (2000, 800, 1200), (2000, 500, 1500),
            (2000, 2500, -500), (2000, 2000, 0), (2000, -100, 2100), (2000.50m, 1000.25m, 1000.25m)
        })
        {
            var info = new Infrastructure.Dtos.CustomerFinanceInfo { CreditLimit = limit, Balance = balance };
            if (info.AvailableRestriction != expected || info.CreditLimit != limit || info.Balance != balance)
                throw new InvalidOperationException("Available restriction must subtract balance without mutating either value.");
        }
        Console.WriteLine("PASS: available restriction arithmetic, including zero, debt over limit, credit, and fractions");
    }

    public async Task RunAsync()
    {
        const string customerId = "RESTRICTION-TEST";
        await using var db = createContext();
        db.Customers.Add(new CustomerEntity
        {
            Id = customerId, FullName = "Restriction test", Restriction = 2000, Debt = 1000
        });
        await db.SaveChangesAsync();
        var finance = new CustomerFinanceService(new TestContextFactory(createContext));

        async Task Check(decimal expectedBalance, decimal expectedAvailable)
        {
            var info = await finance.GetFinanceInfoAsync(customerId, orderId: null!);
            if (info.Balance != expectedBalance || info.AvailableRestriction != expectedAvailable)
                throw new InvalidOperationException($"Unexpected balance/restriction: {info.Balance}/{info.AvailableRestriction}.");
            if (await db.Customers.AsNoTracking().Where(c => c.Id == customerId).Select(c => c.Restriction).SingleAsync() != 2000)
                throw new InvalidOperationException("Displaying the available restriction changed the saved customer limit.");
        }

        await Check(1000, 1000);
        db.Payments.Add(new CustomerPaymentEntity { CustomerId = customerId, Amount = 200, Date = DateTime.Today });
        await db.SaveChangesAsync();
        await Check(800, 1200);
        db.Returns.Add(new ReturnEntity
        {
            Id = "RESTRICTION-RETURN", CustomerId = customerId,
            RefundMethod = RefundMethodConstants.Balance, TotalAmount = 300
        });
        await db.SaveChangesAsync();
        await Check(500, 1500);
        await db.Customers.Where(c => c.Id == customerId).ExecuteUpdateAsync(s => s.SetProperty(c => c.Debt, 3000));
        await Check(2500, -500);
        await db.Customers.Where(c => c.Id == customerId).ExecuteUpdateAsync(s => s.SetProperty(c => c.Debt, 2500));
        await Check(2000, 0);
        Console.WriteLine("PASS: restriction minus summary balance, payment/return refresh, negative/zero results, and saved limit preserved");
    }
}
