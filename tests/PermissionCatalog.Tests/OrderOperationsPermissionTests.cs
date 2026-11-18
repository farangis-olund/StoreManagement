using Infrastructure.Constants;
using Infrastructure.Contexts;
using Infrastructure.Entities;
using Infrastructure.Security;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

internal sealed class OrderOperationsPermissionTests(Func<DatabaseContext> createContext)
{
    public static async Task CheckAuthorizationAsync()
    {
        string[] keys = [PermissionCatalog.Orders, PermissionCatalog.OrdersDelivery, PermissionCatalog.OrdersDeliveryView,
            PermissionCatalog.OrdersDeliveryUpdate, PermissionCatalog.OrdersDeliveryPrint, PermissionCatalog.OrdersDeliveryReport,
            PermissionCatalog.OrdersAssignPickers];
        var grants = new TestPermissions();
        var session = new TestUserContext { UserId = 1, IsLoggedIn = true };
        var calls = 0;
        var factory = new TestContextFactory(() => { calls++; throw new Exception("Database boundary"); });
        var service = new OrderService(null!, null!, NullLogger<OrderService>.Instance, null!, factory, grants, session: session);
        var policy = new OrderOperationsAccessPolicy(grants);
        for (var mask = 0; mask < (1 << keys.Length); mask++)
        {
            grants.Keys.Clear();
            for (var i = 0; i < keys.Length; i++) if ((mask & (1 << i)) != 0) grants.Keys.Add(keys[i]);
            var orders = (mask & 1) != 0;
            var delivery = orders && (mask & 2) != 0;
            var view = delivery && (mask & 4) != 0;
            Check(policy.Can(keys[1]) == delivery && policy.Can(keys[2]) == view &&
                policy.Can(keys[3]) == (view && (mask & 8) != 0) &&
                policy.Can(keys[4]) == (view && (mask & 16) != 0) &&
                policy.Can(keys[5]) == (view && (mask & 32) != 0) &&
                policy.Can(keys[6]) == (orders && (mask & 64) != 0), "Order-operation permissions were not independent or lost their parent prerequisites.");
            if (!view) await Denied(() => service.GetOrdersByCourierAsync("C"));
            if (!policy.Can(keys[3]))
            {
                await Denied(() => service.ConfirmDeliveryAsync("C", "O", null));
                await Denied(() => service.UpdatePaymentStatusAsync("O", true));
                await Denied(() => service.AddCourierPaymentAsync("C", "O", 1, 11, DateTime.Today));
            }
            if (!policy.Can(keys[6]))
            {
                await Denied(() => service.GetOrdersInRangeForPickersAsync(DateTime.Today, DateTime.Today.AddDays(1)));
                await Denied(() => service.UpdateAssignPickerAsync("O", "P"));
            }
        }
        grants.Keys.UnionWith(keys); session.IsLoggedIn = false;
        await Denied(() => service.GetOrdersByCourierAsync("C"));
        await Denied(() => service.ConfirmDeliveryAsync("C", "O", null));
        await Denied(() => service.GetOrdersInRangeForPickersAsync(DateTime.Today, DateTime.Today.AddDays(1)));
        await Denied(() => service.UpdateAssignPickerAsync("O", "P"));
        Check(calls == 0 && !policy.Can("Unknown"), "Denied operations reached the database or an unknown permission was accepted.");
        Console.WriteLine("PASS: all 128 delivery/picker permission combinations; separate view/update/print/report, required parents and denial before database access");
    }

    public async Task RunAsync()
    {
        var prefix = "OPS-" + Guid.NewGuid().ToString("N")[..8];
        await using var db = createContext();
        var user = new UserEntity { UserName = prefix, FirstName = "Ops", LastName = "Test", Password = "test" };
        var customer = new CustomerEntity { Id = prefix, FullName = "Operations client" };
        var courier = new CourierEntity { Id = prefix, FullName = "Courier" };
        var picker = new StorekeeperEntity { Id = prefix, FullName = "Picker" };
        var order = new OrderEntity { Id = prefix, Customer = customer, Courier = courier, User = user, Date = DateTime.Today, SuminWords = "Test" };
        var payment = new CustomerPaymentEntity { Order = order, Customer = customer, Courier = courier, Date = DateTime.Today, Amount = 12.5m, Rate = 11 };
        db.Payments.Add(payment); db.Storekeepers.Add(picker); await db.SaveChangesAsync();
        var grants = new TestPermissions();
        grants.Keys.UnionWith([PermissionCatalog.Orders, PermissionCatalog.OrdersDelivery, PermissionCatalog.OrdersDeliveryView]);
        var session = new TestUserContext { UserId = user.Id, IsLoggedIn = true };
        var service = new OrderService(null!, null!, NullLogger<OrderService>.Instance, null!, new TestContextFactory(createContext), grants, session: session);
        Check((await service.GetOrdersByCourierAsync(courier.Id)).Single().PaymentId == payment.Id, "View-only delivery must load actual receipts.");
        await Denied(() => service.ConfirmDeliveryAsync(courier.Id, order.Id, payment.Id));
        grants.Keys.Add(PermissionCatalog.OrdersDeliveryPrint); grants.Keys.Add(PermissionCatalog.OrdersDeliveryReport);
        await Denied(() => service.ConfirmDeliveryAsync(courier.Id, order.Id, payment.Id));
        Check(!await db.Payments.AsNoTracking().Where(p => p.Id == payment.Id).Select(p => p.IsDeliveryConfirmed).SingleAsync(),
            "Print/report permission allowed settlement to be changed.");
        grants.Keys.Add(PermissionCatalog.OrdersDeliveryUpdate);
        Check(await service.ConfirmDeliveryAsync(courier.Id, order.Id, payment.Id) &&
            await db.CourierPayments.CountAsync(p => p.CustomerPaymentId == payment.Id) == 1, "Authorized delivery update must retain normal settlement accounting.");
        grants.Keys.Remove(PermissionCatalog.OrdersDeliveryView);
        await Denied(() => service.GetOrdersByCourierAsync(courier.Id));
        await Denied(() => service.ConfirmDeliveryAsync(courier.Id, order.Id, payment.Id));
        grants.Keys.Clear(); grants.Keys.UnionWith([PermissionCatalog.Orders, PermissionCatalog.OrdersAssignPickers]);
        Check((await service.GetOrdersInRangeForPickersAsync(DateTime.Today, DateTime.Today.AddDays(1))).Any(o => o.OrderId == order.Id) &&
            await service.UpdateAssignPickerAsync(order.Id, picker.Id) &&
            await db.Orders.AsNoTracking().Where(o => o.Id == order.Id).Select(o => o.StorekeeperId).SingleAsync() == picker.Id,
            "Picker assignment must operate independently of delivery permissions.");
        var changing = new OrderService(null!, null!, NullLogger<OrderService>.Instance, null!, new TestContextFactory(() =>
        { grants.Keys.Remove(PermissionCatalog.OrdersAssignPickers); return createContext(); }), grants, session: session);
        await Denied(() => changing.UpdateAssignPickerAsync(order.Id, null));
        Check(await db.Orders.AsNoTracking().Where(o => o.Id == order.Id).Select(o => o.StorekeeperId).SingleAsync() == picker.Id,
            "Revoking picker access during loading must prevent a write.");
        await db.CourierPayments.Where(p => p.CustomerPaymentId == payment.Id).ExecuteDeleteAsync();
        await db.Payments.Where(p => p.Id == payment.Id).ExecuteDeleteAsync();
        await db.Orders.Where(o => o.Id == order.Id).ExecuteDeleteAsync();
        await db.Customers.Where(c => c.Id == customer.Id).ExecuteDeleteAsync();
        await db.Couriers.Where(c => c.Id == courier.Id).ExecuteDeleteAsync();
        await db.Storekeepers.Where(p => p.Id == picker.Id).ExecuteDeleteAsync();
        await db.Users.Where(u => u.Id == user.Id).ExecuteDeleteAsync();
        Console.WriteLine("PASS: delivery view-only isolation, authorized settlement, independent picker assignment and live revocation prevent unauthorized writes");
    }
    private static async Task Denied(Func<Task> action)
    {
        try { await action(); } catch (UnauthorizedAccessException) { return; }
        throw new InvalidOperationException("Expected order-operation permission denial.");
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
