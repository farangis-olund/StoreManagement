using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Infrastructure.Contexts;
using Infrastructure.Dtos;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using PresentationWpf.Dtos;
using PresentationWpf.Services;
using PresentationWpf.ViewModels;
using PresentationWpf.Views;

internal static class RetailCustomerSelectionUiTests
{
    public static void Run()
    {
        var vm = new SelectionViewModel();
        var first = new Customer { Id = "C1", FullName = "Первый клиент", OfficialCustomer = true };
        var second = new Customer { Id = "C2", FullName = "Второй клиент" };
        vm.AllCustomers.Add(first); vm.AllCustomers.Add(second);
        var view = new RetailView { DataContext = vm }; Layout(view);
        var clear = Descendants<Button>(view).Single(b =>
            b.GetBindingExpression(Button.CommandProperty)?.ParentBinding.Path.Path == nameof(vm.ClearCartCommand));
        Check(!clear.IsEnabled, "The empty cart must disable the clear button.");
        var customerInputs = Descendants<ComboBox>(view).Where(c =>
            c.GetBindingExpression(ComboBox.SelectedItemProperty)?.ParentBinding.Path.Path == nameof(vm.SelectedCustomer)).ToArray();
        Check(customerInputs.Length == 2, "Both customer code and name inputs must be tested.");
        customerInputs[0].SelectedItem = first; Layout(view);
        var courier = new Courier { Id = "D1", FullName = "Доставщик" };
        var picker = new Storekeeper { Id = "S1", FullName = "Сборщик" };
        vm.CourierList.Add(courier); vm.StorekeeperList.Add(picker);
        vm.SelectedCourier = courier; vm.SelectedStorekeeper = picker;
        typeof(RetailViewModel).GetField("_customerBalance", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(vm, 0m);
        vm.AvailableRestriction = 1000m;
        vm.CustomerMonthlyPlanRemaining = 500m;
        var row = (ProductModel)new Product { ArticleNumber = "TEST", Quentity = 10, PriceLevel1 = 5m };
        row.OrderQuentity = 2;
        vm.SelectedProductList.Add(row);
        var total = vm.TotalSum;
        var warnings = 0;
        void CheckUnchanged()
        {
            Layout(view);
            Check(vm.SelectedCustomer == first && customerInputs.All(c => c.SelectedItem == first),
                "A rejected selection must restore both customer inputs to the original customer.");
            Check(customerInputs.All(c => c.Text == (c.DisplayMemberPath == "Id" ? first.Id : first.FullName)),
                "The rejected customer's text must not remain visible in either editable input.");
            Check(vm.SelectedProductList.Count == 1 && vm.SelectedProductList[0] == row && vm.TotalSum == total &&
                vm.SelectedCourier == courier && vm.SelectedStorekeeper == picker && vm.AvailableRestriction == 1000m &&
                vm.CustomerMonthlyPlanRemaining == 500m && vm.OfficialOrder,
                "A rejected customer switch changed the cart, delivery, prices, finance or monthly plan.");
            Check(vm.Warnings == ++warnings, "Each attempted switch must show exactly one warning.");
        }
        foreach (var input in customerInputs)
        {
            input.SelectedItem = second;
            CheckUnchanged();
        }
        customerInputs.Single(c => c.DisplayMemberPath == "Id").Text = second.Id;
        CheckUnchanged();
        customerInputs.Single(c => c.DisplayMemberPath == "FullName").Text = "";
        CheckUnchanged();
        vm.SelectedCustomer = null; CheckUnchanged();
        vm.SelectCustomerByIdAsync(second.Id).GetAwaiter().GetResult(); CheckUnchanged();

        ComboBox InactiveInput(string property) => Descendants<ComboBox>(view).Single(c =>
            c.GetBindingExpression(ComboBox.SelectedItemProperty)?.ParentBinding.Path.Path == property);
        var debt = new InactiveCustomerDto(second.Id, second.FullName, 0, 25m);
        var planned = new InactiveCustomerDto(second.Id, second.FullName, 0, 40m);
        vm.InactiveDebtCustomers.Add(debt); vm.InactivePlannedCustomers.Add(planned);
        var debtInput = InactiveInput(nameof(vm.SelectedRequiredPurchaseCombo));
        var planInput = InactiveInput(nameof(vm.SelectedPlannedPurchaseCombo));
        debtInput.SelectedItem = debt; CheckUnchanged();
        planInput.SelectedItem = planned; CheckUnchanged();
        Check(debtInput.SelectedItem == null && planInput.SelectedItem == null &&
            vm.RequiredPurchase == 0 && vm.PlannedPurchase == 0, "Rejected inactive-panel selections changed the customer indicators.");

        row.OrderQuentity = 0; total = vm.TotalSum;
        customerInputs[0].SelectedItem = second; CheckUnchanged();
        var another = (ProductModel)new Product { ArticleNumber = "SECOND", Quentity = 10, PriceLevel1 = 5m };
        another.OrderQuentity = 2; vm.SelectedProductList.Add(another); Layout(view);
        var totalBeforeClear = vm.TotalSum;
        Check(clear.IsEnabled, "Adding rows must enable the clear button.");
        clear.Command.Execute(clear.CommandParameter); Layout(view);
        Check(vm.ClearConfirmations == 1 && vm.SelectedProductList.Count == 2 && vm.TotalSum == totalBeforeClear &&
            vm.SelectedCustomer == first && vm.SelectedCourier == courier && vm.SelectedStorekeeper == picker,
            "Declining confirmation must preserve all cart rows, totals and customer/delivery selections.");
        vm.AllowClear = true;
        clear.Command.Execute(clear.CommandParameter); Layout(view);
        Check(vm.ClearConfirmations == 2 && vm.SelectedProductList.Count == 0 && vm.TotalSum == 0 && !clear.IsEnabled &&
            vm.SelectedCustomer == first && vm.SelectedCourier == courier && vm.SelectedStorekeeper == picker,
            "Confirming must remove all rows, including zero quantities, reset the total and retain the selected customer/delivery.");
        vm.ClearCartCommand.Execute(null);
        Check(vm.ClearConfirmations == 2, "An empty cart must not show another confirmation.");
        customerInputs[1].SelectedItem = second; Layout(view);
        Check(vm.SelectedCustomer == second && customerInputs.All(c => c.SelectedItem == second) && vm.Warnings == warnings &&
            vm.SelectedCourier == null && vm.SelectedStorekeeper == null,
            "An empty cart must allow switching customers and clear delivery as usual.");
        vm.SelectedProductList.Add(row);
        vm.ResetState(); Layout(view);
        Check(vm.SelectedCustomer == null && vm.SelectedProductList.Count == 0 && vm.Warnings == warnings,
            "An explicit order reset must clear the customer and cart without a switch warning.");
        Console.WriteLine("PASS: nonempty cart blocks customer changes; Clear Cart requires confirmation, cancellation preserves rows and totals, confirmation removes all rows and permits switching");
    }

    private sealed class SelectionViewModel() : RetailViewModel(null!, null!, null!,
        new CustomerService(null!, new PendingDatabase(), null!, null!, null!, null!, null!), null!, null!, null!, null!, null!, null!,
        new CustomerFinanceService(new PendingDatabase()), new PermissionService(), null!)
    {
        public int Warnings { get; private set; }
        public int ClearConfirmations { get; private set; }
        public bool AllowClear { get; set; }
        protected override void ShowCustomerChangeBlockedMessage() => Warnings++;
        protected override bool ConfirmCartClear() { ClearConfirmations++; return AllowClear; }
    }
    private sealed class PendingDatabase : IDbContextFactory<DatabaseContext>
    {
        public DatabaseContext CreateDbContext() => throw new Exception("No real database is used in this UI test.");
        public Task<DatabaseContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => new TaskCompletionSource<DatabaseContext>().Task;
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typed) yield return typed;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }
    private static void Layout(FrameworkElement view)
    {
        view.Measure(new Size(1450, 900)); view.Arrange(new Rect(0, 0, 1450, 900)); view.UpdateLayout();
        view.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
}
