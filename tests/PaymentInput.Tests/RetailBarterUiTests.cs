using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;
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

internal static class RetailBarterUiTests
{
    public static void Run()
    {
        var permissions = new PermissionService();
        permissions.SetPermissions([PermissionCatalog.Orders, PermissionCatalog.OrdersBarter]);
        CheckCustomerAndEntryFlow(permissions);
        var database = new PendingDatabase();
        var orders = new CapturedOrders(database, permissions);
        var plans = new PendingPlans(permissions);
        var vm = new BarterRetail(permissions, orders, database, plans);
        var customer = new Customer { Id = "BARTER", FullName = "Barter client", OfficialCustomer = true,
            BuyLimit = 1, UnpaidPaymentLimit = 1 };
        vm.AllCustomers.Add(customer);
        Set(vm, "_selectedCustomer", customer);
        vm.IsOrderBlockedByUnpaidPaymentLimit = true;
        vm.AvailableRestriction = -1000;
        vm.StartMonthlyPlanRefresh();
        try
        {
            var row = (ProductModel)new Product { ArticleNumber = "BARTER-ITEM", PriceLevel1 = 20, Quentity = 50 };
            row.CustomerPriceLevel = 1;
            row.CustomerDiscountPercentage = 20;
            vm.SelectedProductList.Add(row);
            var view = new RetailView { DataContext = vm };
            Layout(view);
            var checkbox = (CheckBox)view.FindName("BarterCheckBox");
            checkbox.IsChecked = true;
            Layout(view);
            Check(vm.IsBarter && checkbox.IsChecked == true && database.Calls == 0,
                "Enabling barter must not query unpaid orders, finance or debt eligibility.");
            Check(vm.AddToChartCommand.CanExecute(row) && !vm.SaveOrderCommand.CanExecute(null),
                "Barter must permit adding while finance is loading but require an entered price before saving.");
            row.OrderQuentity = 5;
            Check(row.OrderQuentity == 5 && row.BarterPriceSom == null && row.EffectivePriceSom == null && vm.TotalSum == 0,
                "An empty barter price must not fall back to the database price or contribute to totals.");
            row.BarterPriceSom = 20;
            Check(vm.TotalSum == 100 && vm.Warnings == 0,
                "Barter must ignore limits and use a final manual price without applying the ordinary-sale discount.");

            plans.Completion.SetResult(new OfficialMonthlyPlanRetailState(DateTime.Today, customer.Id, true, 1, 1, 1));
            Layout(view);
            Set(vm, "_customerBalance", 5000m);
            vm.AvailableRestriction = -4000;
            Check(!vm.OfficialOrder && row.OrderQuentity == 5 && vm.Warnings == 0,
                "A delayed monthly/finance refresh must not mark barter official or shrink the cart.");
            var extra = (ProductModel)new Product { ArticleNumber = "EXTRA", PriceLevel1 = 30, Quentity = 10 };
            extra.CustomerPriceLevel = 1;
            vm.AddToChartCommand.Execute(extra);
            Check(vm.SelectedProductList.Contains(extra) && extra.BarterPriceSom == null && extra.EffectivePriceSom == null,
                "Products added in barter mode must start with an empty price.");
            vm.RemoveProductCommand.Execute(extra);

            var grid = (DataGrid)view.FindName("CartGrid");
            var priceColumn = grid.Columns.Single(c => Equals(c.Header, "Цена"));
            grid.SelectedItem = row;
            grid.CurrentCell = new DataGridCellInfo(row, priceColumn);
            Check(grid.BeginEdit(), "The barter price cell must enter edit mode.");
            Layout(view);
            var editor = Descendants<TextBox>(priceColumn.GetCellContent(row)).Single();
            Check(!editor.IsReadOnly, "The barter price editor is still read-only.");
            void Enter(string text)
            {
                editor.Text = text;
                editor.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
                Layout(view);
            }
            Enter("17,");
            Check(editor.Text == "17," && row.BarterPriceSom == 17, "Typing a decimal separator must retain the entered text.");
            Enter("17,25");
            Check(row.BarterPriceSom == 17.25m && vm.TotalSum == 86.25m && row.OrderQuentity == 5,
                "A typed barter price must immediately update the row and cart totals without reducing quantity.");
            foreach (var invalid in new[] { "", "wrong price", "-5" })
            {
                Enter(invalid);
                Check(row.BarterPriceSom == null && !vm.SaveOrderCommand.CanExecute(null),
                    "An invalid price must not silently retain and save the previous value.");
            }
            Enter("17.257");
            Check(row.BarterPriceSom == 17.26m && vm.TotalSum == 86.30m, "Price rounding must match the stored monetary precision.");
            Enter("17.25");
            grid.CommitEdit(DataGridEditingUnit.Cell, true);
            grid.CommitEdit(DataGridEditingUnit.Row, true);
            Layout(view);
            Check(vm.SaveOrderCommand.CanExecute(null) && vm.Warnings == 0,
                "An edited valid barter price must allow checkout without courier/storekeeper or financial checks.");
            var saving = vm.SaveOrderCommand.ExecuteAsync(null);
            Check(!saving.IsCompleted && orders.Captured is { IsBarter: true, OfficialOrder: false } captured &&
                captured.OrderDetails.Single().Price == 17.25m && captured.OrderDetails.Single().Quentity == 5 &&
                captured.OrderDetails.Sum(d => d.Price * d.Quentity) == 86.25m && database.Calls == 0,
                "Checkout must reach persistence with the edited price and quantity, bypassing every financial gate.");
            vm.IsBarter = false;
            Layout(view);
            Check(vm.IsBarter && !checkbox.IsEnabled, "The order mode must not change during an active checkout.");
            // The fake persistence task stays pending so no success dialog, actual order, payment or print job is produced.
        }
        finally { vm.StopMonthlyPlanRefresh(); }

        var restored = new BarterRetail(permissions, orders, database, null);
        var ordinaryCustomer = new Customer { Id = "ORDINARY", FullName = "Ordinary", BuyLimit = 50 };
        restored.AllCustomers.Add(ordinaryCustomer);
        Set(restored, "_selectedCustomer", ordinaryCustomer);
        Set(restored, "_customerBalance", 0m);
        restored.AvailableRestriction = 50;
        restored.IsBarter = true;
        var ordinaryRow = (ProductModel)new Product { ArticleNumber = "RESTORE", PriceLevel1 = 20, Quentity = 50 };
        ordinaryRow.CustomerPriceLevel = 1;
        restored.AddToChartCommand.Execute(ordinaryRow);
        ordinaryRow.BarterPriceSom = 17.25m;
        ordinaryRow.OrderQuentity = 5;
        restored.IsBarter = false;
        Check(ordinaryRow.BarterPriceSom == null && ordinaryRow.EffectivePriceSom == 20 && ordinaryRow.OrderQuentity == 2 &&
            restored.TotalSum == 40 && restored.Warnings == 1,
            "Disabling barter must restore ordinary prices and financial limits.");
        restored.IsBarter = true;
        ordinaryRow.OrderQuentity = 5;
        permissions.SetPermissions([PermissionCatalog.Orders]);
        Check(!restored.IsBarter && ordinaryRow.OrderQuentity == 2 && restored.Warnings == 2,
            "Revoking the barter right must restore ordinary-sale rules immediately.");
        restored.IsBarter = true;
        Check(!restored.IsBarter, "Barter must still require its explicit permission.");
        Console.WriteLine("PASS: barter bypasses debt/payment/credit/plan gates, editable prices accept both separators and persist exactly, ordinary rules return on disabling or revocation");
    }

    private static void CheckCustomerAndEntryFlow(PermissionService permissions)
    {
        var database = new PendingDatabase();
        var vm = new BarterRetail(permissions, new CapturedOrders(database, permissions), database, null);
        var view = new RetailView { DataContext = vm };
        Layout(view);
        var checkbox = (CheckBox)view.FindName("BarterCheckBox");
        Check(!checkbox.IsEnabled, "Barter must be disabled until an order customer is selected.");
        vm.IsBarter = true;
        Check(!vm.IsBarter, "Directly setting barter must not bypass customer selection.");
        var firstCustomer = new Customer { Id = "FLOW-A", FullName = "First" };
        var secondCustomer = new Customer { Id = "FLOW-B", FullName = "Second" };
        vm.AllCustomers.Add(firstCustomer); vm.AllCustomers.Add(secondCustomer);
        vm.SelectedCustomer = firstCustomer; Layout(view);
        Check(checkbox.IsEnabled, "Selecting a customer must enable the permitted barter option.");
        vm.IsBarter = true;
        vm.SelectedCustomer = secondCustomer; Layout(view);
        Check(!vm.IsBarter && checkbox.IsChecked == false && checkbox.IsEnabled,
            "Switching to another client must uncheck barter.");
        vm.IsBarter = true;
        vm.SelectedCustomer = null; Layout(view);
        Check(!vm.IsBarter && !checkbox.IsEnabled, "Clearing the client must uncheck and disable barter.");
        vm.SelectedCustomer = firstCustomer;
        vm.IsBarter = true;
        var rows = new[] { "FLOW-1", "FLOW-2" }.Select(id =>
        {
            var product = (ProductModel)new Product { ArticleNumber = id, ProductName = "Товар", Quentity = 20, PriceLevel1 = 99 };
            product.CustomerPriceLevel = 1;
            vm.AddToChartCommand.Execute(product);
            return product;
        }).ToArray();
        var grid = (DataGrid)view.FindName("CartGrid");
        var quantity = grid.Columns.Single(c => Equals(c.Header, "Кол-во Заказа"));
        var price = grid.Columns.Single(c => Equals(c.Header, "Цена"));
        var stock = grid.Columns.Single(c => Equals(c.Header, "Кол-во"));
        var host = new Window { Content = view, Width = 1450, Height = 900, ShowActivated = false, ShowInTaskbar = false,
            Left = -20000, Top = -20000, WindowStartupLocation = WindowStartupLocation.Manual };
        try
        {
            host.Show(); Layout(view);
            Check(stock.Visibility == Visibility.Collapsed && quantity.DisplayIndex < price.DisplayIndex && !price.IsReadOnly,
                "Barter must hide stock quantity and place order quantity before editable price.");
            Check(((SolidColorBrush)checkbox.Foreground).Color == (Color)ColorConverter.ConvertFromString("#7E22CE"),
                "The Barter label must be purple.");
            typeof(RetailView).GetMethod("FocusCartEditor", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(view, [rows[0], quantity]);
            Layout(view);
            TextBox Editor()
            {
                var content = grid.CurrentColumn.GetCellContent(grid.CurrentItem);
                return content as TextBox ?? Descendants<TextBox>(content).Single();
            }
            void Enter()
            {
                grid.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(view)!, Environment.TickCount, Key.Enter)
                    { RoutedEvent = Keyboard.PreviewKeyDownEvent });
                Layout(view);
            }
            Editor().Text = "3";
            Enter();
            Check(rows[0].OrderQuentity == 3 && grid.CurrentCell.Item == rows[0] && grid.CurrentColumn == price && Editor().Text == "",
                "Enter in quantity must commit it and focus the same row's empty price.");
            Editor().Text = "12,50";
            Enter();
            Check(rows[0].BarterPriceSom == 12.5m && grid.CurrentCell.Item == rows[1] && grid.CurrentColumn == quantity,
                "Enter in price must commit the manual amount and focus the next row's quantity.");
            Editor().Text = "2"; Enter();
            Check(rows[1].OrderQuentity == 2 && grid.CurrentColumn == price, "The quantity-to-price flow must work for following rows.");
            Editor().Text = "invalid"; Enter();
            Check(grid.CurrentCell.Item == rows[1] && grid.CurrentColumn == price && !vm.SaveOrderCommand.CanExecute(null),
                "An invalid price must stay editable and prevent checkout.");
            Editor().Text = "5"; Enter();
            Check(vm.TotalSum == 47.5m, "Keyboard entry must update the exact manually entered barter total.");
            var row = (DataGridRow)grid.ItemContainerGenerator.ContainerFromItem(rows[0]);
            Check(((SolidColorBrush)row.Background).Color == (Color)ColorConverter.ConvertFromString("#F3E8FF"),
                "Barter cart rows must be light purple.");
            var folder = Path.Combine(AppContext.BaseDirectory, "test-images"); Directory.CreateDirectory(folder);
            var bitmap = new RenderTargetBitmap(1450, 900, 96, 96, PixelFormats.Pbgra32); bitmap.Render(view);
            var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
            using (var file = File.Create(Path.Combine(folder, "barter-cart.png"))) png.Save(file);
            vm.IsBarter = false; Layout(view);
            Check(stock.Visibility == Visibility.Visible && price.DisplayIndex < quantity.DisplayIndex && price.IsReadOnly,
                "Leaving barter must restore the ordinary column layout and read-only catalog price.");
        }
        finally { host.Content = null; host.Close(); vm.StopMonthlyPlanRefresh(); }
        Console.WriteLine("PASS: customer-gated barter resets on client changes; empty prices, purple cart, hidden stock, column order and Enter quantity/price/next-row entry");
    }

    private static void Set(RetailViewModel vm, string field, object value) => typeof(RetailViewModel)
        .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(vm, value);
    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T typed) yield return typed;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }
    private static void Layout(FrameworkElement view)
    {
        view.Measure(new Size(1450, 900)); view.Arrange(new Rect(0, 0, 1450, 900)); view.UpdateLayout();
        view.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }
    private sealed class PendingDatabase : IDbContextFactory<DatabaseContext>
    {
        public int Calls;
        public DatabaseContext CreateDbContext() => throw new Exception("No working database is used by this UI test.");
        public Task<DatabaseContext> CreateDbContextAsync(CancellationToken ct = default)
        { Calls++; return new TaskCompletionSource<DatabaseContext>().Task; }
    }
    private sealed class CapturedOrders(PendingDatabase database, PermissionService permissions)
        : OrderService(null!, null!, null!, null!, database, permissions)
    {
        public OrderEntity? Captured;
        public override Task<OrderEntity?> AddOrderAsync(OrderEntity order)
        { Captured = order; return new TaskCompletionSource<OrderEntity?>().Task; }
    }
    private sealed class PendingPlans(PermissionService permissions) : OfficialMonthlyPlanService(null!, permissions)
    {
        public TaskCompletionSource<OfficialMonthlyPlanRetailState> Completion { get; } = new();
        public override Task<OfficialMonthlyPlanRetailState> GetRetailStateAsync(string? id, DateTime date) => Completion.Task;
    }
    private sealed class BarterRetail(PermissionService permissions, OrderService orders, PendingDatabase database, OfficialMonthlyPlanService? plans)
        : RetailViewModel(null!, new UserSessionService(null!, null!, null!), orders,
            new CustomerService(null!, database, null!, null!, null!, null!, null!), null!, null!, null!, null!, null!, null!,
            new CustomerFinanceService(database), permissions, null!, plans)
    {
        public int Warnings;
        protected override void ShowBuyLimitReachedMessage() => Warnings++;
        protected override void ShowMonthlyPlanLimitReachedMessage() => Warnings++;
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
