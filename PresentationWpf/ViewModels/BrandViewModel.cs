using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Infrastructure.Entities;
using Infrastructure.Repositories;
using Infrastructure.Services;
using Microsoft.Extensions.Logging;
using PresentationWpf.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;

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
			foreach (var brand in Brands)
			{
				await _brandService.UpdateBrandAsync(brand);
			}

			await RefreshAsync();
		}
		catch (Exception ex)
		{
			_logger.LogError($"Error saving brands: {ex.Message}");
			Debug.WriteLine($"Error saving brands: {ex}");
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
