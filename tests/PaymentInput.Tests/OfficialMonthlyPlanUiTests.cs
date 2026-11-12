using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Media.Imaging;
using System.IO;
using Infrastructure.Constants;
using Infrastructure.Contexts;
using Infrastructure.Dtos;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using PresentationWpf.Services;
using PresentationWpf.ViewModels;
using PresentationWpf.Views;

internal static class OfficialMonthlyPlanUiTests
{
    public static void Run()
    {
        var permissions = new PermissionService();
        permissions.SetPermissions([PermissionCatalog.Orders, PermissionCatalog.OrdersBarter, PermissionCatalog.OfficialMonthlyPlansTotal,
            PermissionCatalog.OfficialMonthlyPlansIndividual, PermissionCatalog.OfficialMonthlyPlansView, PermissionCatalog.OfficialMonthlyPlansManage]);
        var service = new PlanService(permissions);
        var pendingDatabase = new PendingDatabase();
        var vm = new RetailViewModel(null!, null!, null!, new CustomerService(null!, pendingDatabase, null!, null!, null!, null!, null!),
            null!, null!, null!, null!, null!, null!, new CustomerFinanceService(pendingDatabase), permissions, null!, service) { User = "Test Seller" };
        var first = new Customer { Id = "C1", FullName = "Первый клиент", OfficialCustomer = true };
        var completed = new Customer { Id = "C2", FullName = "Выполнен", OfficialCustomer = true };
        var unplanned = new Customer { Id = "U", FullName = "Без плана", OfficialCustomer = true };
        var regular = new Customer { Id = "N", FullName = "Обычный" };
        foreach (var customer in new[] { first, completed, unplanned, regular }) vm.AllCustomers.Add(customer);
        var view = new RetailView { DataContext = vm };
        vm.StartMonthlyPlanRefresh();
        var total = (TextBlock)view.FindName("TotalMonthlyPlanLabel");
        var individual = (TextBlock)view.FindName("CustomerMonthlyPlanLabel");
        foreach (var width in new[] { 1280d, 1700d })
        {
            Layout(view, width);
            var route = Descendants<ComboBox>(view).Single(c => c.GetBindingExpression(ComboBox.SelectedValueProperty)?.ParentBinding.Path.Path == "SelectedTerritory");
            Check(LogicalTreeHelper.GetParent(total) == LogicalTreeHelper.GetParent(route) && Grid.GetColumn(total) == Grid.GetColumn(route) + 1,
                "The global total must be in the same horizontal row immediately to the right of Route.");
            var position = total.TranslatePoint(new Point(), view);
            Check(position.X + total.ActualWidth <= width - 300 && total.ActualWidth > 100,
                "The global plan total must fit in the order panel, including a narrower window.");
        }
        Check(total.Visibility == Visibility.Visible && vm.TotalMonthlyPlanRemaining == 1300 && individual.Visibility == Visibility.Collapsed,
            "The global total must be visible before a customer is selected.");
        vm.SelectedCustomer = first; Layout(view);
        Check(vm.OfficialOrder && vm.CustomerMonthlyPlanRemaining == 300 && individual.Visibility == Visibility.Visible,
            "Selecting an official client must load the individual remaining plan.");
        var barter = (CheckBox)view.FindName("BarterCheckBox");
        var individualPanel = (FrameworkElement)LogicalTreeHelper.GetParent(individual);
        Check(LogicalTreeHelper.GetParent(individualPanel) == LogicalTreeHelper.GetParent(barter) &&
            Grid.GetColumn(individualPanel) == Grid.GetColumn(barter) + 1,
            "The individual remaining plan must be in the same row immediately to the right of Barter.");
        foreach (var width in new[] { 1280d, 1700d })
        {
            Layout(view, width);
            Check(barter.Visibility == Visibility.Visible && individual.TranslatePoint(new Point(), view).X >=
                barter.TranslatePoint(new Point(barter.ActualWidth, 0), view).X &&
                individual.TranslatePoint(new Point(individual.ActualWidth, 0), view).X <= width - 300,
                "Barter and the individual remaining plan must fit side by side in the order panel.");
        }
        Layout(view);
        Render(view, "monthly-plan-retail.png");
        service.Total = 1250; service.FirstRemaining = 250; service.NotifyChanged(); Layout(view);
        Check(vm.TotalMonthlyPlanRemaining == 1250 && vm.CustomerMonthlyPlanRemaining == 250,
            "Committed data changes must refresh both visible indicators immediately.");
        service.Total = 1300; service.FirstRemaining = 300; service.NotifyChanged();
        service.Threshold = 5;
        foreach (var remaining in new[] { 3m, 5m })
        {
            service.FirstRemaining = remaining; service.Total = 1000; service.NotifyChanged(); Layout(view);
            Check(vm.CustomerMonthlyPlanRemaining == 0 && individual.Text == "Остаток по плану: 0" &&
                vm.TotalMonthlyPlanRemaining == 1000 && !vm.OfficialOrder,
                "The order panel must show zero and exclude a threshold-completed client from the global total.");
        }
        service.Threshold = 0; service.Total = 1300; service.FirstRemaining = 300; service.NotifyChanged();
        Check(!Descendants<CheckBox>(view).Any(c => c.GetBindingExpression(CheckBox.IsCheckedProperty)?.ParentBinding.Path.Path == "OfficialOrder"),
            "The official-order checkbox must not be shown next to plan information.");
        vm.SelectedCustomer = completed; Layout(view);
        Check(!vm.OfficialOrder && vm.CustomerMonthlyPlanRemaining == 0 && individual.Visibility == Visibility.Visible && vm.TotalMonthlyPlanRemaining == 1300,
            "A completed plan must retain its zero indicator and disable the new-order default.");
        typeof(RetailViewModel).GetField("_customerBalance", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(vm, 0m);
        vm.AvailableRestriction = 10000;
        var courier = new Courier { Id = "D", FullName = "Доставщик" };
        var keeper = new Storekeeper { Id = "S", FullName = "Сборщик" };
        vm.CourierList.Add(courier); vm.StorekeeperList.Add(keeper);
        vm.SelectedCourier = courier; vm.SelectedStorekeeper = keeper;
        Check(vm.SaveOrderCommand.CanExecute(null), "Meeting the monthly plan must still allow ordinary sales under the existing sale rules.");
        vm.SelectedCustomer = unplanned; Layout(view);
        Check(vm.OfficialOrder && individual.Visibility == Visibility.Collapsed && total.Visibility == Visibility.Visible,
            "An official client without a plan must retain its original order default without a plan indicator.");
        vm.SelectedCustomer = regular; Layout(view);
        Check(!vm.OfficialOrder && individual.Visibility == Visibility.Collapsed && vm.TotalMonthlyPlanRemaining == 1300,
            "Switching to a normal client must not clear the global total.");
        vm.ResetState(); Layout(view);
        Check(total.Visibility == Visibility.Visible && vm.TotalMonthlyPlanRemaining == 1300, "ResetState must preserve the global indicator.");

        var old = new TaskCompletionSource<OfficialMonthlyPlanRetailState>();
        service.DelayFirst = old;
        vm.SelectedCustomer = first;
        vm.SelectedCustomer = completed;
        old.SetResult(new(new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1), first.Id, true, 999, 999));
        Layout(view);
        Check(!vm.OfficialOrder && vm.CustomerMonthlyPlanRemaining == 0 && vm.TotalMonthlyPlanRemaining == 1300,
            "A delayed response for the previous client must not overwrite current plan information.");
        service.DelayFirst = null;
        vm.SelectedCustomer = first;
        permissions.SetPermissions([PermissionCatalog.Orders, PermissionCatalog.OfficialMonthlyPlansTotal]); Layout(view);
        Check(individual.Visibility == Visibility.Collapsed && total.Visibility == Visibility.Visible && vm.OfficialOrder,
            "Display permissions must be independent from the new-order default.");
        permissions.SetPermissions([PermissionCatalog.Orders, PermissionCatalog.OfficialMonthlyPlansIndividual]); Layout(view);
        Check(individual.Visibility == Visibility.Visible && total.Visibility == Visibility.Collapsed,
            "Individual permission must not grant the global indicator.");
        vm.StopMonthlyPlanRefresh();

        permissions.SetPermissions([PermissionCatalog.OfficialMonthlyPlansView, PermissionCatalog.OfficialMonthlyPlansManage]);
        var plans = new OfficialMonthlyPlansViewModel(service, permissions);
        plans.RefreshCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        Check(plans.Rows.Count == 3 && plans.TotalPlan == 4500 && plans.TotalPurchased == 3200 && plans.TotalRemaining == 1300,
            "The control table must total all planned clients, including completed plans.");
        var control = new OfficialMonthlyPlansView { DataContext = plans }; Layout(control);
        var save = (Button)control.FindName("SavePlansButton");
        var show = (Button)control.FindName("ShowPlansButton");
        Check(save.Visibility == Visibility.Collapsed, "Bulk saving must not appear in the control tab.");
        Render(control, "monthly-plans-control.png");
        var filter = (ComboBox)control.FindName("CustomerPlanFilter");
        Check(filter.Visibility == Visibility.Visible && filter.Items.Count == 4 && Equals(filter.SelectedValue, "") &&
            ((DataGrid)control.FindName("ControlGrid")).Columns[3].Header.Equals("Продажа"),
            "Control must offer All clients and individual clients, and label sales as Продажа.");
        filter.SelectedValue = "C1"; Layout(control);
        Check(plans.VisibleRows.Count == 1 && plans.VisibleRows[0].CustomerId == "C1" &&
            plans.TotalPlan == 1000 && plans.TotalPurchased == 700 && plans.TotalRemaining == 300,
            "Choosing one client must filter rows and totals together.");
        plans.RefreshCommand.ExecuteAsync(null).GetAwaiter().GetResult(); Layout(control);
        Check(plans.SelectedCustomerId == "C1" && plans.VisibleRows.Count == 1,
            "Refreshing must preserve the client filter.");
        filter.SelectedValue = "C2"; Layout(control);
        Check(plans.VisibleRows.Count == 1 && plans.TotalRemaining == 0 && plans.TotalPurchased == 2000,
            "A completed client must remain selectable in the filter.");
        filter.SelectedValue = ""; Layout(control);
        Check(plans.VisibleRows.Count == 3 && plans.TotalRemaining == 1300 && plans.TotalPlan == 4500,
            "All clients must restore the complete list and totals.");
        service.Threshold = 5;
        foreach (var remaining in new[] { 3m, 5m, 5.01m })
        {
            service.FirstRemaining = remaining;
            plans.RefreshCommand.ExecuteAsync(null).GetAwaiter().GetResult(); Layout(control);
            filter.SelectedValue = "C1"; Layout(control);
            var expected = remaining <= 5 ? 0 : remaining;
            Check(plans.VisibleRows.Single().Remaining == expected && plans.TotalRemaining == expected &&
                plans.TotalPlan == 1000 && plans.TotalPurchased == 1000 - remaining,
                "Control rows and filtered totals must apply the threshold without modifying plan or sales values.");
            filter.SelectedValue = ""; Layout(control);
            Check(plans.TotalRemaining == 1000 + expected, "The complete control total must exclude small per-client remainders.");
        }
        service.Threshold = 0; service.FirstRemaining = 300;
        plans.RefreshCommand.ExecuteAsync(null).GetAwaiter().GetResult(); Layout(control);
        filter.SelectedValue = "C2";
        plans.IsSettings = true;
        plans.RefreshCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        Check(plans.Rows.Count == 4 && plans.Rows.Any(r => r.Amount == null), "Settings must include unplanned official clients.");
        Layout(control); Render(control, "monthly-plans-settings.png");
        Check(filter.Visibility == Visibility.Collapsed, "The control filter must not hide clients in plan settings.");
        Check(save.Visibility == Visibility.Visible && save.IsEnabled && save.Command == plans.SaveCommand &&
            LogicalTreeHelper.GetParent(save) == LogicalTreeHelper.GetParent(show) &&
            save.TranslatePoint(new Point(), control).X >= show.TranslatePoint(new Point(show.ActualWidth, 0), control).X,
            "The shared Save button must be immediately to the right of Show in settings.");
        Check(((DataGrid)control.FindName("SettingsGrid")).Columns.Count == 3 &&
            !Descendants<Button>((DataGrid)control.FindName("SettingsGrid")).Any(b => Equals(b.Content, "Сохранить")),
            "Settings must no longer require per-row save buttons.");
        plans.Rows[0].AmountText = "1 000,50";
        Check(plans.Rows[0].TryGetAmount(out var amount) && amount == 1000.50m, "A comma must be parsed as a decimal separator, not thousands.");
        plans.RefreshCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        Check(plans.Rows[0].AmountText == "1 000,50", "Automatic refresh must preserve unsaved plan input.");
        plans.Rows[1].AmountText = "1,000.50";
        Check(!plans.Rows[1].TryGetAmount(out _), "Ambiguous separators must not silently change a plan amount.");
        plans.SaveCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        Check(service.SaveCalls == 0 && plans.Rows[0].IsDirty && plans.Message.Contains("C2"),
            "An invalid row must block the entire batch, identify the client and preserve all input.");
        plans.Rows[1].AmountText = "2 500,25";
        plans.SaveCommand.ExecuteAsync(null).GetAwaiter().GetResult(); Layout(control);
        Check(service.SaveCalls == 1 && service.SavedMonth == new DateTime(plans.SelectedYear, plans.SelectedMonth, 1) &&
            service.SavedAmounts.Count == 2 && service.SavedAmounts["C1"] == 1000.50m && service.SavedAmounts["C2"] == 2500.25m,
            "One Save click must submit all changed plans for the selected month, skipping unchanged and empty rows.");
        Check(plans.Rows.All(r => !r.IsDirty) && plans.Rows[3].Amount == null && plans.Message.Contains("Сохранено планов: 2"),
            "Saved values must reload cleanly without assigning plans to empty rows.");
        plans.SaveCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        Check(service.SaveCalls == 1, "Saving again without edits must not write any plans.");
        plans.Rows[0].AmountText = "1200";
        service.FailNextSave = true;
        plans.SaveCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        Check(plans.Rows[0].IsDirty && plans.Rows[0].AmountText == "1200" && plans.CanEdit,
            "A failed save must keep drafts and allow retrying.");
        plans.SaveCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        Check(!plans.Rows[0].IsDirty && plans.Rows[0].Amount == 1200, "Retry must save and clear the remaining draft.");
        var threshold = (TextBox)control.FindName("NonOfficialThresholdBox");
        threshold.Text = "5,25";
        plans.RefreshCommand.ExecuteAsync(null).GetAwaiter().GetResult(); Layout(control);
        Check(plans.NonOfficialThresholdText == "5,25", "Background refresh must preserve an unsaved global threshold.");
        var beforeThresholdSave = service.SaveCalls;
        plans.SaveCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        Check(service.SaveCalls == beforeThresholdSave + 1 && service.Threshold == 5.25m && service.SavedAmounts.Count == 0,
            "The shared Save button must save the global threshold without requiring changes to any client plan.");
        plans.NonOfficialThresholdText = "-1";
        plans.Rows[0].AmountText = "1300";
        plans.SaveCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        Check(service.SaveCalls == beforeThresholdSave + 1 && plans.Rows[0].IsDirty,
            "An invalid threshold must block all plan changes and preserve drafts.");
        plans.NonOfficialThresholdText = "5";
        plans.SaveCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        Check(service.Threshold == 5 && service.SavedAmounts["C1"] == 1300,
            "The threshold and edited client plans must be submitted together.");
        Layout(control); Render(control, "monthly-plans-threshold-settings.png");
        permissions.SetPermissions([PermissionCatalog.OfficialMonthlyPlansView]); Layout(control);
        Check(plans.ShowControl && !plans.CanEdit, "View permission must not grant editing.");
        Check(((FrameworkElement)control.FindName("PlanThresholdSettings")).Visibility == Visibility.Collapsed,
            "Read-only viewers must not access the global threshold editor.");
        var calls = service.SaveCalls;
        plans.SaveCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        Check(service.SaveCalls == calls && save.Visibility == Visibility.Collapsed, "View-only users must not save any plans.");
        permissions.SetPermissions([PermissionCatalog.OfficialMonthlyPlansManage]); Layout(control);
        Check(plans.ShowSettings && !plans.CanView, "Manage permission must not reveal the detailed control table.");
        CheckMenu();
        Console.WriteLine("PASS: monthly-plan menu, independent permissions, Route placement, totals, safe plan entry, shared Save button, batch validation and retry");
    }

    private static void CheckMenu()
    {
        object State(bool allowed) => new { IsLoggedIn = true, CanOpenMonthlyPlans = allowed, CurrentViewModel = (object?)null,
            CanViewOrders = false, CanViewReturns = false, CanViewCustomers = false, CanViewInventory = false, CanViewProductRequests = false,
            CanViewExpenses = false, CanViewReports = false, CanViewStatistics = false, CanViewAdmin = false, CanViewSettings = false };
        var window = new PresentationWpf.MainWindow(null!) { DataContext = State(false) };
        Layout((FrameworkElement)window.Content);
        var menu = (RadioButton)window.FindName("MonthlyPlansMenuItem");
        Check(menu.Visibility == Visibility.Collapsed, "Monthly-plan menu must be hidden without its permissions.");
        window.DataContext = State(true); Layout((FrameworkElement)window.Content);
        Check(menu.Visibility == Visibility.Visible, "A plan permission must reveal the menu without Admin.");
        window.Close();
    }

    private sealed class PlanService(PermissionService permissions) : OfficialMonthlyPlanService(null!, permissions)
    {
        public TaskCompletionSource<OfficialMonthlyPlanRetailState>? DelayFirst;
        public decimal Total = 1300;
        public decimal FirstRemaining = 300;
        public int SaveCalls;
        public DateTime SavedMonth;
        public Dictionary<string, decimal> SavedAmounts = [];
        public bool FailNextSave;
        public decimal Threshold;
        public override Task<decimal> GetNonOfficialThresholdAsync() => Task.FromResult(Threshold);
        private readonly Dictionary<string, decimal> _amounts = new() { ["C1"] = 1000, ["C2"] = 2000, ["C3"] = 1500 };
        public override Task SetPlansAsync(DateTime month, IReadOnlyDictionary<string, decimal> amounts, decimal? nonOfficialThreshold = null)
        {
            SaveCalls++;
            if (FailNextSave) { FailNextSave = false; return Task.FromException(new InvalidOperationException("Ошибка сохранения.")); }
            SavedMonth = month;
            SavedAmounts = amounts.ToDictionary(p => p.Key, p => p.Value);
            foreach (var (id, amount) in amounts) _amounts[id] = amount;
            if (nonOfficialThreshold.HasValue) Threshold = nonOfficialThreshold.Value;
            NotifyChanged();
            return Task.CompletedTask;
        }
        public override Task<OfficialMonthlyPlanRetailState> GetRetailStateAsync(string? id, DateTime date) =>
            id == "C1" && DelayFirst != null ? DelayFirst.Task : Task.FromResult(new OfficialMonthlyPlanRetailState(
                new DateTime(date.Year, date.Month, 1), id, id == "U" || (id == "C1" && FirstRemaining > Threshold),
                id == "C1" ? FirstRemaining > Threshold ? FirstRemaining : 0 : id == "C2" ? 0 : null,
                Total, id == "C1" && FirstRemaining > Threshold ? FirstRemaining : null));
        public override Task<IReadOnlyList<OfficialMonthlyPlanSummary>> GetControlAsync(DateTime date) =>
            Task.FromResult<IReadOnlyList<OfficialMonthlyPlanSummary>>([new("C1", "Клиент 1", 1000, 1000 - FirstRemaining, Threshold),
                new("C2", "Клиент 2", 2000, 2000, Threshold), new("C3", "Клиент 3", 1500, 500, Threshold)]);
        public override Task<IReadOnlyList<OfficialMonthlyPlanSetting>> GetSettingsAsync(DateTime date) =>
            Task.FromResult<IReadOnlyList<OfficialMonthlyPlanSetting>>([new("C1", "Клиент 1", _amounts["C1"]), new("C2", "Клиент 2", _amounts["C2"]),
                new("C3", "Клиент 3", _amounts["C3"]), new("U", "Без плана", null)]);
        public override Task<IReadOnlyList<int>> GetYearsAsync() => Task.FromResult<IReadOnlyList<int>>([DateTime.Today.Year]);
    }
    private sealed class PendingDatabase : IDbContextFactory<DatabaseContext>
    {
        public DatabaseContext CreateDbContext() => throw new InvalidOperationException("No database in UI tests.");
        public Task<DatabaseContext> CreateDbContextAsync(CancellationToken ct = default) => new TaskCompletionSource<DatabaseContext>().Task;
    }
    private static void Layout(FrameworkElement view, double width = 1280)
    {
        view.Width = width; view.Height = 850; view.Measure(new Size(width, 850)); view.Arrange(new Rect(0, 0, width, 850));
        view.UpdateLayout(); view.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match) yield return match;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }
    private static void Render(FrameworkElement view, string filename)
    {
        var bitmap = new RenderTargetBitmap((int)view.ActualWidth, (int)view.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(view);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(Path.Combine(AppContext.BaseDirectory, filename)); encoder.Save(file);
    }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
}
