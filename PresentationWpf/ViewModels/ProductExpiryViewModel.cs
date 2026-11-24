using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Infrastructure.Constants;
using Infrastructure.Dtos;
using Infrastructure.Services;
using Microsoft.Win32;
using PresentationWpf.Services;

namespace PresentationWpf.ViewModels;

public partial class ProductExpiryViewModel : ObservableObject
{
    private readonly ProductExpiryService _service;
    private readonly PermissionService _permissions;
    private int _generation;
    public ObservableCollection<ExpiryBatchRow> Batches { get; } = [];
    public ObservableCollection<ExpiryBrandOption> Brands { get; } = [];
    public ObservableCollection<ExpiryProductOption> Products { get; } = [];
    public ObservableCollection<ExpiryImportIssue> ImportIssues { get; } = [];
    public ICollectionView BatchesView { get; }
    public ICommand? GoHomeCommand { get; set; }
    [ObservableProperty] private bool isPriorityView;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string articleFilter = "";
    [ObservableProperty] private ExpiryBrandOption? selectedBrand;
    [ObservableProperty] private bool onlyActive = true;
    [ObservableProperty] private ExpiryBatchRow? selectedBatch;
    [ObservableProperty] private string articleText = "";
    [ObservableProperty] private DateTime? manufacturedOn = DateTime.Today;
    [ObservableProperty] private DateTime? expiresOn = DateTime.Today.AddYears(1);
    [ObservableProperty] private string warehousePlace = "";
    [ObservableProperty] private string quantityText = "0";
    [ObservableProperty] private string message = "";
    public bool CanAccess => _permissions.Has(PermissionCatalog.Inventory) || (IsPriorityView && _permissions.Has(PermissionCatalog.Orders));
    public bool CanWork => CanAccess && !IsBusy;
    public bool CanManage => !IsPriorityView && _permissions.Has(PermissionCatalog.Inventory);
    public bool CanEdit => CanManage && !IsBusy;
    public bool CanDelete => CanEdit && SelectedBatch != null;
    public string Title => IsPriorityView ? "Товары со сроком до 1 года" : "Сроки годности";
    public string Description => IsPriorityView
        ? "Партии в наличии со сроком до года включительно. Сначала ближайший срок. Просроченные партии отмечены отдельно."
        : "Укажите остаток каждой партии в пределах общего остатка товара. При продаже количество списывается автоматически.";
    public string EditorTitle => SelectedBatch == null ? "Данные партии" : "Редактирование партии";
    public string Total => $"Показано партий: {BatchesView.Cast<ExpiryBatchRow>().Count()} · В наличии: {BatchesView.Cast<ExpiryBatchRow>().Sum(x => (long)x.Quantity)} · Срок истёк: {BatchesView.Cast<ExpiryBatchRow>().Count(x => x.IsExpired)}";
    public bool HasImportIssues => ImportIssues.Count > 0;

    public ProductExpiryViewModel(ProductExpiryService service, PermissionService permissions)
    {
        _service = service; _permissions = permissions;
        BatchesView = CollectionViewSource.GetDefaultView(Batches);
        BatchesView.Filter = obj => obj is ExpiryBatchRow row &&
            (!OnlyActive || row.IsActive) && (SelectedBrand?.Id == null || row.BrandId == SelectedBrand.Id) &&
            row.ArticleNumber.Contains(ArticleFilter.Trim(), StringComparison.OrdinalIgnoreCase);
        PropertyChangedEventManager.AddHandler(permissions, PermissionsChanged, nameof(PermissionService.CurrentPermissions));
    }

    public void SetPriorityMode(bool priority)
    {
        IsPriorityView = priority; _generation++;
        OnlyActive = true;
        Clear(); NotifyAccess();
        OnPropertyChanged(nameof(Title)); OnPropertyChanged(nameof(Description));
    }
    partial void OnArticleFilterChanged(string value) => ApplyFilter();
    partial void OnSelectedBrandChanged(ExpiryBrandOption? value) => ApplyFilter();
    partial void OnOnlyActiveChanged(bool value) => ApplyFilter();
    partial void OnIsBusyChanged(bool value) => NotifyAccess();
    partial void OnSelectedBatchChanged(ExpiryBatchRow? value)
    {
        if (value != null)
        {
            ArticleText = value.ArticleNumber; ManufacturedOn = value.ManufacturedOn; ExpiresOn = value.ExpiresOn;
            WarehousePlace = value.WarehousePlace; QuantityText = value.Quantity.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        OnPropertyChanged(nameof(EditorTitle)); DeleteCommand.NotifyCanExecuteChanged();
    }
    private void ApplyFilter() { BatchesView.Refresh(); OnPropertyChanged(nameof(Total)); }

    public Task InitializeAsync() => RefreshCommand.ExecuteAsync(null);

    [RelayCommand(CanExecute = nameof(CanWork))]
    private Task RefreshAsync() => RunAsync(async current =>
    {
        var batches = await _service.GetBatchesAsync(IsPriorityView);
        IReadOnlyList<ExpiryProductOption> products = CanManage ? await _service.GetProductsAsync() : [];
        if (!current()) return;
        Replace(batches, products); Message = "Список обновлён.";
    });

    private void Replace(IReadOnlyList<ExpiryBatchRow> rows, IReadOnlyList<ExpiryProductOption>? products = null)
    {
        var brand = SelectedBrand?.Id;
        Batches.Clear(); foreach (var row in rows) Batches.Add(row);
        Brands.Clear(); Brands.Add(new(null, "Все бренды"));
        foreach (var row in rows.GroupBy(x => x.BrandId).Select(g => new ExpiryBrandOption(g.Key, g.First().BrandName)).OrderBy(x => x.Name)) Brands.Add(row);
        SelectedBrand = Brands.FirstOrDefault(x => x.Id == brand) ?? Brands[0];
        if (products != null) { Products.Clear(); foreach (var product in products) Products.Add(product); }
        NewBatch(); ApplyFilter();
    }

    [RelayCommand(CanExecute = nameof(CanEdit))]
    private void NewBatch()
    {
        SelectedBatch = null; ArticleText = ""; WarehousePlace = ""; ManufacturedOn = DateTime.Today;
        ExpiresOn = DateTime.Today.AddYears(1); QuantityText = "0";
    }

    [RelayCommand(CanExecute = nameof(CanEdit))]
    private Task SaveAsync() => !CanEdit ? Task.CompletedTask : RunAsync(async current =>
    {
        if (ManufacturedOn == null || ExpiresOn == null) throw new ArgumentException("Укажите обе даты.");
        if (!int.TryParse(QuantityText.Trim(), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var quantity))
            throw new ArgumentException("В наличии должно быть целое количество не меньше 0.");
        var input = new ExpiryBatchInput(SelectedBatch?.Id ?? Guid.Empty, ArticleText, ManufacturedOn.Value,
            ExpiresOn.Value, WarehousePlace, quantity, SelectedBatch?.Version);
        await _service.SaveAsync(input);
        var rows = await _service.GetBatchesAsync(false);
        if (!current()) return;
        Replace(rows); Message = "Партия сохранена. Отметки в продажах обновлены.";
    });

    [RelayCommand(CanExecute = nameof(CanDelete))]
    private Task DeleteAsync() => !CanDelete ? Task.CompletedTask : RunAsync(async current =>
    {
        var row = SelectedBatch;
        if (row == null || !ConfirmDelete(row)) return;
        await _service.DeleteAsync(row.Id, row.Version);
        var rows = await _service.GetBatchesAsync(false);
        if (!current()) return;
        Replace(rows); Message = "Партия удалена.";
    });

    [RelayCommand(CanExecute = nameof(CanEdit))]
    private async Task ImportAsync()
    {
        if (!CanEdit) return;
        var path = SelectImportFile(); if (path == null) return;
        await ImportFileAsync(path);
    }

    public Task ImportFileAsync(string path) => !CanEdit ? Task.CompletedTask : RunAsync(async current =>
    {
        ImportIssues.Clear(); OnPropertyChanged(nameof(HasImportIssues));
        var file = await Task.Run(() => ProductExpiryExcel.Read(path));
        if (!current()) return;
        var result = await _service.ImportAsync(file);
        var rows = await _service.GetBatchesAsync(false);
        if (!current()) return;
        Replace(rows);
        foreach (var issue in result.Issues) ImportIssues.Add(issue);
        OnPropertyChanged(nameof(HasImportIssues));
        Message = $"Добавлено: {result.AddedCount}; обновлено: {result.UpdatedCount}; повторов в файле: {result.DuplicateCount}; ошибок: {result.Issues.Count}.";
    });

    [RelayCommand(CanExecute = nameof(CanEdit))]
    private Task TemplateAsync() => !CanEdit ? Task.CompletedTask : RunAsync(async current =>
    {
        var path = SelectTemplateFile(); if (path == null) return;
        await Task.Run(() => ProductExpiryExcel.SaveTemplate(path));
        if (current()) Message = "Шаблон сохранён. Заполните строки, сохраняя артикулы как текст.";
    });

    protected virtual string? SelectImportFile()
    {
        var dialog = new OpenFileDialog { Filter = "Excel (*.xlsx)|*.xlsx", Title = "Импорт партий и сроков годности" };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
    protected virtual string? SelectTemplateFile()
    {
        var dialog = new SaveFileDialog { Filter = "Excel (*.xlsx)|*.xlsx", FileName = "Сроки годности.xlsx" };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
    protected virtual bool ConfirmDelete(ExpiryBatchRow row) => MessageBox.Show(
        $"Удалить партию {row.ArticleNumber}, годную до {row.ExpiresOn:dd.MM.yyyy}, место {row.WarehousePlace}?",
        "Удаление партии", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    private async Task RunAsync(Func<Func<bool>, Task> work)
    {
        if (!CanWork) return;
        var generation = _generation; IsBusy = true; Message = "";
        bool Current() => generation == _generation && CanAccess;
        try { await work(Current); }
        catch (Exception ex) { if (Current()) Message = ex.Message; }
        finally { IsBusy = false; }
    }
    private void PermissionsChanged(object? sender, PropertyChangedEventArgs e)
    {
        _generation++; if (!CanAccess) Clear(); NotifyAccess();
    }
    private void Clear()
    {
        Batches.Clear(); Products.Clear(); Brands.Clear(); ImportIssues.Clear(); NewBatch();
        Message = ""; OnPropertyChanged(nameof(Total)); OnPropertyChanged(nameof(HasImportIssues));
    }
    private void NotifyAccess()
    {
        OnPropertyChanged(nameof(CanAccess)); OnPropertyChanged(nameof(CanWork)); OnPropertyChanged(nameof(CanManage));
        OnPropertyChanged(nameof(CanEdit)); OnPropertyChanged(nameof(CanDelete));
        RefreshCommand.NotifyCanExecuteChanged(); NewBatchCommand.NotifyCanExecuteChanged(); SaveCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged(); ImportCommand.NotifyCanExecuteChanged(); TemplateCommand.NotifyCanExecuteChanged();
    }
}
