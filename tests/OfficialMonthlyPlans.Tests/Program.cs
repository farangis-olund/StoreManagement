using Infrastructure.Constants;
using Infrastructure.Contexts;
using Infrastructure.Entities;
using Infrastructure.Migrations;
using Infrastructure.Security;
using Infrastructure.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging.Abstractions;

return await new MonthlyPlanTests().Run(args.Contains("--no-database"));

internal sealed class MonthlyPlanTests
{
    private readonly string _name = "MonthlyPlanTests_" + Guid.NewGuid().ToString("N");
    private readonly Grants _grants = new();
    private readonly DateTime _month = new(2026, 11, 1);
    private int _userId;
    private string Master => new SqlConnectionStringBuilder { DataSource = @"(LocalDB)\MSSQLLocalDB", InitialCatalog = "master",
        IntegratedSecurity = true, TrustServerCertificate = true, ConnectTimeout = 15 }.ConnectionString;
    private DatabaseContext Context() => new(new DbContextOptionsBuilder<DatabaseContext>().UseSqlServer(
        new SqlConnectionStringBuilder(Master) { InitialCatalog = _name }.ConnectionString).Options);
    private Factory ContextFactory => new(Context);
    private OfficialMonthlyPlanService Service => new(ContextFactory, _grants);

    public async Task<int> Run(bool noDatabase)
    {
        var created = false;
        try
        {
            await CheckPermissions();
            Check(OfficialMonthlyPlanService.MonthStart(new DateTime(2026, 12, 31)) == new DateTime(2026, 12, 1), "Month normalization failed.");
            await using (var db = Context())
            {
                var model = db.GetService<IDesignTimeModel>().Model;
                var snapshot = db.GetService<IModelRuntimeInitializer>().Initialize(db.GetService<IMigrationsAssembly>().ModelSnapshot!.Model, true);
                var diff = db.GetService<IMigrationsModelDiffer>().GetDifferences(snapshot.GetRelationalModel(), model.GetRelationalModel());
                Check(diff.Count == 0, "Migration snapshot mismatch: " + string.Join(", ", diff.Select(x => x.GetType().Name)));
            }
            Console.WriteLine("PASS: four independent permissions, denied access before DB queries, period normalization and migration snapshot");
            if (noDatabase) return 0;
            var directory = Path.Combine(AppContext.BaseDirectory, "test-data");
            Directory.CreateDirectory(directory);
            var mdf = Path.Combine(directory, _name + ".mdf").Replace("'", "''");
            var ldf = Path.Combine(directory, _name + "_log.ldf").Replace("'", "''");
            await MasterSql($"CREATE DATABASE [{_name}] ON PRIMARY (NAME=N'{_name}',FILENAME=N'{mdf}') LOG ON (NAME=N'{_name}_log',FILENAME=N'{ldf}')");
            created = true;
            await using (var db = Context())
            {
                await db.Database.EnsureCreatedAsync();
                foreach (var command in db.GetService<IMigrationsSqlGenerator>().Generate(new AddOfficialMonthlyPlanSettings().DownOperations,
                    db.GetService<IDesignTimeModel>().Model)) await db.Database.ExecuteSqlRawAsync(command.CommandText);
                foreach (var command in db.GetService<IMigrationsSqlGenerator>().Generate(new AddOfficialMonthlyPlans().DownOperations,
                    db.GetService<IDesignTimeModel>().Model)) await db.Database.ExecuteSqlRawAsync(command.CommandText);
                var history = db.GetService<IHistoryRepository>();
                await db.Database.ExecuteSqlRawAsync(history.GetCreateIfNotExistsScript());
                foreach (var id in db.GetService<IMigrationsAssembly>().Migrations.Keys.Where(x => x != "20261110000000_AddOfficialMonthlyPlans" && x != "20261110010000_AddOfficialMonthlyPlanSettings"))
                    await db.Database.ExecuteSqlRawAsync(history.GetInsertScript(new HistoryRow(id, "8.0.1")));
                await db.Database.MigrateAsync();
                await db.Database.MigrateAsync();
            }
            await Seed();
            await CheckCalculations();
            await CheckNewOrders();
            await CheckBulkSaving();
            await CheckCarryForward();
            await CheckThresholdAndOrderCap();
            Console.WriteLine("PASS: isolated LocalDB upgrade, monthly plans, official-only purchases, refunds, edits, deletion, history, totals and concurrent order defaults");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally
        {
            if (created)
            {
                SqlConnection.ClearAllPools();
                await MasterSql($"ALTER DATABASE [{_name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_name}]");
            }
        }
    }

    private static async Task CheckPermissions()
    {
        foreach (var key in new[] { PermissionCatalog.OfficialMonthlyPlansView, PermissionCatalog.OfficialMonthlyPlansManage,
            PermissionCatalog.OfficialMonthlyPlansTotal, PermissionCatalog.OfficialMonthlyPlansIndividual, PermissionCatalog.Orders })
        {
            var grants = new Grants(); grants.Keys.Add(key);
            var factory = new ProbeFactory();
            var service = new OfficialMonthlyPlanService(factory, grants);
            if (key != PermissionCatalog.OfficialMonthlyPlansView) await Denied(() => service.GetControlAsync(DateTime.Today));
            if (key != PermissionCatalog.OfficialMonthlyPlansManage) await Denied(() => service.GetSettingsAsync(DateTime.Today));
            if (key != PermissionCatalog.OfficialMonthlyPlansManage) await Denied(() => service.GetNonOfficialThresholdAsync());
            if (key != PermissionCatalog.OfficialMonthlyPlansManage) await Denied(() => service.SetPlansAsync(DateTime.Today,
                new Dictionary<string, decimal>(), 5));
            if (key != PermissionCatalog.OfficialMonthlyPlansManage) await Denied(() => service.SetPlanAsync("C1", DateTime.Today, 100));
            if (key != PermissionCatalog.OfficialMonthlyPlansManage) await Denied(() => service.SetPlansAsync(DateTime.Today,
                new Dictionary<string, decimal> { ["C1"] = 100, ["C2"] = 200 }));
            if (key != PermissionCatalog.Orders) await Denied(() => service.GetRetailStateAsync("C1", DateTime.Today));
            Check(factory.Calls == 0, "Denied operations reached the database.");
        }
        var manager = new Grants(); manager.Keys.Add(PermissionCatalog.OfficialMonthlyPlansManage);
        var probe = new ProbeFactory();
        foreach (var amount in new[] { 0m, -1m, 1.001m, 10000000000000000m })
        {
            try { await new OfficialMonthlyPlanService(probe, manager).SetPlanAsync("C1", DateTime.Today, amount); throw new Exception("Invalid amount allowed."); }
            catch (ArgumentException) { }
            try
            {
                await new OfficialMonthlyPlanService(probe, manager).SetPlansAsync(DateTime.Today,
                    new Dictionary<string, decimal> { ["C1"] = 100, ["C2"] = amount });
                throw new Exception("Invalid bulk amount allowed.");
            }
            catch (ArgumentException) { }
        }
        Check(probe.Calls == 0, "Invalid amounts reached the database.");
        foreach (var threshold in new[] { -1m, .001m, 10000000000000000m })
        {
            try
            {
                await new OfficialMonthlyPlanService(probe, manager).SetPlansAsync(DateTime.Today, new Dictionary<string, decimal>(), threshold);
                throw new Exception("Invalid threshold allowed.");
            }
            catch (ArgumentException) { }
        }
        Check(probe.Calls == 0, "Invalid global thresholds reached the database.");
    }

    private async Task Seed()
    {
        _grants.Keys.UnionWith(new[] { PermissionCatalog.OfficialMonthlyPlansManage, PermissionCatalog.OfficialMonthlyPlansView,
            PermissionCatalog.OfficialMonthlyPlansTotal, PermissionCatalog.OfficialMonthlyPlansIndividual,
            PermissionCatalog.Orders, PermissionCatalog.OrdersDirectFromStock });
        await using var db = Context();
        var user = new UserEntity { UserName = "plan-test", FirstName = "Plan", LastName = "Test", Password = "test-only" };
        var brand = new BrandEntity { BrandName = "Plans" };
        var group = new GroupEntity { GroupName = "Plans" };
        db.AddRange(user, brand, group);
        foreach (var id in new[] { "C1", "C2", "C3", "UNPLANNED", "CONCURRENT", "NORMAL" })
            db.Customers.Add(new CustomerEntity { Id = id, FullName = id, OfficialCustomer = id != "NORMAL" });
        await db.SaveChangesAsync();
        _userId = user.Id;
        db.Products.Add(new ProductEntity { ArticleNumber = "TEST", ProductName = "Test", Marka = "", Model = "", Alternative = "",
            WarehousePlace = "", BrandId = brand.Id, GroupId = group.Id, Quentity = 1000 });
        await db.SaveChangesAsync();
    }

    private OrderEntity Order(string id, string client, decimal amount, bool official = true, DateTime? date = null) => new()
    {
        Id = id, CustomerId = client, Date = date ?? _month.AddDays(3), OfficialOrder = official, UserId = _userId,
        Rate = 1, SuminWords = "Test", DirectFromStock = true,
        OrderDetails = [new OrderDetailEntity { OrderId = id, ArticleNumber = "TEST", Quentity = 1, Price = amount }]
    };
    private async Task Add(OrderEntity order) { await using var db = Context(); db.Orders.Add(order); await db.SaveChangesAsync(); }
    private async Task CheckRemaining(decimal purchased, decimal remaining)
    {
        var row = (await Service.GetControlAsync(_month)).Single(r => r.CustomerId == "C1");
        Check(row.Purchased == purchased && row.Remaining == remaining, $"Unexpected purchased/remaining: {row.Purchased}/{row.Remaining}");
    }

    private async Task CheckCalculations()
    {
        var service = Service;
        await service.SetPlanAsync("C1", _month.AddDays(14), 1000);
        await Add(Order("OFFICIAL-300", "C1", 300)); await CheckRemaining(300, 700);
        await Add(Order("NORMAL-200", "C1", 200, false)); await CheckRemaining(300, 700);
        await Add(Order("OFFICIAL-400", "C1", 400)); await CheckRemaining(700, 300);
        await Add(Order("OFFICIAL-LAST", "C1", 300)); await CheckRemaining(1000, 0);
        var met = await service.GetRetailStateAsync("C1", _month);
        Check(!met.OfficialOrder && met.IndividualRemaining == 0, "Met plans must disable the default flag for new orders.");
        await service.SetPlanAsync("C2", _month, 2000); await Add(Order("C2-ORDER", "C2", 2000));
        await service.SetPlanAsync("C3", _month, 1500); await Add(Order("C3-ORDER", "C3", 500));
        await Add(Order("PREVIOUS", "C1", 9999, date: _month.AddMonths(-1)));
        await Add(Order("NEXT", "C1", 0, false, _month.AddMonths(1)));
        await using (var db = Context())
        {
            db.Returns.AddRange(
                new ReturnEntity { Id = "REFUND", CustomerId = "C1", InvoiceNumber = "OFFICIAL-300", Date = _month.AddMonths(1), TotalAmount = 300, RefundMethod = "cash" },
                new ReturnEntity { Id = "ORDINARY-REFUND", CustomerId = "C1", InvoiceNumber = "NORMAL-200", TotalAmount = 100 },
                new ReturnEntity { Id = "MANUAL-REFUND", CustomerId = "C1", InvoiceNumber = "OFFICIAL-300", IsManual = true, TotalAmount = 999 });
            await db.SaveChangesAsync();
        }
        await CheckRemaining(700, 300);
        var control = await service.GetControlAsync(_month);
        Check(control.Count == 3 && control.Sum(r => r.Amount) == 4500 && control.Sum(r => r.Purchased) == 3200 && control.Sum(r => r.Remaining) == 1300,
            "The control table must retain completed clients and total remaining plans per client.");
        var panel = await service.GetRetailStateAsync("C1", _month);
        Check(panel.TotalRemaining == 1300 && panel.IndividualRemaining == 300 && panel.OfficialOrder, "Retail indicators/default disagree with the table.");
        var none = await service.GetRetailStateAsync(null, _month);
        Check(none.TotalRemaining == 1300 && none.IndividualRemaining == null, "The global total must not depend on selection.");
        var unplanned = await service.GetRetailStateAsync("UNPLANNED", _month);
        Check(unplanned.OfficialOrder && unplanned.IndividualRemaining == null && unplanned.TotalRemaining == 1300, "Unplanned official customers must keep their original behavior.");
        Check(!(await service.GetRetailStateAsync("NORMAL", _month)).OfficialOrder, "A regular customer must not default to official orders.");
        Check((await service.GetSettingsAsync(_month)).Count == 5, "Settings must include every official customer, even without a plan.");
        _grants.Keys.Remove(PermissionCatalog.OfficialMonthlyPlansIndividual);
        Check((await service.GetRetailStateAsync("C1", _month)).IndividualRemaining == null, "Individual information leaked without its permission.");
        _grants.Keys.Remove(PermissionCatalog.OfficialMonthlyPlansTotal);
        var hidden = await service.GetRetailStateAsync("C1", _month);
        Check(hidden.TotalRemaining == null && hidden.OfficialOrder, "Display permissions must not change the automatic order rule.");
        _grants.Keys.UnionWith(new[] { PermissionCatalog.OfficialMonthlyPlansIndividual, PermissionCatalog.OfficialMonthlyPlansTotal });
        await service.SetPlanAsync("C1", _month.AddMonths(1), 777);
        var next = (await service.GetControlAsync(_month.AddMonths(1))).Single(r => r.CustomerId == "C1");
        Check(next.Amount == 777 && next.Purchased == 0 && next.Remaining == 777, "A later refund must affect the original month, without consuming next month's plan.");
        await using (var db = Context())
        {
            await db.OrderDetails.Where(d => d.OrderId == "OFFICIAL-400").ExecuteUpdateAsync(s => s.SetProperty(d => d.Price, 200));
        }
        await CheckRemaining(500, 500);
        await using (var db = Context()) await db.Orders.Where(o => o.Id == "OFFICIAL-LAST").ExecuteDeleteAsync();
        await CheckRemaining(200, 800);
        await using (var db = Context()) await db.Returns.Where(r => r.Id == "REFUND").ExecuteDeleteAsync();
        await CheckRemaining(500, 500);
        await service.SetPlanAsync("C1", _month, 500);
        await CheckRemaining(500, 0);
        await using (var db = Context())
        {
            Check(await db.Customers.Where(c => c.Id == "C1").Select(c => c.OfficialCustomer).SingleAsync(), "Meeting a plan changed the customer status.");
            Check(await db.Orders.Where(o => o.Id == "OFFICIAL-300").Select(o => o.OfficialOrder).SingleAsync(), "A historical order's flag was rewritten.");
            Check(await db.OfficialMonthlyPlans.CountAsync(p => p.CustomerId == "C1") == 2, "Saving a month replaced plan history.");
        }
    }

    private async Task CheckNewOrders()
    {
        var service = Service;
        var orders = new OrderService(null!, null!, NullLogger<OrderService>.Instance, null!, ContextFactory, _grants, service);
        var changes = 0;
        service.Changed += (_, _) => Interlocked.Increment(ref changes);
        var afterMet = await orders.AddOrderAsync(Order("AFTER-MET", "C1", 100));
        Check(afterMet != null && !afterMet.OfficialOrder, "The save layer allowed a stale OfficialOrder flag after the plan was met.");
        Check(changes == 1, "Saving an order must notify open plan displays after commit.");
        await service.SetPlanAsync("CONCURRENT", _month, 100);
        var saved = await Task.WhenAll(orders.AddOrderAsync(Order("RACE-A", "CONCURRENT", 100)), orders.AddOrderAsync(Order("RACE-B", "CONCURRENT", 100)));
        Check(saved.All(o => o != null) && saved.Count(o => o!.OfficialOrder) == 1,
            "Simultaneous orders must check the plan serially, without rewriting previously saved orders.");
        Check(changes == 4, "Plan changes and both committed orders must notify the displays.");
    }

    private async Task CheckBulkSaving()
    {
        var service = Service;
        var notifications = 0;
        service.Changed += (_, _) => notifications++;
        try
        {
            await service.SetPlansAsync(_month, new Dictionary<string, decimal> { ["C1"] = 1250.50m, ["NORMAL"] = 300 });
            throw new Exception("Bulk save accepted a non-official customer.");
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("официальному клиенту")) { }
        await using (var db = Context())
        {
            Check((await db.OfficialMonthlyPlans.FindAsync("C1", _month))!.Amount == 500,
                "A failed batch partially saved a valid customer's plan.");
            Check(!await db.OfficialMonthlyPlans.AnyAsync(p => p.CustomerId == "NORMAL"), "An invalid batch inserted a plan.");
        }
        Check(notifications == 0, "Failed batches must not notify plan displays.");
        await service.SetPlansAsync(_month.AddDays(15), new Dictionary<string, decimal> { ["C1"] = 1250.50m, ["UNPLANNED"] = 300 });
        await service.SetPlansAsync(_month, new Dictionary<string, decimal>());
        Check(notifications == 1, "A committed batch must notify once; an empty batch must not notify.");
        await using (var db = Context())
        {
            Check((await db.OfficialMonthlyPlans.FindAsync("C1", _month))!.Amount == 1250.50m &&
                (await db.OfficialMonthlyPlans.FindAsync("UNPLANNED", _month))!.Amount == 300,
                "The batch must update existing plans and insert new plans for the selected month.");
            Check((await db.OfficialMonthlyPlans.FindAsync("C2", _month))!.Amount == 2000 &&
                (await db.OfficialMonthlyPlans.FindAsync("C1", _month.AddMonths(1)))!.Amount == 777,
                "Bulk saving changed an untouched customer or a different month.");
        }
        Console.WriteLine("PASS: bulk plan saving, validation before writes, atomic failure, single notification, untouched rows and month history");
    }

    private async Task CheckCarryForward()
    {
        var service = Service;
        await using (var db = Context())
        {
            db.Customers.AddRange(new CustomerEntity { Id = "NEVER", FullName = "Never planned", OfficialCustomer = true },
                new CustomerEntity { Id = "FORMER", FullName = "Former official", OfficialCustomer = false });
            db.OfficialMonthlyPlans.Add(new() { CustomerId = "FORMER", Month = _month, Amount = 900 });
            await db.SaveChangesAsync();
        }
        var january = _month.AddMonths(2);
        var settingsTask = service.GetSettingsAsync(january);
        var controlTask = service.GetControlAsync(january);
        var retailTask = service.GetRetailStateAsync("C1", january);
        await Task.WhenAll(settingsTask, controlTask, retailTask);
        var settings = await settingsTask;
        Check(settings.Single(r => r.CustomerId == "C1").Amount == 777 &&
            settings.Single(r => r.CustomerId == "C2").Amount == 2000,
            "The new year must inherit the latest saved amounts, including December overrides.");
        Check(settings.Single(r => r.CustomerId == "CONCURRENT").Amount == 100 &&
            settings.Single(r => r.CustomerId == "UNPLANNED").Amount == 300,
            "Skipped periods must inherit full plans, not last month's remaining balances.");
        Check(settings.Single(r => r.CustomerId == "NEVER").Amount == null && settings.All(r => r.CustomerId != "FORMER"),
            "Never-planned and no-longer-official customers must not receive inherited plans.");
        Check((await controlTask).All(r => r.Purchased == 0 && r.Remaining == r.Amount) &&
            (await retailTask).IndividualRemaining == 777,
            "New-month control and retail must use inherited plans and current-month sales only.");
        await service.SetPlansAsync(january, new Dictionary<string, decimal> { ["C1"] = 800 });
        settings = await service.GetSettingsAsync(january);
        Check(settings.Single(r => r.CustomerId == "C1").Amount == 800 && settings.Single(r => r.CustomerId == "C2").Amount == 2000,
            "Editing one inherited plan must leave the other inherited amounts unchanged.");
        await using (var db = Context())
        {
            Check(await db.OfficialMonthlyPlans.CountAsync(p => p.Month == january) == 5,
                "Concurrent initialization or repeated refresh created duplicate plans.");
            Check((await db.OfficialMonthlyPlans.FindAsync("C1", _month))!.Amount == 1250.50m &&
                (await db.OfficialMonthlyPlans.FindAsync("C1", _month.AddMonths(1)))!.Amount == 777,
                "Inheriting and editing a new month must preserve historical amounts.");
        }
        var seller = new Grants();
        seller.Keys.UnionWith([PermissionCatalog.Orders, PermissionCatalog.OfficialMonthlyPlansTotal, PermissionCatalog.OfficialMonthlyPlansIndividual]);
        var sellerPlans = new OfficialMonthlyPlanService(ContextFactory, seller);
        var february = await sellerPlans.GetRetailStateAsync("C1", january.AddMonths(1));
        Check(february.IndividualRemaining == 800 && february.OfficialOrder,
            "Opening Orders must initialize a new month without waiting for a manager to open settings.");
        var orders = new OrderService(null!, null!, NullLogger<OrderService>.Instance, null!, ContextFactory, _grants, sellerPlans);
        var march = january.AddMonths(2);
        var saved = await orders.AddOrderAsync(Order("ROLLOVER-FIRST", "C1", 800, date: march));
        Check(saved?.OfficialOrder == true && !(await sellerPlans.GetRetailStateAsync("C1", march)).OfficialOrder,
            "The first new-month sale must apply the inherited plan even before the order panel has loaded it.");
        var april = march.AddMonths(1);
        await Task.WhenAll(service.SetPlanAsync("C1", april, 850), service.SetPlanAsync("C2", april, 2100),
            service.GetSettingsAsync(april), sellerPlans.GetRetailStateAsync("C1", april.AddMonths(1)));
        var edited = await service.GetSettingsAsync(april);
        Check(edited.Single(r => r.CustomerId == "C1").Amount == 850 && edited.Single(r => r.CustomerId == "C2").Amount == 2100 &&
            edited.Single(r => r.CustomerId == "C3").Amount == 1500,
            "Concurrent initialization and individual edits must preserve both explicit changes and inherited amounts.");
        Console.WriteLine("PASS: automatic plan rollover, year boundary, skipped months, concurrent initialization, per-client edits, history and seller-first access");
    }

    private async Task CheckThresholdAndOrderCap()
    {
        var service = Service;
        Check(await service.GetNonOfficialThresholdAsync() == 0, "An upgraded database must start with threshold zero.");
        await using (var db = Context())
        {
            foreach (var id in new[] { "CAP", "SMALL", "EQUAL", "ABOVE", "CAP-RACE" })
                db.Customers.Add(new() { Id = id, FullName = id, OfficialCustomer = true });
            await db.SaveChangesAsync();
        }
        await service.SetPlansAsync(_month, new Dictionary<string, decimal>
            { ["CAP"] = 1000, ["SMALL"] = 1000, ["EQUAL"] = 1000, ["ABOVE"] = 1000, ["CAP-RACE"] = 1000 }, 5);
        await Add(Order("SMALL-HISTORY", "SMALL", 997));
        await Add(Order("EQUAL-HISTORY", "EQUAL", 995));
        await Add(Order("ABOVE-HISTORY", "ABOVE", 994.99m));
        var small = await service.GetRetailStateAsync("SMALL", _month);
        var equal = await service.GetRetailStateAsync("EQUAL", _month);
        var above = await service.GetRetailStateAsync("ABOVE", _month);
        Check(!small.OfficialOrder && small.IndividualRemaining == 0 && small.OfficialOrderLimit == null &&
            !equal.OfficialOrder && equal.IndividualRemaining == 0 && equal.OfficialOrderLimit == null &&
            above.OfficialOrder && above.OfficialOrderLimit == 5.01m,
            "Threshold 5 must make remainders 3 and exactly 5 ordinary, while 5.01 still caps an official order.");
        var displayed = await service.GetControlAsync(_month);
        Check(displayed.Single(r => r.CustomerId == "SMALL") is { Amount: 1000, Purchased: 997, Remaining: 0 } &&
            displayed.Single(r => r.CustomerId == "EQUAL") is { Amount: 1000, Purchased: 995, Remaining: 0 } &&
            displayed.Single(r => r.CustomerId == "ABOVE").Remaining == 5.01m,
            "Control must display zero at or below the threshold without altering plans or purchases.");
        Check((await service.GetRetailStateAsync(null, _month)).TotalRemaining == displayed.Sum(r => r.Remaining),
            "The global retail total must sum the displayed remainders, excluding threshold-completed plans.");
        _grants.Keys.Remove(PermissionCatalog.OfficialMonthlyPlansTotal);
        Check((await service.GetRetailStateAsync("SMALL", _month)).IndividualRemaining == 0 &&
            (await service.GetRetailStateAsync("EQUAL", _month)).IndividualRemaining == 0,
            "Individual-only access must use the same threshold-aware remainder without a global summary query.");
        _grants.Keys.Add(PermissionCatalog.OfficialMonthlyPlansTotal);
        var orders = new OrderService(null!, null!, NullLogger<OrderService>.Instance, null!, ContextFactory, _grants, service);
        Check((await orders.AddOrderAsync(Order("SMALL-ORDINARY", "SMALL", 2000)))?.OfficialOrder == false &&
            (await orders.AddOrderAsync(Order("EQUAL-ORDINARY", "EQUAL", 2000)))?.OfficialOrder == false,
            "The save layer must downgrade stale official flags at or below the threshold and allow ordinary amounts.");
        Check((await service.GetRetailStateAsync("SMALL", _month)).IndividualRemaining == 0 &&
            (await service.GetControlAsync(_month)).Single(r => r.CustomerId == "SMALL").Purchased == 997,
            "Ordinary sales must retain the zero display without changing actual official purchases.");
        _grants.Keys.Remove(PermissionCatalog.OfficialMonthlyPlansIndividual);
        _grants.Keys.Remove(PermissionCatalog.OfficialMonthlyPlansTotal);
        var hidden = await service.GetRetailStateAsync("CAP", _month);
        Check(hidden.OfficialOrderLimit == 1000 && hidden.IndividualRemaining == null && hidden.TotalRemaining == null,
            "The operating cap must be enforced independently of permissions to display plan information.");
        _grants.Keys.UnionWith([PermissionCatalog.OfficialMonthlyPlansIndividual, PermissionCatalog.OfficialMonthlyPlansTotal, PermissionCatalog.OrdersExceedBuyLimit]);
        var oversized = Order("CAP-REJECTED", "CAP", 1000.01m);
        oversized.DirectFromStock = false;
        try { await orders.AddOrderAsync(oversized); throw new Exception("An oversized official order was saved."); }
        catch (OfficialMonthlyPlanLimitException ex) { Check(ex.Remaining == 1000 && ex.Message.Contains("отдельным заказом"), "The rejection must explain splitting the sale."); }
        await using (var db = Context()) Check(!await db.Orders.AnyAsync(o => o.Id == oversized.Id), "A rejected order was inserted.");
        Check((await orders.AddOrderAsync(Order("CAP-EXACT", "CAP", 1000)))?.OfficialOrder == true &&
            (await orders.AddOrderAsync(Order("CAP-AFTER", "CAP", 2000)))?.OfficialOrder == false,
            "The exact remainder must be allowed, and subsequent orders must become ordinary.");
        async Task<bool> Race(string id)
        {
            try { return (await orders.AddOrderAsync(Order(id, "CAP-RACE", 600)))?.OfficialOrder == true; }
            catch (OfficialMonthlyPlanLimitException ex) { Check(ex.Remaining == 400, "Concurrent rejection must use the latest committed remainder."); return false; }
        }
        Check((await Task.WhenAll(Race("CAP-RACE-A"), Race("CAP-RACE-B"))).Count(saved => saved) == 1,
            "Concurrent official orders must not jointly exceed the plan.");
        Check((await service.GetControlAsync(_month)).Single(r => r.CustomerId == "CAP-RACE").Purchased == 600,
            "The rejected concurrent order changed monthly purchases.");
        try
        {
            await service.SetPlansAsync(_month, new Dictionary<string, decimal> { ["NORMAL"] = 2000 }, 9);
            throw new Exception("Invalid settings batch was saved.");
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("официальному клиенту")) { }
        Check(await service.GetNonOfficialThresholdAsync() == 5, "A failed plan batch partially changed the global threshold.");
        await service.SetPlansAsync(_month.AddMonths(1), new Dictionary<string, decimal>(), 5.25m);
        Check(await Service.GetNonOfficialThresholdAsync() == 5.25m && !(await service.GetRetailStateAsync("ABOVE", _month)).OfficialOrder,
            "A threshold-only save must be global across months and service instances.");
        await service.SetPlansAsync(_month, new Dictionary<string, decimal>(), 0);
        Check((await service.GetRetailStateAsync("SMALL", _month)) is { OfficialOrder: true, IndividualRemaining: 3 } &&
            (await service.GetControlAsync(_month)).Single(r => r.CustomerId == "SMALL").Remaining == 3,
            "Lowering the threshold must restore the actual remainder and official cap without changing historical data.");
        Console.WriteLine("PASS: global threshold migration, inclusive 3/5 boundary, official cart cap, save rejection, ordinary sales, atomic settings and concurrent orders");
    }

    private async Task MasterSql(string sql)
    {
        await using var connection = new SqlConnection(Master); await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection); await command.ExecuteNonQueryAsync();
    }
    private static async Task Denied(Func<Task> action) { try { await action(); throw new Exception("Unauthorized operation allowed."); } catch (UnauthorizedAccessException) { } }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private sealed class Grants : IPermissionChecker { public HashSet<string> Keys { get; } = []; public bool Has(string key) => Keys.Contains(key); }
    private sealed class Factory(Func<DatabaseContext> create) : IDbContextFactory<DatabaseContext> { public DatabaseContext CreateDbContext() => create(); }
    private sealed class ProbeFactory : IDbContextFactory<DatabaseContext> { public int Calls; public DatabaseContext CreateDbContext() { Calls++; throw new Exception("No database is used by permission tests."); } }
}
