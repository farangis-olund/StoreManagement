using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Infrastructure.Constants;
using Infrastructure.Dtos;
using Infrastructure.Services;
using PresentationWpf.Services;

namespace PresentationWpf.ViewModels;

public partial class OfficialMonthlyPlansViewModel : ObservableObject
{
    private readonly OfficialMonthlyPlanService _service;
    private readonly PermissionService _permissions;
    private DateTime? _loadedMonth;
    private bool _refreshPending;
    private bool _active;
    private string _originalThresholdText = "0";
    private bool _thresholdLoaded;
    private readonly System.Windows.Threading.DispatcherTimer _timer = new() { Interval = TimeSpan.FromMinutes(1) };
    public OfficialMonthlyPlansViewModel(OfficialMonthlyPlanService service, PermissionService permissions)
    {
        _service = service;
        _permissions = permissions;
        _isSettings = !CanView && CanManage;
        PropertyChangedEventManager.AddHandler(permissions, OnPermissionsChanged, nameof(PermissionService.CurrentPermissions));
        WeakEventManager<OfficialMonthlyPlanService, EventArgs>.AddHandler(service, nameof(service.Changed), OnDataChanged);
        _timer.Tick += (_, _) => _ = RefreshCommand.ExecuteAsync(null);
    }

    public ObservableCollection<OfficialMonthlyPlanRowViewModel> Rows { get; } = [];
    public ObservableCollection<OfficialMonthlyPlanRowViewModel> VisibleRows { get; } = [];
    public ObservableCollection<CustomerOption> CustomerFilters { get; } = [new("", "Все клиенты")];
    [ObservableProperty] private string? _selectedCustomerId = "";
    public ObservableCollection<int> Years { get; } = new(Enumerable.Range(DateTime.Today.Year - 5, 11));
    public IReadOnlyList<MonthOption> Months { get; } = Enumerable.Range(1, 12)
        .Select(m => new MonthOption(m, CultureInfo.GetCultureInfo("ru-RU").DateTimeFormat.GetMonthName(m))).ToArray();
    [ObservableProperty] private int _selectedYear = DateTime.Today.Year;
    [ObservableProperty] private int _selectedMonth = DateTime.Today.Month;
    [ObservableProperty] private bool _isSettings;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _message = "";
    [ObservableProperty] private decimal _totalPlan;
    [ObservableProperty] private decimal _totalPurchased;
    [ObservableProperty] private decimal _totalRemaining;
    [ObservableProperty] private string _nonOfficialThresholdText = "0";
    private bool IsThresholdDirty => NonOfficialThresholdText != _originalThresholdText;
    public bool CanView => _permissions.Has(PermissionCatalog.OfficialMonthlyPlansView);
    public bool CanManage => _permissions.Has(PermissionCatalog.OfficialMonthlyPlansManage);
    public bool ShowControl => CanView && !IsSettings;
    public bool ShowSettings => CanManage && IsSettings;
    public bool CanEdit => ShowSettings && !IsBusy;
    private DateTime Period => new(SelectedYear, SelectedMonth, 1);

    partial void OnSelectedYearChanged(int value) { if (_active) _ = RefreshCommand.ExecuteAsync(null); }
    partial void OnSelectedMonthChanged(int value) { if (_active) _ = RefreshCommand.ExecuteAsync(null); }
    partial void OnIsSettingsChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowControl)); OnPropertyChanged(nameof(ShowSettings)); OnPropertyChanged(nameof(CanEdit));
        if (_active) _ = RefreshCommand.ExecuteAsync(null);
    }
    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(CanEdit));
    partial void OnSelectedCustomerIdChanged(string? value) => ApplyCustomerFilter();

    private void ApplyCustomerFilter()
    {
        var rows = Rows.Where(r => IsSettings || string.IsNullOrEmpty(SelectedCustomerId) || r.CustomerId == SelectedCustomerId).ToArray();
        VisibleRows.Clear();
        foreach (var row in rows) VisibleRows.Add(row);
        TotalPlan = rows.Sum(r => r.Amount ?? 0);
        TotalPurchased = rows.Sum(r => r.Purchased);
        TotalRemaining = rows.Sum(r => r.Remaining);
    }

    public void Activate() { _active = true; _timer.Start(); _ = RefreshCommand.ExecuteAsync(null); }
    public void Deactivate() { _active = false; _timer.Stop(); }
    private void OnDataChanged(object? sender, EventArgs e)
    {
        if (!_active) return;
        if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
            dispatcher.BeginInvoke(new Action(() => _ = RefreshCommand.ExecuteAsync(null)));
        else _ = RefreshCommand.ExecuteAsync(null);
    }
    private void OnPermissionsChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(CanView)); OnPropertyChanged(nameof(CanManage));
        if (!CanManage) IsSettings = false;
        else if (!CanView) IsSettings = true;
        OnPropertyChanged(nameof(ShowControl)); OnPropertyChanged(nameof(ShowSettings)); OnPropertyChanged(nameof(CanEdit));
        OnDataChanged(sender, EventArgs.Empty);
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task Refresh()
    {
        if (IsBusy) { _refreshPending = true; return; }
        if (!CanView && !CanManage) { Rows.Clear(); VisibleRows.Clear(); return; }
        IsBusy = true;
        try
        {
            do
            {
                _refreshPending = false;
                var month = Period;
                var settings = IsSettings;
                var drafts = settings && _loadedMonth == month ? Rows.Where(r => r.IsDirty)
                    .ToDictionary(r => r.CustomerId, r => r.AmountText) : [];
                OfficialMonthlyPlanRowViewModel[] rows;
                if (settings)
                    rows = (await _service.GetSettingsAsync(month)).Select(r => new OfficialMonthlyPlanRowViewModel(
                        r.CustomerId, r.CustomerName, r.Amount, 0)).ToArray();
                else
                    rows = (await _service.GetControlAsync(month)).Select(r => new OfficialMonthlyPlanRowViewModel(
                        r.CustomerId, r.CustomerName, r.Amount, r.Purchased, r.NonOfficialThreshold)).ToArray();
                var years = await _service.GetYearsAsync();
                var threshold = settings ? await _service.GetNonOfficialThresholdAsync() : (decimal?)null;
                if (Period != month || settings != IsSettings) { _refreshPending = true; continue; }
                if (threshold.HasValue && (!_thresholdLoaded || !IsThresholdDirty))
                {
                    NonOfficialThresholdText = threshold.Value.ToString("0.##", CultureInfo.InvariantCulture);
                    _originalThresholdText = NonOfficialThresholdText;
                    _thresholdLoaded = true;
                }
                Rows.Clear();
                foreach (var row in rows)
                {
                    if (drafts.TryGetValue(row.CustomerId, out var amount)) row.AmountText = amount;
                    Rows.Add(row);
                }
                if (!settings)
                {
                    var selected = SelectedCustomerId;
                    CustomerFilters.Clear();
                    CustomerFilters.Add(new("", "Все клиенты"));
                    foreach (var row in rows) CustomerFilters.Add(new(row.CustomerId, $"{row.CustomerId} — {row.CustomerName}"));
                    SelectedCustomerId = CustomerFilters.Any(c => c.Id == selected) ? selected : "";
                }
                foreach (var year in years.Where(y => !Years.Contains(y))) Years.Add(year);
                _loadedMonth = month;
                ApplyCustomerFilter();
                Message = settings ? "Установите планы нужным клиентам и нажмите «Сохранить». Пустое поле означает, что план не установлен."
                    : $"Планы за {month.ToString("MMMM yyyy", CultureInfo.GetCultureInfo("ru-RU"))}. Клиентов: {rows.Length}.";
            } while (_refreshPending);
        }
        catch (Exception ex) { Message = ex.GetBaseException().Message; }
        finally
        {
            IsBusy = false;
            if (_refreshPending && _active)
            {
                _refreshPending = false;
                _ = RefreshCommand.ExecuteAsync(null);
            }
        }
    }

    [RelayCommand]
    private async Task Save()
    {
        if (!CanEdit) return;
        if (_loadedMonth != Period) { Message = "Дождитесь загрузки выбранного месяца."; return; }
        var rows = Rows.Where(r => r.IsDirty).ToArray();
        if (rows.Length == 0 && !IsThresholdDirty) { Message = "Нет изменений для сохранения."; return; }
        decimal? threshold = null;
        if (IsThresholdDirty)
        {
            if (!_thresholdLoaded || !OfficialMonthlyPlanRowViewModel.TryParseAmount(NonOfficialThresholdText, out var value) ||
                value < 0 || value > 9999999999999999.99m || decimal.Round(value, 2) != value)
            {
                Message = "Укажите общий порог не меньше нуля, не более двух знаков после запятой. Изменения не сохранены.";
                return;
            }
            threshold = value;
        }
        var amounts = new Dictionary<string, decimal>();
        foreach (var row in rows)
        {
            if (!row.TryGetAmount(out var amount) || amount <= 0 || amount > 9999999999999999.99m || decimal.Round(amount, 2) != amount)
            {
                Message = $"Клиент {row.CustomerId} ({row.CustomerName}): укажите план больше нуля, не более двух знаков после запятой. Изменения не сохранены.";
                return;
            }
            amounts.Add(row.CustomerId, amount);
        }
        var month = Period;
        IsBusy = true;
        try
        {
            await _service.SetPlansAsync(month, amounts, threshold);
            foreach (var row in rows) row.AcceptChanges();
            if (threshold.HasValue) _originalThresholdText = NonOfficialThresholdText;
        }
        catch (Exception ex) { Message = ex.GetBaseException().Message; return; }
        finally { IsBusy = false; }
        await Refresh();
        if (_loadedMonth == month && ShowSettings) Message = $"Сохранено планов: {rows.Length}." + (threshold.HasValue ? " Общий порог сохранён." : "");
    }

    [RelayCommand] private void ShowControlTable() { if (CanView) IsSettings = false; }
    [RelayCommand] private void ShowPlanSettings() { if (CanManage) IsSettings = true; }
    public sealed record MonthOption(int Number, string Name);
    public sealed record CustomerOption(string Id, string Name);
}

public partial class OfficialMonthlyPlanRowViewModel(string customerId, string customerName, decimal? amount, decimal purchased,
    decimal nonOfficialThreshold = 0) : ObservableObject
{
    private string _originalText = amount?.ToString("0.##", CultureInfo.InvariantCulture) ?? "";
    public string CustomerId { get; } = customerId;
    public string CustomerName { get; } = customerName;
    public decimal Purchased { get; } = purchased;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Amount))]
    [NotifyPropertyChangedFor(nameof(Remaining))]
    private string _amountText = amount?.ToString("0.##", CultureInfo.InvariantCulture) ?? "";
    public decimal? Amount => TryGetAmount(out var value) ? value : null;
    public bool IsDirty => AmountText != _originalText;
    public decimal Remaining => OfficialMonthlyPlanSummary.CalculateRemaining(Amount ?? 0, Purchased, nonOfficialThreshold);
    public bool TryGetAmount(out decimal amount) => TryParseAmount(AmountText, out amount);
    public static bool TryParseAmount(string text, out decimal amount) => decimal.TryParse(text.Trim().Replace(" ", "").Replace("\u00a0", "")
        .Replace("\u202f", "").Replace(',', '.'), System.Globalization.NumberStyles.AllowDecimalPoint |
        System.Globalization.NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out amount);
    public void AcceptChanges() => _originalText = AmountText;
}
