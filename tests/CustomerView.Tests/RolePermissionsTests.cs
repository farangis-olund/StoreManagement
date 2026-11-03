using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Infrastructure.Constants;
using Infrastructure.Contexts;
using Infrastructure.Entities;
using Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using PresentationWpf.Converters;
using PresentationWpf.Dtos;
using PresentationWpf.ViewModels;
using PresentationWpf.Views;

internal static class RolePermissionsTests
{
    public static void Run(Application app)
    {
        app.Resources["GrayButton"] = new Style(typeof(Button));
        app.Resources["BoolToColorConverter"] = new BoolToColorConverter();
        app.Resources["BoolToVisibilityConverter2"] = new BoolToVisibilityConverter();
        var factory = new NoDatabaseFactory();
        var repository = new EmptyPermissionRepository(factory);
        var vm = new RoleManagementViewModel(new EmptyRoleRepository(factory), repository,
            new RolePermissionRepository(factory), new PermissionsView(), new PermissionsViewModel(repository),
            new PresentationWpf.Services.SessionPermissionRefresher(
                new PresentationWpf.Services.UserSessionService(null!, null!, null!),
                new RolePermissionRepository(factory), new PresentationWpf.Services.PermissionService()));
        vm.Permissions = new ObservableCollection<PermissionItem>(PermissionCatalog.All.Select((p, i) =>
            new PermissionItem { Id = i + 1, Key = p.Key, Description = p.Description, Parent = vm }));
        vm.IsPermissionsVisible = true;
        var hidden = vm.Permissions.Single(p => p.Key == PermissionCatalog.Admin);
        hidden.IsAssigned = true;
        vm.PermissionSearchText = "  лИмИт  ";
        var expectedLimitKeys = CustomerPermissionCatalog.BuyLimit.Definitions
            .Concat(CustomerPermissionCatalog.UnpaidPaymentLimit.Definitions)
            .Select(permission => permission.Key).Append(PermissionCatalog.OrdersExceedBuyLimit)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert(expectedLimitKeys.SetEquals(vm.FilteredPermissions.Cast<PermissionItem>().Select(p => p.Key)),
            "Russian description search should ignore case and surrounding spaces.");
        vm.AreAllSelected = true;
        Assert(vm.FilteredPermissions.Cast<PermissionItem>().All(p => p.IsAssigned), "Select all did not select every result.");
        Assert(hidden.IsAssigned && vm.Permissions.Count(p => p.IsAssigned) == expectedLimitKeys.Count + 1,
            "Select all changed hidden assignments.");
        vm.AreAllSelected = false;
        Assert(vm.Permissions.Count(p => p.IsAssigned) == 1 && hidden.IsAssigned, "Unselect all changed hidden assignments.");
        vm.PermissionSearchText = "customers.coefficients edit";
        Assert(vm.FilteredPermissions.Cast<PermissionItem>().Single().Key == CustomerPermissionCatalog.Coefficients.Edit,
            "Search must match keys and multiple terms.");
        vm.FilteredPermissions.Cast<PermissionItem>().Single().IsAssigned = true;
        Assert(vm.AreAllSelected, "Select all state did not follow individual selection.");
        vm.PermissionSearchText = "no matching permission";
        Assert(!vm.HasVisiblePermissions && !vm.AreAllSelected, "Empty results must disable select all.");
        vm.ClearPermissionSearchCommand.Execute(null);
        Assert(vm.FilteredPermissions.Cast<PermissionItem>().Count() == vm.Permissions.Count &&
            vm.Permissions.Count(p => p.IsAssigned) == 2, "Clearing search lost assignments.");

        // Replacing the loaded role's permission list must retain the current filter.
        vm.PermissionSearchText = "Customers.Coefficients";
        vm.Permissions = new ObservableCollection<PermissionItem>(vm.Permissions);
        Assert(vm.FilteredPermissions.Cast<PermissionItem>().Count() == 4, "Role reload lost the search filter.");
        vm.ClearPermissionSearchCommand.Execute(null);
        vm.Permissions.Insert(0, new PermissionItem
        {
            Key = "Test.Long", Parent = vm,
            Description = string.Join(" ", Enumerable.Repeat("Длинное название права доступа", 6))
        });
        vm.Roles.Add(new RoleDto { RoleName = "Администратор" });
        vm.IsPermissionManagerVisible = true;
        var view = new RoleManagementView { DataContext = vm };
        var scroll = (ScrollViewer)view.FindName("PermissionScroll");
        var list = (ItemsControl)view.FindName("PermissionList");
        var search = (TextBox)view.FindName("PermissionSearchBox");
        void Layout(double width, double height)
        {
            view.Measure(new Size(width, height));
            view.Arrange(new Rect(0, 0, width, height));
            view.UpdateLayout();
            view.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        }

        Layout(1600, 1000);
        Assert(scroll.ActualHeight > 400 && list.ActualWidth > 295, "Permissions still use a fixed narrow/short panel.");
        var longText = Descendants(list).OfType<TextBlock>().First(t => t.Text == vm.Permissions[0].Description);
        Assert(longText.TextWrapping == TextWrapping.Wrap && longText.ActualHeight > 25, "Long labels did not wrap.");
        search.Text = "нет такого права";
        Layout(1600, 1000);
        Assert(list.Items.Count == 0 && !vm.HasVisiblePermissions, "Search TextBox is not connected to the filter.");
        vm.ClearPermissionSearchCommand.Execute(null);
        Layout(1000, 600);
        Assert(scroll.ViewportWidth > 0 && scroll.ExtentWidth <= scroll.ViewportWidth + 1,
            "Permission labels require horizontal scrolling at a smaller window size.");
        scroll.ScrollToEnd();
        Layout(1000, 600);
        Assert(scroll.VerticalOffset > 0 && Math.Abs(scroll.VerticalOffset - scroll.ScrollableHeight) < 1,
            "The last permission cannot be reached by scrolling.");
        var narrowWidth = list.ActualWidth;
        vm.IsPermissionManagerVisible = false;
        Layout(1000, 600);
        Assert(list.ActualWidth > narrowWidth, "Hiding the catalogue did not release space to the permissions list.");

        vm.IsPermissionManagerVisible = true;
        Layout(1600, 1000);
        scroll.ScrollToTop();
        Layout(1600, 1000);
        var bitmap = new RenderTargetBitmap(1600, 1000, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(view);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var output = File.Create(Path.Combine(AppContext.BaseDirectory, "role-permissions-preview.png"));
        encoder.Save(output);
        Console.WriteLine("PASS: role permission search, filtered select-all, preserved assignments, wrapping, resizing, and scrolling to the last item");
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    private sealed class NoDatabaseFactory : IDbContextFactory<DatabaseContext>
    {
        public DatabaseContext CreateDbContext() => throw new InvalidOperationException("UI tests must not access a database.");
    }
    private sealed class EmptyRoleRepository(IDbContextFactory<DatabaseContext> factory) : RoleRepository(factory)
    {
        public override Task<IEnumerable<RoleEntity>> GetAllAsync() => Task.FromResult(Enumerable.Empty<RoleEntity>());
    }
    private sealed class EmptyPermissionRepository(IDbContextFactory<DatabaseContext> factory) : PermissionRepository(factory)
    {
        public override Task<IEnumerable<PermissionEntity>> GetAllAsync() => Task.FromResult(Enumerable.Empty<PermissionEntity>());
    }
}
