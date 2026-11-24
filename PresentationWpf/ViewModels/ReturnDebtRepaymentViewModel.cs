using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Infrastructure.Contexts;
using Infrastructure.Dtos;
using Infrastructure.Entities;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using PresentationWpf.Documents;
using PresentationWpf.Views;
using QuestPDF.Fluent;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Windows;
using System.Windows.Media;

namespace PresentationWpf.ViewModels
{
    public partial class ReturnDebtRepaymentViewModel : ObservableObject
    {
        private readonly StoreExchangeService _exchangeService;
        private readonly StoreService _storeService;
        private readonly ProductService _productService;
        private readonly OrganizationInfoService _organizationInfoService;
        private readonly IDbContextFactory<DatabaseContext> _dbFactory;
        private readonly StoreEntity _allStores = new() { StoreCode = "Все", Name = "Все" };
        private readonly SemaphoreSlim _queryGate = new(1, 1);
        private int _selectionVersion;
        public ReturnDebtRepaymentViewModel(StoreExchangeService exchangeService, StoreService storeService, ProductService productService,
            OrganizationInfoService organizationInfoService, IDbContextFactory<DatabaseContext> dbFactory)
        {
            _exchangeService = exchangeService;
            _storeService = storeService;
            _productService = productService;
            _organizationInfoService = organizationInfoService;
            _dbFactory = dbFactory;
          
            _ = LoadAsync();
        }

        // ─── Properties ───────────────────────────────

        [ObservableProperty] private ObservableCollection<StoreEntity> stores = [];
        [ObservableProperty] private StoreEntity? selectedStore;
        public bool IsAllStores => ReferenceEquals(SelectedStore, _allStores);
        public bool CanSubmit => SelectedStore != null && !IsAllStores;
        public bool CanPreview => SelectedStore != null;

        [ObservableProperty] private ObservableCollection<DebtRepaymentRow> products=[];
        [ObservableProperty] private ObservableCollection<DebtRepaymentRow> artikulList = [];   

        [ObservableProperty] private DebtRepaymentRow? selectedArtikul;

        [ObservableProperty] private string currentType = "получение_возврата"; // получение_возврата / погащение_долга
        [ObservableProperty] private string descriptionText = "Ваш магазин получает товар обратно, который был передан ранее другому магазину.";
        [ObservableProperty] private bool isStoreEnabled;
        [ObservableProperty] private bool isArtikulEnabled;
        [ObservableProperty] private bool isContentVisible=true;
        [ObservableProperty] private int orderQuantity;

        [ObservableProperty] private bool isInputVisible = true;

        [ObservableProperty] private Brush contentBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#BDB76B"));
        
        public event Action? RequestClose;

        public event Action? RequestFocusArtikul;
        public event Action? RequestFocusQuantity;


        // ─── Load Stores ───────────────────────────────
        [RelayCommand]
        private async Task LoadAsync()
        {
            Stores.Clear();
            var list = await _storeService.GetAllAsync();
            Stores.Add(_allStores);
            foreach (var s in list.OrderBy(x => x.StoreCode, StringComparer.OrdinalIgnoreCase))
                Stores.Add(s);
        }

        // ─── Button handlers ───────────────────────────
        [RelayCommand]
        private void SelectReturn()
        {
            CurrentType = "получение_возврата";
            DescriptionText = "Ваш магазин получает товар обратно, который был передан ранее другому магазину.";
            IsContentVisible = true;
            IsStoreEnabled = true;
            IsArtikulEnabled = false;
            Products.Clear();
            ArtikulList.Clear();
            SelectedStore = null;
            IsInputVisible = true;
            ContentBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#BDB76B")); ;
        }

        [RelayCommand]
        private void SelectRepayment()
        {
            CurrentType = "погащение_долга";
            DescriptionText = "Ваш магазин возвращает ранее полученный товар другому магазину.";
            IsContentVisible = true;
            IsStoreEnabled = true;
            IsArtikulEnabled = false;
            Products.Clear();
            ArtikulList.Clear();
            SelectedStore = null;
            IsInputVisible = false;
            ContentBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#708090"));

        }

        partial void OnSelectedStoreChanged(StoreEntity? value)
        {
            _selectionVersion++;
            Products.Clear(); ArtikulList.Clear(); SelectedArtikul = null; OrderQuantity = 0;
            IsArtikulEnabled = false;
            IsInputVisible = CurrentType == "получение_возврата" && !IsAllStores;
            OnPropertyChanged(nameof(IsAllStores)); OnPropertyChanged(nameof(CanSubmit)); OnPropertyChanged(nameof(CanPreview));
            SubmitCommand.NotifyCanExecuteChanged(); ShowCommand.NotifyCanExecuteChanged();
            if (value != null && !IsAllStores)
                _ = OnStoreSelectedAsync(); 
        }

        private async Task<ExchangeProductDto[]> ReadProductsAsync(string? storeCode, string type)
        {
            await _queryGate.WaitAsync();
            try
            {
                return (await _exchangeService.GetExchangeProductsAsync(storeCode, type))
                    .OrderBy(x => x.StoreCode, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(x => x.ArticleNumber, StringComparer.OrdinalIgnoreCase).ToArray();
            }
            finally { _queryGate.Release(); }
        }


        // ─── When store selected ───────────────────────
        [RelayCommand]
       
        private async Task OnStoreSelectedAsync()
        {
            var store = SelectedStore;
            var type = CurrentType;
            var version = _selectionVersion;
            if (store == null || IsAllStores)
            {
                return;
            }

            Products.Clear();
            ArtikulList.Clear();

            if (CurrentType == "получение_возврата")
            {
                var result = await ReadProductsAsync(store.StoreCode, "передача_товара");
                if (version != _selectionVersion || type != CurrentType) return;
                if (result == null || !result.Any())
                {
                    MessageBox.Show("Выбранный магазин уже возвратил все товары!");
                    return;
                }

                foreach (var p in result)
                {
                    ArtikulList.Add(new DebtRepaymentRow
                    {
                        Artikul = p.ArticleNumber,
                        Name = p.ProductName,
                        Brand = p.BrandName,
                        Marka = p.Marka,
                        Store = p.StoreCode,
                        Debt = p.Debt,
                        WarehouseQuantity = p.Quantity,
                        Location = p.WarehousePlace,
                        CurrentType = CurrentType
                    });
                }

                IsArtikulEnabled = true;
            }
            else if (CurrentType == "погащение_долга")
            {
                var result = await ReadProductsAsync(store.StoreCode, "получение_товара");
                if (version != _selectionVersion || type != CurrentType) return;
                if (result == null || !result.Any())
                {
                    MessageBox.Show("Ваш магазин уже погасил весь долг выбранному магазину или не достаточно товаров на складе!");
                    return;
                }

                foreach (var p in result)
                {
                    int allowedQty = p.Quantity < p.Debt ? p.Quantity : p.Debt;
                    Products.Add(new DebtRepaymentRow
                    {
                        Artikul = p.ArticleNumber,
                        Name = p.ProductName,
                        Brand = p.BrandName,
                        Marka = p.Marka,
                        Store = p.StoreCode,
                        Debt = p.Debt,
                        Quantity = allowedQty,
                        Location = p.WarehousePlace,
                        WarehouseQuantity = p.Quantity,
                        CurrentType = CurrentType
                    });
                }
            }
        }

        [RelayCommand]
        private void ConfirmSelectedArtikul()
        {
            if (SelectedArtikul == null)
                return;
                        
            if (Products.Any(x => x.Artikul == SelectedArtikul.Artikul))
            {
                MessageBox.Show("Этот артикул уже добавлен!", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Products.Add(new DebtRepaymentRow
            {
                Artikul = SelectedArtikul.Artikul,
                Name = SelectedArtikul.Name,
                Brand = SelectedArtikul.Brand,
                Marka = SelectedArtikul.Marka,
                Store = SelectedArtikul.Store,
                Debt = SelectedArtikul.Debt,
                Quantity = SelectedArtikul.Quantity,
                Location = SelectedArtikul.Location,
                CurrentType = CurrentType,
                WarehouseQuantity = SelectedArtikul.WarehouseQuantity
            });
                       
            RequestFocusQuantity?.Invoke();
        }

        [RelayCommand]
        private void ApplyQuantity()
        {
            if (SelectedArtikul == null || OrderQuantity <= 0)
                return;

            var product = Products.FirstOrDefault(p => p.Artikul == SelectedArtikul.Artikul);
            if (product != null)
            {
                product.Quantity = OrderQuantity;
            }
            OrderQuantity = 0;
            SelectedArtikul = null;
            RequestFocusArtikul?.Invoke();
        }

        [RelayCommand(CanExecute = nameof(CanSubmit))]
        private async Task SubmitAsync()
        {
            if (SelectedStore == null || IsAllStores)
            {
                ShowReportMessage("Для оформления выберите конкретный магазин.");
                return;
            }

            if (Products.Count == 0 || !Products.Any(p => p.Quantity > 0))
            {
                MessageBox.Show("Не указано количество товаров!");
                return;
            }

            // Determine SQL operation type (for saving)
            string sqlType = CurrentType switch
            {
                "получение_возврата" => "передача_товара",
                "погащение_долга" => "получение_товара",
                _ => ""
            };

            // Save entries
            var savedItems = new List<StoreExchangeEntity>();
            foreach (var p in Products.Where(x => x.Quantity > 0))
            {
                var newExchange = new StoreExchangeEntity
                {
                    StoreCode = SelectedStore.StoreCode,
                    ArticleNumber = p.Artikul,
                    Quantity = p.Quantity,
                    ExchangeType = CurrentType
                };

                await _exchangeService.AddAsync(newExchange);
                savedItems.Add(newExchange); 
            }

            // === ✅ Build Transfer Report ===
            try
            {
                var orgInfo = (await _organizationInfoService.GetShopDisplayAsync())?.Trim() ?? string.Empty;
                var date = DateTime.Now;

                // Get related product info for the report
                await using var db = await _dbFactory.CreateDbContextAsync();
                var artikuls = savedItems.Select(x => x.ArticleNumber).ToList();
                var products = await db.Products
                    .Include(x => x.Brand)
                    .Include(x => x.Group)
                    .Where(x => artikuls.Contains(x.ArticleNumber))
                    .ToListAsync();

                var lines = (from s in savedItems
                             join p in products on s.ArticleNumber equals p.ArticleNumber
                             select new TransferReportLine
                             {
                                 StoreCode = s.StoreCode,
                                 Article = p.ArticleNumber,
                                 ProductName = p.ProductName,
                                 Brand = p.Brand.BrandName,
                                 Marka = p.Marka,
                                 Model = p.Model,
                                 WarehousePlace = p.WarehousePlace,
                                 Quantity = s.Quantity
                             }).ToList();

              
                var document = new TransferReportDocument(orgInfo, date, CurrentType, lines, DescriptionText, "report", SelectedStore.StoreCode);

                string folder = Path.Combine(Path.GetTempPath(), "Reports");
                Directory.CreateDirectory(folder);
                string filePath = Path.Combine(folder, $"TransferReport_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");

                document.GeneratePdf(filePath);

                // === ✅ Preview PDF ===
                var preview = new DocumentPreviewView(filePath);
                var window = new Window
                {
                    Title = "Отчёт о перемещении товаров",
                    Content = preview,
                    Width = 900,
                    Height = 1000,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen
                };

                // === ✅ Done ===
                MessageBox.Show("Оформление успешно завершено!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

                window.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при создании отчёта: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            Products.Clear();
            _ = OnStoreSelectedAsync();
        }

        [RelayCommand(CanExecute = nameof(CanPreview))]
        private async Task Show()
        {
            var store = SelectedStore;
            var type = CurrentType;
            var version = _selectionVersion;
            var allStores = IsAllStores;
            var description = DescriptionText;
            if (store == null)
            {
                ShowReportMessage("Выберите магазин для отчёта!");
                return;
            }

            try
            {
                var result = await ReadProductsAsync(allStores ? null : store.StoreCode,
                    type == "получение_возврата" ? "передача_товара" : "получение_товара");
                if (version != _selectionVersion || type != CurrentType) return;
                if (result.Length == 0)
                {
                    ShowReportMessage("По выбранным магазинам нет непогашенных товаров для отчёта.");
                    return;
                }
                var orgInfo = (await GetReportShopDisplayAsync())?.Trim() ?? "";
                if (version != _selectionVersion || type != CurrentType) return;
                var lines = result.Select(p => new TransferReportLine
                {
                    StoreCode = p.StoreCode,
                    StoreName = Stores.FirstOrDefault(s => s.StoreCode == p.StoreCode && !ReferenceEquals(s, _allStores))?.Name ?? "",
                    Article = p.ArticleNumber, ProductName = p.ProductName, Brand = p.BrandName,
                    Marka = p.Marka, Model = p.Model, WarehousePlace = p.WarehousePlace, Quantity = p.Debt
                }).ToList();
                OpenReportPreview(new TransferReportDocument(orgInfo, DateTime.Now, type, lines,
                    description, "view", allStores ? "Все" : store.StoreCode, groupByStore: allStores));
            }
            catch (Exception ex)
            {
                ShowReportMessage($"Ошибка при создании отчёта: {ex.Message}");
            }
        }

        protected virtual Task<string?> GetReportShopDisplayAsync() => _organizationInfoService.GetShopDisplayAsync();

        protected virtual void ShowReportMessage(string message) =>
            MessageBox.Show(message, "Отчёт о перемещении товаров", MessageBoxButton.OK, MessageBoxImage.Information);

        protected virtual void OpenReportPreview(TransferReportDocument document)
        {
            var folder = Path.Combine(Path.GetTempPath(), "Reports");
            Directory.CreateDirectory(folder);
            var filePath = Path.Combine(folder, $"TransferReportPreview_{DateTime.Now:yyyyMMdd_HHmmss_fff}.pdf");
            document.GeneratePdf(filePath);
            new Window
            {
                Title = "Предварительный отчёт о перемещении товаров", Content = new DocumentPreviewView(filePath),
                Width = 900, Height = 1000, WindowStartupLocation = WindowStartupLocation.CenterScreen
            }.ShowDialog();
        }

        [RelayCommand]
        private void Close()
        {
           
            SelectedStore = null;
            SelectedArtikul = null;
            OrderQuantity = 0;
            IsArtikulEnabled = false;
            IsContentVisible = false;
            Products.Clear();
            ArtikulList.Clear();
                        
            RequestClose?.Invoke();
        }

    }

    // ─── Row Model ───────────────────────────────────
    public partial class DebtRepaymentRow : ObservableObject
    {
        [ObservableProperty] private string artikul = "";
        [ObservableProperty] private string name = "";
        [ObservableProperty] private string brand = "";
        [ObservableProperty] private string marka = "";
        [ObservableProperty] private string store = "";
        [ObservableProperty] private string location = "";
        [ObservableProperty] private int warehouseQuantity;
        [ObservableProperty] private int debt;
        [ObservableProperty] private string currentType = "";

        private int quantity;
        public int Quantity
        {
            get => quantity;
            set
            {
                if (value < 0)
                    value = 0;

                // 🟡 validation logic for "погащение_долга"
                if (CurrentType == "погащение_долга")
                {
                    if (WarehouseQuantity <= 0)
                    {
                        MessageBox.Show("Количество на складе нуль или меньше — погащение невозможно!");
                        value = 0;
                    }
                    else if (value > WarehouseQuantity)
                    {
                        MessageBox.Show($"Количество превышает остаток на складе ({WarehouseQuantity})!");
                        value = WarehouseQuantity;
                    }
                    else if (value > Debt)
                    {
                        MessageBox.Show($"Количество превышает сумму долга ({Debt})!");
                        value = Debt;
                    }
                }
                else if (CurrentType == "получение_возврата")
                {
                    // For returns, only limit by debt (no warehouse constraint)
                    if (value > Debt)
                    {
                        MessageBox.Show($"Количество превышает сумму долга ({Debt})!");
                        value = Debt;
                    }
                }

                if (quantity != value)
                {
                    quantity = value;
                    OnPropertyChanged();
                }
            }
        }
    }
}
