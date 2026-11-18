using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Infrastructure.Constants;
using Infrastructure.Dtos;
using Infrastructure.Entities;
using Infrastructure.Repositories;
using Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using PresentationWpf.Services;
using PresentationWpf.ViewModels;
using PresentationWpf.Views;

internal static class OrderOperationsUiTests
{
    public static void Run()
    {
        var permissions = new PermissionService();
        var session = new UserSessionService(null!, null!, null!); SetUser(session, 101);
        var orders = new Orders(); var couriers = new Couriers(); var keepers = new Keepers();
        var retail = new RetailViewModel(null!, session, orders, null!, null!, null!, null!, null!, null!, null!, null!, permissions, null!);
        var retailView = new RetailView { DataContext = retail }; Layout(retailView);
        var deliveryEntry = Buttons(retailView).Single(b => b.Command == retail.ShowDeliveryOrdersCommand);
        var pickerEntry = Buttons(retailView).Single(b => b.Command == retail.ShowAssignPickersCommand);
        Check(deliveryEntry.Visibility == Visibility.Collapsed && pickerEntry.Visibility == Visibility.Collapsed,
            "Delivery and picker entry buttons must be hidden without module grants.");
        retail.ShowDeliveryOrdersCommand.Execute(null); retail.ShowAssignPickersCommand.Execute(null);
        var delivery = new DeliveryOrdersViewModel(orders, couriers, session, permissions);
        var picker = new AssignPickersViewModel(orders, keepers, permissions, session);
        var deliveryView = new DeliveryOrdersView { DataContext = delivery }; Layout(deliveryView);
        var pickerView = new AssignPickersView { DataContext = picker }; Layout(pickerView);
        Check(couriers.Calls == 0 && keepers.Calls == 0 && orders.Loads == 0 && orders.PickerLoads == 0,
            "Opening either view directly without permission must not load data.");
        permissions.SetPermissions([PermissionCatalog.Orders, PermissionCatalog.OrdersDelivery]); Layout(retailView);
        Check(deliveryEntry.Visibility == Visibility.Visible && pickerEntry.Visibility == Visibility.Collapsed &&
            !delivery.CanView && couriers.Calls == 0, "Delivery access must be separate from its View grant and from picker assignment.");
        string[] viewGrants = [PermissionCatalog.Orders, PermissionCatalog.OrdersDelivery, PermissionCatalog.OrdersDeliveryView];
        string[] actions = [PermissionCatalog.OrdersDeliveryUpdate, PermissionCatalog.OrdersDeliveryPrint, PermissionCatalog.OrdersDeliveryReport];
        var update = Buttons(deliveryView).Single(b => Equals(b.Content, "Обновить"));
        var print = Buttons(deliveryView).Single(b => Equals(b.Content, "Печать"));
        var report = Buttons(deliveryView).Single(b => Equals(b.Content, "Отчёт"));
        var detail = Descendants<DataGrid>(deliveryView).Single(g => g.GetBindingExpression(ItemsControl.ItemsSourceProperty)?.ParentBinding.Path.Path == "OrdersForSelectedDate");
        for (var mask = 0; mask < 8; mask++)
        {
            permissions.SetPermissions(viewGrants.Concat(actions.Where((_, i) => (mask & (1 << i)) != 0)));
            delivery.SelectedCourier = delivery.Couriers.Single(); Layout(deliveryView);
            var canUpdate = (mask & 1) != 0; var canPrint = (mask & 2) != 0; var canReport = (mask & 4) != 0;
            Check(delivery.CanView && delivery.OrdersForSelectedDate.Count == 1 &&
                update.Visibility == (canUpdate ? Visibility.Visible : Visibility.Collapsed) &&
                print.Visibility == (canPrint ? Visibility.Visible : Visibility.Collapsed) &&
                report.Visibility == (canReport ? Visibility.Visible : Visibility.Collapsed) &&
                delivery.UpdateCommand.CanExecute(null) == canUpdate && delivery.PrintCommand.CanExecute(null) == canPrint &&
                delivery.ReportCommand.CanExecute(null) == canReport && detail.IsReadOnly == !canUpdate,
                "Delivery buttons, commands and read-only mode must follow all eight independent action combinations.");
            var paid = Descendants<CheckBox>(detail).Single();
            Check(paid.IsEnabled == canUpdate, "The Paid checkbox must require Update permission.");
            if (!canUpdate)
            {
                delivery.OrdersForSelectedDate.Single().IsPaid = true;
                Check(!delivery.OrdersForSelectedDate.Single().IsPaid && !delivery.HasUnsavedChanges,
                    "A view-only row must reject changes to Paid even if its model is accessed directly.");
                delivery.UpdateCommand.ExecuteAsync(null).GetAwaiter().GetResult();
            }
            else
            {
                paid.IsChecked = true; Layout(deliveryView);
                Check(delivery.HasUnsavedChanges && delivery.TotalSum == 110,
                    "Authorized payment selection must preserve totals and the unsaved-change notification.");
            }
            if (!canPrint) delivery.PrintCommand.Execute(null);
            if (!canReport) delivery.ReportCommand.Execute(null);
        }
        Check(orders.Updates == 0, "Denied direct update command calls must not reach the service.");
        permissions.SetPermissions(viewGrants);
        var oldDelivery = new TaskCompletionSource<IReadOnlyList<OrderWithPaymentsDto>>(); orders.NextDelivery = oldDelivery;
        delivery.SelectedCourier = delivery.Couriers.Single();
        permissions.SetPermissions([PermissionCatalog.Orders, PermissionCatalog.OrdersDelivery]);
        oldDelivery.SetResult(Orders.DeliveryRows); Layout(deliveryView);
        Check(delivery.DeliverySessions.Count == 0 && delivery.OrdersForSelectedDate.Count == 0 && delivery.Couriers.Count == 0 &&
            delivery.TotalSale == 0 && delivery.TotalSum == 0 && !delivery.HasUnsavedChanges,
            "Revoking View must clear all cached data and reject delayed delivery results.");

        permissions.SetPermissions([PermissionCatalog.Orders, PermissionCatalog.OrdersAssignPickers]); Layout(retailView); Layout(pickerView);
        Check(pickerEntry.Visibility == Visibility.Visible && deliveryEntry.Visibility == Visibility.Collapsed &&
            picker.CanAccess && picker.FilteredOrders.Count == 1 && picker.StorekeeperList.Count == 1 &&
            !Descendants<DataGrid>(pickerView).Single().IsReadOnly, "Picker assignment must load and edit independently of delivery access.");
        var oldPicker = new TaskCompletionSource<IReadOnlyList<AssignPickerDto>>(); orders.NextPicker = oldPicker;
        var loadingPicker = picker.InitializeAsync();
        permissions.SetPermissions([PermissionCatalog.Orders]);
        oldPicker.SetResult([new() { OrderId = "STALE", CustomerName = "Old client" }]); Layout(pickerView);
        picker.UpdateCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        Check(loadingPicker.IsCompletedSuccessfully && picker.FilteredOrders.Count == 0 && picker.Orders.Count == 0 && picker.OrderIds.Count == 0 &&
            picker.StorekeeperList.Count == 0 && orders.PickerUpdates == 0 && Descendants<DataGrid>(pickerView).Single().IsReadOnly,
            "Revoking picker access must clear rows and reject both stale loads and direct update calls.");
        permissions.SetPermissions(viewGrants.Concat(actions)); delivery.SelectedCourier = delivery.Couriers.Single();
        session.Logout(); Layout(deliveryView);
        Check(delivery.DeliverySessions.Count == 0 && !delivery.CanView && !delivery.CanUpdate && !delivery.CanPrint && !delivery.CanReport,
            "Logging out must clear delivery data and disable every action.");
        retailView.DataContext = null; deliveryView.DataContext = null; pickerView.DataContext = null;
        Console.WriteLine("PASS: independent delivery/picker entry grants, eight delivery action combinations, read-only Paid checkbox, direct-command denial, live revocation and stale-response rejection");
    }

    private sealed class Orders() : OrderService(null!, null!, NullLogger<OrderService>.Instance, null!, null!, null!)
    {
        public static readonly OrderWithPaymentsDto[] DeliveryRows = [new() { OrderId = "СЧ-ABC123", PaymentId = 1,
            ReceiptNumber = "ПЛ-ABC123", CustomerId = "N0001", FullName = "Клиент", Rate = 11, Date = DateTime.Today, PaymentAmount = 10, SaleAmount = 20 }];
        public int Loads, Updates, PickerLoads, PickerUpdates;
        public TaskCompletionSource<IReadOnlyList<OrderWithPaymentsDto>>? NextDelivery;
        public TaskCompletionSource<IReadOnlyList<AssignPickerDto>>? NextPicker;
        public override Task<IReadOnlyList<OrderWithPaymentsDto>> GetOrdersByCourierAsync(string courierId, bool onlyUnpaid = true, CancellationToken ct = default)
        { Loads++; var delay = NextDelivery; NextDelivery = null; return delay?.Task ?? Task.FromResult<IReadOnlyList<OrderWithPaymentsDto>>(DeliveryRows); }
        public override Task<bool> ConfirmDeliveryAsync(string courierId, string? orderId, int? paymentId, CancellationToken ct = default)
        { Updates++; return Task.FromResult(true); }
        public override Task<IReadOnlyList<AssignPickerDto>> GetOrdersInRangeForPickersAsync(DateTime from, DateTime to, CancellationToken ct = default)
        { PickerLoads++; var delay = NextPicker; NextPicker = null; return delay?.Task ?? Task.FromResult<IReadOnlyList<AssignPickerDto>>([new() { OrderId = "СЧ-ABC123", CustomerName = "Клиент", Date = DateTime.Today }]); }
        public override Task<bool> UpdateAssignPickerAsync(string orderId, string? pickerId, CancellationToken ct = default)
        { PickerUpdates++; return Task.FromResult(true); }
    }
    private sealed class Couriers() : CourierRepository(null!)
    {
        public int Calls;
        public override Task<IEnumerable<CourierEntity>> GetAllAsync()
        { Calls++; return Task.FromResult<IEnumerable<CourierEntity>>([new() { Id = "C", FullName = "Курьер" }]); }
    }
    private sealed class Keepers() : StorekeeperRepository(null!)
    {
        public int Calls;
        public override Task<IEnumerable<StorekeeperEntity>> GetAllAsync()
        { Calls++; return Task.FromResult<IEnumerable<StorekeeperEntity>>([new() { Id = "P", FullName = "Сборщик" }]); }
    }
    private static void SetUser(UserSessionService session, int id)
    {
        typeof(UserSessionService).GetProperty(nameof(session.UserId))!.SetValue(session, id);
        typeof(UserSessionService).GetProperty(nameof(session.IsLoggedIn))!.SetValue(session, true);
    }
    private static void Layout(FrameworkElement view)
    {
        view.Measure(new Size(1450, 900)); view.Arrange(new Rect(0, 0, 1450, 900)); view.UpdateLayout();
        view.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }
    private static IEnumerable<Button> Buttons(DependencyObject view) => Descendants<Button>(view);
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
