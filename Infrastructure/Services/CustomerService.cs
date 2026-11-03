
using Infrastructure.Contexts;
using Infrastructure.Dtos;
using Infrastructure.Entities;
using Infrastructure.Repositories;
using Infrastructure.Security;
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
    private readonly CustomerFieldPermissionPolicy _fieldPermissions;
    public CustomerService(CustomerRepository customerRepository, IDbContextFactory<DatabaseContext> dbFactory, PriceLevelRepository priceLevelRepository, SalesManagerRepository salesManagerRepository, ILogger<CustomerService> logger, CustomerFieldPermissionPolicy fieldPermissions)
	{
		_customerRepository = customerRepository;
		_logger = logger;
		_priceLevelRepository = priceLevelRepository;
		_salesManagerRepository = salesManagerRepository;
		_dbFactory = dbFactory;
        _fieldPermissions = fieldPermissions;
	}

	public async Task<CustomerEntity> AddCustomerAsync(CustomerEntity customer)
	{
		try
		{
            await using var db = await _dbFactory.CreateDbContextAsync();
            await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var existing = string.IsNullOrWhiteSpace(customer.Id) ? null
                : await db.Customers.FirstOrDefaultAsync(c => c.Id == customer.Id);
            if (existing == null && !string.IsNullOrWhiteSpace(customer.Id))
                throw new InvalidOperationException("Клиент больше не существует. Обновите список клиентов.");

            _fieldPermissions.EnsureCanSave(existing, customer);
            await ValidateRequiredFieldsAsync(db, customer);

            if (existing == null)
            {
                customer.Id = GenerateNextCustomerId(await db.Customers.AsNoTracking().ToListAsync());
                db.Customers.Add(customer);
            }
            else
            {
                db.Entry(existing).CurrentValues.SetValues(customer);
            }

            // Save the manager link here, without a second service rewriting a stale customer.
            var links = await db.ManagerCustomers.Where(link => link.CustomerId == customer.Id).ToListAsync();
            db.ManagerCustomers.RemoveRange(links.Where(link => link.ManagerId != customer.SalesManagerId));
            if (!links.Any(link => link.ManagerId == customer.SalesManagerId))
                db.ManagerCustomers.Add(new ManagerCustomerEntity { CustomerId = customer.Id, ManagerId = customer.SalesManagerId! });

            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return existing ?? customer;
		}
        catch (Exception ex) when (ex is UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            throw;
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

    public Task<CustomerEntity> UpdateCustomerAsync(CustomerEntity customer) =>
        string.IsNullOrWhiteSpace(customer.Id) ? Task.FromResult<CustomerEntity>(null!) : AddCustomerAsync(customer);

    private static async Task ValidateRequiredFieldsAsync(DatabaseContext db, CustomerEntity customer)
    {
        if (string.IsNullOrWhiteSpace(customer.FullName) || string.IsNullOrWhiteSpace(customer.MobilePhone) ||
            string.IsNullOrWhiteSpace(customer.Address) || string.IsNullOrWhiteSpace(customer.City) ||
            string.IsNullOrWhiteSpace(customer.Region) || string.IsNullOrWhiteSpace(customer.PriceLevelId) ||
            string.IsNullOrWhiteSpace(customer.SalesManagerId) || string.IsNullOrWhiteSpace(customer.Territory))
            throw new ArgumentException("Заполните все обязательные поля клиента.");

        if (!await db.PriceLevels.AnyAsync(level => level.Level == customer.PriceLevelId))
            throw new ArgumentException("Выберите уровень из списка.");
        if (!await db.SalesManagers.AnyAsync(manager => manager.Id == customer.SalesManagerId))
            throw new ArgumentException("Выберите менеджера по продажам из списка.");
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
