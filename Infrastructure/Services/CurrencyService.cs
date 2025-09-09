using Infrastructure.Entities;
using Infrastructure.Repositories;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Infrastructure.Services;

public class CurrencyService
{
    private readonly CurrencyRepository _currencyRepository;
    private readonly ILogger<CurrencyService> _logger;

    public CurrencyService(CurrencyRepository currencyRepository, ILogger<CurrencyService> logger)
    {
        _currencyRepository = currencyRepository;
        _logger = logger;
    }

    public async Task<CurrencyEntity> AddCurrencyAsync(string code, string currencyName)
    {
        try
        {
            var existingcurrency = await _currencyRepository.GetOneAsync(b => b.Code == code)
                                  ?? await _currencyRepository.AddAsync(new CurrencyEntity { Code = code, CurrencyName = currencyName });

            return existingcurrency;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error getting/adding currency: {ex.Message}");
            Debug.WriteLine($"Error getting/adding currency: {ex.Message}");
            return null!;
        }
    }


    public async Task<CurrencyEntity> GetCurrencyAsync(string code)
    {
        try
        {
            return await _currencyRepository.GetOneAsync(b => b.Code == code);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error getting/adding currency: {ex.Message}");
            Debug.WriteLine($"Error getting/adding currency: {ex.Message}");
            return null!;
        }
    }


    public async Task<IEnumerable<CurrencyEntity>> GetAllCurrenciesAsync()
    {
        try
        {
            return await _currencyRepository.GetAllAsync() ?? Enumerable.Empty<CurrencyEntity>();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error getting currencys: {ex.Message}");
            Debug.WriteLine($"Error getting currencys: {ex.Message}");
            return Enumerable.Empty<CurrencyEntity>();
        }
    }

        public async Task<CurrencyEntity> UpdateCurrencyAsync(CurrencyEntity currency)
    {
        try
        {
            return await _currencyRepository.UpdateAsync(c => c.Code == currency.Code, currency);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error in updating currency: {ex.Message}");
            Debug.WriteLine($"Error in updating currency: {ex.Message}");
            return null!;
        }
    }


    public async Task<bool> DeleteCurrencyAsync(string code)
    {
        try
        {
            var result = await _currencyRepository.GetOneAsync(b => b.Code == code);
            if (result != null) 
            {
                await _currencyRepository.RemoveAsync(b => b.Code == code);
                return true;
            } else 
                return false; 
            
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error deleting currency: {ex.Message}");
            Debug.WriteLine($"Error deleting currency: {ex.Message}");
            return false;
        }
    }
}
