using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Infrastructure.Constants;
using Infrastructure.Contexts;
using Infrastructure.Dtos;
using Infrastructure.Entities;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using PresentationWpf.Dtos;
using PresentationWpf.Services;
using PresentationWpf.ViewModels;
using PresentationWpf.Views;

internal static class RetailManagerUiTests
{
    public static void Run()
    {
        var permissions = new PermissionService(); permissions.SetPermissions([PermissionCatalog.Orders]);
        var session = new UserSessionService(null!, null!, null!); SetUser(session, 101);
        var service = new Managers(permissions, session);
        var finance = new Finance();
        var customerService = new CustomerService(null!, new PendingDatabase(), null!, null!, null!, null!, null!);
        var vm = new SelectionViewModel(session, customerService, finance, permissions, service);
        vm.RefreshManagerContextAsync().GetAwaiter().GetResult();
        var view = new RetailView { DataContext = vm }; Layout(view);
        var combo = (ComboBox)view.FindName("SalesManagerComboBox");
        Check(vm.SelectedSalesManager?.Id == "M-A" && combo.SelectedItem == vm.SelectedSalesManager && !combo.IsEnabled,
            "The current UserId manager must be selected automatically and locked without the new permission.");
        Check(vm.SalesManagerList.Count == 1 && VisibleClients(vm).SequenceEqual(["A1", "A2"]) &&
            vm.InactiveDebtCustomers.All(c => c.CustomerId.StartsWith("A")) && vm.InactivePlannedCount == 2,
            "Customers and dependent panels did not follow the current manager.");
        vm.SelectedSalesManager = new() { Id = "M-B", FullName = "Other" };
        vm.SelectedSalesManager = null; Layout(view);
        Check(vm.SelectedSalesManager?.Id == "M-A" && combo.SelectedItem == vm.SelectedSalesManager, "Direct selection/clearing bypassed the manager lock.");
        vm.SelectedCustomer = (Customer)Managers.Customers[2];
        Check(vm.SelectedCustomer == null, "A stale foreign customer bypassed the fixed manager.");
        vm.ResetState(); Layout(view);
        Check(vm.SelectedSalesManager?.Id == "M-A", "Reopening/resetting Orders cleared the automatic manager.");

        permissions.SetPermissions([PermissionCatalog.Orders, PermissionCatalog.OrdersViewAllManagers]); Layout(view);
        Check(combo.IsEnabled && vm.SalesManagerList.Count == 2 && vm.SelectedSalesManager?.Id == "M-A", "The separate right did not unlock all managers while retaining the own default.");
        vm.SelectedCustomer = vm.AllCustomers.Single(c => c.Id == "A1");
        vm.SelectedProductList.Add(new ProductModel { ArticleNumber = "ITEM", Quentity = 10, OrderQuentity = 1 });
        vm.SelectedCourier = new() { Id = "C", FullName = "Courier" }; vm.SelectedStorekeeper = new() { Id = "S", FullName = "Picker" };
        vm.SelectedSalesManager = vm.SalesManagerList.Single(m => m.Id == "M-B"); Layout(view);
        Check(vm.SelectedSalesManager?.Id == "M-A" && combo.SelectedItem == vm.SelectedSalesManager &&
            vm.SelectedCustomer?.Id == "A1" && vm.SelectedProductList.Count == 1 &&
            vm.SelectedCourier != null && vm.SelectedStorekeeper != null && vm.Warnings == 1,
            "Changing manager must preserve the customer, cart and delivery while the cart has rows.");
        vm.SelectedTerritory = "South"; Layout(view);
        Check(vm.SelectedTerritory == null && vm.SelectedCustomer?.Id == "A1" && vm.SelectedProductList.Count == 1 && vm.Warnings == 2,
            "Changing route must not silently remove a customer with a nonempty cart.");
        vm.SelectedProductList.Clear();
        vm.SelectedSalesManager = vm.SalesManagerList.Single(m => m.Id == "M-B"); Layout(view);
        Check(VisibleClients(vm).SequenceEqual(["B1"]) && vm.SelectedCustomer == null && vm.SelectedProductList.Count == 0 &&
            vm.SelectedCourier == null && vm.SelectedStorekeeper == null && vm.TerritoryOptions.All(t => t.Value is null or "West") &&
            vm.InactiveDebtCustomers.Single().CustomerId == "B1", "Changing manager retained a foreign client/cart, delivery, route or inactive panel.");
        vm.SelectedSalesManager = vm.SalesManagerList.Single(m => m.Id == "M-A"); vm.SelectedTerritory = "North";
        Check(VisibleClients(vm).SequenceEqual(["A1"]) && vm.InactivePlannedCustomers.Single().CustomerId == "A1", "Route and inactive panels are inconsistent with the manager.");
        vm.SelectedSalesManager = vm.SalesManagerList.Single(m => m.Id == "M-B");
        vm.SelectedCustomer = vm.AllCustomers.Single(c => c.Id == "B1");
        vm.SelectedProductList.Add(new ProductModel { ArticleNumber = "REVOKED", OrderQuentity = 0 });
        permissions.SetPermissions([PermissionCatalog.Orders]); Layout(view);
        Check(!combo.IsEnabled && vm.SelectedSalesManager?.Id == "M-A" && vm.SelectedCustomer == null && vm.SalesManagerList.Count == 1 &&
            vm.SelectedProductList.Count == 0 && vm.Warnings == 2,
            "Revoking ViewAllManagers did not restore the own manager and clear inaccessible state.");

        permissions.SetPermissions([PermissionCatalog.Orders, PermissionCatalog.OrdersViewAllManagers]);
        var delayedDebt = new TaskCompletionSource<List<InactiveCustomerDto>>(); finance.DelayOther = delayedDebt;
        vm.SelectedSalesManager = vm.SalesManagerList.Single(m => m.Id == "M-B");
        var oldInactive = (Task)typeof(RetailViewModel).GetField("_inactiveRefreshTask", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(vm)!;
        vm.SelectedSalesManager = vm.SalesManagerList.Single(m => m.Id == "M-A");
        delayedDebt.SetResult([new("B1", "Old result", 0, 99)]); oldInactive.GetAwaiter().GetResult();
        Check(vm.InactiveDebtCustomers.All(c => c.CustomerId.StartsWith("A")), "A delayed result for the previous manager overwrote the current panel.");

        var delayedScope = new TaskCompletionSource<RetailManagerState>(); service.NextLoad = delayedScope;
        var oldScope = vm.RefreshManagerContextAsync();
        session.Logout(); Layout(view);
        Check(vm.SelectedSalesManager == null && vm.AllCustomers.Count == 0 && !combo.IsEnabled && vm.InactiveDebtCount == 0, "Logout retained another user's manager or data.");
        SetUser(session, 202); vm.RefreshManagerContextAsync().GetAwaiter().GetResult();
        delayedScope.SetResult(service.StateFor(101)); oldScope.GetAwaiter().GetResult(); Layout(view);
        Check(vm.SelectedSalesManager?.Id == "M-B" && vm.SelectedSalesManager.UserId == 202 && VisibleClients(vm).SequenceEqual(["B1"]),
            "An old login's load replaced the new user's manager or clients.");
        vm.ResetState();
        Check(vm.SelectedSalesManager?.Id == "M-B", "Reset failed to preserve the second user's default manager.");
        permissions.SetPermissions([]); Layout(view);
        Check(vm.SelectedSalesManager == null && vm.AllCustomers.Count == 0 && !combo.IsEnabled, "Revoking Orders did not clear protected manager state.");
        Console.WriteLine("PASS: retail UserId auto-selection, locked manager field, explicit all-manager override, linked clients/routes/panels, reset/revocation and delayed results across login changes");
    }
    private static string[] VisibleClients(RetailViewModel vm) => vm.CustomersView.Cast<Customer>().Select(c => c.Id).OrderBy(id => id).ToArray();
    private static void SetUser(UserSessionService session, int id)
    {
        typeof(UserSessionService).GetProperty(nameof(session.UserId))!.SetValue(session, id);
        typeof(UserSessionService).GetProperty(nameof(session.IsLoggedIn))!.SetValue(session, true);
        session.UpdateCurrentUser(new() { Id = id, FirstName = "Login", LastName = id.ToString(), UserName = id.ToString() });
    }
    private static void Layout(FrameworkElement view)
    {
        view.Measure(new Size(1450, 900)); view.Arrange(new Rect(0, 0, 1450, 900)); view.UpdateLayout();
        view.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private sealed class SelectionViewModel(UserSessionService session, CustomerService customers, CustomerFinanceService finance,
        PermissionService permissions, RetailManagerService managers) : RetailViewModel(null!, session, null!, customers,
            null!, null!, null!, null!, null!, null!, finance, permissions, null!, null!, managers)
    {
        public int Warnings { get; private set; }
        protected override void ShowCustomerChangeBlockedMessage() => Warnings++;
    }
    private sealed class Managers(PermissionService permissions, UserSessionService session) : RetailManagerService(null!, permissions, session)
    {
        public static CustomerEntity[] Customers = [new() { Id = "A1", FullName = "First", SalesManagerId = "M-A", Territory = "North" },
            new() { Id = "A2", FullName = "Second", SalesManagerId = "M-A", Territory = "South" },
            new() { Id = "B1", FullName = "Third", SalesManagerId = "M-B", Territory = "West" }];
        public TaskCompletionSource<RetailManagerState>? NextLoad;
        public RetailManagerState StateFor(int userId)
        {
            var all = permissions.Has(PermissionCatalog.OrdersViewAllManagers);
            var own = userId == 101 ? "M-A" : "M-B";
            SalesManager[] managers = [new() { Id = "M-A", UserId = 101, FullName = "Different name" }, new() { Id = "M-B", UserId = 202, FullName = "Other" }];
            return new(userId, own, all, false, managers.Where(m => all || m.Id == own).ToList(), Customers.Where(c => all || c.SalesManagerId == own).ToList());
        }
        public override Task<RetailManagerState> LoadAsync()
        {
            var delay = NextLoad; NextLoad = null;
            return delay?.Task ?? Task.FromResult(StateFor(session.UserId));
        }
    }
    private sealed class Finance() : CustomerFinanceService(new PendingDatabase())
    {
        public TaskCompletionSource<List<InactiveCustomerDto>>? DelayOther;
        private static List<InactiveCustomerDto> Rows() => Managers.Customers.Select(c => new InactiveCustomerDto(c.Id, c.FullName, 0, 10)).ToList();
        public override Task<List<InactiveCustomerDto>> NeaktivOtEzhigodPogashenieAsync(string? managerId = null, string? territory = null, string? customerId = null)
        {
            if (managerId == "M-B" && DelayOther is { } delay) { DelayOther = null; return delay.Task; }
            return Task.FromResult(Rows());
        }
        public override Task<List<InactiveCustomerDto>> NeaktivNeZakupAsync(string? managerId = null, string? territory = null, string? customerId = null) => Task.FromResult(Rows());
    }
    private sealed class PendingDatabase : IDbContextFactory<DatabaseContext>
    {
        public DatabaseContext CreateDbContext() => throw new Exception("No real database is used in this UI test.");
        public Task<DatabaseContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => new TaskCompletionSource<DatabaseContext>().Task;
    }
}
