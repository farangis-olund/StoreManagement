using System.Globalization;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Infrastructure.Constants;
using Infrastructure.Services;
using PresentationWpf.Dtos;

namespace PresentationWpf.ViewModels;

public partial class RetailViewModel
{
    private OfficialMonthlyPlanService? _monthlyPlanService;
    private readonly DispatcherTimer _monthlyPlanTimer = new() { Interval = TimeSpan.FromMinutes(1) };
    private bool _monthlyPlansActive;
    private int _monthlyPlanVersion;
    private bool _monthlyPlanReady = true;
    private decimal? _officialOrderLimit;
    [ObservableProperty] private decimal? _totalMonthlyPlanRemaining;
    [ObservableProperty] private decimal? _customerMonthlyPlanRemaining;
    [ObservableProperty] private string _monthlyPlanError = "";
    public bool CanShowTotalMonthlyPlan => _permissionService.Has(PermissionCatalog.OfficialMonthlyPlansTotal);
    public bool ShowCustomerMonthlyPlan => _permissionService.Has(PermissionCatalog.OfficialMonthlyPlansIndividual) &&
        SelectedCustomer != null && CustomerMonthlyPlanRemaining.HasValue;
    public string TotalMonthlyPlanText => "Всего остаток по плану: " + FormatPlanAmount(TotalMonthlyPlanRemaining);
    public string CustomerMonthlyPlanText => "Остаток по плану: " + FormatPlanAmount(CustomerMonthlyPlanRemaining);
    private static string FormatPlanAmount(decimal? value) => value?.ToString("#,0.##", CultureInfo.GetCultureInfo("ru-RU")) ?? "…";
    private bool IsOfficialPlanReady => _monthlyPlanService == null || SelectedCustomer?.OfficialCustomer != true || _monthlyPlanReady;
    private decimal? ActiveOfficialOrderLimit => !IsBarter && OfficialOrder && IsOfficialPlanReady && IsCustomerFinanceReady ? _officialOrderLimit : null;
    private bool IsMonthlyPlanLimitExceeded(decimal total) => ActiveOfficialOrderLimit.HasValue && total > ActiveOfficialOrderLimit.Value;

    private void EnforceMonthlyPlanLimit(ProductModel? changedProduct = null, bool showMessage = true)
    {
        if (ActiveOfficialOrderLimit.HasValue && ReduceCartToLimit(ActiveOfficialOrderLimit.Value, changedProduct) && showMessage)
            ShowMonthlyPlanLimitReachedMessage();
    }

    protected virtual void ShowMonthlyPlanLimitReachedMessage()
    {
        MessageBox.Show($"Сумма официального заказа не может превышать остаток по месячному плану: {ActiveOfficialOrderLimit:N2}.\n\n" +
            "Количество в корзине ограничено доступным остатком. Оформите остальные товары отдельным заказом.",
            "Остаток по месячному плану", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void InitializeMonthlyPlans(OfficialMonthlyPlanService? service)
    {
        _monthlyPlanService = service;
        if (service != null)
            WeakEventManager<OfficialMonthlyPlanService, EventArgs>.AddHandler(service, nameof(service.Changed), MonthlyPlanDataChanged);
        _monthlyPlanTimer.Tick += (_, _) => _ = RefreshMonthlyPlanStateAsync();
    }
    public void StartMonthlyPlanRefresh()
    {
        _monthlyPlansActive = true;
        _monthlyPlanTimer.Start();
        _ = RefreshMonthlyPlanStateAsync();
    }
    public void StopMonthlyPlanRefresh() { _monthlyPlansActive = false; _monthlyPlanTimer.Stop(); }
    private void MonthlyPlanDataChanged(object? sender, EventArgs e)
    {
        if (!_monthlyPlansActive) return;
        if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
            dispatcher.BeginInvoke(new Action(() => _ = RefreshMonthlyPlanStateAsync()));
        else _ = RefreshMonthlyPlanStateAsync();
    }
    private void RefreshMonthlyPlanPermissions()
    {
        OnPropertyChanged(nameof(CanShowTotalMonthlyPlan));
        OnPropertyChanged(nameof(ShowCustomerMonthlyPlan));
        _ = RefreshMonthlyPlanStateAsync();
    }
    partial void OnTotalMonthlyPlanRemainingChanged(decimal? value) => OnPropertyChanged(nameof(TotalMonthlyPlanText));
    partial void OnCustomerMonthlyPlanRemainingChanged(decimal? value)
    {
        OnPropertyChanged(nameof(CustomerMonthlyPlanText));
        OnPropertyChanged(nameof(ShowCustomerMonthlyPlan));
    }

    private async Task<bool> RefreshMonthlyPlanStateAsync(bool enforceCart = true)
    {
        var version = ++_monthlyPlanVersion;
        var customer = SelectedCustomer;
        var date = DateTime.Today;
        CustomerMonthlyPlanRemaining = null;
        _officialOrderLimit = null;
        OnPropertyChanged(nameof(ShowCustomerMonthlyPlan));
        MonthlyPlanError = "";
        if (_monthlyPlanService == null)
        {
            OfficialOrder = !IsBarter && customer?.OfficialCustomer == true;
            return true;
        }
        _monthlyPlanReady = false;
        AddToChartCommand.NotifyCanExecuteChanged();
        SaveOrderCommand.NotifyCanExecuteChanged();
        try
        {
            var state = await _monthlyPlanService.GetRetailStateAsync(customer?.Id, date);
            if (version != _monthlyPlanVersion || SelectedCustomer != customer || DateTime.Today != date) return false;
            // Keep the global value on screen while switching clients; replace it only with a fresh result.
            if (state.TotalRemaining.HasValue) TotalMonthlyPlanRemaining = state.TotalRemaining;
            CustomerMonthlyPlanRemaining = state.IndividualRemaining;
            OfficialOrder = !IsBarter && state.OfficialOrder;
            _officialOrderLimit = state.OfficialOrderLimit;
            _monthlyPlanReady = true;
            // A committed order publishes its new remainder before SaveOrder clears the sold cart.
            // Keep the indicators fresh; the explicit pre-save check still validates the order.
            if (enforceCart && !_isSavingOrder) EnforceBuyLimit();
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            if (version == _monthlyPlanVersion)
                MonthlyPlanError = "Не удалось обновить месячный план. Проверьте подключение к базе данных.";
            return false;
        }
        finally
        {
            if (version == _monthlyPlanVersion)
            {
                AddToChartCommand.NotifyCanExecuteChanged();
                SaveOrderCommand.NotifyCanExecuteChanged();
            }
        }
    }
}
