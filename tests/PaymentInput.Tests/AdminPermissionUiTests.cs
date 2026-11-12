using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;
using Infrastructure.Constants;
using Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore;
using PresentationWpf.Services;
using PresentationWpf.ViewModels;
using PresentationWpf.Views;

internal static class AdminPermissionUiTests
{
    public static void Run()
    {
        var permissions = new PermissionService();
        var provider = new ProbeProvider();
        var admin = new AdminViewModel(provider, null!, permissions);
        var view = new AdminView { DataContext = admin };
        var tabs = new (string Label, string Key, ICommand Command, Type? Type)[]
        {
            ("Бренды и фирмы", PermissionCatalog.AdminBrand, admin.OpenBrandsCommand, typeof(BrandViewModel)),
            ("Менеджеры", PermissionCatalog.AdminManagers, admin.OpenManagersCommand, typeof(ManagerViewModel)),
            ("Коэффициенты", PermissionCatalog.AdminCoefficients, admin.OpenCoefficientsCommand, typeof(CoefficientViewModel)),
            ("Бонусы", PermissionCatalog.AdminBonuses, admin.OpenBonusesCommand, typeof(BonusesViewModel)),
            ("Пользователи", PermissionCatalog.AdminUsers, admin.OpenUsersCommand, typeof(UserViewModel)),
            ("Справочники", PermissionCatalog.AdminReferences, admin.OpenReferencesCommand, typeof(ReferenceViewModel)),
            ("База данных", PermissionCatalog.AdminDatabase, admin.OpenDBCommand, typeof(DataBaseViewModel)),
            ("Очистка базы", PermissionCatalog.AdminCleanup, admin.OpenDeleteAllCommand, null)
        };
        foreach (var tab in tabs)
        {
            permissions.SetPermissions([PermissionCatalog.Admin, tab.Key]); Layout(view);
            var visible = Descendants(view).OfType<RadioButton>().Where(b => b.Visibility == Visibility.Visible).ToArray();
            Check(visible.Length == 1 && Equals(visible[0].Content, tab.Label), "A missing permission left another admin tab visible: " + tab.Label);
            Check(tab.Command.CanExecute(null), "Granted tab command was disabled.");
            if (tab.Type != null)
            {
                tab.Command.Execute(null); Layout(view);
                Check(admin.CurrentViewModel?.GetType() == tab.Type && visible[0].IsChecked == true, "The tab did not open or select its matching content.");
            }
            permissions.SetPermissions([PermissionCatalog.Admin]); Layout(view);
            Check(admin.CurrentViewModel == null && !tab.Command.CanExecute(null), "Revocation kept protected content or commands active.");
            var count = provider.Requests;
            if (tab.Command is IAsyncRelayCommand asyncCommand) asyncCommand.ExecuteAsync(null).GetAwaiter().GetResult();
            else tab.Command.Execute(null);
            Check(provider.Requests == count && admin.CurrentViewModel == null, "Direct Execute bypassed admin tab access.");
        }
        permissions.SetPermissions(tabs.Select(t => t.Key)); Layout(view);
        Check(!Descendants(view).OfType<RadioButton>().Any(b => b.Visibility == Visibility.Visible) && tabs.All(t => !t.Command.CanExecute(null)),
            "Tab rights bypassed the missing Admin module.");

        var references = new ReferenceViewModel(new NavigationService(provider), null!, null!, permissions);
        var referenceView = new ReferenceView { DataContext = references };
        var links = new (string Label, string Key, ICommand Command)[]
        {
            ("Организация", PermissionCatalog.AdminReferenceOrganization, references.OpenOrganizationCommand),
            ("Управление ролями", PermissionCatalog.AdminReferenceRoles, references.OpenRoleCommand),
            ("Доставщики", PermissionCatalog.AdminReferenceCouriers, references.OpenCourierCommand),
            ("Сборщики", PermissionCatalog.AdminReferencePickers, references.OpenAssignPickerCommand),
            ("Менеджеры", PermissionCatalog.AdminReferenceManagers, references.OpenManagerCommand),
            ("Уровни цен клиента", PermissionCatalog.AdminReferencePriceLevels, references.OpenLevelCommand),
            ("Назначение валюты", PermissionCatalog.AdminReferenceCurrency, references.OpenCurrencyCommand)
        };
        foreach (var link in links)
        {
            permissions.SetPermissions([PermissionCatalog.Admin, PermissionCatalog.AdminReferences, link.Key]); Layout(referenceView);
            var visible = Descendants(referenceView).OfType<Button>().Where(b => b.Visibility == Visibility.Visible).ToArray();
            Check(visible.Length == 1 && Equals(visible[0].Content, link.Label) && link.Command.CanExecute(null), "Reference link access failed: " + link.Label);
            permissions.SetPermissions([PermissionCatalog.Admin, link.Key]); Layout(referenceView);
            Check(!link.Command.CanExecute(null), "A reference link bypassed its parent tab.");
            var count = provider.Requests;
            if (link.Command is IAsyncRelayCommand asyncCommand) asyncCommand.ExecuteAsync(null).GetAwaiter().GetResult();
            else link.Command.Execute(null);
            Check(provider.Requests == count && references.SelectedReferenceView == null, "Direct Execute opened an unauthorized reference.");
        }
        permissions.SetPermissions([PermissionCatalog.Admin, PermissionCatalog.AdminReferences, PermissionCatalog.AdminReferenceOrganization]);
        references.OpenOrganizationCommand.Execute(null);
        Check(references.SelectedReferenceView is OrganizationInfoView, "Authorized reference navigation failed.");
        permissions.SetPermissions([PermissionCatalog.Admin, PermissionCatalog.AdminReferences]);
        Check(references.SelectedReferenceView == null, "Revoking a nested right kept reference content open.");

        using var db = new DatabaseContext(new DbContextOptionsBuilder<DatabaseContext>()
            .UseSqlServer("Server=unused;Database=unused;Integrated Security=True;TrustServerCertificate=True").Options);
        var database = new DataBaseViewModel(db, permissions);
        Check(!database.CanAccess && database.TablesA_G.Count == 0, "Database metadata loaded without permission.");
        database.AddCommand.Execute(null); database.UpdateCommand.Execute(null); database.DeleteCommand.Execute(null); database.RefreshCommand.Execute(null);
        permissions.SetPermissions([PermissionCatalog.Admin, PermissionCatalog.AdminDatabase]);
        Check(database.CanAccess && database.TablesA_G.Count > 0, "Granted database access did not load table names.");
        database.TableData.Add(new object());
        permissions.SetPermissions([PermissionCatalog.Admin]);
        Check(database.TableData.Count == 0 && !database.UpdateCommand.CanExecute(null) && !database.AddCommand.CanExecute(null), "Database revocation retained data or CRUD commands.");
        try { database.CreateNewRow(); throw new Exception("Unauthorized direct row creation was allowed."); }
        catch (UnauthorizedAccessException) { }
        Check(db.Database.GetDbConnection().State == System.Data.ConnectionState.Closed, "UI authorization checks unexpectedly connected to a database.");
        Console.WriteLine("PASS: all 8 admin tabs and 7 reference links, Brand selection binding, independent rights, direct-command rejection, immediate revocation and database CRUD guards");
    }
    private static void Layout(FrameworkElement view)
    {
        view.Measure(new Size(1450, 800)); view.Arrange(new Rect(0, 0, 1450, 800)); view.UpdateLayout();
        view.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject node)
    {
        yield return node;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
            foreach (var child in Descendants(VisualTreeHelper.GetChild(node, i))) yield return child;
    }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private sealed class ProbeProvider : IServiceProvider
    {
        public int Requests;
        public object? GetService(Type type)
        {
            Requests++;
            // Resolve views normally, but never start their viewmodels' constructor database loads.
            return typeof(FrameworkElement).IsAssignableFrom(type) ? Activator.CreateInstance(type) : RuntimeHelpers.GetUninitializedObject(type);
        }
    }
}
