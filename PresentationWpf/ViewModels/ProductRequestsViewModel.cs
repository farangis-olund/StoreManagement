using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Infrastructure.Dtos;
using Infrastructure.Entities;
using Infrastructure.Services;

namespace PresentationWpf.ViewModels;

public partial class ProductRequestsViewModel : ObservableObject
{
    private readonly ProductRequestService _service;

    public ProductRequestsViewModel(ProductRequestService service)
    {
        _service = service;
        Rows.CollectionChanged += (_, _) =>
        {
            DeleteDisplayedCommand.NotifyCanExecuteChanged();
            MarkDisplayedProcessedCommand.NotifyCanExecuteChanged();
        };
    }

    public ObservableCollection<ProductRequestSummary> Rows { get; } = [];
    public ObservableCollection<ProductRequestEntity> Details { get; } = [];
    public ObservableCollection<string> Brands { get; } = ["Все бренды"];
    [ObservableProperty] private int _selectedTab;
    [ObservableProperty] private string _selectedBrand = "Все бренды";
    [ObservableProperty] private DateTime? _from;
    [ObservableProperty] private DateTime? _through;
    [ObservableProperty] private bool _includeProcessed;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteDisplayedCommand))]
    [NotifyCanExecuteChangedFor(nameof(MarkDisplayedProcessedCommand))]
    private bool _isBusy;
    [ObservableProperty] private string _message = "";
    [ObservableProperty] private string _detailTitle = "";
    [ObservableProperty] private string _filterDescription = "";

    public bool IsShortage => SelectedTab == 0;
    public bool IsOutOfStock => SelectedTab == 1;
    public bool IsNewArticles => SelectedTab == 2;
    public bool CanDelete => !IsNewArticles;
    public string Title => SelectedTab switch
    {
        0 => "Недостаточный остаток", 1 => "Нет в наличии", _ => "Новые артикулы"
    };

    partial void OnSelectedTabChanged(int value)
    {
        OnPropertyChanged(nameof(IsShortage));
        OnPropertyChanged(nameof(IsOutOfStock));
        OnPropertyChanged(nameof(IsNewArticles));
        OnPropertyChanged(nameof(CanDelete));
        OnPropertyChanged(nameof(Title));
        Rows.Clear();
        Details.Clear();
        DetailTitle = "";
    }

    [RelayCommand]
    private async Task SelectTab(string? index)
    {
        if (IsBusy || !int.TryParse(index, out var tab) || tab < 0 || tab > 2 || tab == SelectedTab) return;
        SelectedTab = tab;
        await Refresh();
    }

    [RelayCommand]
    private async Task Refresh()
    {
        if (IsBusy) return;
        IsBusy = true;
        Message = "";
        // Capture filters so changing controls during the query cannot mix result sets.
        var tab = SelectedTab;
        var from = From;
        var through = Through;
        var brand = SelectedBrand;
        var processed = IncludeProcessed;
        try
        {
            var brands = await _service.GetBrandsAsync();
            var rows = await _service.GetJournalAsync((ProductRequestKind)(tab + 1), from, through,
                brand == "Все бренды" ? null : brand, processed);
            Brands.Clear();
            Brands.Add("Все бренды");
            foreach (var name in brands) Brands.Add(name);
            SelectedBrand = Brands.Contains(brand) ? brand : "Все бренды";
            Rows.Clear();
            foreach (var row in rows) Rows.Add(row);
            Details.Clear();
            DetailTitle = "";
            Message = $"Артикулов: {Rows.Count}. Всего запрошено: {Rows.Sum(x => x.Quantity)} шт.";
            FilterDescription = $"Период: {from?.ToString("dd.MM.yyyy") ?? "без ограничения"} — " +
                $"{through?.ToString("dd.MM.yyyy") ?? "без ограничения"}. Бренд: {brand}";
        }
        catch (Exception ex) { Message = ex.GetBaseException().Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void ShowDetails(ProductRequestSummary? row)
    {
        Details.Clear();
        if (row == null) return;
        DetailTitle = $"Заявки: {row.ArticleNumber} — {row.Quantity} шт.";
        foreach (var detail in row.Details) Details.Add(detail);
    }

    [RelayCommand]
    private async Task Delete(ProductRequestSummary? row)
    {
        if (row == null || IsNewArticles || IsBusy) return;
        var ids = row.Details.Select(x => x.Id).ToArray();
        if (MessageBox.Show($"Удалить заявки на артикул {row.ArticleNumber}, отображаемые по текущим фильтрам?\n" +
                $"Заявок: {ids.Length}, количество: {row.Quantity} шт.", "Удаление заявок",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        await DeleteRequests(ids);
    }

    private bool CanDeleteDisplayed() => CanDelete && !IsBusy && Rows.Count > 0;

    [RelayCommand(CanExecute = nameof(CanDeleteDisplayed))]
    private async Task DeleteDisplayed()
    {
        if (!CanDeleteDisplayed()) return;
        // Capture the displayed snapshot, so later arrivals and hidden requests are preserved.
        var ids = Rows.SelectMany(row => row.Details).Select(request => request.Id).Distinct().ToArray();
        if (ids.Length == 0) return;
        if (MessageBox.Show($"Удалить все заявки, показанные во вкладке «{Title}»?\n\n" +
                $"{FilterDescription}\nАртикулов: {Rows.Count}. Заявок: {ids.Length}. " +
                $"Количество: {Rows.Sum(row => row.Quantity)} шт.", "Удаление показанных заявок",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        await DeleteRequests(ids);
    }

    private async Task DeleteRequests(Guid[] ids)
    {
        IsBusy = true;
        try { await _service.DeleteAsync(ids); }
        catch (Exception ex) { Message = ex.GetBaseException().Message; return; }
        finally { IsBusy = false; }
        await Refresh();
    }

    [RelayCommand]
    private async Task ToggleProcessed(ProductRequestSummary? row)
    {
        if (row == null || !IsNewArticles || IsBusy) return;
        IsBusy = true;
        try { await _service.SetProcessedAsync(row.Details.Select(x => x.Id), !row.IsProcessed); }
        catch (Exception ex) { Message = ex.GetBaseException().Message; return; }
        finally { IsBusy = false; }
        await Refresh();
    }

    private bool CanMarkDisplayedProcessed() => IsNewArticles && !IsBusy &&
        Rows.SelectMany(row => row.Details).Any(request => !request.IsProcessed);

    [RelayCommand(CanExecute = nameof(CanMarkDisplayedProcessed))]
    private async Task MarkDisplayedProcessed()
    {
        if (!CanMarkDisplayedProcessed()) return;
        // Use the displayed snapshot, including every request behind each grouped article.
        var ids = Rows.SelectMany(row => row.Details).Where(request => !request.IsProcessed)
            .Select(request => request.Id).Distinct().ToArray();
        IsBusy = true;
        try { await _service.SetProcessedAsync(ids, true); }
        catch (Exception ex) { Message = ex.GetBaseException().Message; return; }
        finally { IsBusy = false; }
        await Refresh();
    }
}
