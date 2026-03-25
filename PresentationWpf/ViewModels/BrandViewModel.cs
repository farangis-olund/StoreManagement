using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Infrastructure.Entities;
using Infrastructure.Repositories;
using Infrastructure.Services;
using Microsoft.Extensions.Logging;
using PresentationWpf.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;

namespace PresentationWpf.ViewModels;

public partial class BrandViewModel : ObservableObject
{
	private readonly BrandService _brandService;
	private readonly CategoryRepository _categoryRepository;
	private readonly ProductRepository _productRepository;
	private readonly ILogger<BrandViewModel> _logger;

	[ObservableProperty] private ObservableCollection<BrandEntity> brands = [];
	[ObservableProperty] private ObservableCollection<CategoryEntity> categories = [];
	[ObservableProperty] private ObservableCollection<string> firms = [];

	[ObservableProperty] private BrandEntity? selectedBrand;
	[ObservableProperty] private string? selectedFirm;

	// backup full brand list for filtering
	private List<BrandEntity> _allBrands = [];

	public BrandViewModel(
		BrandService brandService,
		CategoryRepository categoryRepository,
		ProductRepository productRepository,
		ILogger<BrandViewModel> logger)
	{
		_brandService = brandService;
		_categoryRepository = categoryRepository;
		_productRepository = productRepository;
		_logger = logger;

		_ = LoadInitialDataAsync();
	}

	// === INITIALIZATION ===
	private async Task LoadInitialDataAsync()
	{
		await LoadCategoriesAsync();
		await LoadBrandsAsync();
		await LoadFirmsAsync();
	}

	private async Task LoadCategoriesAsync()
	{
		var data = await _categoryRepository.GetAllAsync();
		Categories = new ObservableCollection<CategoryEntity>(data.OrderBy(c => c.CategoryName));
	}

	private async Task LoadBrandsAsync()
	{
		var allBrands = await _brandService.GetAllBrandsAsync();
		_allBrands = allBrands.OrderBy(b => b.BrandName).ToList();
		Brands = new ObservableCollection<BrandEntity>(_allBrands);
	}

	private async Task LoadFirmsAsync()
	{
		var firmNames = (await _brandService.GetAllBrandsAsync())
			.Where(b => !string.IsNullOrEmpty(b.CompanyName))
			.Select(b => b.CompanyName)
			.Distinct()
			.OrderBy(f => f)
			.ToList();

		Firms = new ObservableCollection<string>(firmNames);
	}

	// === COMMANDS ===

	[RelayCommand]
	private async Task ShowAllAsync()
	{
		Brands = new ObservableCollection<BrandEntity>(_allBrands);
		SelectedFirm = null;
	}

	[RelayCommand]
	private async Task RefreshAsync()
	{
		await LoadInitialDataAsync();
	}

    [RelayCommand]
    private async Task SaveChangesAsync()
    {
        try
        {
            int added = 0;
            int updated = 0;

            foreach (var brand in Brands)
            {
                if (string.IsNullOrWhiteSpace(brand.BrandName))
                    continue;

                // 🔹 Try to find an existing brand by name (case-insensitive)
                var existing = await _brandService.GetBrandAsync(brand.BrandName);

                if (existing == null || existing.BrandName == null)
                {
                    // --- Add new brand ---
                    await _brandService.AddBrandAsync(brand.BrandName, brand.CompanyName, (int)brand.CategoryId);
                    added++;
                }
                else
                {
                    // --- Update existing brand ---
                    existing.CompanyName = brand.CompanyName;
                    existing.CategoryId = brand.CategoryId;

                    await _brandService.UpdateBrandAsync(existing);
                    updated++;
                }
            }

            MessageBox.Show(
                $"Изменения сохранены!\nДобавлено: {added}, Обновлено: {updated}",
                "Сохранение",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            await RefreshAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Ошибка при сохранении брендов: {ex.Message}");
            MessageBox.Show($"Ошибка при сохранении данных:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }


    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (SelectedBrand == null)
        {
            MessageBox.Show("Выберите бренд для удаления.", "Удаление", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (MessageBox.Show($"Удалить бренд '{SelectedBrand.BrandName}'?",
            "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            try
            {
                await _brandService.DeleteBrandAsync(SelectedBrand.BrandName);
                Brands.Remove(SelectedBrand);
                SelectedBrand = null;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Ошибка при удалении бренда: {ex.Message}");
                MessageBox.Show("Ошибка при удалении бренда.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        await RefreshAsync();
    }


    [RelayCommand]
    private async Task DeleteAllAsync()
    {
        if (Brands == null || Brands.Count == 0)
        {
            MessageBox.Show("Нет брендов для удаления.", "Удаление", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (MessageBox.Show("Удалить все бренды?", "Подтверждение",
            MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
        {
            try
            {
                foreach (var brand in Brands.ToList())
                {
                    await _brandService.DeleteBrandAsync(brand.BrandName);
                }

                Brands.Clear();
                MessageBox.Show("Все бренды удалены.", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Ошибка при удалении всех брендов: {ex.Message}");
                MessageBox.Show("Ошибка при удалении всех брендов.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        await RefreshAsync();
    }

    [RelayCommand]
    private void PasteFromClipboard()
    {
        try
        {
            if (!Clipboard.ContainsText())
            {
                MessageBox.Show("Буфер обмена пуст.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var clipboardText = Clipboard.GetText();
            var lines = clipboardText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            var newFirms = new HashSet<string>(Firms ?? new ObservableCollection<string>(), StringComparer.OrdinalIgnoreCase);

            foreach (var line in lines)
            {
                // Excel format: Brand | Firm | Category
                var cells = line.Split('\t');
                if (cells.Length < 1)
                    continue;

                var brandName = cells.ElementAtOrDefault(0)?.Trim();
                var firm = cells.ElementAtOrDefault(1)?.Trim();
                var categoryName = cells.ElementAtOrDefault(2)?.Trim();

                if (string.IsNullOrWhiteSpace(brandName))
                    continue;

                // 🔹 Find category by name
                var category = Categories.FirstOrDefault(c =>
                    string.Equals(c.CategoryName, categoryName, StringComparison.OrdinalIgnoreCase));

                // 🔹 Create and add brand
                var newBrand = new BrandEntity
                {
                    BrandName = brandName,
                    CompanyName = firm ?? string.Empty,
                    CategoryId = category?.Id
                };

                Brands.Add(newBrand);

                // 🔹 Add new firm to Firms list if missing
                if (!string.IsNullOrWhiteSpace(firm) && !newFirms.Contains(firm))
                    newFirms.Add(firm);
            }

            // 🔹 Update Firms collection in ViewModel
            Firms = new ObservableCollection<string>(newFirms.OrderBy(f => f));

            MessageBox.Show("Данные успешно вставлены из Excel!", "Импорт",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Ошибка при вставке из Excel: {ex.Message}");
            MessageBox.Show("Ошибка при вставке данных из Excel.", "Ошибка",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }


    // === FILTER BY FIRM ===
    partial void OnSelectedFirmChanged(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			Brands = new ObservableCollection<BrandEntity>(_allBrands);
		}
		else
		{
			var filtered = _allBrands
				.Where(b => string.Equals(b.CompanyName, value, StringComparison.OrdinalIgnoreCase))
				.OrderBy(b => b.BrandName)
				.ToList();

			Brands = new ObservableCollection<BrandEntity>(filtered);
		}
	}

	// === NEW BRANDS COMMAND ===
	[RelayCommand]
	private async Task NewBrandAsync()
	{
		try
		{
			// 1 Get all product brand names
			var productBrands = (await _productRepository.GetAllAsync())
				.Where(p => p.Brand != null && !string.IsNullOrEmpty(p.Brand.BrandName))
				.Select(p => p.Brand.BrandName)
				.Distinct()
				.ToList();

			// 2️ Get all brands in DB
			var existingBrands = (await _brandService.GetAllBrandsAsync()).ToList();

			// 3️ Find brands missing or without firm name
			var missingBrands = productBrands
				.Where(pb => !existingBrands.Any(eb => eb.BrandName == pb))
				.Select(pb => new BrandEntity
				{
					BrandName = pb,
					CompanyName = string.Empty,
					CategoryId = null,
					Category = null
				})
				.ToList();

			var incompleteBrands = existingBrands
				.Where(b => string.IsNullOrEmpty(b.CompanyName))
				.ToList();

			// Combine both lists
			var result = missingBrands.Concat(incompleteBrands).OrderBy(b => b.BrandName).ToList();

			// 4️⃣ Add missing to DB
			foreach (var brand in missingBrands)
				await _brandService.AddBrandAsync(brand.BrandName);

			// 5️⃣ Show them in grid
			Brands = new ObservableCollection<BrandEntity>(result);

			if (Brands.Count == 0)
				Debug.WriteLine("No new or unassigned brands found.");
		}
		catch (Exception ex)
		{
			_logger.LogError($"Error loading new brands: {ex.Message}");
			Debug.WriteLine($"Error loading new brands: {ex}");
		}
	}
}
