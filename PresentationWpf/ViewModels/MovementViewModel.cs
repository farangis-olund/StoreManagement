using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Infrastructure.Dtos;
using Infrastructure.Helpers;
using Infrastructure.Services;
using PresentationWpf.Dtos;
using System;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace PresentationWpf.ViewModels
{
    public partial class MovementViewModel : ObservableObject
    {
        private readonly ProductService _productService;
        private readonly OrganizationInfoService _organizationInfoService;
        private readonly ExportHelper _exportHelper;
        public event Action? RequestClose;
        public MovementViewModel(ProductService productService, OrganizationInfoService organizationInfoService, ExportHelper exportHelper)
        {
            _productService = productService;
            _organizationInfoService = organizationInfoService;
            _exportHelper = exportHelper;
            _ = InitializeAsync();
        }

        // === Properties ===
        [ObservableProperty] private ObservableCollection<string> _articles = [];
        [ObservableProperty] private string? _selectedArticle;
        [ObservableProperty] private int _quantity;
        [ObservableProperty] private int _dbQuantity;
        [ObservableProperty] private DateTime _movementDate = DateTime.Today;

        [ObservableProperty] private ObservableCollection<ProductRow> _inventory = [];

        [ObservableProperty] private ObservableCollection<MovementChangeDto> _movementChanges = [];

        [ObservableProperty] private bool _isIncoming;
        [ObservableProperty] private bool _isOutgoing;

        // === Initialization ===
        [RelayCommand]
        public async Task InitializeAsync()
        {
            // load only articles list for ComboBox
            var products = await _productService.GetAllProductAsync();
            Articles = new ObservableCollection<string>(products.Select(p => p.ArticleNumber));
            IsIncoming = false;
            IsOutgoing = false;
            // ✅ Start with empty inventory
            Inventory.Clear();
        }

        // 🔹 React to ComboBox selection
        partial void OnSelectedArticleChanged(string? value)
        {
            _ = RefreshInventoryForSelectedAsync();
        }

        private async Task RefreshInventoryForSelectedAsync()
        {
            Inventory.Clear();

            if (!string.IsNullOrEmpty(SelectedArticle))
            {
                var product = await _productService.GetProductByArticleAsync(SelectedArticle);
                if (product != null)
                {
                    Inventory.Add(new ProductRow(product));
                    DbQuantity = product.Quentity;
                }
            }
        }


        // === Commands ===
        [RelayCommand]
        public async Task AddIncomingAsync()
        {
            if (string.IsNullOrEmpty(SelectedArticle) || Quantity <= 0)
                return;

            var product = await _productService.GetProductByArticleAsync(SelectedArticle);
            if (product == null)
            {
                MessageBox.Show("Продукт не найден.");
                return;
            }

            var newTotal = product.Quentity + Quantity;

            await _productService.AddQuantitiesByArticlesAsync(
                new[] { (SelectedArticle, Quantity) });

            MovementChanges.Add(new MovementChangeDto
            {
                Article = SelectedArticle,
                Quantity = Quantity,
                QuantityDb = product.Quentity,
                Total = newTotal,
                Location = product.WarehousePlace
            });

            // ✅ Update only DbQuantity and inventory row
            var existing = Inventory.FirstOrDefault(p => p.ArticleNumber == SelectedArticle);
            if (existing != null)
            {
                existing.Quentity = newTotal; // will trigger INotifyPropertyChanged
            }


            Quantity = 0; // reset for next input
        }


        [RelayCommand]
        public async Task AddOutgoingAsync()
        {
            if (string.IsNullOrEmpty(SelectedArticle) || Quantity <= 0)
                return;

            var product = await _productService.GetProductByArticleAsync(SelectedArticle);
            if (product == null)
            {
                MessageBox.Show("Продукт не найден.");
                return;
            }

            if (product.Quentity < Quantity)
            {
                MessageBox.Show("Недостаточно на складе.");
                return;
            }

            var newTotal = product.Quentity - Quantity;

            // ✅ Now we can just call service
            var result = await _productService.DeductStockAsync(
                new[] { new StockDeductionItem(SelectedArticle, Quantity) });

            if (!result.Success)
            {
                MessageBox.Show("Недостаточно на складе.");
                return;
            }

            MovementChanges.Add(new MovementChangeDto
            {
                Article = SelectedArticle,
                Quantity = Quantity,
                QuantityDb = product.Quentity,
                Total = newTotal,
                Location = product.WarehousePlace
            });

            // ✅ Update only DbQuantity and inventory row
            DbQuantity = newTotal;
            // 🔹 Update the Inventory DataGrid row
            var existing = Inventory.FirstOrDefault(p => p.ArticleNumber == SelectedArticle);
            if (existing != null)
            {
                existing.Quentity = newTotal; // will trigger INotifyPropertyChanged
            }

            Quantity = 0;
        }

        [RelayCommand]
        private void Clear()
        {
            MovementChanges.Clear();
        }

        [RelayCommand]
        private void Close()
        {
            // Clear all fields
            MovementChanges.Clear();
            SelectedArticle = null;
            Quantity = 0;
            DbQuantity = 0;
            MovementDate = DateTime.Today;
            IsIncoming = false;
            IsOutgoing = false;

            // Notify host to close window
            RequestClose?.Invoke();
        }

        [RelayCommand]
        private void Print()
        {
            if (!MovementChanges.Any())
            {
                MessageBox.Show("Нет изменений для печати.");
                return;
            }

            string report = string.Join(Environment.NewLine,
                MovementChanges.Select(c => $"{c.Article} | {c.Quantity} | {c.QuantityDb} | {c.Total} | {c.Location}"));

            MessageBox.Show(report, "Печать изменений");
        }

        [RelayCommand]
        private async Task ImportReceiptFromExcelAsync()
        {
            if (await _productService.IsStockMovementExistAsync(MovementDate, "Receipt"))
            {
                MessageBox.Show($"Receipt already exists for {MovementDate:dd.MM.yyyy}!");
                return;
            }

            var org = await _organizationInfoService.GetAsync();
            if (org == null || string.IsNullOrWhiteSpace(org.ImportPath))
            {
                MessageBox.Show("Import path not configured!");
                return;
            }

            string filePath = Path.Combine(org.ImportPath, $"Receipt {MovementDate:dd.MM.yyyy}.xlsx");

            await ProcessStockMovementFromExcelAsync("Receipt", filePath, "экспортЧека", MovementDate);
        }

        [RelayCommand]
        private async Task ImportWriteOffFromExcelAsync()
        {
            if (await _productService.IsStockMovementExistAsync(MovementDate, "WriteOff"))
            {
                MessageBox.Show($"Write-off already exists for {MovementDate:dd.MM.yyyy}!");
                return;
            }

            var org = await _organizationInfoService.GetAsync();
            if (org == null || string.IsNullOrWhiteSpace(org.ImportPath))
            {
                MessageBox.Show("Import path not configured!");
                return;
            }

            string filePath = Path.Combine(org.ImportPath, $"WriteOff {MovementDate:dd.MM.yyyy}.xlsx");

            await ProcessStockMovementFromExcelAsync("WriteOff", filePath, "экспортЧека", MovementDate);
        }

        private async Task ProcessStockMovementFromExcelAsync(string movementType, string filePath, string sheetName, DateTime date)
        {
            DataTable? dt = _exportHelper.ImportExcel(filePath, sheetName);

            if (dt == null || dt.Rows.Count == 0)
            {
                MessageBox.Show("Excel file not found or empty.");
                return;
            }

            int processedCount = 0;
            await _productService.ClearStockImportErrorsAsync();
            HasImportErrors = false;
            MovementChanges.Clear();

            foreach (DataRow dr in dt.Rows)
            {
                string article = dr[0].ToString() ?? "";
                if (!int.TryParse(dr[1].ToString(), out int qty)) continue;

                if (await _productService.ExistsByArticleAsync(article))
                {
                    processedCount++;

                    if (movementType == "Receipt")
                        await _productService.UpdateProductQuantityAsync(qty, article, "+");
                    else
                        await _productService.UpdateProductQuantityAsync(qty, article, "-");

                    int newQty = await _productService.GetProductQuantityAsync(article);
                    var product = await _productService.GetProductByArticleAsync(article);

                    MovementChanges.Add(new MovementChangeDto
                    {
                        Article = article,
                        Quantity = qty,
                        Type = movementType,
                        Total = newQty,
                        Location = product?.WarehousePlace ?? ""
                    });
                }
                else
                {
                    await _productService.AddStockImportErrorAsync(qty, article);
                    HasImportErrors = true;
                }
            }

            if (processedCount > 0)
            {
                await _productService.InsertStockMovementAsync(processedCount, date, movementType);

                if (movementType == "Receipt")
                    MessageBox.Show("Stock receipt has been completed!");
                else
                    MessageBox.Show("Stock write-off has been completed!");
            }
        }

        [ObservableProperty]
        private bool _hasImportErrors;

        [RelayCommand(CanExecute = nameof(CanShowImportErrors))]
        private async Task ShowArticlesAsync()
        {
            var errors = await _productService.GetStockImportErrorsAsync();
            if (errors == null || !errors.Any())
            {
                MessageBox.Show("No import errors found.");
                HasImportErrors = false;
                return;
            }

            string report = string.Join(Environment.NewLine,
                errors.Select(e => $"{e.ArticleNumber} | {e.Quantity}"));

            MessageBox.Show(report, "Products not found in DB");
        }

        // This is called automatically by CommunityToolkit to decide if the command can execute
        private bool CanShowImportErrors() => HasImportErrors;


    }


}
