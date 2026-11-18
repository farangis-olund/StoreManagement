using Infrastructure.Contexts;
using Infrastructure.Constants;
using Infrastructure.Entities;
using Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.EntityFrameworkCore;

internal sealed class UnpaidDeliveryTests(Func<DatabaseContext> createContext)
{
    public static void CheckDisplay()
    {
        var row = new Infrastructure.Dtos.UnpaidOrderDto { Id = "INVOICE-TEST", Paid = 0 };
        Check(row.DocumentNumber == "INVOICE-TEST" && row.PaymentId == null && row.Paid == 0,
            "Orders without payments must display their invoice and must not invent a receipt or payment.");
        row.PaymentId = 42;
        row.Paid = 20.5m;
        Check(row.DocumentNumber == "42" && row.Paid == 20.5m, "Existing receipts must retain their number and amount.");
        row.ReceiptNumber = "ПЛ-3H3KLC";
        Check(row.DocumentNumber == "ПЛ-3H3KLC" && row.PaymentId == 42,
            "The public receipt number must be displayed without replacing the internal payment identity.");
        Console.WriteLine("PASS: unpaid order display falls back to invoice only when no receipt exists");
    }

    public async Task RunAsync()
    {
        await using var db = createContext();
        var customer = new CustomerEntity { Id = "UNPAID-TEST", FullName = "Unpaid delivery test" };
        var other = new CustomerEntity { Id = "UNPAID-OTHER", FullName = "Other customer" };
        var user = new UserEntity { UserName = "unpaid-test", FirstName = "Test", LastName = "User", Password = "test" };
        var courier = new CourierEntity { Id = "UNPAID-COURIER", FullName = "Courier" };
        OrderEntity Order(string id, bool delivery = true, bool paid = false, bool barter = false) => new()
        {
            Id = id, Customer = customer, User = user, Courier = delivery ? courier : null,
            IsPaid = paid, IsBarter = barter, SuminWords = "Test", Date = DateTime.Today
        };
        var noPayment = Order("UNPAID-NO-PAYMENT");
        var zeroPayment = Order("UNPAID-ZERO");
        var withPayment = Order("UNPAID-WITH-PAYMENT");
        var product = new ProductEntity
        {
            ArticleNumber = "DELIVERY-PRODUCT", ProductName = "Delivery test", Model = "Test", Marka = "Test",
            Alternative = "", WarehousePlace = "Test", Quentity = 10,
            Brand = new BrandEntity { BrandName = "Delivery test" },
            Group = new GroupEntity { GroupName = "Delivery test" }
        };
        db.OrderDetails.Add(new OrderDetailEntity { Order = withPayment, Product = product, Price = 7, Quentity = 2 });
        var withoutCourier = Order("UNPAID-NO-COURIER", delivery: false);
        var otherOrder = Order("UNPAID-OTHER-ORDER");
        otherOrder.Customer = other;
        db.Orders.AddRange(noPayment, zeroPayment, withPayment, withoutCourier, otherOrder,
            Order("UNPAID-PAID", paid: true), Order("UNPAID-BARTER", barter: true),
            Order("UNPAID-NOT-DELIVERY", delivery: false));
        db.Payments.AddRange(
            new CustomerPaymentEntity { Order = zeroPayment, Courier = courier, Customer = customer, Amount = 0, Date = DateTime.Today },
            new CustomerPaymentEntity { Order = withPayment, Courier = courier, Customer = customer, Amount = 15, Date = DateTime.Today, Rate = 11 },
            new CustomerPaymentEntity { Order = withPayment, Courier = courier, Customer = customer, Amount = 25, Date = DateTime.Today.AddDays(1), Rate = 11 },
            new CustomerPaymentEntity { Order = withoutCourier, Customer = customer, Amount = 5, Date = DateTime.Today });
        await db.SaveChangesAsync();
        var permissions = new TestPermissions();
        permissions.Keys.UnionWith([PermissionCatalog.Orders, PermissionCatalog.OrdersDelivery,
            PermissionCatalog.OrdersDeliveryView, PermissionCatalog.OrdersDeliveryUpdate]);
        var service = new OrderService(null!, null!, NullLogger<OrderService>.Instance, null!,
            new TestContextFactory(createContext), permissions, session: new TestUserContext { UserId = user.Id, IsLoggedIn = true });

        var rows = await service.GetUnpaidOrdersAsync(customer.Id);
        Check(rows.Select(r => r.Id).ToHashSet().SetEquals(new[] { noPayment.Id, zeroPayment.Id, withPayment.Id, withoutCourier.Id }),
            "Include unpaid deliveries without payments and existing unpaid payment rows, but exclude paid/barter/other customers.");
        var missingReceipt = rows.Single(r => r.Id == noPayment.Id);
        Check(missingReceipt.Paid == 0 && missingReceipt.PaymentId == null && missingReceipt.DocumentNumber == noPayment.Id,
            "An order without a payment needs zero payment and an invoice number, not receipt zero.");
        var multiple = rows.Where(r => r.Id == withPayment.Id).OrderBy(r => r.PaymentId).ToList();
        Check(multiple.Count == 2 && multiple.Sum(r => r.Paid) == 40 && multiple.All(r => r.PaymentId != null),
            "Every payment needs its own row and receipt number.");
        Check(await service.GetUnpaidPaymentTotalAsync(customer.Id) == 45, "Unpaid payment limit must still sum payments, not sales.");
        var deliveryRows = (await service.GetOrdersByCourierAsync(courier.Id)).Where(r => r.OrderId == withPayment.Id).ToList();
        Check(deliveryRows.Count == 2 && deliveryRows.Sum(r => r.SaleAmount) == 14 && deliveryRows.Sum(r => r.PaymentAmount) == 40,
            "Multiple receipts must not duplicate the invoice's sales amount.");
        Check(!(await service.GetOrdersByCourierAsync("OTHER-COURIER")).Any(), "Courier filter leaked another courier's receipts.");

        Check(await service.ConfirmDeliveryAsync(courier.Id, noPayment.Id, null), "Delivery paid status did not save.");
        Check(!(await service.GetUnpaidOrdersAsync(customer.Id)).Any(r => r.Id == noPayment.Id), "Paid delivery remains in unpaid list.");
        Check(!await service.ConfirmDeliveryAsync("WRONG-COURIER", withPayment.Id, multiple[0].PaymentId), "Wrong courier confirmed a receipt.");
        Check(await service.ConfirmDeliveryAsync(courier.Id, withPayment.Id, multiple[0].PaymentId), "First receipt was not confirmed.");
        Check((await service.GetUnpaidOrdersAsync(customer.Id)).Count(r => r.Id == withPayment.Id) == 1,
            "Confirming one receipt must leave the other pending.");
        Check((await service.GetOrdersByCourierAsync(courier.Id)).Any(r => r.PaymentId == multiple[1].PaymentId),
            "Second receipt disappeared from delivery.");
        Check(await service.GetUnpaidPaymentTotalAsync(customer.Id) == 30, "Only confirmed receipt should leave unpaid total.");
        Check(await service.ConfirmDeliveryAsync(courier.Id, withPayment.Id, multiple[0].PaymentId), "Repeat confirmation should be idempotent.");
        Check(await service.ConfirmDeliveryAsync(courier.Id, withPayment.Id, multiple[1].PaymentId), "Second receipt was not confirmed.");
        Check(!(await service.GetUnpaidOrdersAsync(customer.Id)).Any(r => r.Id == withPayment.Id), "Paid order with payment remains listed.");
        Check(await service.GetUnpaidPaymentTotalAsync(customer.Id) == 5, "Paid delivery's payment remains in unpaid total.");
        Check(await db.CourierPayments.CountAsync(p => p.OrderId == withPayment.Id) == 2, "Repeated confirmation duplicated a settlement.");
        Check(await db.CourierPayments.Where(p => p.OrderId == withPayment.Id).SumAsync(p => p.AmountInTJS) == 440,
            "Settlement must use each receipt's saved rate and amount.");

        var count = await db.Payments.CountAsync();
        Check(await service.AddPaymentAsync(customer.Id, 20.5m, 11, orderId: withPayment.Id) == null,
            "Non-barter payment must require an explicitly selected courier.");
        Check(await db.Payments.CountAsync() == count, "Invalid payment persisted.");
        var late = await service.AddPaymentAsync(customer.Id, 20.5m, 11, orderId: withPayment.Id, courierId: courier.Id);
        Check(late != null && late.OrderId == null && late.CourierId == courier.Id && !late.IsDeliveryConfirmed,
            "Payment after settled order must be standalone and retain the selected courier.");
        Check((await service.GetOrdersByCourierAsync(courier.Id)).Any(r => r.PaymentId == late!.Id && r.OrderId == "" && r.PaymentAmount == 20.5m),
            "Standalone receipt missing from delivery.");
        Check((await service.GetUnpaidOrdersAsync(customer.Id)).Any(r => r.PaymentId == late!.Id), "Standalone receipt missing from customer summary.");
        Check(await service.GetUnpaidPaymentTotalAsync(customer.Id) == 25.5m, "Standalone receipt missing from unpaid limit.");

        await db.Database.ExecuteSqlRawAsync("ALTER TABLE CourierPayments ADD CONSTRAINT RejectTestSettlement CHECK (AmountInEuro <> 20.5)");
        try
        {
            Check(!await service.ConfirmDeliveryAsync(courier.Id, null, late!.Id), "Forced settlement failure should be reported.");
            Check(!await db.Payments.Where(p => p.Id == late.Id).Select(p => p.IsDeliveryConfirmed).SingleAsync(),
                "Failed settlement incorrectly confirmed the receipt.");
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE CourierPayments DROP CONSTRAINT RejectTestSettlement");
        }
        Check(await service.ConfirmDeliveryAsync(courier.Id, null, late!.Id), "Standalone confirmation failed.");
        Check(!(await service.GetOrdersByCourierAsync(courier.Id)).Any(r => r.PaymentId == late.Id), "Confirmed receipt remains in delivery.");
        Check(!(await service.GetUnpaidOrdersAsync(customer.Id)).Any(r => r.PaymentId == late.Id), "Confirmed receipt remains in summary.");
        Check(await db.CourierPayments.AnyAsync(p => p.CustomerPaymentId == late.Id && p.OrderId == null), "Standalone settlement lost receipt identity.");
        var withoutOrder = await service.AddPaymentAsync(customer.Id, 12m, 10, courierId: courier.Id);
        Check(withoutOrder != null && withoutOrder.OrderId == null, "Customers must be able to pay without an order.");
        var barterPayment = await service.AddPaymentAsync(customer.Id, 8m, 11, orderId: "UNPAID-BARTER");
        Check(barterPayment != null && barterPayment.IsDeliveryConfirmed && barterPayment.CourierId == null,
            "Automatic barter settlement must not require courier selection or enter the collection queue.");
        Check(!(await service.GetUnpaidOrdersAsync(customer.Id)).Any(r => r.PaymentId == barterPayment!.Id),
            "Barter receipt incorrectly appeared in unpaid payments.");
        var raced = await service.AddPaymentAsync(customer.Id, 3m, 11, orderId: zeroPayment.Id, courierId: courier.Id);
        Check(raced != null && !await service.ConfirmDeliveryAsync(courier.Id, zeroPayment.Id, null),
            "Stale order-only checkbox must not confirm newly added receipts.");
        Check(await service.UpdatePaymentStatusAsync(noPayment.Id, false), "Could not restore unpaid state.");
        Check((await service.GetUnpaidOrdersAsync(customer.Id)).Any(r => r.Id == noPayment.Id), "Restored unpaid order must reappear.");
        Console.WriteLine("PASS: independent delivery receipts, standalone payments, courier validation, paid-order detachment, settlement rollback and idempotency");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
