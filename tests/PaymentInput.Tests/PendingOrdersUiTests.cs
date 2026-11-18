using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Infrastructure.Constants;
using Infrastructure.Dtos;
using Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using PresentationWpf.Services;
using PresentationWpf.ViewModels;
using PresentationWpf.Views;

internal static class PendingOrdersUiTests
{
    public static void Run()
    {
        var permissions = new PermissionService(); permissions.SetPermissions([PermissionCatalog.Orders]);
        var session = new UserSessionService(null!, null!, null!); SetUser(session, 101);
        var orders = new Orders(permissions, session);
        var vm = new PendingOrdersViewModel(orders, permissions, session);
        var view = new PendingOrdersView { DataContext = vm }; Layout(view);
        var grid = Descendants<DataGrid>(view).Single();
        Check(vm.PendingOrders.Single().CustomerId == "OWN" && grid.IsReadOnly && !grid.CanUserAddRows && !grid.CanUserDeleteRows,
            "Pending orders must load the own-manager result and forbid editing, adding and deleting rows.");
        var row = vm.PendingOrders.Single();
        grid.SelectedItem = row;
        foreach (var column in grid.Columns)
        {
            grid.CurrentCell = new DataGridCellInfo(row, column);
            Check(!grid.BeginEdit(), "A pending-order cell entered edit mode.");
        }
        Check(typeof(PendingOrdersViewModel).GetProperty("UpdateCommand") == null,
            "The read-only pending list must not expose a save/status-update command.");

        permissions.SetPermissions([PermissionCatalog.Orders, PermissionCatalog.OrdersViewAllManagers]); Layout(view);
        Check(vm.PendingOrders.Count == 2, "The all-manager grant must refresh an open pending list.");
        vm.ManagerId = "M-OWN";
        Check(vm.PendingOrders.Single().CustomerId == "OWN" && orders.LastManagerId == "M-OWN",
            "The manager selected on the order form must be passed to the pending-order query.");
        vm.ManagerId = "M-OTHER";
        Check(vm.PendingOrders.Single().CustomerId == "OTHER", "Selecting another manager must replace the pending-order rows.");
        vm.ManagerId = null;
        Check(vm.PendingOrders.Count == 2 && orders.LastManagerId == null, "Clearing the manager with all-manager access must restore all orders.");
        var oldManager = new TaskCompletionSource<IReadOnlyList<PendingOrderDto>>(); orders.Next = oldManager;
        vm.ManagerId = "M-OWN";
        Check(vm.PendingOrders.Count == 0, "Changing the manager must clear rows while the new query is pending.");
        vm.ManagerId = "M-OTHER";
        oldManager.SetResult([Orders.Rows[0]]); Layout(view);
        Check(vm.PendingOrders.Single().CustomerId == "OTHER", "A delayed response must not replace a newer manager's orders.");
        permissions.SetPermissions([PermissionCatalog.Orders]);
        Check(vm.PendingOrders.Single().CustomerId == "OWN" && orders.LastManagerId == null,
            "Revoking ViewAllManagers must restore the own-manager scope despite a previously selected foreign manager.");
        vm.ManagerId = null;
        permissions.SetPermissions([PermissionCatalog.Orders, PermissionCatalog.OrdersViewAllManagers]);
        var oldAll = new TaskCompletionSource<IReadOnlyList<PendingOrderDto>>(); orders.Next = oldAll;
        var loadingAll = vm.RefreshAsync();
        Check(vm.PendingOrders.Count == 0 && vm.SelectedOrder == null, "Refreshing the list must clear the old rows and selection.");
        permissions.SetPermissions([PermissionCatalog.Orders]);
        oldAll.SetResult(Orders.Rows); Layout(view);
        Check(loadingAll.IsCompletedSuccessfully && vm.PendingOrders.Single().CustomerId == "OWN",
            "A delayed all-manager response must not restore foreign orders after permission revocation.");

        var oldUser = new TaskCompletionSource<IReadOnlyList<PendingOrderDto>>(); orders.Next = oldUser;
        var loadingOldUser = vm.RefreshAsync();
        SetUser(session, 202);
        oldUser.SetResult([Orders.Rows[0]]); Layout(view);
        Check(loadingOldUser.IsCompletedSuccessfully && vm.PendingOrders.Single().CustomerId == "OTHER",
            "Switching login must refresh order scope and reject a delayed previous user's response.");
        permissions.SetPermissions([PermissionCatalog.Orders, PermissionCatalog.CustomersViewAll]);
        Check(vm.PendingOrders.Single().CustomerId == "OTHER", "Full-customer rights must not reveal every manager's orders.");

        var calls = orders.Calls;
        permissions.SetPermissions([PermissionCatalog.OrdersViewAllManagers]); Layout(view);
        Check(vm.PendingOrders.Count == 0 && orders.Calls == calls, "Removing Orders access must clear rows without querying the service.");
        permissions.SetPermissions([PermissionCatalog.Orders, PermissionCatalog.OrdersViewAllManagers]);
        Check(vm.PendingOrders.Count == 2, "Regranting Orders access must reload the list.");
        var afterGrant = orders.Calls;
        session.Logout(); Layout(view);
        Check(vm.PendingOrders.Count == 0 && vm.SelectedOrder == null && orders.Calls == afterGrant,
            "Logout must clear the pending list without starting a new query.");
        view.DataContext = null;
        Console.WriteLine("PASS: pending-order grid is read-only; selected manager filters rows, clearing restores all, revocation restores own scope and delayed manager/account responses are rejected");
    }

    private sealed class Orders(PermissionService permissions, UserSessionService session)
        : OrderService(null!, null!, NullLogger<OrderService>.Instance, null!, null!, permissions, session: session)
    {
        public static readonly PendingOrderDto[] Rows =
        [new() { InvoiceNumber = "СЧ-OWN123", CustomerId = "OWN", Date = DateTime.Today },
         new() { InvoiceNumber = "СЧ-OTH123", CustomerId = "OTHER", Date = DateTime.Today }];
        public TaskCompletionSource<IReadOnlyList<PendingOrderDto>>? Next;
        public int Calls;
        public string? LastManagerId;
        public override Task<IReadOnlyList<PendingOrderDto>> GetUnsentOrdersAsync(string? managerId = null)
        {
            Calls++;
            LastManagerId = managerId;
            var delay = Next; Next = null;
            IReadOnlyList<PendingOrderDto> result = permissions.Has(PermissionCatalog.OrdersViewAllManagers)
                ? managerId == null ? Rows : managerId == "M-OWN" ? [Rows[0]] : managerId == "M-OTHER" ? [Rows[1]] : []
                : [Rows[session.UserId == 101 ? 0 : 1]];
            return delay?.Task ?? Task.FromResult(result);
        }
    }
    private static void SetUser(UserSessionService session, int id)
    {
        typeof(UserSessionService).GetProperty(nameof(session.UserId))!.SetValue(session, id);
        typeof(UserSessionService).GetProperty(nameof(session.IsLoggedIn))!.SetValue(session, true);
        session.UpdateCurrentUser(new() { Id = id, UserName = id.ToString(), FirstName = "User", LastName = id.ToString() });
    }
    private static void Layout(FrameworkElement view)
    {
        view.Measure(new Size(600, 650)); view.Arrange(new Rect(0, 0, 600, 650)); view.UpdateLayout();
        view.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match) yield return match;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
