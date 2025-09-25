using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Infrastructure.Dtos;
using Infrastructure.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace PresentationWpf.ViewModels
{
    public partial class MovementViewModel : ObservableObject
    {
        private readonly ProductService _productService;

        public MovementViewModel(ProductService productService)
        {
            _productService = productService;
            _ = InitializeAsync();
        }

        // === Properties ===
        [ObservableProperty] private ObservableCollection<string> _articles = [];
        [ObservableProperty] private string? _selectedArticle;
        [ObservableProperty] private int _quantity;
        [ObservableProperty] private int _dbQuantity;
        [ObservableProperty] private DateTime _movementDate = DateTime.Today;

        [ObservableProperty] private ObservableCollection<Product> _inventory = [];
        [ObservableProperty] private ObservableCollection<MovementChangeDto> _movementChanges = [];

        // Radio buttons
        [ObservableProperty] private bool _isIncoming = true;
        [ObservableProperty] private bool _isOutgoing;

        // === Initialization ===
        [RelayCommand]
        public async Task InitializeAsync()
        {
            var products = await _productService.GetAllProductAsync();
            Inventory = new ObservableCollection<Product>(products);
            Articles = new ObservableCollection<string>(products.Select(p => p.ArticleNumber));
        }

        // === Commands ===
        [RelayCommand]
        private async Task AddIncomingAsync()
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
                Type = "Приход",
                Total = newTotal,
                Location = product.WarehousePlace
            });

            await InitializeAsync();
        }

        [RelayCommand]
        private async Task AddOutgoingAsync()
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
                Type = "Расход",
                Total = newTotal,
                Location = product.WarehousePlace
            });

            await InitializeAsync();
        }

        [RelayCommand]
        private void Clear()
        {
            MovementChanges.Clear();
        }

        [RelayCommand]
        private void Close()
        {
            MovementChanges.Clear();
            Quantity = 0;
            DbQuantity = 0;
            SelectedArticle = null;
            Inventory.Clear();
            Articles.Clear();
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
                MovementChanges.Select(c => $"{c.Article} | {c.Quantity} | {c.Type} | {c.Total} | {c.Location}"));

            MessageBox.Show(report, "Печать изменений");
        }
    }
}
