using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using Infrastructure.Constants;
using Infrastructure.Dtos;
using PresentationWpf.Dtos;
using PresentationWpf.Services;
using PresentationWpf.ViewModels;
using PresentationWpf.Views;

internal static class RetailLimitUiTests
{
    public static void Run()
    {
        CheckInvalidQuantityHighlight();
        var permissions = new PermissionService();
        var vm = Ready(permissions, 50);
        var product = Add(vm, "0986494153", 19.64m);
        var view = new RetailView { DataContext = vm, Width = 1280, Height = 850 };
        Layout(view);
        var cart = (DataGrid)view.FindName("CartGrid");
        var quantityColumn = cart.Columns.OfType<DataGridTextColumn>().Single(x =>
            (x.Binding as Binding)?.Path.Path == nameof(ProductModel.OrderQuentity));
        cart.SelectedItem = product;
        cart.CurrentCell = new DataGridCellInfo(product, quantityColumn);
        Check(cart.BeginEdit(), "The cart quantity must be editable.");
        Layout(view);
        var editor = (TextBox)quantityColumn.GetCellContent(product);
        editor.Text = "10";
        editor.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
        cart.CommitEdit(DataGridEditingUnit.Cell, true);
        cart.CommitEdit(DataGridEditingUnit.Row, true);
        Layout(view);
        Check(product.OrderQuentity == 2 && vm.TotalSum == 39.28m && vm.Warnings == 1,
            "A committed quantity of 10 at 19.64 must be reduced to 2 under the displayed limit of 50.");
        Check(((TextBlock)quantityColumn.GetCellContent(product)).Text == "2",
            "The cart must display the corrected quantity, not the rejected input.");
        Check(vm.LastWarningTotal == 39.28m && vm.AvailableRestriction == 50,
            "The warning must follow correction and must not change the displayed customer restriction.");

        var cheap = Add(vm, "CHEAP", 5);
        cheap.OrderQuentity = 2;
        product.OrderQuentity = 3;
        Check(product.OrderQuentity == 2 && cheap.OrderQuentity == 2 && vm.TotalSum == 49.28m && vm.Warnings == 2,
            "Only the edited row should be reduced, accounting for the other cart rows.");
        product.OrderQuentity = 1;
        cheap.OrderQuentity = 6;
        Check(vm.TotalSum == 49.64m && vm.Warnings == 2, "Reducing a row must release allowance for other products.");

        var exact = Ready(new PermissionService(), 50);
        var exactProduct = Add(exact, "EXACT", 25);
        exactProduct.OrderQuentity = 2;
        Check(exact.TotalSum == 50 && exact.Warnings == 0, "An order exactly at the limit must be allowed.");
        exactProduct.OrderQuentity = 3;
        Check(exactProduct.OrderQuentity == 2 && exact.TotalSum == 50 && exact.Warnings == 1,
            "Exceeding an exact boundary must restore the permitted quantity.");
        var blocked = (ProductModel)new Product { ArticleNumber = "BLOCKED", PriceLevel1 = 1, Quentity = 100 };
        exact.AddToChartCommand.Execute(blocked);
        Check(!exact.SelectedProductList.Contains(blocked) && exact.Warnings == 2,
            "Adding another product when the allowance is exhausted must show a warning.");

        foreach (var available in new[] { 50m, 0m, -10m })
        {
            var noRoom = Ready(new PermissionService(), available);
            var expensive = Add(noRoom, "EXPENSIVE", 60);
            expensive.OrderQuentity = 1;
            Check(expensive.OrderQuentity == 0 && noRoom.TotalSum == 0 && noRoom.Warnings == 1,
                "An unaffordable unit or an exhausted restriction must reduce quantity to zero.");
        }

        var privilegedPermissions = new PermissionService();
        privilegedPermissions.SetPermissions([PermissionCatalog.OrdersExceedBuyLimit]);
        var privileged = Ready(privilegedPermissions, 50);
        var privilegedProduct = Add(privileged, "OVERRIDE", 19.64m);
        privilegedProduct.OrderQuentity = 10;
        Check(privilegedProduct.OrderQuentity == 10 && privileged.TotalSum == 196.40m && privileged.Warnings == 0,
            "The explicit sale-over-limit permission must allow the requested quantity.");
        privilegedPermissions.SetPermissions([]);
        Check(privilegedProduct.OrderQuentity == 2 && privileged.TotalSum == 39.28m && privileged.Warnings == 1,
            "Revoking the permission must immediately reduce an existing cart.");
        privileged.AvailableRestriction = 30;
        Check(privilegedProduct.OrderQuentity == 1 && privileged.TotalSum == 19.64m && privileged.Warnings == 2,
            "Refreshing a lower available restriction must recheck an existing cart.");

        var multiple = Ready(new PermissionService(), 50);
        var first = Add(multiple, "FIRST", 30);
        first.OrderQuentity = 1;
        var last = Add(multiple, "LAST", 20);
        last.OrderQuentity = 1;
        multiple.AvailableRestriction = 10;
        Check(first.OrderQuentity == 0 && last.OrderQuentity == 0 && multiple.TotalSum == 0 && multiple.Warnings == 1,
            "A lower restriction must correct all necessary rows and show one warning.");

        var separate = Ready(new PermissionService(), 50, buyLimit: 30);
        var separateProduct = Add(separate, "ORDER-LIMIT", 19.64m);
        separateProduct.OrderQuentity = 2;
        Check(separateProduct.OrderQuentity == 1 && separate.TotalSum == 19.64m && separate.Warnings == 1,
            "A stricter separate order limit must still be enforced.");
        Console.WriteLine("PASS: cart edit 10 -> 2 under displayed restriction 50; multiple rows, exact/zero boundaries, notifications, override, revocation and refreshed limits");
    }

    private static void CheckInvalidQuantityHighlight()
    {
        var vm = Ready(new PermissionService(), 50);
        var product = Add(vm, "ZERO-QUANTITY", 5);
        var view = new RetailView { DataContext = vm, Width = 1280, Height = 850 };
        Layout(view);
        var cart = (DataGrid)view.FindName("CartGrid");
        var row = (DataGridRow)cart.ItemContainerGenerator.ContainerFromItem(product);
        var article = (TextBlock)cart.Columns.OfType<DataGridTextColumn>().Single(c =>
            (c.Binding as Binding)?.Path.Path == nameof(ProductModel.ArticleNumber)).GetCellContent(product);
        var quantity = (TextBlock)cart.Columns.OfType<DataGridTextColumn>().Single(c =>
            (c.Binding as Binding)?.Path.Path == nameof(ProductModel.OrderQuentity)).GetCellContent(product);
        product.IsInvalid = true; Layout(view);
        Check(row.FontWeight == FontWeights.Bold && article.FontWeight == FontWeights.Bold && quantity.FontWeight == FontWeights.Bold,
            "A zero-quantity warning must immediately make the cart row and its quantity bold.");
        Check(row.Foreground is SolidColorBrush brush && brush.Color != Color.FromRgb(255, 59, 48) && row.ToolTip != null,
            "The quantity warning must explain the zero quantity without turning the row red.");
        product.OrderQuentity = 1; Layout(view);
        Check(!product.IsInvalid && row.FontWeight != FontWeights.Bold && quantity.FontWeight != FontWeights.Bold && row.ToolTip == null,
            "Correcting the quantity must immediately remove the warning, bold font and tooltip.");
        product.IsClearance = true; product.OrderQuentity = 0; product.IsInvalid = true; Layout(view);
        Check(article.FontWeight == FontWeights.Bold && (row.Background is not SolidColorBrush rowBrush || rowBrush.Color != Color.FromRgb(255, 205, 210)),
            "A clearance article must become bold for zero quantity without restoring a red background.");
        product.OrderQuentity = 1; Layout(view);
        Check(!product.IsInvalid && row.FontWeight != FontWeights.Bold && article.FontWeight == FontWeights.SemiBold,
            "Correcting quantity must restore the article's ordinary clearance styling.");
        var outOfStock = (ProductModel)new Product { ArticleNumber = "OUT-OF-STOCK", Quentity = 0 };
        outOfStock.IsInvalid = true; outOfStock.OrderQuentity = 5;
        Check(outOfStock.OrderQuentity == 0 && outOfStock.IsInvalid, "A correction capped to zero stock must retain the warning.");
        Console.WriteLine("PASS: zero-quantity cart rows use bold text instead of red; correction resets the live warning, preserving clearance styling and validation for quantities capped to zero");
    }

    private static LimitRetailViewModel Ready(PermissionService permissions, decimal available, decimal? buyLimit = null)
    {
        var vm = new LimitRetailViewModel(permissions);
        // Avoid customer loading and database side effects; supply the completed finance state.
        var customer = new Customer { Id = "LIMIT-TEST", FullName = "Клиент", BuyLimit = buyLimit };
        vm.AllCustomers.Add(customer);
        typeof(RetailViewModel).GetField("_selectedCustomer", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(vm, customer);
        typeof(RetailViewModel).GetField("_customerBalance", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(vm, 0m);
        vm.AvailableRestriction = available;
        return vm;
    }

    private static ProductModel Add(LimitRetailViewModel vm, string article, decimal price)
    {
        var product = (ProductModel)new Product { ArticleNumber = article, PriceLevel1 = price, Quentity = 100, ExchangeRate = 11 };
        product.CustomerPriceLevel = 1;
        // The normal Add command rejects rows at a zero allowance; inserting a zero-quantity row
        // also checks the quantity guard against direct edits of an existing cart.
        if (vm.AvailableRestriction > 0)
            vm.AddToChartCommand.Execute(product);
        else
            vm.SelectedProductList.Add(product);
        return product;
    }

    private static void Layout(FrameworkElement view)
    {
        view.Measure(new Size(1280, 850));
        view.Arrange(new Rect(0, 0, 1280, 850));
        view.UpdateLayout();
        view.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }

    private sealed class LimitRetailViewModel(PermissionService permissions) : RetailViewModel(
        null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, permissions, null!)
    {
        public int Warnings { get; private set; }
        public decimal LastWarningTotal { get; private set; }
        protected override void ShowBuyLimitReachedMessage()
        {
            Warnings++;
            LastWarningTotal = TotalSum;
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
