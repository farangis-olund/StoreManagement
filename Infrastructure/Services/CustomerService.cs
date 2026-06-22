
using Infrastructure.Contexts;
using Infrastructure.Dtos;
using Infrastructure.Entities;
using Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Infrastructure.Services;

public class CustomerService
{
	private readonly CustomerRepository _customerRepository;
	private readonly ILogger<CustomerService> _logger;
	private readonly PriceLevelRepository _priceLevelRepository;
	private readonly SalesManagerRepository _salesManagerRepository;
    private readonly IDbContextFactory<DatabaseContext> _dbFactory;
    public CustomerService(CustomerRepository customerRepository, IDbContextFactory<DatabaseContext> dbFactory, PriceLevelRepository priceLevelRepository, SalesManagerRepository salesManagerRepository, ILogger<CustomerService> logger)
	{
		_customerRepository = customerRepository;
		_logger = logger;
		_priceLevelRepository = priceLevelRepository;
		_salesManagerRepository = salesManagerRepository;
		_dbFactory = dbFactory;
	}

	public async Task<CustomerEntity> AddCustomerAsync(CustomerEntity customer)
	{
		try
		{
			var existingCustomer = await _customerRepository.GetOneAsync(c => c.Id == customer.Id);

			if (existingCustomer is not null)
			{
				// Update existing customer
				await UpdateCustomerAsync(customer);
			}
			else
			{
				// Add new customer
				var allCustomers = await _customerRepository.GetAllAsync();

				// Generate and assign ID
				customer.Id = GenerateNextCustomerId(allCustomers);
				await _customerRepository.AddAsync(customer);
			}

			return customer;
		}
		catch (Exception ex)
		{
			_logger.LogError($"Error getting/adding customer: {ex.Message}");
			Debug.WriteLine($"Error getting/adding customer: {ex.Message}");
			return null!;
		}
	}

    private string GenerateNextCustomerId(IEnumerable<CustomerEntity> existingCustomers)
    {
        var numbers = existingCustomers
            .Select(c => c.Id)
            .Where(id => id != null && id.StartsWith("N") && id.Length > 1)
            .Select(id => id.Substring(1))
            .Where(str => int.TryParse(str, out _))
            .Select(int.Parse)
            .ToList();

        int maxNumber = numbers.Any() ? numbers.Max() : 0;

        int nextNumber = Math.Max(1, maxNumber + 1);   // <-- force start from 1

        return $"N{nextNumber:0000}";
    }

    public async Task<CustomerEntity> GetCustomerAsync(string fulName)
	{
		try
		{
			return await _customerRepository.GetOneAsync(b => b.FullName == fulName);
		}
		catch (Exception ex)
		{
			_logger.LogError($"Error getting/adding customer: {ex.Message}");
			Debug.WriteLine($"Error getting/adding customer: {ex.Message}");
			return null!;
		}
	}

	public async Task<CustomerEntity> GetCustomerByIdAsync(string id)
	{
		try
		{
			var customerEntities = await _customerRepository.GetOneAsync(b => b.Id == id);
			return customerEntities ?? null!;
		}
		catch (Exception ex)
		{
			_logger.LogError($"Error getting/adding customer: {ex.Message}");
			Debug.WriteLine($"Error getting/adding customer: {ex.Message}");
			return null!;
		}
	}

	public async Task<IEnumerable<CustomerEntity>> GetAllCustomersAsync()
	{
		try
		{
			var customerEntities = await _customerRepository.GetAllAsync();
			return customerEntities ?? Enumerable.Empty<CustomerEntity>();
		}
		catch (Exception ex)
		{
			_logger.LogError($"Error getting customers: {ex.Message}");
			Debug.WriteLine($"Error getting customers: {ex.Message}");
			return Enumerable.Empty<CustomerEntity>();
		}
	}

	public async Task<CustomerEntity> UpdateCustomerAsync(CustomerEntity customer)
	{
		try
		{
			return await _customerRepository.UpdateAsync(c => c.Id == customer.Id, customer);
		}
		catch (Exception ex)
		{
			_logger.LogError($"Error in updating product: {ex.Message}");
			Debug.WriteLine($"Error in updating product: {ex.Message}");
			return null!;
		}
	}

	public async Task<bool> DeleteCustomerAsync(CustomerEntity customer)
	{
		try
		{
			var existingCustomer = await _customerRepository.GetOneAsync(b => b.Id == customer.Id);
			if (existingCustomer != null)
			{
				await _customerRepository.RemoveAsync(customer);
				return true;
			}
			else
			{
				return false;
			}
		}
		catch (Exception ex)
		{
			_logger.LogError($"Error deleting customer: {ex.Message}");
			Debug.WriteLine($"Error deleting customer: {ex.Message}");
			return false;
		}
	}

	public async Task<IEnumerable<PriceLevelEntity>> GetAllPriceLevelsAsync()
	{
		try
		{
			var levels = await _priceLevelRepository.GetAllAsync();

			// ✅ Always return sorted by Level
			return (levels ?? Enumerable.Empty<PriceLevelEntity>())
				.OrderBy(l => l.Level)
				.ToList();
		}
		catch (Exception ex)
		{
			_logger.LogError($"Error getting price levels: {ex.Message}");
			Debug.WriteLine($"Error getting price levels: {ex.Message}");
			return Enumerable.Empty<PriceLevelEntity>();
		}
	}


	public async Task<IEnumerable<SalesManagerEntity>> GetAllSalesManagersAsync()
	{
		try
		{
			var result = await _salesManagerRepository.GetAllAsync();
			return result ?? Enumerable.Empty<SalesManagerEntity>();
		}
		catch (Exception ex)
		{
			_logger.LogError($"Error getting sales managers: {ex.Message}");
			Debug.WriteLine($"Error getting sales managers: {ex.Message}");
			return Enumerable.Empty<SalesManagerEntity>();
		}
	}

    /// <summary>
    /// Updates the customer's current balance (debt).
    /// </summary>
    public async Task<bool> UpdateBalanceAsync(string customerId, decimal newBalance, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        try
        {
            var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == customerId, ct);
            if (customer == null)
            {
                _logger.LogWarning("Customer not found for balance update: {CustomerId}", customerId);
                return false;
            }

            customer.Debt -= (double)newBalance;
           
            await db.SaveChangesAsync(ct);

            _logger.LogInformation("Updated balance for customer {CustomerId}: {NewBalance}", customerId, newBalance);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating balance for customer {CustomerId}", customerId);
            return false;
        }
    }

    public async Task<CustomerLevelChangeInfoDto?> GetCustomerLevelChangeAsync(string customerId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var customer = await db.Customers
		.AsNoTracking()
		.Include(x => x.PriceLevel)
		.FirstOrDefaultAsync(x => x.Id == customerId);

        if (customer == null)
            return null;

        var from = DateTime.Today.AddDays(-30);
        var to = DateTime.Today.AddDays(1);

        var total30Days = await db.OrderDetails
            .AsNoTracking()
            .Where(x =>
                x.Order.CustomerId == customerId &&
                x.Order.Date >= from &&
                x.Order.Date < to &&
                !x.Order.IsBarter)
            .SumAsync(x => (decimal?)x.Price * x.Quentity) ?? 0m;

        var levels = await db.Set<PriceLevelEntity>()
            .AsNoTracking()
            .Where(x => x.Code.HasValue)
            .OrderBy(x => x.Code)
            .ToListAsync();

        var matchedLevel = FindLevelByAmount(levels, (double)total30Days);

        int currentLevelCode = customer.PriceLevel.Code ?? 0;

        var currentLevel = levels.FirstOrDefault(x => x.Code == currentLevelCode);

        return new CustomerLevelChangeInfoDto
        {
            CurrentLevelCode = currentLevelCode,
            CalculatedLevelCode = matchedLevel?.Code ?? 0,
            CurrentPriceType = currentLevel?.PriceType ?? customer.PriceLevelId,
            CalculatedPriceType = matchedLevel?.PriceType,
            Last30DaysTotal = total30Days
        };
    }

    private static PriceLevelEntity? FindLevelByAmount(List<PriceLevelEntity> levels, double amount)
    {
        var ordered = levels
            .Where(x => x.Code.HasValue)
            .OrderBy(x => x.Code)
            .ToList();

        for (int i = 0; i < ordered.Count; i++)
        {
            var level = ordered[i];

            bool minOk = !level.MinLimit.HasValue || amount >= level.MinLimit.Value;

            bool maxOk;
            if (i == ordered.Count - 1)
                maxOk = !level.MaxLimit.HasValue || amount <= level.MaxLimit.Value;
            else
                maxOk = !level.MaxLimit.HasValue || amount < level.MaxLimit.Value;

            if (minOk && maxOk)
                return level;
        }

        return null;
    }
}
