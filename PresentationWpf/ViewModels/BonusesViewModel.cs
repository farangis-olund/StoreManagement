using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Infrastructure.Dtos;

using PresentationWpf.Dtos;
using PresentationWpf.Services;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace PresentationWpf.ViewModels;

public partial class BonusesViewModel : ObservableObject
{
    private readonly BonusService _bonusService;

    public BonusesViewModel(BonusService bonusService)
    {
        _bonusService = bonusService;
        _ = InitializeAsync();
    }

    public ObservableCollection<CustomerLevelReviewDto> LowerThanLevelCustomers { get; } = new();
    public ObservableCollection<CustomerLevelReviewDto> HigherThanLevelCustomers { get; } = new();
    public ObservableCollection<BrandBonusRowDto> BrandBonusCustomers { get; } = new();
    public ObservableCollection<PromotionGapRowDto> PromotionCustomers { get; } = new();
    public ObservableCollection<string> Brands { get; } = new();

    [ObservableProperty]
    private string? selectedBrand;

    [ObservableProperty]
    private DateTime? bonusFromDate;

    [ObservableProperty]
    private DateTime? bonusToDate;

    [ObservableProperty]
    private bool isLoading;

    public ObservableCollection<ShopBonusRowDto> ShopBonusRows { get; } = new();

    public ObservableCollection<ShopBonusRowDto> ShopBonusUsers { get; } = new();

    [ObservableProperty]
    private ShopBonusRowDto? selectedShopBonusUser;

    [ObservableProperty]
    private DateTime? shopBonusFromDate;

    [ObservableProperty]
    private DateTime? shopBonusToDate;

    partial void OnShopBonusFromDateChanged(DateTime? value)
    {
        _ = LoadShopBonusUsersAsync();
    }

    partial void OnShopBonusToDateChanged(DateTime? value)
    {
        _ = LoadShopBonusUsersAsync();
    }

    [ObservableProperty]
    private decimal shopBonusPercent;

    [ObservableProperty]
    private int shopBonusTotalQuantity;

    [ObservableProperty]
    private decimal shopBonusTotalAmount;

    [ObservableProperty]
    private decimal shopBonusTotalBonus;

    public async Task InitializeAsync()
    {
        await LoadBrandsAsync();
        await LoadAllAsync();
        await LoadShopBonusUsersAsync();
    }

    [RelayCommand]
    private async Task RefreshAll()
    {
        await LoadAllAsync();
    }

    [RelayCommand]
    private async Task RefreshBrandBonus()
    {
        await LoadBrandBonusAsync();
    }

    [RelayCommand]
    private void ClearBonusPeriod()
    {
        BonusFromDate = null;
        BonusToDate = null;
    }

    [RelayCommand]
    private async Task UpdateLowerLevels()
    {
        var checkedRows = LowerThanLevelCustomers.Where(x => x.IsSelected).ToList();

        if (checkedRows.Count == 0)
        {
            MessageBox.Show("Выберите клиентов для обновления.", "Обновление уровня",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var updated = await _bonusService.UpdateCustomersToSuggestedLevelAsync(checkedRows);

        MessageBox.Show(
            $"Обновлено клиентов: {updated}",
            "Обновление уровня",
            MessageBoxButton.OK,
            MessageBoxImage.Information);

        await LoadAllAsync();
    }

    [RelayCommand]
    private async Task UpdateHigherLevels()
    {
        var checkedRows = HigherThanLevelCustomers.Where(x => x.IsSelected).ToList();

        if (checkedRows.Count == 0)
        {
            MessageBox.Show("Выберите клиентов для обновления.", "Обновление уровня",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var updated = await _bonusService.UpdateCustomersToSuggestedLevelAsync(checkedRows);

        MessageBox.Show(
            $"Обновлено клиентов: {updated}",
            "Обновление уровня",
            MessageBoxButton.OK,
            MessageBoxImage.Information);

        await LoadAllAsync();
    }

    partial void OnSelectedBrandChanged(string? value)
    {
        _ = LoadBrandBonusAsync();
    }

    partial void OnBonusFromDateChanged(DateTime? value)
    {
        _ = LoadBrandBonusAsync();
    }

    partial void OnBonusToDateChanged(DateTime? value)
    {
        _ = LoadBrandBonusAsync();
    }

    private async Task LoadAllAsync()
    {
        try
        {
            IsLoading = true;

            var levelTask = _bonusService.GetCustomerLevelReviewAsync();
            var promoTask = _bonusService.GetPromotionGapAsync();
            var brandTask = LoadBrandBonusAsync();

            var levelResult = await levelTask;
            var promoResult = await promoTask;
            await brandTask;

            LowerThanLevelCustomers.Clear();
            foreach (var item in levelResult.Lower)
                LowerThanLevelCustomers.Add(item);

            HigherThanLevelCustomers.Clear();
            foreach (var item in levelResult.Higher)
                HigherThanLevelCustomers.Add(item);

            PromotionCustomers.Clear();
            foreach (var item in promoResult)
                PromotionCustomers.Add(item);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadBrandsAsync()
    {
        Brands.Clear();

        var brands = await _bonusService.GetBrandsAsync();
        foreach (var b in brands)
            Brands.Add(b);
    }

    private async Task LoadBrandBonusAsync()
    {
        var rows = await _bonusService.GetBrandBonusAsync(
            SelectedBrand,
            BonusFromDate,
            BonusToDate);

        BrandBonusCustomers.Clear();
        foreach (var row in rows)
            BrandBonusCustomers.Add(row);
    }

    [RelayCommand]
    private void ExportExcel(DataGrid? grid)
    {
        if (grid == null)
            return;
        ExcelExportHelper.ExportFromDataGrid(grid, "Бонус.xlsx");
    }

    [RelayCommand]
    private async Task ShowShopBonus()
    {
        if (ShopBonusFromDate == null || ShopBonusToDate == null)
        {
            MessageBox.Show("Выберите период.", "Бонус магазина",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (ShopBonusPercent <= 0)
        {
            MessageBox.Show("Введите процент больше 0.", "Бонус магазина",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var rows = await _bonusService.GetShopBonusAsync(
          ShopBonusFromDate.Value,
          ShopBonusToDate.Value,
          ShopBonusPercent,
          SelectedShopBonusUser?.UserId);

        ShopBonusRows.Clear();

        foreach (var row in rows)
            ShopBonusRows.Add(row);

        ShopBonusTotalQuantity = rows.Sum(x => x.TotalQuantity);
        ShopBonusTotalAmount = rows.Sum(x => x.TotalAmount);
        ShopBonusTotalBonus = rows.Sum(x => x.BonusAmount);
    }

    [RelayCommand]
    private void ClearShopBonus()
    {
        ShopBonusFromDate = null;
        ShopBonusToDate = null;
        ShopBonusPercent = 0;
        ShopBonusRows.Clear();

        ShopBonusTotalQuantity = 0;
        ShopBonusTotalAmount = 0;
        ShopBonusTotalBonus = 0;
        SelectedShopBonusUser = null;
        ShopBonusUsers.Clear();
    }

    private async Task LoadShopBonusUsersAsync()
    {
        var oldSelectedUserId = SelectedShopBonusUser?.UserId;

        ShopBonusUsers.Clear();

        var users = await _bonusService.GetShopBonusUsersAsync(
            ShopBonusFromDate,
            ShopBonusToDate);

        foreach (var user in users)
            ShopBonusUsers.Add(user);

        SelectedShopBonusUser = ShopBonusUsers
            .FirstOrDefault(x => x.UserId == oldSelectedUserId);
    }
}

