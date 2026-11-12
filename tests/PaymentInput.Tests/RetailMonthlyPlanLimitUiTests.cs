using System.Reflection;
using Infrastructure.Constants;
using Infrastructure.Contexts;
using Infrastructure.Dtos;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using PresentationWpf.Dtos;
using PresentationWpf.Services;
using PresentationWpf.ViewModels;

internal static class RetailMonthlyPlanLimitUiTests
{
    public static void Run()
    {
        var running = new List<PlanRetail>();
        PlanRetail Ready(decimal remaining = 1000, decimal threshold = 5, decimal finance = 10000, bool bypass = false, bool showIndividual = true,
            CustomerFinanceService? financeService = null)
        {
            var permissions = new PermissionService();
            var rights = new List<string> { PermissionCatalog.Orders };
            if (showIndividual) rights.Add(PermissionCatalog.OfficialMonthlyPlansIndividual);
            if (bypass) rights.Add(PermissionCatalog.OrdersExceedBuyLimit);
            permissions.SetPermissions(rights);
            var service = new Plans(permissions) { Remaining = remaining, Threshold = threshold };
            var vm = new PlanRetail(permissions, service, financeService);
            var customer = new Customer { Id = "PLAN", FullName = "Plan client", OfficialCustomer = true };
            vm.AllCustomers.Add(customer);
            typeof(RetailViewModel).GetField("_selectedCustomer", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(vm, customer);
            typeof(RetailViewModel).GetField("_customerBalance", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(vm, 0m);
            vm.AvailableRestriction = finance;
            vm.StartMonthlyPlanRefresh(); running.Add(vm);
            return vm;
        }
        try
        {
            var vm = Ready();
            var first = Add(vm, "FIRST", 300);
            first.OrderQuentity = 4;
            Check(first.OrderQuentity == 3 && vm.TotalSum == 900 && vm.PlanWarnings == 1, "An official cart must be capped at the remaining plan.");
            var second = Add(vm, "SECOND", 100); second.OrderQuentity = 1;
            Check(vm.TotalSum == 1000 && vm.PlanWarnings == 1, "An exact plan boundary must be allowed.");
            var blocked = Add(vm, "BLOCKED", 1);
            Check(!vm.SelectedProductList.Contains(blocked) && vm.PlanWarnings == 2, "A full official cart must reject additional products.");
            first.OrderQuentity = 4;
            Check(first.OrderQuentity == 3 && second.OrderQuentity == 1 && vm.TotalSum == 1000,
                "Correcting the edited quantity must preserve other cart rows.");
            vm.RemoveProductCommand.Execute(second);
            first.OrderQuentity = 2;
            var released = Add(vm, "RELEASED", 100); released.OrderQuentity = 4;
            Check(vm.TotalSum == 1000, "Removing and reducing quantities must release plan allowance.");
            first.BarterPriceSom = 600;
            Check(vm.TotalSum <= 1000 && first.OrderQuentity == 1, "Price changes must also revalidate the official cart.");

            var fractional = Ready();
            var fraction = Add(fractional, "FRACTION", 333.34m); fraction.OrderQuentity = 3;
            Check(fraction.OrderQuentity == 2 && fractional.TotalSum == 666.68m, "Currency fractions must not allow exceeding the plan.");
            var expensive = Ready(); var unit = Add(expensive, "TOO-EXPENSIVE", 1000.01m); unit.OrderQuentity = 1;
            Check(unit.OrderQuentity == 0 && expensive.PlanWarnings == 1, "An unaffordable single unit must be corrected to zero.");

            foreach (var remaining in new[] { 0m, 3m, 5m })
            {
                var ordinary = Ready(remaining);
                var product = Add(ordinary, "ORDINARY", 100); product.OrderQuentity = 15;
                Check(!ordinary.OfficialOrder && ordinary.CustomerMonthlyPlanRemaining == 0 && product.OrderQuentity == 15 && ordinary.PlanWarnings == 0,
                    "At or below threshold 5, including remaining 3 and exactly 5, the sale must be ordinary and ignore the plan cap.");
            }
            var above = Ready(5.01m); var small = Add(above, "ABOVE", 1); small.OrderQuentity = 6;
            Check(above.OfficialOrder && small.OrderQuentity == 5 && above.PlanWarnings == 1, "A remainder just above the threshold must still cap the official sale.");

            var financial = Ready(finance: 500); var limited = Add(financial, "FINANCE", 300); limited.OrderQuentity = 4;
            Check(limited.OrderQuentity == 1 && financial.BuyWarnings == 1 && financial.PlanWarnings == 0,
                "A stricter existing financial restriction must be checked first.");
            var both = Ready(finance: 1200); var bothProduct = Add(both, "BOTH", 300); bothProduct.OrderQuentity = 5;
            Check(bothProduct.OrderQuentity == 3 && both.WarningOrder.SequenceEqual(new[] { "finance", "plan" }),
                "When both limits are exceeded, financial validation must precede plan validation.");
            var privileged = Ready(finance: 500, bypass: true); var privilegedProduct = Add(privileged, "PRIVILEGED", 300); privilegedProduct.OrderQuentity = 4;
            Check(privilegedProduct.OrderQuentity == 3 && privileged.BuyWarnings == 0 && privileged.PlanWarnings == 1,
                "Sale-over-credit permission must not bypass the monthly official plan.");
            var hidden = Ready(showIndividual: false); var hiddenProduct = Add(hidden, "HIDDEN", 300); hiddenProduct.OrderQuentity = 4;
            Check(!hidden.ShowCustomerMonthlyPlan && hiddenProduct.OrderQuentity == 3,
                "Plan enforcement must work independently from permission to display the individual remainder.");

            vm.Plans.Threshold = 1000; vm.Plans.NotifyChanged();
            first.OrderQuentity = 3;
            Check(!vm.OfficialOrder && vm.TotalSum > 1000, "Increasing the shared threshold must switch the open cart to an ordinary order.");
            vm.Plans.Threshold = 5; vm.Plans.NotifyChanged();
            Check(vm.OfficialOrder && vm.TotalSum <= 1000, "Restoring an active plan must correct an existing oversized cart.");
            var pending = new PendingFinanceDatabase();
            var checkout = Ready(financeService: new CustomerFinanceService(pending));
            var sold = Add(checkout, "SOLD", 102.96m); sold.OrderQuentity = 1;
            var checkoutCustomer = checkout.SelectedCustomer;
            var courier = new Courier { Id = "COURIER", FullName = "Courier" };
            var keeper = new Storekeeper { Id = "KEEPER", FullName = "Keeper" };
            checkout.CourierList.Add(courier); checkout.StorekeeperList.Add(keeper);
            checkout.SelectedCourier = courier; checkout.SelectedStorekeeper = keeper;
            var saving = checkout.SaveOrderCommand.ExecuteAsync(null);
            try
            {
                Check(checkout.SaveOrderCommand.IsRunning && !saving.IsCompleted, "The test must hold an actual checkout command in progress.");
                // Supply the loaded finance state, then reproduce the plan notification sent after committing the sale.
                typeof(RetailViewModel).GetField("_customerBalance", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(checkout, 0m);
                checkout.AvailableRestriction = 10000;
                checkout.Plans.Remaining = 41.76m;
                checkout.Plans.NotifyChanged();
                Check(checkout.CustomerMonthlyPlanRemaining == 41.76m && sold.OrderQuentity == 1 && checkout.PlanWarnings == 0,
                    "Post-commit plan refresh must update indicators without reducing the sold cart or repeating the warning.");
                checkout.SelectedProductList.Clear();
                checkout.Plans.NotifyChanged();
                Check(checkout.PlanWarnings == 0, "Clearing the sold cart must not show a plan-limit warning.");
            }
            finally
            {
                // End the held request as stale, without contacting a database or showing an error dialog.
                typeof(RetailViewModel).GetField("_selectedCustomer", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(checkout, null);
                pending.Completion.SetException(new InvalidOperationException("End isolated checkout test."));
                saving.GetAwaiter().GetResult();
            }
            typeof(RetailViewModel).GetField("_selectedCustomer", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(checkout, checkoutCustomer);
            var next = Add(checkout, "NEXT", 102.96m); next.OrderQuentity = 1;
            Check(next.OrderQuentity == 0 && checkout.PlanWarnings == 1,
                "After checkout, the next cart must still enforce the remaining plan and show the normal warning.");
            Console.WriteLine("PASS: monthly cart cap, exact/fractional boundaries, edited rows, prices, existing-limit precedence, display permissions, overrides and threshold 5 with remainder 3/5");
            Console.WriteLine("PASS: checkout plan refresh updates indicators without rechecking the sold cart or repeating warnings; the next cart remains protected");
        }
        finally { foreach (var vm in running) vm.StopMonthlyPlanRefresh(); }
    }

    private static ProductModel Add(PlanRetail vm, string article, decimal price)
    {
        var product = (ProductModel)new Product { ArticleNumber = article, PriceLevel1 = price, Quentity = 100 };
        product.CustomerPriceLevel = 1;
        vm.AddToChartCommand.Execute(product);
        return product;
    }
    private sealed class Plans(PermissionService permissions) : OfficialMonthlyPlanService(null!, permissions)
    {
        public decimal Remaining;
        public decimal Threshold;
        public override Task<OfficialMonthlyPlanRetailState> GetRetailStateAsync(string? id, DateTime date) =>
            Task.FromResult(new OfficialMonthlyPlanRetailState(MonthStart(date), id, id != null && Remaining > Threshold,
                id != null && permissions.Has(PermissionCatalog.OfficialMonthlyPlansIndividual) ? Remaining > Threshold ? Remaining : 0 : null, null,
                id != null && Remaining > Threshold ? Remaining : null));
    }
    private sealed class PendingFinanceDatabase : IDbContextFactory<DatabaseContext>
    {
        public TaskCompletionSource<DatabaseContext> Completion { get; } = new();
        public DatabaseContext CreateDbContext() => throw new InvalidOperationException("No database in this UI test.");
        public Task<DatabaseContext> CreateDbContextAsync(CancellationToken ct = default) => Completion.Task;
    }
    private sealed class PlanRetail(PermissionService permissions, Plans plans, CustomerFinanceService? financeService = null) : RetailViewModel(
        null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, financeService!, permissions, null!, plans)
    {
        public Plans Plans { get; } = plans;
        public int PlanWarnings;
        public int BuyWarnings;
        public List<string> WarningOrder { get; } = [];
        protected override void ShowMonthlyPlanLimitReachedMessage() { PlanWarnings++; WarningOrder.Add("plan"); }
        protected override void ShowBuyLimitReachedMessage() { BuyWarnings++; WarningOrder.Add("finance"); }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
}
