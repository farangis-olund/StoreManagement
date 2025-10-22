using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Infrastructure.Dtos;
using Infrastructure.Entities;
using Infrastructure.Services;
using System.Collections.ObjectModel;

namespace PresentationWpf.ViewModels;
public partial class StockLocationViewModel : ObservableObject
{
    private readonly ProductService _productService;
    public event Action? RequestClose;
    public StockLocationViewModel(ProductService productService)
    {
        _productService = productService;
        _ = LoadArticlesAsync();
    }

    [ObservableProperty] private ObservableCollection<string> _articles = [];
    [ObservableProperty] private string? _selectedArticle;
    [ObservableProperty] private string? _newWarehousePlace;
    [ObservableProperty] private ObservableCollection<Product> _stockItems = [];

    private async Task LoadArticlesAsync()
    {
        var products = await _productService.GetAllProductAsync();
        Articles = new ObservableCollection<string>(products.Select(p => p.ArticleNumber));
    }

    // 🔹 Called when SelectedArticle changes
    partial void OnSelectedArticleChanged(string? value)
    {
        _ = LoadSelectedProductAsync();
    }

    private async Task LoadSelectedProductAsync()
    {
        StockItems.Clear();

        if (!string.IsNullOrWhiteSpace(SelectedArticle))
        {
            var product = await _productService.GetProductByArticleAsync(SelectedArticle);
            if (product != null)
            {
                StockItems.Add(product);

                // 🔹 Also set the current place in the textbox
                NewWarehousePlace = product.WarehousePlace;
            }
        }
    }

    public async Task UpdateWarehousePlaceAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedArticle) || string.IsNullOrWhiteSpace(NewWarehousePlace))
            return;

        await _productService.UpdateWarehousePlaceAsync(SelectedArticle, NewWarehousePlace);

        // Reload and refresh grid
        await LoadSelectedProductAsync();

        // Optional: clear textbox after saving
        // NewWarehousePlace = string.Empty;
    }

    [RelayCommand]
    private void Close()
    {
        SelectedArticle = null;
        NewWarehousePlace = string.Empty;
       
        RequestClose?.Invoke();   // 🔹 trigger window close
    }
}
