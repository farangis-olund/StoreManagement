
using System.Diagnostics;
using Infrastructure.Entities;
using Infrastructure.Repositories;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

public class BrandService
{
	private readonly BrandRepository _brandRepository;
	private readonly ILogger<BrandService> _logger;

	public BrandService(BrandRepository brandRepository, ILogger<BrandService> logger)
	{
		_brandRepository = brandRepository;
		_logger = logger;
	}

	/// <summary>
	/// Adds a brand if it doesn't already exist.
	/// Returns the existing or newly created brand.
	/// </summary>
	public async Task<BrandEntity?> AddBrandAsync(string brandName)
	{
		try
		{
			if (string.IsNullOrWhiteSpace(brandName))
				return null;

			var existingBrand = await _brandRepository.GetOneAsync(b => b.BrandName == brandName);

			// ✅ Return if a valid brand already exists
			if (existingBrand != null && !string.IsNullOrEmpty(existingBrand.BrandName))
				return existingBrand;

			// Otherwise, create a new one
			var newBrand = new BrandEntity
			{
				BrandName = brandName,
				CompanyName = string.Empty,
				CategoryId = null
			};

			return await _brandRepository.AddAsync(newBrand);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error adding brand {BrandName}", brandName);
			Debug.WriteLine($"Error adding brand {brandName}: {ex}");
			return null;
		}
	}


	/// <summary>
	/// Returns a brand by name.
	/// </summary>
	public async Task<BrandEntity?> GetBrandAsync(string brandName)
	{
		try
		{
			return await _brandRepository.GetOneAsync(b => b.BrandName == brandName);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error fetching brand {BrandName}", brandName);
			Debug.WriteLine($"Error fetching brand {brandName}: {ex}");
			return null;
		}
	}

	/// <summary>
	/// Returns all brands including their related categories.
	/// </summary>
	public async Task<IEnumerable<BrandEntity>> GetAllBrandsAsync()
	{
		try
		{
			// use the new method that includes Category
			var brands = await _brandRepository.GetAllWithCategoryAsync();
			return brands ?? Enumerable.Empty<BrandEntity>();
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error getting all brands");
			Debug.WriteLine($"Error getting all brands: {ex}");
			return Enumerable.Empty<BrandEntity>();
		}
	}

	/// <summary>
	/// Returns a distinct list of firm names (CompanyName) from all brands.
	/// </summary>
	public async Task<IEnumerable<string>> GetDistinctFirmsAsync()
	{
		try
		{
			var brands = await _brandRepository.GetAllAsync();
			return brands
				?.Where(b => !string.IsNullOrEmpty(b.CompanyName))
				.Select(b => b.CompanyName)
				.Distinct()
				.OrderBy(f => f)
				.ToList()
				?? new List<string>();
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error getting distinct firm names");
			Debug.WriteLine($"Error getting distinct firm names: {ex}");
			return Enumerable.Empty<string>();
		}
	}

	/// <summary>
	/// Updates an existing brand entity.
	/// </summary>
	public async Task<BrandEntity?> UpdateBrandAsync(BrandEntity brand)
	{
		try
		{
			if (brand == null)
				throw new ArgumentNullException(nameof(brand));

			return await _brandRepository.UpdateAsync(c => c.Id == brand.Id, brand);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error updating brand {BrandId}", brand?.Id);
			Debug.WriteLine($"Error updating brand {brand?.Id}: {ex}");
			return null;
		}
	}

	/// <summary>
	/// Deletes a brand by name.
	/// </summary>
	public async Task<bool> DeleteBrandAsync(string brandName)
	{
		try
		{
			var brandToRemove = await _brandRepository.GetOneAsync(b => b.BrandName == brandName);
			if (brandToRemove == null)
				return false;

			await _brandRepository.RemoveAsync(brandToRemove);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error deleting brand {BrandName}", brandName);
			Debug.WriteLine($"Error deleting brand {brandName}: {ex}");
			return false;
		}
	}

	
}
