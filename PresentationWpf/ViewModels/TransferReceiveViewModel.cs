using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Infrastructure.Entities;
using Infrastructure.Services;
using System.Collections.ObjectModel;
using System.Windows.Media;
using System.Windows;
using Infrastructure.Dtos;
using PresentationWpf.Views;
using System.IO;
using QuestPDF.Fluent;
using Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore;
using PresentationWpf.Services;

namespace PresentationWpf.ViewModels;

public partial class TransferReceiveViewModel : ObservableObject
{
    private readonly StoreService _storeService;
    private readonly StoreExchangeService _exchangeService;
    private readonly ProductService _productService;
    private readonly OrganizationInfoService _organizationInfoService;
    private readonly IDbContextFactory<DatabaseContext> _dbFactory;
    private readonly UserSessionService _userSessionService;

    public TransferReceiveViewModel(StoreService storeService,
        StoreExchangeService exchangeService,
        ProductService productService,
        OrganizationInfoService organizationInfoService, IDbContextFactory<DatabaseContext> dbFactory,
        UserSessionService userSessionService)
    {
        _storeService = storeService;
        _exchangeService = exchangeService;
        _productService = productService;
        _organizationInfoService = organizationInfoService;
        _dbFactory = dbFactory;
        _userSessionService = userSessionService;

        _ = LoadStoresAsync();

    }
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
    private async Task SubmitAsync()
    {
        // 🔹 Validate store selection
        if (SelectedStore == null)
        {
            MessageBox.Show("Выберите магазин перед оформлением!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // 🔹 Validate product list
        if (Products == null || Products.Count == 0 || !Products.Any(p => p.Quantity > 0))
        {
            MessageBox.Show("Добавьте товары с количеством больше 0 перед оформлением!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // 🔹 Create exchange entries for products that have quantity
        var savedItems = new List<StoreExchangeEntity>();

        foreach (var product in Products.Where(p => p.Quantity > 0))
        {
            var newExchange = new StoreExchangeEntity
            {
                StoreCode = SelectedStore.StoreCode,
                ArticleNumber = product.Artikul,
                Quantity = product.Quantity,
                ExchangeType = currentType // e.g. "приход" or "расход"
            };

            await _exchangeService.AddAsync(newExchange);
            savedItems.Add(newExchange);
        }

        // 🔴 SAVE ONLY IF "передача_товара"
        if (currentType == "передача_товара")
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            int totalQty = Products.Where(p => p.Quantity > 0).Sum(p => p.Quantity);

            // If you DON'T have price → keep 0
            decimal totalAmount = 0;

            // OPTIONAL (only if you want real total money)
            foreach (var p in Products.Where(p => p.Quantity > 0))
            {
                var dbProduct = await db.Products
                    .FirstOrDefaultAsync(x => x.ArticleNumber == p.Artikul);

                if (dbProduct != null)
                {
                    // ⚠️ change to your real price field
                    totalAmount += p.Quantity * (dbProduct.PriceLevel1);
                }
            }

            var summary = new StoreTransferSummaryEntity
            {
                Date = DateTime.Now,
                StoreCode = SelectedStore.StoreCode,
                TotalQuantity = totalQty,
                TotalAmount = totalAmount,
                UserId = _userSessionService.UserId
            };

            db.StoreTransferSummaries.Add(summary);
            await db.SaveChangesAsync();
        }

        // ✅ Clear UI
        Products.Clear();
        SelectedArtikul = null;
        OrderQuantity = 0;

        // ✅ Refresh data
        await LoadArtikulsAsync();

        // ✅ Prepare report data
        var orgInfo = (await _organizationInfoService.GetShopDisplayAsync())?.Trim() ?? string.Empty;
        var date = DateTime.Now;

        // Load product details for report
        var reportLines = new List<TransferReportLine>();
        await using (var db = await _dbFactory.CreateDbContextAsync())
        {
            foreach (var item in savedItems)
            {
                var product = await db.Products
                    .Include(p => p.Brand)
                    .Include(p => p.Group)
                    .FirstOrDefaultAsync(p => p.ArticleNumber == item.ArticleNumber);

                reportLines.Add(new TransferReportLine
                {
                    Article = item.ArticleNumber,
                    ProductName = product?.ProductName ?? string.Empty,
                    Brand = product?.Brand?.BrandName ?? string.Empty,
                    Marka = product?.Marka ?? string.Empty,
                    Model = product?.Model ?? string.Empty,
                    Quantity = item.Quantity,
                    WarehousePlace = product?.WarehousePlace ?? string.Empty
                });
            }
        }

        // ✅ Create and generate PDF
        var report = new Documents.TransferReportDocument(orgInfo, date, currentType, reportLines, Label1Text, "report", SelectedStore.StoreCode);

        string folder = Path.Combine(Path.GetTempPath(), "Reports");
        Directory.CreateDirectory(folder);
        string filePath = Path.Combine(folder, $"Transfer_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
        report.GeneratePdf(filePath);

        // ✅ Show print preview
        var preview = new DocumentPreviewView(filePath);
        var window = new Window
        {
            Title = $"Отчёт о {currentType}е товаров",
            Content = preview,
            Width = 900,
            Height = 1000,
            WindowStartupLocation = WindowStartupLocation.CenterScreen
        };
        MessageBox.Show("Оформление успешно завершено!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
        window.ShowDialog();

       
    }
    
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

public class TransferReportLine
{
    public string Article { get; set; } = "";
    public string ProductName { get; set; } = "";
    public string Brand { get; set; } = "";
    public string Marka { get; set; } = "";
    public string Model { get; set; } = "";
    public int Quantity { get; set; }
    public string WarehousePlace { get; set; } = "";

}
