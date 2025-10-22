using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Infrastructure.Entities;
using Infrastructure.Services;
using System.Collections.ObjectModel;
using System.Windows.Media;
using System.Windows;
using Infrastructure.Dtos;

namespace PresentationWpf.ViewModels;

public partial class TransferReceiveViewModel : ObservableObject
{
    private readonly StoreService _storeService;
    private readonly StoreExchangeService _exchangeService;
    private readonly ProductService _productService;
    public event Action? RequestClose;

    [ObservableProperty] private ObservableCollection<StoreEntity> storeList = [];
    [ObservableProperty] private StoreEntity? selectedStore;

    [ObservableProperty] private ObservableCollection<Product> artikulList = [];
    [ObservableProperty] private Product? selectedArtikul;

    [ObservableProperty] private ObservableCollection<TransferProduct> products = [];
    [ObservableProperty] private int orderQuantity;

    [ObservableProperty]
    private bool isArtikulEnabled = false;

    [ObservableProperty] private string label1Text = "Описание: Ваш магазин временно берет товар у другого магазина.";
  
    [ObservableProperty]
    private Brush contentBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C2E6ED"));


    private string currentType = "получение_товара"; // default type = outgoing

    [ObservableProperty]
    private bool isContentVisible = true; // hidden by default


    public event Action? RequestFocusArtikul;
    public event Action? RequestFocusQuantity;


    public TransferReceiveViewModel(StoreService storeService, StoreExchangeService exchangeService, ProductService productService)
    {
        _storeService = storeService;
        _exchangeService = exchangeService;
        _productService = productService;

        _ = LoadStoresAsync();
       
    }

    // === Load Stores ===
    [RelayCommand]
    private async Task LoadStoresAsync()
    {
        StoreList.Clear();
        var stores = await _storeService.GetAllAsync();
        foreach (var s in stores)
            StoreList.Add(s);
    }
       

    [RelayCommand]
    private async Task LoadProductsAsync()
    {
        Products.Clear();

        var allProducts = await _productService.GetAllProductAsync();

        foreach (var p in allProducts)
        {
            Products.Add(new TransferProduct
            {
                Artikul = p.ArticleNumber,
                Name = p.ProductName,
                Brand = p.BrandName,
                Marka = p.Marka,
                WarehouseQuantity = p.Quentity,
                Quantity = 0,
                Location = p.WarehousePlace
            });
        }
    }

    // === Приход (Incoming) ===
    [RelayCommand]
    private Task SelectIncomingAsync()
    {
        IsContentVisible = true; // ✅ show content
        currentType = "получение_товара";
        Label1Text = "Описание: Ваш магазин временно берет товар у другого магазина.";
        ContentBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C2E6ED")); // light blue
        Products.Clear();
        SelectedStore = null;
        return Task.CompletedTask;
    }

    // === Расход (Outgoing) ===
    [RelayCommand]
    private Task SelectOutgoingAsync()
    {
        IsContentVisible = true; // ✅ show content
        currentType = "передача_товара";
        Label1Text = "Описание: Ваш магазин передает товар другому магазину.";
        ContentBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8EBACD")); // light beige
        Products.Clear();
        SelectedStore = null;
        return Task.CompletedTask;
    }

    [RelayCommand]
    // === Оформить (Submit) ===
    private async Task SubmitAsync()
    {
        // 🔹 Validate store selection
        if (SelectedStore == null)
        {
            MessageBox.Show("Выберите магазин перед оформлением!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // 🔹 Validate that there is at least one product with Quantity > 0
        if (Products == null || Products.Count == 0 || !Products.Any(p => p.Quantity > 0))
        {
            MessageBox.Show("Добавьте товары с количеством больше 0 перед оформлением!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // 🔹 Create exchange entries for products that have quantity
        foreach (var product in Products.Where(p => p.Quantity > 0))
        {
            var newExchange = new StoreExchangeEntity
            {
                StoreCode = SelectedStore.StoreCode,
                ArticleNumber = product.Artikul,
                Quantity = product.Quantity,
                ExchangeType = currentType
            };

            await _exchangeService.AddAsync(newExchange);
        }
        Products.Clear();
        SelectedArtikul = null;
        OrderQuantity = 0;
        // 🔹 Refresh data
        await LoadArtikulsAsync();

        MessageBox.Show("Оформление успешно завершено!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // === Load Artikuls for ComboBox ===
    [RelayCommand]
    private async Task LoadArtikulsAsync()
    {
        ArtikulList.Clear();
        var artikuls = await _productService.GetAllProductAsync();
        foreach (var a in artikuls)
            ArtikulList.Add(a);
    }
    // === Store selection ===
    partial void OnSelectedStoreChanged(StoreEntity? value)
    {
        if (value != null)
        {
            IsArtikulEnabled = true;
            _ = LoadArtikulsAsync();
        }
        else
        {
            IsArtikulEnabled = false;
            SelectedArtikul = null;
            Products.Clear();
        }
    }


    [RelayCommand]
    private void ApplyQuantity()
    {
        if (SelectedArtikul == null || OrderQuantity <= 0)
            return;

        var product = Products.FirstOrDefault(p => p.Artikul == SelectedArtikul.ArticleNumber);
        if (product != null)
        {
            product.Quantity = OrderQuantity;
        }

        // Reset input fields
        OrderQuantity = 0;
        SelectedArtikul = null;

        // 🔹 Notify the View to focus the ComboBox again
        RequestFocusArtikul?.Invoke();
    }

    [RelayCommand]
    private void ConfirmSelectedArtikul()
    {
        if (SelectedArtikul == null)
            return;

        // Prevent duplicates
        if (Products.Any(p => p.Artikul == SelectedArtikul.ArticleNumber))
            return;

        // Add new product
        Products.Add(new TransferProduct
        {
            Artikul = SelectedArtikul.ArticleNumber,
            Name = SelectedArtikul.ProductName,
            Brand = SelectedArtikul.BrandName,
            Marka = SelectedArtikul.Marka,
            WarehouseQuantity = SelectedArtikul.Quentity,
            Quantity = 0,
            Location = SelectedArtikul.WarehousePlace,
            CurrentType = currentType
        });

        RequestFocusQuantity?.Invoke();
    }



    [RelayCommand]
    private void Close()
    {
        // Clear all temporary state
        SelectedStore = null;
        SelectedArtikul = null;
        OrderQuantity = 0;
        Label1Text = string.Empty;
        IsArtikulEnabled = false;
        IsContentVisible = false;
        Products.Clear();
        ArtikulList.Clear();
                
        // Notify parent (StockViewModel) to close the view
        RequestClose?.Invoke();
    }


}

// === DTO for DataGrid ===
public partial class TransferProduct : ObservableObject
{
    [ObservableProperty] private string artikul = "";
    [ObservableProperty] private string name = "";
    [ObservableProperty] private string brand = "";
    [ObservableProperty] private string marka = "";
    [ObservableProperty] private int warehouseQuantity;
    [ObservableProperty] private string location = "";

    // 👇 new field to know if we're in "расход"
    public string CurrentType { get; set; } = "получение_товара";

    private int quantity;
    public int Quantity
    {
        get => quantity;
        set
        {
            if (value < 0)
                value = 0;

            // ✅ Only enforce limit when it's "расход"
            if (CurrentType == "передача_товара" && value > WarehouseQuantity)
            {
                MessageBox.Show(
                    $"Количество не может превышать остаток на складе ({WarehouseQuantity}).",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);

                value = WarehouseQuantity;
            }

            if (quantity != value)
            {
                quantity = value;
                OnPropertyChanged();
            }
        }
    }
}
