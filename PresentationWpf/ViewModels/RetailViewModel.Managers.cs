using Infrastructure.Constants;
using Infrastructure.Dtos;
using Infrastructure.Services;
using PresentationWpf.Dtos;
using PresentationWpf.Services;
using System.Collections.ObjectModel;
using System.Windows.Data;

namespace PresentationWpf.ViewModels;

public partial class RetailViewModel
{
    private readonly RetailManagerService? _retailManagerService;
    private RetailManagerState? _retailManagerState;
    private bool _managerScopeLoading;
    private bool _managerContextStarted;
    private bool _isChangingManager;
    private int _managerScopeVersion;
    private int _retailInitializationVersion;
    private int _inactiveRequestVersion;
    private string _managerLoadError = "";
    private Task<bool> _inactiveRefreshTask = Task.FromResult(false);

    private bool IsRetailManagerContextReady => _retailManagerService == null ||
        (!_managerScopeLoading && _retailManagerState != null && _userSessionService.IsLoggedIn &&
         _userSessionService.UserId == _retailManagerState.UserId && _permissionService.Has(PermissionCatalog.Orders));
    public bool CanChooseSalesManager => IsRetailManagerContextReady &&
        _permissionService.Has(PermissionCatalog.Orders) && _permissionService.Has(PermissionCatalog.OrdersViewAllManagers);
    public string ManagerSelectionHint => _managerLoadError.Length > 0 ? _managerLoadError :
        CanChooseSalesManager ? "Выберите менеджера для просмотра его клиентов." :
        _retailManagerState?.OwnManagerId != null ? "Автоматически выбран менеджер текущего пользователя." :
        "Для выбора другого менеджера требуется право «Заказы.Просмотр всех менеджеров».";

    private void NotifyManagerAccess()
    {
        OnPropertyChanged(nameof(CanChooseSalesManager)); OnPropertyChanged(nameof(ManagerSelectionHint));
        OnPropertyChanged(nameof(CanChangeBarterMode));
        AddToChartCommand.NotifyCanExecuteChanged(); SaveOrderCommand.NotifyCanExecuteChanged();
    }

    public async Task RefreshManagerContextAsync()
    {
        if (_retailManagerService == null) return;
        _managerContextStarted = true;
        var previousManagerId = SelectedSalesManager?.Id;
        ClearRetailManagerContext();
        var version = _managerScopeVersion;
        var userId = _userSessionService.UserId;
        if (!_userSessionService.IsLoggedIn || !_permissionService.Has(PermissionCatalog.Orders)) return;
        _managerScopeLoading = true; NotifyManagerAccess();
        try
        {
            var state = await _retailManagerService.LoadAsync();
            if (version != _managerScopeVersion || state.UserId != userId || userId != _userSessionService.UserId ||
                !_userSessionService.IsLoggedIn || !_permissionService.Has(PermissionCatalog.Orders)) return;
            _retailManagerState = state;
            SalesManagerList = new ObservableCollection<SalesManager>(state.Managers);
            AllCustomers = new ObservableCollection<Customer>(state.Customers.Select(c => (Customer)c));
            CustomersView = CollectionViewSource.GetDefaultView(AllCustomers);
            CustomersView.Filter = CustomerFilter;
            OnPropertyChanged(nameof(CustomersView));
            _managerScopeLoading = false;
            var selectedId = state.CanViewAllManagers && previousManagerId != null ? previousManagerId : state.OwnManagerId;
            var selected = SalesManagerList.FirstOrDefault(m => m.Id == selectedId)
                ?? SalesManagerList.FirstOrDefault(m => m.Id == state.OwnManagerId);
            SetSalesManagerCore(selected, force: true);
            NotifyManagerAccess();
            // Purchase panels refresh independently; their per-filter version prevents stale data.
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            if (version == _managerScopeVersion)
            {
                ClearRetailManagerContext();
                _managerLoadError = "Не удалось загрузить менеджеров и клиентов. Повторно откройте заказы.";
            }
        }
        finally
        {
            if (version == _managerScopeVersion) _managerScopeLoading = false;
            NotifyManagerAccess();
        }
    }

    private void ClearRetailManagerContext()
    {
        _managerScopeVersion++; _inactiveRequestVersion++;
        _retailManagerState = null; _managerScopeLoading = false; _managerLoadError = "";
        SelectedProductList.Clear(); ResetProductRequestEntry();
        SelectedCustomer = null; SelectedCourier = null; SelectedStorekeeper = null;
        _selectedSalesManager = null; OnPropertyChanged(nameof(SelectedSalesManager));
        _selectedTerritory = null; OnPropertyChanged(nameof(SelectedTerritory));
        SalesManagerList.Clear(); AllCustomers.Clear();
        TerritoryOptions.Clear(); TerritoryOptions.Add(new TerritoryOption { Value = null, Display = "Все территории" });
        ClearInactiveCustomers(); CustomersView?.Refresh();
        NotifyManagerAccess();
    }

    private void OnRetailUserChanged(object? sender, EventArgs e)
    {
        User = _userSessionService.IsLoggedIn ? $"{_userSessionService.FirstName} {_userSessionService.LastName}" : "";
        var reload = _managerContextStarted && _retailManagerState?.UserId == _userSessionService.UserId && _userSessionService.IsLoggedIn;
        _retailInitializationVersion++;
        _managerContextStarted = false;
        ClearRetailManagerContext();
        if (reload) _ = RefreshManagerContextAsync();
    }

    private void RefreshRetailManagerPermissions()
    {
        NotifyManagerAccess();
        OnPropertyChanged(nameof(CanViewCustomerCode)); OnPropertyChanged(nameof(CustomerDisplayMemberPath));
        if (_retailManagerService != null && _managerContextStarted)
        {
            var state = _retailManagerState;
            if (state == null || state.CanViewAllManagers != _permissionService.Has(PermissionCatalog.OrdersViewAllManagers) ||
                state.CanViewAllCustomers != _permissionService.Has(PermissionCatalog.CustomersViewAll) ||
                !_permissionService.Has(PermissionCatalog.Orders))
            {
                _ = RefreshManagerContextAsync();
                return;
            }
        }
        RetailFiltersChanged();
    }

    private void SetSalesManagerCore(SalesManager? manager, bool force = false)
    {
        if (!force && _selectedSalesManager == manager) return;
        _selectedSalesManager = manager;
        OnPropertyChanged(nameof(SelectedSalesManager));
        _isChangingManager = true;
        try { RebuildTerritories(); }
        finally { _isChangingManager = false; }
        RetailFiltersChanged();
    }

    private void RetailFiltersChanged()
    {
        if (SelectedCustomer != null && !CustomerFilter(SelectedCustomer))
        {
            SelectedProductList.Clear(); ResetProductRequestEntry();
            SelectedCustomer = null;
        }
        CustomersView?.Refresh();
        _inactiveRefreshTask = LoadInactiveCustomersAsync();
        SaveOrderCommand.NotifyCanExecuteChanged();
    }

    private void ClearInactiveCustomers()
    {
        SelectedRequiredPurchaseCombo = null; SelectedPlannedPurchaseCombo = null;
        InactiveDebtCustomers.Clear(); InactivePlannedCustomers.Clear();
        InactiveDebtCount = 0; InactivePlannedCount = 0;
        RequiredPurchase = 0; PlannedPurchase = 0;
    }
}
