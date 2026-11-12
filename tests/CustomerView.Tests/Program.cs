using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using Infrastructure.Constants;
using Infrastructure.Dtos;
using Infrastructure.Entities;
using Infrastructure.Security;
using PresentationWpf.Services;
using PresentationWpf.ViewModels;
using PresentationWpf.Views;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        try
        {
            var app = new Application();
            app.Resources["BoolToVisibilityConverter"] = new BooleanToVisibilityConverter();
            var listener = new BindingErrors();
            PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
            PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
            var permissions = new PermissionService();
            var policy = new CustomerFieldPermissionPolicy(permissions);
            var vm = new CustomerViewModel(null!, null!, permissions, policy);
            vm.PriceLevels.Add(new PriceLevelEntity { Level = "L1", PriceType = "Level" });
            vm.SalesManagers.Add(new SalesManagerEntity { Id = "M1", FullName = "Manager" });
            vm.DistinctTerritories.Add("T1");
            var customer = new Customer
            {
                Id = "N0001", FullName = "Test", Debt = 100, Restriction = 200, BuyLimit = 1000,
                UnpaidPaymentLimit = 1300, PriceLevelId = "L1", SalesManagerId = "M1", Territory = "T1",
                ContractDate = new DateTime(2026, 1, 1), DailyPurchaseCoefficient = 1,
                DailyRepaymentCoefficient = 2, DailyPlannedPurchaseCoefficient = null, ExcludeDailyRepayment = true
            };
            vm.SelectedCustomer = customer;
            var view = new CustomerView { DataContext = vm };
            void Refresh()
            {
                vm.RefreshPermissions();
                view.Measure(new Size(900, 800));
                view.Arrange(new Rect(0, 0, 900, 800));
                view.UpdateLayout();
                view.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            }
            void Grants(params string[] rights)
            {
                permissions.SetPermissions(rights.Prepend(PermissionCatalog.Customers));
                Refresh();
            }
            T Control<T>(DependencyProperty property, string path) where T : FrameworkElement =>
                Descendants(view).OfType<T>().Single(c =>
                    BindingOperations.GetBindingExpression(c, property)?.ParentBinding.Path?.Path == path);
            FrameworkElement FieldGroup(string field) => Control<FrameworkElement>(UIElement.VisibilityProperty,
                "DataContext." + field + "Access.CanView");

            Grants();
            foreach (var field in CustomerPermissionCatalog.Fields)
                Assert(FieldGroup(field.Field.ToString()).Visibility == Visibility.Collapsed, "Field visible without View: " + field.Field);
            Assert(!vm.CreateCustomerCommand.CanExecute(null), "New customer enabled without required Add rights.");
            var delete = (Button)view.FindName("DeleteCustomerButton");
            Assert(delete.Visibility == Visibility.Collapsed && !vm.DeleteCustomerCommand.CanExecute(null),
                "Client deletion must be hidden and disabled without its own permission.");
            vm.DeleteCustomerCommand.ExecuteAsync(null).GetAwaiter().GetResult();
            Grants(CustomerPermissionCatalog.Debt.Delete);
            Assert(delete.Visibility == Visibility.Collapsed, "Field-clear permission must not reveal client deletion.");
            Grants(PermissionCatalog.CustomersDelete);
            Assert(delete.Visibility == Visibility.Visible && delete.IsEnabled && vm.DeleteCustomerCommand.CanExecute(null),
                "The deletion grant must reveal and enable Delete for a saved client.");
            vm.SelectedCustomer = null; Refresh();
            Assert(!delete.IsEnabled, "Delete must be disabled when no client is selected.");
            vm.SelectedCustomer = new Customer(); Refresh();
            Assert(!delete.IsEnabled, "An unsaved new client must not enable Delete.");
            vm.SelectedCustomer = customer; Refresh();
            Assert(delete.IsEnabled, "Selecting a saved client must re-enable Delete.");
            permissions.SetPermissions([PermissionCatalog.CustomersDelete]); Refresh();
            Assert(delete.Visibility == Visibility.Collapsed && !vm.DeleteCustomerCommand.CanExecute(null),
                "Client deletion must require access to the Customers module.");
            Grants(PermissionCatalog.CustomersDelete);
            Grants();
            Assert(delete.Visibility == Visibility.Collapsed && !delete.IsEnabled, "Revoking deletion rights must update the existing toolbar.");
            Console.WriteLine("PASS: client Delete button, saved-client selection, separate permission, module access and immediate revocation");

            // A session refresh must update the existing view without calling RefreshPermissions manually.
            permissions.SetPermissions(new[] { PermissionCatalog.Customers, CustomerPermissionCatalog.Debt.View,
                CustomerPermissionCatalog.Debt.Edit });
            view.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            Assert(Control<TextBox>(TextBox.TextProperty, "Debt").IsEnabled, "Granted Edit did not reach the open customer view.");
            permissions.SetPermissions(new[] { PermissionCatalog.Customers, CustomerPermissionCatalog.Debt.View });
            view.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            Assert(!Control<TextBox>(TextBox.TextProperty, "Debt").IsEnabled, "Revoked Edit remained enabled in the open customer view.");
            permissions.SetPermissions(new[] { PermissionCatalog.Customers });
            view.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            Assert(FieldGroup("Debt").Visibility == Visibility.Collapsed, "Revoked View remained visible.");
            Assert(!policy.CanEdit(CustomerField.Debt), "The save policy kept the old permission.");
            Console.WriteLine("PASS: granting and revoking permissions refreshes the existing customer view without logging in again");

            Grants(CustomerPermissionCatalog.Debt.View);
            Assert(FieldGroup("Debt").Visibility == Visibility.Visible, "View did not reveal debt.");
            Assert(!Control<TextBox>(TextBox.TextProperty, "Debt").IsEnabled, "View must not permit editing.");
            Grants(CustomerPermissionCatalog.Debt.View, CustomerPermissionCatalog.Debt.Delete);
            var debt = Control<TextBox>(TextBox.TextProperty, "Debt");
            Assert(debt.IsEnabled, "Delete-only users must be able to erase the saved value directly.");
            debt.Text = "";
            debt.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
            Refresh();
            Assert(customer.Debt == null && Control<TextBox>(TextBox.TextProperty, "Debt").Text == "", "Clear did not refresh the DTO binding.");
            var originalDebt = new CustomerEntity { Debt = 100 };
            policy.EnsureCanChange(CustomerField.Debt, originalDebt, customer);
            debt.Text = "200";
            debt.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
            AssertDenied(() => policy.EnsureCanChange(CustomerField.Debt, originalDebt, customer),
                "Delete-only users must not save replacement values.");
            Grants(CustomerPermissionCatalog.Debt.View, CustomerPermissionCatalog.Debt.Add);
            Assert(!debt.IsEnabled, "Erasing a saved field must not turn Edit into Add permission.");

            Grants(CustomerPermissionCatalog.BuyLimit.View, CustomerPermissionCatalog.BuyLimit.Edit);
            var buyLimit = Control<TextBox>(TextBox.TextProperty, "BuyLimit");
            Assert(buyLimit.IsEnabled, "Existing limit is not editable with Edit.");
            buyLimit.Text = "1500";
            buyLimit.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
            Assert(customer.BuyLimit == 1500, "Limit input did not reach the DTO.");
            buyLimit.Text = "";
            buyLimit.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
            AssertDenied(() => policy.EnsureCanChange(CustomerField.BuyLimit,
                new CustomerEntity { BuyLimit = 1000 }, customer), "Edit alone must not save an empty value.");
            buyLimit.Text = "1600";
            buyLimit.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
            policy.EnsureCanChange(CustomerField.BuyLimit, new CustomerEntity { BuyLimit = 1000 }, customer);

            Grants(CustomerPermissionCatalog.Coefficients.View, CustomerPermissionCatalog.Coefficients.Edit);
            Assert(Control<TextBox>(TextBox.TextProperty, "DailyPurchaseCoefficient").IsEnabled, "Saved coefficient needs Edit.");
            Assert(!Control<TextBox>(TextBox.TextProperty, "DailyPlannedPurchaseCoefficient").IsEnabled, "Empty coefficient must require Add.");
            Assert(!vm.CalculateCoefficientsForCustomerCommand.CanExecute(null), "Mixed calculation must require both Add and Edit.");

            Grants(CustomerPermissionCatalog.ExcludeDailyRepayment.View, CustomerPermissionCatalog.ExcludeDailyRepayment.Delete);
            var exclude = Control<CheckBox>(CheckBox.IsCheckedProperty, "ExcludeDailyRepayment");
            Assert(exclude.IsChecked == true && exclude.IsEnabled, "Repayment checkbox binding or Delete permission failed.");
            exclude.IsChecked = false;
            exclude.GetBindingExpression(CheckBox.IsCheckedProperty)!.UpdateSource();
            Assert(customer.ExcludeDailyRepayment == false, "Repayment checkbox did not update the DTO.");

            Grants(CustomerPermissionCatalog.Fields.Where(f => f.IsRequired)
                .SelectMany(f => new[] { f.View, f.Add, f.Delete }).ToArray());
            Assert(vm.CreateCustomerCommand.CanExecute(null), "New customer blocked despite all required Add rights.");
            foreach (var field in CustomerPermissionCatalog.Fields.Where(f => f.IsRequired))
                Assert(!policy.CanDelete(field.Field), "Required field can be cleared.");
            vm.SelectedCustomer = new Customer { ContractDate = null };
            Refresh();
            Assert(Control<ComboBox>(ComboBox.SelectedValueProperty, "PriceLevelId").IsEnabled, "New customer price level requires Add.");
            Assert(Control<ComboBox>(ComboBox.SelectedValueProperty, "SalesManagerId").IsEnabled, "New customer manager requires Add.");
            vm.SelectedCustomer = new Customer
            {
                Id = "N0002", Debt = 1, Restriction = 1, BuyLimit = 1, UnpaidPaymentLimit = 1,
                DailyPurchaseCoefficient = 1, DailyRepaymentCoefficient = 1, DailyPlannedPurchaseCoefficient = 1,
                ContractDate = new DateTime(2026, 1, 1), ExcludeDailyRepayment = true
            };
            Grants(CustomerPermissionCatalog.Fields.SelectMany(f => new[] { f.View, f.Delete }).ToArray());
            Assert(!vm.CalculateCoefficientsForCustomerCommand.CanExecute(null), "Delete must not allow calculation of replacements.");
            foreach (var property in new[] { "Debt", "Restriction", "BuyLimit", "UnpaidPaymentLimit",
                "DailyPurchaseCoefficient", "DailyRepaymentCoefficient", "DailyPlannedPurchaseCoefficient" })
            {
                var input = Control<TextBox>(TextBox.TextProperty, property);
                Assert(input.IsEnabled, "Delete-only direct clearing is disabled: " + property);
                input.Text = "";
                input.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
                Assert(typeof(Customer).GetProperty(property)!.GetValue(vm.SelectedCustomer) == null,
                    "Empty text did not clear the model: " + property);
            }
            var date = Control<DatePicker>(DatePicker.SelectedDateProperty, "ContractDate");
            Assert(date.IsEnabled, "Delete-only date clearing is disabled.");
            date.Text = "";
            date.GetBindingExpression(DatePicker.SelectedDateProperty)!.UpdateSource();
            Refresh();
            foreach (var property in new[] { "Debt", "Restriction", "BuyLimit", "UnpaidPaymentLimit",
                "DailyPurchaseCoefficient", "DailyRepaymentCoefficient", "DailyPlannedPurchaseCoefficient" })
                Assert(Control<TextBox>(TextBox.TextProperty, property).Text == "", "Clear left stale text: " + property);
            Assert(Control<DatePicker>(DatePicker.SelectedDateProperty, "ContractDate").SelectedDate == null,
                "Clear left the old contract date visible.");
            Assert(vm.SelectedCustomer.ContractDate == null, "Direct date clearing did not reach the model.");
            Assert(!Descendants(view).OfType<Button>().Any(b => b.ToolTip?.ToString() == "Очистить значение"),
                "Customer field clear buttons should not be displayed.");
            Assert(listener.Messages.Count == 0, string.Join(Environment.NewLine, listener.Messages));
            Console.WriteLine("PASS: customer WPF bindings, hidden/read-only fields, Add/Edit/Delete controls, required selections, and checkbox binding");
            CheckPermissionsView(app);
            RolePermissionsTests.Run(app);
            Assert(listener.Messages.Count == 0, string.Join(Environment.NewLine, listener.Messages));
            app.Shutdown();
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static void AssertDenied(Action action, string message)
    {
        try { action(); }
        catch (UnauthorizedAccessException) { return; }
        throw new InvalidOperationException(message);
    }

    private static void CheckPermissionsView(Application app)
    {
        // Reproduce the fixed row height inherited from the application's SummaryGrid style.
        var summaryGrid = new Style(typeof(DataGrid));
        summaryGrid.Setters.Add(new Setter(DataGrid.RowHeightProperty, 28d));
        app.Resources["SummaryGrid"] = summaryGrid;
        var entries = PermissionCatalog.All.Select(p => new PermissionEntity
        {
            Key = p.Key, Description = p.Description
        }).ToList();
        entries.Insert(0, new PermissionEntity
        {
            Key = "Test.LongDescription",
            Description = string.Join(" ", Enumerable.Repeat("Long permission description", 8))
        });
        var view = new PermissionsView { DataContext = new { Permissions = entries } };
        view.Measure(new Size(400, 600));
        view.Arrange(new Rect(0, 0, 400, 600));
        view.UpdateLayout();
        view.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        var grid = Descendants(view).OfType<DataGrid>().Single();
        Assert(grid.Items.Count == entries.Count, "Permission catalogue did not load its entries.");
        Assert(grid.IsReadOnly && !grid.CanUserAddRows && !grid.CanUserDeleteRows,
            "Permission catalogue must remain read-only.");
        Assert(double.IsNaN(grid.RowHeight), "Permission rows must size automatically despite the shared fixed-height style.");
        var row = grid.ItemContainerGenerator.ContainerFromIndex(0) as DataGridRow;
        Assert(row != null && row.ActualHeight > grid.MinRowHeight,
            "A long permission description must expand the row instead of being clipped.");
        Console.WriteLine("PASS: permission catalogue opens, binds all entries, and expands wrapped rows");
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        yield return root;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (var child in Descendants(VisualTreeHelper.GetChild(root, i))) yield return child;
    }
    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    private sealed class BindingErrors : TraceListener
    {
        public List<string> Messages { get; } = new();
        public override void Write(string? message) { if (message != null) Messages.Add(message); }
        public override void WriteLine(string? message) { if (message != null) Messages.Add(message); }
    }
}
