using Infrastructure.Constants;
using Infrastructure.Contexts;
using Infrastructure.Entities;
using Infrastructure.Repositories;
using Infrastructure.Security;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

internal sealed class BarterOrderTests(Func<DatabaseContext> createContext)
{
    public async Task RunAsync()
    {
        await using var db = createContext();
        var prefix = "BT-" + Guid.NewGuid().ToString("N")[..8];
        var customer = new CustomerEntity { Id = prefix, FullName = "Barter fixture", OfficialCustomer = true,
            Debt = 5000, Restriction = 1, BuyLimit = 1, UnpaidPaymentLimit = 1,
            SalesManager = new SalesManagerEntity { Id = prefix + "-M", FullName = "Barter manager" } };
        var user = new UserEntity { UserName = prefix, FirstName = "Barter", LastName = "Test", Password = "test" };
        var courier = new CourierEntity { Id = prefix, FullName = "Barter courier" };
        var product = new ProductEntity { ArticleNumber = prefix, ProductName = "Barter product", Model = "Test", Marka = "Test",
            Alternative = "", WarehousePlace = "A1/1", Quentity = 100, PriceLevel1 = 20,
            Brand = new BrandEntity { BrandName = prefix }, Group = new GroupEntity { GroupName = prefix } };
        var oldOrder = new OrderEntity { Id = prefix + "-UNPAID", Customer = customer, User = user, SuminWords = "Test", IsPaid = false,
            OrderDetails = [new OrderDetailEntity { Product = product, Price = 20m, Quentity = 2 }] };
        db.Products.Add(product);
        db.Couriers.Add(courier);
        db.Orders.Add(oldOrder);
        db.Payments.Add(new() { Customer = customer, Amount = 100, Date = DateTime.Today, IsDeliveryConfirmed = false });
        await db.SaveChangesAsync();
        var permissions = new TestPermissions();
        permissions.Keys.UnionWith([PermissionCatalog.Orders, PermissionCatalog.OrdersBarter, PermissionCatalog.OrdersViewAllManagers,
            PermissionCatalog.OrdersDelivery, PermissionCatalog.OrdersDeliveryView,
            PermissionCatalog.Customers, PermissionCatalog.CustomersBalanceView, PermissionCatalog.CustomersViewAll,
            CustomerPermissionCatalog.Coefficients.View, CustomerPermissionCatalog.Coefficients.Add, CustomerPermissionCatalog.Coefficients.Edit,
            CustomerPermissionCatalog.Coefficients.Delete,
            PermissionCatalog.OfficialMonthlyPlansManage, PermissionCatalog.OfficialMonthlyPlansView]);
        var factory = new TestContextFactory(createContext);
        var plans = new OfficialMonthlyPlanService(factory, permissions);
        await plans.SetPlanAsync(customer.Id, DateTime.Today, 10);
        var products = new ProductService(null!, null!, null!, NullLogger<ProductService>.Instance, null!, factory);
        var orders = new OrderService(new OrderRepository(factory), null!, NullLogger<OrderService>.Instance, products, factory, permissions, plans,
            new TestUserContext { UserId = user.Id, IsLoggedIn = true });
        var finance = new CustomerFinanceService(factory);
        var balanceBefore = (await finance.GetFinanceInfoAsync(customer.Id, "")).Balance;
        Check(balanceBefore == 4940m, "The fixture must include initial debt, an ordinary sale and an ordinary payment.");
        var order = new OrderEntity
        {
            CustomerId = customer.Id, UserId = user.Id, CourierId = courier.Id, Date = DateTime.Today.AddDays(-1), Rate = 11, SuminWords = "Test",
            IsBarter = true, OfficialOrder = true,
            OrderDetails = [new OrderDetailEntity { ArticleNumber = product.ArticleNumber, Price = 17.25m, Quentity = 5 }]
        };
        var saved = await orders.AddOrderAsync(order);
        Check(saved != null && saved.Id.StartsWith("БТ-") && saved.IsBarter && !saved.OfficialOrder,
            "Barter must save despite debts, exhausted credit/order/payment limits and a smaller official plan.");
        var stored = await db.OrderDetails.AsNoTracking().SingleAsync(d => d.OrderId == saved!.Id);
        Check(stored.Price == 17.25m && stored.Quentity == 5 && stored.Price * stored.Quentity == 86.25m,
            "Saving barter must retain the user's price instead of the catalog price.");
        Check(await db.Products.AsNoTracking().Where(p => p.ArticleNumber == product.ArticleNumber).Select(p => p.Quentity).SingleAsync() == 95,
            "Barter must retain normal inventory accounting.");
        var payment = await db.Payments.AsNoTracking().SingleAsync(p => p.OrderId == saved!.Id);
        Check(payment.Amount == 86.25m && payment.IsDeliveryConfirmed && payment.CourierId == null &&
            payment.Date == order.Date && payment.Rate == 11 &&
            System.Text.RegularExpressions.Regex.IsMatch(payment.ReceiptNumber ?? "", "^ПЛ-[A-Z0-9]{6}$") &&
            await db.Orders.AsNoTracking().Where(o => o.Id == saved!.Id).Select(o => o.IsPaid).SingleAsync(),
            "Saving barter must automatically create a normal numbered payment for the edited total and settle the order.");
        var expectedBalance = balanceBefore - payment.Amount;
        foreach (var info in new[]
        {
            await finance.GetFinanceInfoAsync(customer.Id),
            await finance.GetFinanceInfoAsync(customer.Id, ""),
            await finance.GetFinanceInfoAsync(customer.Id, null!)
        })
            Check(info.TotalSales == 40m && info.PreviousPayments + info.CurrentPayment == 186.25m &&
                info.Balance == expectedBalance && info.AvailableRestriction == 1m - expectedBalance,
                "Barter must reduce the balance only by its payment, without increasing sales or deducting twice.");
        var receiptFinance = await finance.GetFinanceInfoAsync(customer.Id, saved!.Id);
        Check(receiptFinance.CurrentSale == 86.25m && receiptFinance.CurrentPayment == 86.25m &&
            receiptFinance.OldDebt == balanceBefore && receiptFinance.Balance == expectedBalance,
            "The barter receipt must retain its merchandise total and show debt before and after its payment.");
        var ordinaryFinance = await finance.GetFinanceInfoAsync(customer.Id, oldOrder.Id);
        Check(ordinaryFinance.CurrentSale == 40m && ordinaryFinance.OldDebt == expectedBalance - 40m &&
            ordinaryFinance.Balance == expectedBalance,
            "An ordinary invoice must include prior barter payments in its old debt exactly once.");
        var customerService = new CustomerService(null!, factory, null!, null!, NullLogger<CustomerService>.Instance,
            new CustomerFieldPermissionPolicy(permissions), new CustomerAccessPolicy(permissions, new TestUserContext()));
        Check(await customerService.GetBalanceForEditorAsync(customer.Id) == expectedBalance,
            "The customer card must show the same payment-only barter balance as the summary.");
        var reconciliation = await finance.GetReconciliationAsync(customer.Id);
        Check(reconciliation.Sum(r => r.Sales) == 40m && reconciliation.Sum(r => r.Payments) == 186.25m &&
            5000m + reconciliation.Sum(r => r.Delta) == expectedBalance,
            "Reconciliation must show barter only as payment and agree with the customer's balance.");
        Check((await finance.GetClientsByManagerAsync(customer.SalesManagerId!)).Single().Balance == expectedBalance &&
            (await finance.GetClientsByManagerAndPeriodAsync(customer.SalesManagerId!, DateTime.Today.AddDays(-2), DateTime.Today.AddDays(1)))
                .Single().Balance == expectedBalance,
            "Manager debt lists must also exclude barter sales while counting their payments.");

        await using (var coefficientDb = createContext())
        {
            Check(!await coefficientDb.RaschetKoefficenta.AnyAsync(), "Coefficient settings must belong only to this fixture.");
            var coefficients = new CoefficientService(coefficientDb, factory,
                new CustomerFieldPermissionPolicy(permissions), new CustomerAccessPolicy(permissions, new TestUserContext()));
            var settings = await coefficients.GetOrCreateAsync();
            var calculated = await coefficients.CalculateForCustomerAsync(customer.Id);
            var expectedPurchase = (double)Math.Round(expectedBalance * settings.KoefZakupa / settings.KoefZakupaDni, 2);
            var expectedRepayment = (double)Math.Round(expectedBalance / settings.KoefEzhPogashDin1, 2);
            Check(calculated.DailyPurchaseCoefficient == expectedPurchase && calculated.DailyRepaymentCoefficient == expectedRepayment,
                "Card coefficient recalculation must use the balance reduced by barter payments.");
            await coefficients.CalculateEzhPogashForAllAsync();
            await coefficients.CalculateZakupForAllAsync();
            var bulk = await coefficientDb.Customers.AsNoTracking().SingleAsync(c => c.Id == customer.Id);
            Check(bulk.DailyPurchaseCoefficient == expectedPurchase && bulk.DailyRepaymentCoefficient == expectedRepayment,
                "Bulk coefficient recalculation must use the same payment-only barter balance.");
            await coefficientDb.RaschetKoefficenta.Where(c => c.Id == settings.Id).ExecuteDeleteAsync();
        }
        var history = (await orders.GetOrdersByCustomerAsync(customer.Id)).Single(o => o.Id == saved!.Id);
        var receipts = await orders.GetPaymentsByCustomerAsync(customer.Id);
        Check(history.IsBarter && history.TotalAmount == 86.25m && receipts.Single(p => p.OrderId == saved!.Id).ReceiptNumber == payment.ReceiptNumber,
            "Customer history must contain the barter invoice and its ordinary payment receipt.");
        var reprint = await orders.GetOrderAsync(saved!.Id);
        Check(reprint?.IsBarter == true && reprint.User?.FirstName == "Barter" &&
            reprint.OrderDetails.Single().Price == 17.25m && reprint.OrderDetails.Single().Product.Brand.BrandName == prefix,
            "Reprinting must load the persisted barter, seller, product and manual price.");
        Check(!(await orders.GetOrdersByCourierAsync(courier.Id, onlyUnpaid: false)).Any(o => o.OrderId == saved.Id || o.PaymentId == payment.Id) &&
            !(await orders.GetUnsentOrdersAsync()).Any(o => o.Invoice == saved.Id) &&
            !(await orders.GetUnpaidOrdersAsync(customer.Id)).Any(o => o.Id == saved.Id || o.PaymentId == payment.Id) &&
            await orders.GetUnpaidPaymentTotalAsync(customer.Id) == 100,
            "Barter and its automatic payment must not enter delivery, unsent orders or unpaid payment lists.");
        Check(await orders.AddOrderAsync(order) == null && await db.Payments.CountAsync(p => p.OrderId == saved.Id) == 1 &&
            await db.Products.AsNoTracking().Where(p => p.ArticleNumber == product.ArticleNumber).Select(p => p.Quentity).SingleAsync() == 95,
            "Resubmitting an already saved barter must not duplicate the receipt or deduct stock again.");
        var control = (await plans.GetControlAsync(DateTime.Today)).Single(r => r.CustomerId == customer.Id);
        Check(control.Purchased == 0 && control.Remaining == 10 &&
            await db.Customers.AsNoTracking().Where(c => c.Id == customer.Id).Select(c => c.OfficialCustomer).SingleAsync(),
            "Barter must neither consume the official plan nor change the customer's official status.");
        var ordinary = new OrderEntity { Id = prefix + "-NORMAL", CustomerId = customer.Id, UserId = user.Id,
            Date = DateTime.Today, OfficialOrder = true, SuminWords = "Test",
            OrderDetails = [new OrderDetailEntity { ArticleNumber = product.ArticleNumber, Price = 17.25m, Quentity = 5 }] };
        try { await orders.AddOrderAsync(ordinary); throw new Exception("An ordinary order bypassed the monthly limit."); }
        catch (OfficialMonthlyPlanLimitException) { }
        Check(!await db.Orders.AnyAsync(o => o.Id == ordinary.Id) &&
            await db.Products.AsNoTracking().Where(p => p.ArticleNumber == product.ArticleNumber).Select(p => p.Quentity).SingleAsync() == 95,
            "Rejecting an ordinary over-limit order must not change stock.");

        var noPlanService = new OrderService(new OrderRepository(factory), null!, NullLogger<OrderService>.Instance, products, factory, permissions);
        var another = new OrderEntity { CustomerId = customer.Id, UserId = user.Id, SuminWords = "Test", IsBarter = true, OfficialOrder = true,
            OrderDetails = [new OrderDetailEntity { ArticleNumber = product.ArticleNumber, Price = 9.5m, Quentity = 1 }] };
        Check((await noPlanService.AddOrderAsync(another))?.OfficialOrder == false,
            "Barter must not become official when the monthly-plan service is absent.");
        Check((await db.Payments.AsNoTracking().SingleAsync(p => p.OrderId == another.Id)).Amount == 9.5m,
            "Automatic settlement must also work without the monthly-plan service.");

        var rejected = new OrderEntity { Id = prefix + "-ROLLBACK", CustomerId = customer.Id, UserId = user.Id,
            SuminWords = "Test", IsBarter = true,
            OrderDetails = [new OrderDetailEntity { ArticleNumber = product.ArticleNumber, Price = 13.37m, Quentity = 1 }] };
        await db.Database.ExecuteSqlRawAsync($"ALTER TABLE [Payments] ADD CONSTRAINT [RejectBarterTestPayment] " +
            $"CHECK ([CustomerId] <> N'{customer.Id.Replace("'", "''")}' OR [Amount] <> 13.37)");
        try
        {
            Check(await orders.AddOrderAsync(rejected) == null && !await db.Orders.AnyAsync(o => o.Id == rejected.Id) &&
                !await db.Payments.AnyAsync(p => p.OrderId == rejected.Id) &&
                await db.Products.AsNoTracking().Where(p => p.ArticleNumber == product.ArticleNumber).Select(p => p.Quentity).SingleAsync() == 94,
                "A payment insert failure must roll back the invoice, its lines and the stock deduction.");
        }
        finally { await db.Database.ExecuteSqlRawAsync("ALTER TABLE [Payments] DROP CONSTRAINT [RejectBarterTestPayment]"); }
        Check(await orders.AddOrderAsync(rejected) != null && await db.Payments.CountAsync(p => p.OrderId == rejected.Id) == 1 &&
            await db.Products.AsNoTracking().Where(p => p.ArticleNumber == product.ArticleNumber).Select(p => p.Quentity).SingleAsync() == 93,
            "Retrying after a failed barter payment must save exactly one invoice, receipt and stock deduction.");
        var finalFinance = await finance.GetFinanceInfoAsync(customer.Id);
        Check(finalFinance.TotalSales == 40m && finalFinance.Balance == balanceBefore - 86.25m - 9.5m - 13.37m &&
            (await db.Customers.AsNoTracking().SingleAsync(c => c.Id == customer.Id)).Debt == 5000,
            "Retries and repeated barters must subtract each saved payment once without changing initial debt.");
        await db.Customers.Where(c => c.Id == customer.Id).ExecuteUpdateAsync(s => s.SetProperty(c => c.Debt, 0));
        Check((await finance.GetFinanceInfoAsync(customer.Id)).Balance == -169.12m,
            "A barter payment larger than outstanding debt must retain the resulting customer credit.");

        await db.Payments.Where(p => p.CustomerId == customer.Id).ExecuteDeleteAsync();
        await db.OrderDetails.Where(d => d.Order.CustomerId == customer.Id).ExecuteDeleteAsync();
        await db.Orders.Where(o => o.CustomerId == customer.Id).ExecuteDeleteAsync();
        await db.OfficialMonthlyPlans.Where(p => p.CustomerId == customer.Id).ExecuteDeleteAsync();
        await db.Customers.Where(c => c.Id == customer.Id).ExecuteDeleteAsync();
        await db.SalesManagers.Where(m => m.Id == customer.SalesManagerId).ExecuteDeleteAsync();
        await db.Products.Where(p => p.ArticleNumber == product.ArticleNumber).ExecuteDeleteAsync();
        await db.Brands.Where(b => b.Id == product.BrandId).ExecuteDeleteAsync();
        await db.Groups.Where(g => g.Id == product.GroupId).ExecuteDeleteAsync();
        await db.Users.Where(u => u.Id == user.Id).ExecuteDeleteAsync();
        await db.Couriers.Where(c => c.Id == courier.Id).ExecuteDeleteAsync();
        Console.WriteLine("PASS: barter payment alone reduces balances across summary, card, invoices, reconciliation, manager debt lists and coefficients; history/reprint, rollback, duplicate protection, credit, delivery exclusion and ordinary plan limits");
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
