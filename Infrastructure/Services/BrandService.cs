using Infrastructure.Entities;
using Infrastructure.Repositories;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

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

    public async Task<BrandEntity> AddBrandAsync(string brandName)
    {
        try
        {
            var existingBrand = await _brandRepository.GetOneAsync(b => b.BrandName == brandName)
                                  ?? await _brandRepository.AddAsync(new BrandEntity { BrandName = brandName });

            return existingBrand;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error getting/adding brand: {ex.Message}");
            Debug.WriteLine($"Error getting/adding brand: {ex.Message}");
            return null!;
        }
    }


    public async Task<BrandEntity> GetBrandAsync(string brandName)
    {
        try
        {
            return await _brandRepository.GetOneAsync(b => b.BrandName == brandName);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error getting/adding brand: {ex.Message}");
            Debug.WriteLine($"Error getting/adding brand: {ex.Message}");
            return null!;
        }
    }


    public async Task<IEnumerable<BrandEntity>> GetAllBrandsAsync()
    {
        try
        {
            return await _brandRepository.GetAllAsync() ?? Enumerable.Empty<BrandEntity>();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error getting brands: {ex.Message}");
            Debug.WriteLine($"Error getting brands: {ex.Message}");
            return Enumerable.Empty<BrandEntity>();
        }
    }

    public async Task<BrandEntity> UpdateBrandAsync(BrandEntity brand)
    {
        try
        {
            return await _brandRepository.UpdateAsync(c => c.Id == brand.Id, brand);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error in updating product: {ex.Message}");
            Debug.WriteLine($"Error in updating product: {ex.Message}");
            return null!;
        }
    }

    public async Task<bool> DeleteBrandAsync(string brandName)
    {
        try
        {
            var brandToRemove = await _brandRepository.GetOneAsync(b => b.BrandName == brandName);
            if (brandToRemove != null)
            {
                await _brandRepository.RemoveAsync(brandToRemove);
                return true;
            }
            else
            {
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error deleting brand: {ex.Message}");
            Debug.WriteLine($"Error deleting brand: {ex.Message}");
            return false;
        }
    }

}
