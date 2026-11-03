using Infrastructure.Contexts;
using Infrastructure.Entities;
using Infrastructure.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

internal sealed class DeliveryPaymentMigrationTests(Func<DatabaseContext> createContext)
{
    public async Task RunAsync()
    {
        await using var db = createContext();
        var customer = new CustomerEntity { Id = "MIGRATION-CUSTOMER", FullName = "Migration test" };
        var courier = new CourierEntity { Id = "MIGRATION-COURIER", FullName = "Migration courier" };
        var user = new UserEntity { UserName = "migration-test", FirstName = "Test", LastName = "User", Password = "test" };
        OrderEntity Order(string id, bool paid = false, bool barter = false) => new()
        {
            Id = id, Customer = customer, Courier = courier, User = user,
            Date = DateTime.Today, SuminWords = "Test", IsPaid = paid, IsBarter = barter
        };
        var open = Order("MIGRATION-OPEN");
        var paid = Order("MIGRATION-PAID", paid: true);
        var barter = Order("MIGRATION-BARTER", barter: true);
        CustomerPaymentEntity Receipt(OrderEntity? order) => new()
        {
            Customer = customer, Order = order, Amount = 10, Rate = 11, Date = DateTime.Today
        };
        var pendingReceipt = Receipt(open);
        var paidReceipt = Receipt(paid);
        var barterReceipt = Receipt(barter);
        var standalone = Receipt(null);
        db.Payments.AddRange(pendingReceipt, paidReceipt, barterReceipt, standalone);
        db.CourierPayments.Add(new CourierPaymentEntity
        {
            Courier = courier, Order = paid, Date = DateTime.Today, AmountInEuro = 10, AmountInTJS = 110
        });
        await db.SaveChangesAsync();

        var migration = new TrackDeliveryPayments();
        // Reconstruct the previous schema in this disposable database, then upgrade it.
        await ExecuteAsync(db, migration.DownOperations);
        var history = db.GetService<IHistoryRepository>();
        await db.Database.ExecuteSqlRawAsync(history.GetCreateIfNotExistsScript());
        foreach (var id in db.GetService<IMigrationsAssembly>().Migrations.Keys.Where(id => id != "20261029000000_TrackDeliveryPayments"))
            await db.Database.ExecuteSqlRawAsync(history.GetInsertScript(new HistoryRow(id, "8.0.1")));
        // Exercise the same migrator used during application startup, including a second startup.
        await db.Database.MigrateAsync();
        await db.Database.MigrateAsync();
        db.ChangeTracker.Clear();
        var receipts = await db.Payments.Where(p => p.CustomerId == customer.Id).ToDictionaryAsync(p => p.Id);
        Check(receipts[pendingReceipt.Id].CourierId == courier.Id && !receipts[pendingReceipt.Id].IsDeliveryConfirmed,
            "Pending legacy receipt did not inherit its courier.");
        Check(receipts[paidReceipt.Id].IsDeliveryConfirmed && receipts[barterReceipt.Id].IsDeliveryConfirmed,
            "Migration must preserve settled/barter receipts.");
        Check(receipts[standalone.Id].OrderId == null && receipts[standalone.Id].CourierId == null &&
            receipts.Values.Sum(p => p.Amount) == 40, "Migration changed historical identities, amounts or guessed a courier.");
        Check(await db.CourierPayments.CountAsync(p => p.CourierId == courier.Id) == 1,
            "Migration duplicated courier settlements.");

        var model = db.GetService<IDesignTimeModel>().Model;
        var snapshot = db.GetService<IModelRuntimeInitializer>().Initialize(
            db.GetService<IMigrationsAssembly>().ModelSnapshot!.Model, designTime: true);
        Check(!db.GetService<IMigrationsModelDiffer>().HasDifferences(snapshot.GetRelationalModel(), model.GetRelationalModel()),
            "Database model and migration snapshot differ.");
        await db.CourierPayments.Where(p => p.CourierId == courier.Id).ExecuteDeleteAsync();
        await db.Payments.Where(p => p.CustomerId == customer.Id).ExecuteDeleteAsync();
        await db.Orders.Where(o => o.CustomerId == customer.Id).ExecuteDeleteAsync();
        await db.Customers.Where(c => c.Id == customer.Id).ExecuteDeleteAsync();
        await db.Couriers.Where(c => c.Id == courier.Id).ExecuteDeleteAsync();
        await db.Users.Where(u => u.Id == user.Id).ExecuteDeleteAsync();
        Console.WriteLine("PASS: delivery migration preserves legacy receipts and settlements; model snapshot matches");
    }

    private static async Task ExecuteAsync(DatabaseContext db, IReadOnlyList<MigrationOperation> operations)
    {
        var generator = db.GetService<IMigrationsSqlGenerator>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        foreach (var command in generator.Generate(operations, db.GetService<IDesignTimeModel>().Model))
            await db.Database.ExecuteSqlRawAsync(command.CommandText);
        await transaction.CommitAsync();
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
