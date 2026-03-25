using Infrastructure.Contexts;
using Infrastructure.Entities;
using Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Infrastructure.Services;

public class CurrencyService
{
    private readonly CurrencyRepository _currencyRepository;
    private readonly DatabaseContext _context;
    private readonly ILogger<CurrencyService> _logger;

    public CurrencyService(
        CurrencyRepository currencyRepository,
        DatabaseContext context,
        ILogger<CurrencyService> logger)
    {
        _currencyRepository = currencyRepository;
        _context = context;
        _logger = logger;
    }

    // === 🟢 CURRENCY CRUD ===
    public async Task<CurrencyEntity> AddCurrencyAsync(string code, string currencyName)
    {
        try
        {
            var existing = await _currencyRepository.GetOneAsync(b => b.Code == code)
                            ?? await _currencyRepository.AddAsync(new CurrencyEntity
                            {
                                Code = code,
                                CurrencyName = currencyName
                            });

            return existing;
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
            _logger.LogError($"Error getting currency: {ex.Message}");
            Debug.WriteLine($"Error getting currency: {ex.Message}");
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
            _logger.LogError($"Error getting currencies: {ex.Message}");
            Debug.WriteLine($"Error getting currencies: {ex.Message}");
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
            _logger.LogError($"Error updating currency: {ex.Message}");
            Debug.WriteLine($"Error updating currency: {ex.Message}");
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
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error deleting currency: {ex.Message}");
            Debug.WriteLine($"Error deleting currency: {ex.Message}");
            return false;
        }
    }

    // === 🟡 EXCHANGE RATE METHODS ===
        
    /// <summary>
    /// Always adds a new exchange rate entry (keeps history).
    /// </summary>
    public async Task<ExchangeRateEntity> AddExchangeRateAsync(string code, double rate)
    {
        try
        {
            // ✅ Ensure the currency exists
            var currency = await GetCurrencyAsync(code)
                ?? await AddCurrencyAsync(code, code);

            // ✅ Create a new rate entry (always insert, never update)
            var newRate = new ExchangeRateEntity
            {
                Code = code,         // Foreign key
                Rate = rate,
                Date = DateTime.Now  // precise timestamp for history
            };

            await _context.ExchangeRates.AddAsync(newRate);
            await _context.SaveChangesAsync();

            return newRate;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error adding exchange rate: {ex.Message}");
            Debug.WriteLine($"Error adding exchange rate: {ex.Message}");
            return null!;
        }
    }

    /// <summary>
    /// Gets the latest exchange rate for a specific currency code.
    /// </summary>
    public async Task<double?> GetLatestRateAsync(string code)
    {
        try
        {
            var rate = await _context.ExchangeRates
                .Where(r => r.Code == code)
                .OrderByDescending(r => r.Date)
                .Select(r => r.Rate)
                .FirstOrDefaultAsync();

            return rate == 0 ? null : rate;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error getting latest rate: {ex.Message}");
            Debug.WriteLine($"Error getting latest rate: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Gets all exchange rates for a currency code (ordered by date).
    /// </summary>
    public async Task<List<ExchangeRateEntity>> GetAllRatesAsync(string code)
    {
        try
        {
            return await _context.ExchangeRates
                .Where(r => r.Code == code)
                .OrderByDescending(r => r.Date)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error getting exchange rates: {ex.Message}");
            Debug.WriteLine($"Error getting exchange rates: {ex.Message}");
            return new List<ExchangeRateEntity>();
        }
    }

    /// <summary>
    /// Deletes all exchange rates for a currency.
    /// </summary>
    public async Task<bool> DeleteExchangeRatesAsync(string code)
    {
        try
        {
            var rates = _context.ExchangeRates.Where(r => r.Code == code);
            if (await rates.AnyAsync())
            {
                _context.ExchangeRates.RemoveRange(rates);
                await _context.SaveChangesAsync();
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error deleting exchange rates: {ex.Message}");
            Debug.WriteLine($"Error deleting exchange rates: {ex.Message}");
            return false;
        }
    }
}
