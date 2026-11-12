using Infrastructure.Contexts;
using Infrastructure.Entities;
using Infrastructure.Migrations;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.RegularExpressions;

internal sealed class PaymentReceiptNumberTests(Func<DatabaseContext> createContext)
{
    public async Task RunAsync()
    {
        await using var db = createContext();
        var customer = new CustomerEntity { Id = "RECEIPT-CUSTOMER", FullName = "Receipt numbering" };
        var courier = new CourierEntity { Id = "RECEIPT-COURIER", FullName = "Receipt courier" };
        var user = new UserEntity { UserName = "receipt-test", FirstName = "Receipt", LastName = "Test", Password = "test" };
        var order = new OrderEntity { Id = "СЧ-TEST12", Customer = customer, Courier = courier, User = user, Date = DateTime.Today, SuminWords = "Test" };
        var first = new CustomerPaymentEntity { Customer = customer, Courier = courier, Order = order, Date = DateTime.Today, Amount = 30, Rate = 11 };
        var second = new CustomerPaymentEntity { Customer = customer, Courier = courier, Date = DateTime.Today, Amount = 40, Rate = 10 };
        db.Payments.AddRange(first, second);
        var settlement = new CourierPaymentEntity { CustomerPayment = first, Courier = courier, Order = order,
            Date = DateTime.Today, AmountInEuro = 30, AmountInTJS = 330 };
        db.CourierPayments.Add(settlement);
        await db.SaveChangesAsync();

        // Upgrade the actual pre-numbering schema in the disposable test database.
        var migration = new AddPaymentReceiptNumbers();
        var generator = db.GetService<IMigrationsSqlGenerator>();
        foreach (var command in generator.Generate(migration.DownOperations, db.GetService<IDesignTimeModel>().Model))
            await db.Database.ExecuteSqlRawAsync(command.CommandText);
        await db.Database.ExecuteSqlRawAsync("DELETE FROM __EFMigrationsHistory WHERE MigrationId = '20261112000000_AddPaymentReceiptNumbers'");
        await db.Database.MigrateAsync();
        db.ChangeTracker.Clear();
        var historical = await db.Payments.Where(p => p.CustomerId == customer.Id).OrderBy(p => p.Id).ToListAsync();
        Check(historical.Count == 2 && historical.Select(p => p.Id).ToHashSet().SetEquals([first.Id, second.Id]) &&
            historical.Sum(p => p.Amount) == 70 && historical.Single(p => p.Id == first.Id).OrderId == order.Id &&
            historical.Single(p => p.Id == second.Id).OrderId == null,
            "Number migration changed historical IDs, amounts or invoice relationships.");
        Check(historical.All(p => Valid(p.ReceiptNumber)) && historical.Select(p => p.ReceiptNumber).Distinct().Count() == 2,
            "Historical receipts must receive distinct ПЛ-XXXXXX numbers.");
        Check(await db.CourierPayments.AnyAsync(p => p.Id == settlement.Id && p.CustomerPaymentId == first.Id && p.AmountInTJS == 330),
            "Number migration damaged the original courier settlement.");
        await db.Database.MigrateAsync();
        Check((await db.Payments.Where(p => p.CustomerId == customer.Id).OrderBy(p => p.Id).Select(p => p.ReceiptNumber).ToListAsync())
            .SequenceEqual(historical.Select(p => p.ReceiptNumber)), "A second startup changed existing receipt numbers.");

        var factory = new TestContextFactory(createContext);
        var service = new OrderService(null!, null!, NullLogger<OrderService>.Instance, null!, factory, new TestPermissions());
        var added = await service.AddPaymentAsync(customer.Id, 12.5m, 11, orderId: order.Id, courierId: courier.Id);
        var standalone = await service.AddPaymentAsync(customer.Id, 8m, 10, courierId: courier.Id);
        Check(added != null && standalone != null && Valid(added.ReceiptNumber) && Valid(standalone.ReceiptNumber) &&
            added.ReceiptNumber != standalone.ReceiptNumber, "New invoice and standalone payments need distinct formatted receipt numbers.");
        var candidate = "ПЛ-ZZTEST";
        var collisionService = new Candidates(factory, [added!.ReceiptNumber!, candidate]);
        var retried = await collisionService.AddPaymentAsync(customer.Id, 3m, 11, courierId: courier.Id);
        Check(retried?.ReceiptNumber == candidate && collisionService.Calls == 2, "An existing receipt-number collision was not regenerated.");

        var summary = await service.GetPaymentsByCustomerAsync(customer.Id);
        var delivery = await service.GetOrdersByCourierAsync(courier.Id);
        var unpaid = await service.GetUnpaidOrdersAsync(customer.Id);
        Check(summary.Single(p => p.Id == added.Id).ReceiptNumber == added.ReceiptNumber &&
            delivery.Single(p => p.PaymentId == added.Id).ReceiptNumber == added.ReceiptNumber &&
            unpaid.Single(p => p.PaymentId == added.Id).DocumentNumber == added.ReceiptNumber,
            "The customer summary, courier delivery and unpaid list must use the same persisted receipt number.");
        db.Payments.Add(new() { CustomerId = customer.Id, CourierId = courier.Id, ReceiptNumber = added.ReceiptNumber,
            Amount = 99, Rate = 11, Date = DateTime.Today });
        var duplicateRejected = false;
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException) { duplicateRejected = true; }
        db.ChangeTracker.Clear();
        Check(duplicateRejected && await db.Payments.CountAsync(p => p.CustomerId == customer.Id) == 5,
            "The database must reject duplicate public numbers without adding a payment.");
        Check(await service.ConfirmDeliveryAsync(courier.Id, order.Id, added.Id),
            "A payment with a public receipt number must still be confirmed by its original integer ID.");
        Check(await db.Payments.Where(p => p.Id == added.Id).Select(p => p.ReceiptNumber).SingleAsync() == added.ReceiptNumber,
            "Delivery confirmation changed the public receipt number.");

        await db.CourierPayments.Where(p => p.CourierId == courier.Id).ExecuteDeleteAsync();
        await db.Payments.Where(p => p.CustomerId == customer.Id).ExecuteDeleteAsync();
        await db.Orders.Where(p => p.CustomerId == customer.Id).ExecuteDeleteAsync();
        await db.Customers.Where(p => p.Id == customer.Id).ExecuteDeleteAsync();
        await db.Couriers.Where(p => p.Id == courier.Id).ExecuteDeleteAsync();
        await db.Users.Where(p => p.Id == user.Id).ExecuteDeleteAsync();
        Console.WriteLine("PASS: receipt-number migration/restart preserves IDs, amounts and settlements; unique public numbers, collision regeneration, consistent projections and delivery confirmation");
    }

    private static bool Valid(string? value) => value != null && Regex.IsMatch(value, "^ПЛ-[A-Z0-9]{6}$");
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private sealed class Candidates(TestContextFactory factory, string[] candidates) : OrderService(null!, null!,
        NullLogger<OrderService>.Instance, null!, factory, new TestPermissions())
    {
        public int Calls { get; private set; }
        protected override string CreatePaymentReceiptNumber() => candidates[Calls++];
    }
}
