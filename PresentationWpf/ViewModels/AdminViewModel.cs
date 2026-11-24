using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Infrastructure.Constants;
using Infrastructure.Security;
using Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using PresentationWpf.Services;
using System.ComponentModel;
using System.Windows;

namespace PresentationWpf.ViewModels;

public partial class AdminViewModel : ObservableObject
{
    private readonly IServiceProvider _serviceProvider;
    private readonly CleanService _cleanService;
    private readonly AdminAccessPolicy _access;
    private string? _currentPermission;
    [ObservableProperty] private object? currentViewModel;
    [ObservableProperty] private bool isUsersSelected;
    [ObservableProperty] private bool isCoefficientsSelected;
    [ObservableProperty] private bool isBonusesSelected;
    [ObservableProperty] private bool isClearanceSelected;
    [ObservableProperty] private bool isManagersSelected;
    [ObservableProperty] private bool isBrandsSelected;
    [ObservableProperty] private bool isReferencesSelected;
    [ObservableProperty] private bool isDeleteDataSelected;
    [ObservableProperty] private bool isDBSelected;

    public AdminViewModel(IServiceProvider serviceProvider, CleanService cleanService, PermissionService permissions)
    {
        _serviceProvider = serviceProvider; _cleanService = cleanService;
        _access = new AdminAccessPolicy(permissions);
        PropertyChangedEventManager.AddHandler(permissions, (_, _) => RefreshPermissions(), nameof(PermissionService.CurrentPermissions));
    }

    public bool CanViewBrands => _access.CanOpen(PermissionCatalog.AdminBrand);
    public bool CanViewManagers => _access.CanOpen(PermissionCatalog.AdminManagers);
    public bool CanViewCoefficients => _access.CanOpen(PermissionCatalog.AdminCoefficients);
    public bool CanViewBonuses => _access.CanOpen(PermissionCatalog.AdminBonuses);
    public bool CanViewClearance => _access.CanOpen(PermissionCatalog.AdminClearance);
    public bool CanViewUsers => _access.CanOpen(PermissionCatalog.AdminUsers);
    public bool CanViewReferences => _access.CanOpen(PermissionCatalog.AdminReferences);
    public bool CanViewDatabase => _access.CanOpen(PermissionCatalog.AdminDatabase);
    public bool CanCleanDatabase => _access.CanOpen(PermissionCatalog.AdminCleanup);

    [RelayCommand(CanExecute = nameof(CanViewUsers))]
    private void OpenUsers() => Open<UserViewModel>(PermissionCatalog.AdminUsers, () => IsUsersSelected = true);
    [RelayCommand(CanExecute = nameof(CanViewCoefficients))]
    private void OpenCoefficients() => Open<CoefficientViewModel>(PermissionCatalog.AdminCoefficients, () => IsCoefficientsSelected = true);
    [RelayCommand(CanExecute = nameof(CanViewBonuses))]
    private void OpenBonuses() => Open<BonusesViewModel>(PermissionCatalog.AdminBonuses, () => IsBonusesSelected = true);
    [RelayCommand(CanExecute = nameof(CanViewClearance))]
    private void OpenClearance() => Open<ClearanceViewModel>(PermissionCatalog.AdminClearance, () => IsClearanceSelected = true);
    [RelayCommand(CanExecute = nameof(CanViewManagers))]
    private void OpenManagers() => Open<ManagerViewModel>(PermissionCatalog.AdminManagers, () => IsManagersSelected = true);
    [RelayCommand(CanExecute = nameof(CanViewBrands))]
    private void OpenBrands() => Open<BrandViewModel>(PermissionCatalog.AdminBrand, () => IsBrandsSelected = true);
    [RelayCommand(CanExecute = nameof(CanViewReferences))]
    private void OpenReferences() => Open<ReferenceViewModel>(PermissionCatalog.AdminReferences, () => IsReferencesSelected = true);
    [RelayCommand(CanExecute = nameof(CanViewDatabase))]
    private void OpenDB() => Open<DataBaseViewModel>(PermissionCatalog.AdminDatabase, () => IsDBSelected = true);

    private void Open<T>(string permission, Action select) where T : class
    {
        // Check inside the command too: calling Execute directly must not bypass CanExecute.
        if (!_access.CanOpen(permission)) return;
        var viewModel = _serviceProvider.GetRequiredService<T>();
        ResetSelection(); select();
        _currentPermission = permission;
        CurrentViewModel = viewModel;
    }

    [RelayCommand(CanExecute = nameof(CanCleanDatabase))]
    private async Task OpenDeleteAll()
    {
        if (!CanCleanDatabase) return;
        var result = MessageBox.Show(
            "Будут удалены все заказы, их детали, платежи клиентов и доставщиков, возвраты и их детали, " +
            "обмены магазинов, движения склада, журнал обновлений и ошибки импорта.\n\n" +
            "Клиенты, товары и их текущие остатки, пользователи, роли, права, настройки, расходы и сводки перемещений останутся.\n\n" +
            "Перед очисткой создайте резервную копию. Продолжить?",
            "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (result != MessageBoxResult.Yes || !CanCleanDatabase) return;
        try
        {
            await _cleanService.DeleteAllAsync();
            MessageBox.Show("Очистка завершена. Выбранные категории данных удалены; справочники и настройки сохранены.", "Готово");
            if (CanViewDatabase && CurrentViewModel is DataBaseViewModel databaseView) databaseView.RefreshCommand.Execute(null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            MessageBox.Show($"Очистка отменена, изменения не сохранены.\n\nПричина: {ex.GetBaseException().Message}",
                "Ошибка очистки", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RefreshPermissions()
    {
        OnPropertyChanged(nameof(CanViewBrands)); OnPropertyChanged(nameof(CanViewManagers));
        OnPropertyChanged(nameof(CanViewCoefficients)); OnPropertyChanged(nameof(CanViewBonuses));
        OnPropertyChanged(nameof(CanViewClearance)); OpenClearanceCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanViewUsers)); OnPropertyChanged(nameof(CanViewReferences));
        OnPropertyChanged(nameof(CanViewDatabase)); OnPropertyChanged(nameof(CanCleanDatabase));
        OpenBrandsCommand.NotifyCanExecuteChanged(); OpenManagersCommand.NotifyCanExecuteChanged();
        OpenCoefficientsCommand.NotifyCanExecuteChanged(); OpenBonusesCommand.NotifyCanExecuteChanged();
        OpenUsersCommand.NotifyCanExecuteChanged(); OpenReferencesCommand.NotifyCanExecuteChanged();
        OpenDBCommand.NotifyCanExecuteChanged(); OpenDeleteAllCommand.NotifyCanExecuteChanged();
        if (_currentPermission != null && !_access.CanOpen(_currentPermission))
        {
            CurrentViewModel = null; _currentPermission = null; ResetSelection();
        }
    }
    private void ResetSelection()
    {
        IsUsersSelected = false; IsCoefficientsSelected = false; IsBonusesSelected = false;
        IsClearanceSelected = false;
        IsManagersSelected = false; IsBrandsSelected = false; IsReferencesSelected = false;
        IsDeleteDataSelected = false; IsDBSelected = false;
    }
}
