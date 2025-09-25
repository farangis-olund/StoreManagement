using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Infrastructure.Dtos;
using Infrastructure.Entities;
using Infrastructure.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;


namespace PresentationWpf.ViewModels;

public partial class ProductViewModel : ObservableObject
{
    private readonly ProductService _productService;
    public event Action? RequestClose;
    private readonly BrandService _brandService;
    private readonly GroupService _groupService;
    [ObservableProperty]
    private ObservableCollection<Product> products = [];

    [ObservableProperty]
    private Product? selectedProduct;

    public ICollectionView ProductsView { get; }

    [ObservableProperty]
    private string filterText = string.Empty;

    [ObservableProperty]
    private bool searchAllWords;

    public ProductViewModel(ProductService productService, BrandService brandService, GroupService groupService)
    {
        _productService = productService;
        _brandService = brandService;
        _groupService = groupService;
        ProductsView = CollectionViewSource.GetDefaultView(Products);
        ProductsView.Filter = FilterProducts;

        _ = RefreshAsync();
    }

    // === NEW CLOSE COMMAND ===
    [RelayCommand]
    private void Close()
    {
        RequestClose?.Invoke();
        //_stock.CloseCatalog();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        Products.Clear();
        var items = await _productService.GetAllProductAsync();
        foreach (var p in items)
            Products.Add(p);
        ProductsView.Refresh();
    }


    [RelayCommand]
    private async Task SaveAsync()
    {
        if (Products is null || Products.Count == 0) return;

        int addedCount = 0;
        int updatedCount = 0;

        foreach (var product in Products)
        {
            // ✅ resolve brand/group IDs from names
            var brandEntity = await _brandService.AddBrandAsync(product.BrandName); 
            var groupEntity = await _groupService.AddGroupAsync(product.GroupName); 



            // проверяем — есть ли уже такой артикул
            var exists = await _productService.ExistsByArticleAsync(product.ArticleNumber);
           
            var entity = new ProductEntity
            {
                ArticleNumber = product.ArticleNumber,
                ProductName = product.ProductName,
                Model = product.Model,
                Marka = product.Marka,
                Alternative = product.Alternative,
                GroupId = brandEntity.Id,
                BrandId = groupEntity.Id,
                Quentity = product.Quentity,
                WarehousePlace = product.WarehousePlace,
                MinRemainingQuantity = product.MinRemainingQuantity,
                RetailPriceEuro = product.RetailPriceEuro,
                WholesalePriceEuro = product.WholesalePriceEuro,
                ServicePriceEuro = product.ServicePriceEuro,
                WholesalePrice1Euro = product.WholesalePrice1Euro,
                NetPrice = product.NetPrice,
                SmallWholesalePrice = product.SmallWholesalePrice
            };

            if (exists)
            {
               
                await _productService.UpdateProductAsync(entity);
                updatedCount++;
            }
            else
            {
                await _productService.AddProductAsync(entity);
                addedCount++;
            }
        }

        await RefreshAsync();

        // показываем сообщение
        MessageBox.Show(
            $"Успешно сохранено.\nДобавлено: {addedCount}, Обновлено: {updatedCount}",
            "Сохранение",
            MessageBoxButton.OK,
            MessageBoxImage.Information
        );
    }

    [RelayCommand]
    public async Task DeleteAsync(System.Collections.IList? itemsToDelete)
    {
        if (itemsToDelete == null || itemsToDelete.Count == 0)
            return;

        var products = itemsToDelete.Cast<Product>().ToList();

        int count = itemsToDelete.Count;

        var result = MessageBox.Show(
            $"Вы действительно хотите удалить {count} артикул{(count > 1 ? "ов" : "")}?",
            "Подтверждение удаления",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            foreach (var product in products)
            {
                var success = await _productService.DeleteProductByArticleAsync(product.ArticleNumber);
                if (success)
                    Products.Remove(product);
            }
        }
    }


    partial void OnFilterTextChanged(string value) => ApplyFilter();
    partial void OnSearchAllWordsChanged(bool value) => ApplyFilter();

    private bool FilterProducts(object obj)
    {
        if (obj is not Product product) return false;
        if (string.IsNullOrWhiteSpace(FilterText)) return true;

        var words = FilterText.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        bool ContainsWord(string word) =>
            (product.ArticleNumber?.Contains(word, StringComparison.OrdinalIgnoreCase) ?? false) ||
            (product.ProductName?.Contains(word, StringComparison.OrdinalIgnoreCase) ?? false) ||
            (product.Model?.Contains(word, StringComparison.OrdinalIgnoreCase) ?? false) ||
            (product.Marka?.Contains(word, StringComparison.OrdinalIgnoreCase) ?? false) ||
            (product.BrandName?.Contains(word, StringComparison.OrdinalIgnoreCase) ?? false) ||
            (product.GroupName?.Contains(word, StringComparison.OrdinalIgnoreCase) ?? false);

        return SearchAllWords
            ? words.All(ContainsWord)
            : words.Any(ContainsWord);
    }

    private void ApplyFilter() => ProductsView.Refresh();
}
