using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Infrastructure.Entities;
using Infrastructure.Services;
using PresentationWpf.Services;
using PresentationWpf.ViewModels;
using PresentationWpf.Views;

internal static class UserManagerUiTests
{
    public static void Run()
    {
        var errors = new BindingErrors();
        PresentationTraceSources.DataBindingSource.Listeners.Add(errors);
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
        try
        {
            var role = new RoleEntity { Id = 1, RoleName = "Test", Description = "Test" };
            var linked = new SalesManagerEntity { Id = "M8", FullName = "Linked manager", UserId = 1, Active = true };
            var user = new UserEntity { Id = 1, UserName = "test", FirstName = "First", LastName = "Last", Password = "secret",
                IsManager = true, Address = "Address", SalesManager = linked,
                UserRoles = [new() { UserId = 1, RoleId = 1, Role = role }] };
            var service = new FakeUsers([user], [role]);
            var vm = new UserViewModel(service, new UserSessionService(null!, null!, null!), null!);
            var view = new UserView { DataContext = vm };
            Layout(view);
            vm.SelectedUser = user; Layout(view);
            Check(vm.CurrentUser.Id == 1 && vm.SelectedRole == role && vm.CurrentUser.IsManager,
                "Selecting a linked user must show the manager flag.");
            vm.CurrentUser.FirstName = "Draft"; vm.CurrentUser.Address = "Unsaved address";
            Check(user.FirstName == "First" && user.Address == "Address", "Unsaved draft edits changed the list entity.");
            vm.SelectedUser = null;
            vm.CurrentUser.UserName = "new"; vm.CurrentUser.FirstName = "New"; vm.CurrentUser.LastName = "User";
            vm.CurrentUser.Password = "password"; vm.SelectedRole = role;
            Check(vm.AddUserCommand.CanExecute(null), "Typing required fields must update command availability immediately.");
            vm.CurrentUser.IsManager = true;
            Layout(view);
            Check(Descendants(view).OfType<ComboBox>().All(c =>
                BindingOperations.GetBinding(c, ItemsControl.ItemsSourceProperty)?.Path.Path == "Roles"),
                "The user form must select a role without offering an existing-manager selector.");
            vm.SelectedUser = user; Layout(view);
            var grid = Descendants(view).OfType<DataGrid>().Single();
            Check(grid.Columns.OfType<DataGridCheckBoxColumn>().Any(c => ((Binding)c.Binding).Path.Path == "IsManager") &&
                grid.Columns.OfType<DataGridTextColumn>().Any(c => ((Binding)c.Binding).Path.Path == "Address"), "Missing manager/address user columns.");
            var directory = new ManagerInfoView { DataContext = new ManagerInfoViewModel(new FakeManagers([linked])) };
            ((ManagerInfoViewModel)directory.DataContext).LoadAsync().GetAwaiter().GetResult(); Layout(directory);
            Check(Descendants(directory).OfType<DataGrid>().Single().IsReadOnly &&
                Descendants(directory).OfType<Button>().All(b => b.Content?.ToString() is not ("Добавить" or "Изменить" or "Удалить" or "Сохранить")),
                "The old manager directory must not offer a second profile-editing path.");
            Check(errors.Messages.Count == 0, "User/manager views have binding errors: " + string.Join("\n", errors.Messages));
            SavePreview(view);
            Console.WriteLine("PASS: detached user draft, live validation, manager/address columns, automatic manager creation without a selector and single manager profile editor");
        }
        finally { PresentationTraceSources.DataBindingSource.Listeners.Remove(errors); }
    }
    private static void Layout(FrameworkElement view)
    {
        view.Measure(new Size(1450, 800)); view.Arrange(new Rect(0, 0, 1450, 800)); view.UpdateLayout();
        view.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }
    private static void SavePreview(FrameworkElement view)
    {
        var bitmap = new RenderTargetBitmap(1450, 800, 96, 96, PixelFormats.Pbgra32); bitmap.Render(view);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var output = System.IO.File.Create(System.IO.Path.Combine(AppContext.BaseDirectory, "user-manager-preview.png")); encoder.Save(output);
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject node)
    {
        yield return node;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
            foreach (var child in Descendants(VisualTreeHelper.GetChild(node, i))) yield return child;
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private sealed class BindingErrors : TraceListener
    {
        public List<string> Messages { get; } = [];
        public override void Write(string? message) { if (message != null) Messages.Add(message); }
        public override void WriteLine(string? message) => Write(message);
    }
    private sealed class FakeUsers(List<UserEntity> users, List<RoleEntity> roles) : UserService(null!)
    {
        public override Task<List<UserEntity>> GetAllAsync() => Task.FromResult(users);
        public override Task<List<RoleEntity>> GetRolesAsync() => Task.FromResult(roles);
    }
    private sealed class FakeManagers(List<SalesManagerEntity> managers) : ManagerService(null!)
    {
        public override Task<List<SalesManagerEntity>> GetManagersAsync() => Task.FromResult(managers);
    }
}
