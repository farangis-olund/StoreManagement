using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using Infrastructure.Constants;
using Infrastructure.Dtos;
using Infrastructure.Security;
using Infrastructure.Services;
using PresentationWpf.Services;
using PresentationWpf.ViewModels;
using PresentationWpf.Views;

internal static class CustomerBalanceUiTests
{
    public static void Run()
    {
        var permissions = new PermissionService();
        var service = new BalanceCustomers();
        var vm = new CustomerViewModel(service, null!, permissions, new CustomerFieldPermissionPolicy(permissions));
        var view = new CustomerView { DataContext = vm };
        var panel = (StackPanel)view.FindName("CustomerBalancePanel");
        var input = (TextBox)view.FindName("CustomerBalanceTextBox");
        var first = new Customer { Id = "A", FullName = "First", Debt = 9999 };
        var second = new Customer { Id = "B", FullName = "Second" };
        void Refresh()
        {
            view.Measure(new Size(900, 800)); view.Arrange(new Rect(0, 0, 900, 800)); view.UpdateLayout();
            view.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        }
        void Grants(params string[] rights) { permissions.SetPermissions(rights); Refresh(); }

        Grants(PermissionCatalog.Customers);
        vm.SelectedCustomer = first; Refresh();
        Check(panel.Visibility == Visibility.Collapsed && input.Text == "" && vm.CustomerBalance == null && service.Reads == 0,
            "Balance must stay hidden and must not be queried without its separate View right.");
        Grants(PermissionCatalog.Customers, CustomerPermissionCatalog.Debt.View);
        Check(panel.Visibility == Visibility.Collapsed && service.Reads == 0, "Debt View must not reveal the calculated balance.");
        Grants(PermissionCatalog.Customers, PermissionCatalog.CustomersBalanceView);
        Check(panel.Visibility == Visibility.Visible && vm.CustomerBalance == 1000 &&
            decimal.Parse(input.Text, input.Language.GetSpecificCulture()) == 1000,
            "The permitted balance must come from the summary service, not the draft debt field.");
        Check(input.IsReadOnly && input.GetBindingExpression(TextBox.TextProperty)!.ParentBinding.Mode == BindingMode.OneWay &&
            typeof(CustomerViewModel).GetProperty(nameof(vm.CustomerBalance))!.GetSetMethod(true)!.IsPrivate,
            "Balance must remain read-only in the control, binding and view model.");

        vm.SelectedCustomer = second; Refresh();
        Check(vm.CustomerBalance == -500.25m && decimal.Parse(input.Text, input.Language.GetSpecificCulture()) == -500.25m,
            "Switching customers must display the new balance, including credit and fractions.");
        var delayed = new TaskCompletionSource<decimal>();
        service.Next = delayed.Task;
        vm.SelectedCustomer = first; Refresh();
        Check(vm.CustomerBalance == null && input.Text == "", "The previous customer's value must clear while loading.");
        vm.SelectedCustomer = second; Refresh();
        delayed.SetResult(99999); Refresh();
        Check(vm.CustomerBalance == -500.25m, "A late reply for the previous client replaced the selected client's balance.");

        var revoked = new TaskCompletionSource<decimal>();
        service.Next = revoked.Task;
        vm.SelectedCustomer = first;
        Grants(PermissionCatalog.Customers);
        revoked.SetResult(99999); Refresh();
        Check(panel.Visibility == Visibility.Collapsed && vm.CustomerBalance == null && input.Text == "",
            "Revoking View must immediately clear the value and reject a pending response.");
        Grants(PermissionCatalog.CustomersBalanceView);
        Check(panel.Visibility == Visibility.Collapsed && vm.CustomerBalance == null, "Balance View requires access to Customers.");
        Grants(PermissionCatalog.Customers, PermissionCatalog.CustomersBalanceView);
        var reads = service.Reads;
        vm.SelectedCustomer = new Customer { FullName = "New", Debt = 10 }; Refresh();
        Check(service.Reads == reads && vm.CustomerBalance == null && input.Text == "", "An unsaved customer has no stored balance to load.");
        vm.SelectedCustomer = new Customer { Id = "ZERO" }; Refresh();
        Check(vm.CustomerBalance == 0 && decimal.Parse(input.Text, input.Language.GetSpecificCulture()) == 0, "A real zero must be displayed.");
        service.Next = Task.FromException<decimal>(new InvalidOperationException("Unavailable database"));
        vm.SelectedCustomer = first; Refresh();
        Check(vm.CustomerBalance == null && input.Text == "" && vm.CustomerBalanceError.Length > 0,
            "A failed load must not display zero or a stale balance.");
        vm.SelectedCustomer = null; Refresh();
        Check(vm.CustomerBalance == null && input.Text == "" && vm.CustomerBalanceError == "", "Clearing the client must clear the balance and error.");
        Console.WriteLine("PASS: customer balance is read-only, independently permission-controlled, refreshed on selection, and safe against stale replies, revocation, unsaved clients and failed reads");
    }

    private sealed class BalanceCustomers() : CustomerService(null!, null!, null!, null!, null!, null!, null!)
    {
        public int Reads;
        public Task<decimal>? Next;
        public override Task<decimal> GetBalanceForEditorAsync(string customerId)
        {
            Reads++;
            var next = Next; Next = null;
            return next ?? Task.FromResult(customerId == "A" ? 1000m : customerId == "B" ? -500.25m : 0m);
        }
    }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
