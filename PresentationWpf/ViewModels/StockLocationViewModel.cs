using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Infrastructure.Dtos;
using Infrastructure.Constants;
using Infrastructure.Helpers;
using Infrastructure.Services;
using Microsoft.Win32;
using PresentationWpf.Services;
using PresentationWpf.Views;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;

namespace PresentationWpf.ViewModels;
public partial class StockLocationViewModel : ObservableObject
{
    private readonly WarehousePlaceService _service;
    private readonly PermissionService _permissions;
    private readonly UserSessionService _session;
    private int _selectionVersion;
    private int _loadVersion;
    public WarehousePlaceImportState ImportState { get; }
    public event Action? RequestClose;
    [ObservableProperty] private ObservableCollection<string> articles = [];
    [ObservableProperty] private string? selectedArticle;
    [ObservableProperty] private string? newWarehousePlace;
    [ObservableProperty] private ObservableCollection<Product> stockItems = [];
    [ObservableProperty] private bool isSaving;
    [ObservableProperty] private string statusText = "";

    public bool CanEdit => !IsSaving && !ImportState.IsImporting && _permissions.Has(PermissionCatalog.Inventory);
    public bool CanShowImportErrors => CanEdit && ImportState.Snapshot?.Issues.Count > 0;
    public string ImportErrorsButtonText => ImportState.Snapshot?.Issues.Count > 0
        ? $"Ошибки импорта ({ImportState.Snapshot.Issues.Count})" : "Ошибки импорта";
    public string ImportSummary => ImportState.Snapshot is { } snapshot ? $"{snapshot.FileName}: {snapshot.Summary}" : "";

    public StockLocationViewModel(WarehousePlaceService service, WarehousePlaceImportState importState,
        PermissionService permissions, UserSessionService session)
    {
        _service = service; ImportState = importState; _permissions = permissions; _session = session;
        PropertyChangedEventManager.AddHandler(importState, ImportStateChanged, "");
        PropertyChangedEventManager.AddHandler(permissions, PermissionsChanged, nameof(PermissionService.CurrentPermissions));
        WeakEventManager<UserSessionService, EventArgs>.AddHandler(session, nameof(UserSessionService.UserChanged), UserChanged);
        _ = LoadArticlesAsync();
        ShowImportedProducts();
    }

    private void ImportStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WarehousePlaceImportState.Snapshot)) ShowImportedProducts();
        NotifyCommands();
        OnPropertyChanged(nameof(ImportErrorsButtonText));
        OnPropertyChanged(nameof(ImportSummary));
    }
    private void PermissionsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_permissions.Has(PermissionCatalog.Inventory))
        {
            _selectionVersion++; _loadVersion++; Articles.Clear(); StockItems.Clear(); NewWarehousePlace = null;
        }
        else if (Articles.Count == 0) _ = LoadArticlesAsync();
        NotifyCommands();
    }
    private void UserChanged(object? sender, EventArgs e)
    {
        _selectionVersion++; _loadVersion++; SelectedArticle = null; NewWarehousePlace = null;
        Articles.Clear(); StockItems.Clear(); StatusText = "";
        _ = LoadArticlesAsync();
    }
    private void NotifyCommands()
    {
        OnPropertyChanged(nameof(CanEdit)); OnPropertyChanged(nameof(CanShowImportErrors));
        ImportExcelCommand.NotifyCanExecuteChanged(); ShowImportErrorsCommand.NotifyCanExecuteChanged(); CloseCommand.NotifyCanExecuteChanged();
    }
    partial void OnIsSavingChanged(bool value) => NotifyCommands();

    private async Task LoadArticlesAsync()
    {
        if (!_permissions.Has(PermissionCatalog.Inventory)) return;
        var version = ++_loadVersion;
        try
        {
            var articles = await _service.GetArticlesAsync();
            if (version == _loadVersion && _permissions.Has(PermissionCatalog.Inventory)) Articles = new(articles);
        }
        catch (Exception ex) { if (version == _loadVersion) StatusText = ex.GetBaseException().Message; }
    }

    partial void OnSelectedArticleChanged(string? value) => _ = LoadSelectedProductAsync();
    private async Task LoadSelectedProductAsync()
    {
        var version = ++_selectionVersion;
        var article = SelectedArticle;
        StockItems.Clear(); NewWarehousePlace = null;
        if (!_permissions.Has(PermissionCatalog.Inventory)) return;
        if (string.IsNullOrWhiteSpace(article))
        {
            StockItems = new(ImportState.Snapshot?.UpdatedProducts ?? []);
            return;
        }
        try
        {
            var product = await _service.GetProductAsync(article);
            if (version != _selectionVersion || !_permissions.Has(PermissionCatalog.Inventory)) return;
            if (product != null) { StockItems.Add(product); NewWarehousePlace = product.WarehousePlace; }
        }
        catch (Exception ex) { if (version == _selectionVersion) StatusText = ex.GetBaseException().Message; }
    }

    private void ShowImportedProducts()
    {
        if (SelectedArticle != null) SelectedArticle = null;
        else _ = LoadSelectedProductAsync();
    }

    public async Task UpdateWarehousePlaceAsync()
    {
        if (!CanEdit || string.IsNullOrWhiteSpace(SelectedArticle) || string.IsNullOrWhiteSpace(NewWarehousePlace)) return;
        var article = SelectedArticle; var place = NewWarehousePlace;
        try
        {
            IsSaving = true;
            var updated = await _service.UpdatePlaceAsync(article, place);
            StatusText = updated == 0 ? "Артикул не найден в базе." : "Место на складе сохранено.";
            await LoadSelectedProductAsync();
            if (updated > 0 && StockItems.FirstOrDefault() is { } current && ImportState.Snapshot is { } snapshot)
                foreach (var cached in snapshot.UpdatedProducts.Where(p => p.ArticleNumber == current.ArticleNumber))
                    cached.WarehousePlace = current.WarehousePlace;
        }
        catch (Exception ex) { StatusText = $"Не удалось сохранить место: {ex.GetBaseException().Message}"; }
        finally { IsSaving = false; }
    }

    [RelayCommand(CanExecute = nameof(CanEdit))]
    private async Task ImportExcelAsync()
    {
        if (!CanEdit) return;
        var path = SelectImportFile();
        if (path != null) await ImportFileAsync(path);
    }
    protected virtual string? SelectImportFile()
    {
        var dialog = new OpenFileDialog { Title = "Импорт мест на складе: артикул, место",
            Filter = "Файлы Excel (*.xlsx;*.xlsm)|*.xlsx;*.xlsm", CheckFileExists = true, Multiselect = false };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public async Task ImportFileAsync(string path)
    {
        if (!CanEdit) return;
        var ct = ImportState.BeginImport();
        try
        {
            StatusText = "Чтение Excel и обновление мест на складе…";
            var file = await Task.Run(() => WarehousePlaceExcelReader.Read(path, ct), ct);
            ct.ThrowIfCancellationRequested();
            var result = await _service.ImportAsync(file.Rows, ct);
            ct.ThrowIfCancellationRequested();
            var snapshot = new WarehousePlaceImportSnapshot(Path.GetFileName(path), DateTime.Now, file.RowCount,
                result.UpdatedCount, result.DuplicateCount, file.Issues.Concat(result.Issues).OrderBy(i => i.RowNumber).ToArray())
                { UpdatedProducts = result.UpdatedProducts };
            ImportState.Snapshot = snapshot;
            StatusText = "Импорт завершён.";
            NotifyImportCompleted(snapshot);
        }
        catch (OperationCanceledException) { StatusText = "Импорт отменён."; }
        catch (Exception ex)
        {
            StatusText = $"Импорт не выполнен: {ex.GetBaseException().Message}";
            NotifyImportFailed(StatusText);
        }
        finally { ImportState.EndImport(); }
    }

    protected virtual void NotifyImportCompleted(WarehousePlaceImportSnapshot snapshot) => MessageBox.Show(
        snapshot.Summary + (snapshot.Issues.Count > 0 ? "\n\nОткройте «Ошибки импорта», чтобы посмотреть и распечатать список." : ""),
        "Импорт мест на складе", MessageBoxButton.OK, snapshot.Issues.Count > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
    protected virtual void NotifyImportFailed(string message) => MessageBox.Show(message, "Импорт мест на складе", MessageBoxButton.OK, MessageBoxImage.Error);

    [RelayCommand(CanExecute = nameof(CanShowImportErrors))]
    private void ShowImportErrors()
    {
        if (!CanShowImportErrors || ImportState.Snapshot is not { } snapshot) return;
        OpenImportErrors(snapshot);
    }
    protected virtual void OpenImportErrors(WarehousePlaceImportSnapshot snapshot)
    {
        var window = new WarehousePlaceImportErrorsWindow
        {
            DataContext = new WarehousePlaceImportErrorsViewModel(snapshot, _session.OrganizationDisplayName, _permissions),
            Owner = Application.Current?.MainWindow
        };
        window.ShowDialog();
    }

    [RelayCommand(CanExecute = nameof(CanEdit))]
    private void Close()
    {
        if (!CanEdit) return;
        SelectedArticle = null; NewWarehousePlace = "";
        RequestClose?.Invoke();
    }
}
