
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
    private readonly OfficialMonthlyPlanService? _monthlyPlans;
    private readonly CustomerAccessPolicy _access;
    private readonly CustomerFinanceService _financeService;
    public CustomerService(CustomerRepository customerRepository, IDbContextFactory<DatabaseContext> dbFactory, PriceLevelRepository priceLevelRepository, SalesManagerRepository salesManagerRepository, ILogger<CustomerService> logger, CustomerFieldPermissionPolicy fieldPermissions, CustomerAccessPolicy access, OfficialMonthlyPlanService? monthlyPlans = null, CustomerFinanceService? financeService = null)
	{
		_customerRepository = customerRepository;
		_logger = logger;
		_priceLevelRepository = priceLevelRepository;
		_salesManagerRepository = salesManagerRepository;
		_dbFactory = dbFactory;
        _fieldPermissions = fieldPermissions;
        _monthlyPlans = monthlyPlans;
        _access = access;
        _financeService = financeService ?? new CustomerFinanceService(dbFactory);
	}

	public async Task<CustomerEntity> AddCustomerAsync(CustomerEntity customer)
	{
		try
		{
            await using var db = await _dbFactory.CreateDbContextAsync();
            await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            if (!string.IsNullOrWhiteSpace(customer.Id)) await _access.EnsureAccessAsync(db, customer.Id);
            await _access.EnsureManagerAssignmentAsync(db, customer.SalesManagerId);
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

            if (existing != null) await _access.EnsureAccessAsync(db, customer.Id);
            await _access.EnsureManagerAssignmentAsync(db, customer.SalesManagerId);
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            _monthlyPlans?.NotifyChanged();
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

    public async Task<IEnumerable<CustomerEntity>> GetCustomersForEditorAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var all = _access.CanViewAll;
        var userId = _access.UserId;
        var rows = await _access.Customers(db).AsNoTracking().Include(c => c.PriceLevel).Include(c => c.Payments).ToListAsync();
        if (all != _access.CanViewAll || userId != _access.UserId)
            throw new UnauthorizedAccessException("Права доступа изменились. Повторно откройте клиентов.");
        return rows;
    }

    public virtual async Task<decimal> GetBalanceForEditorAsync(string customerId)
    {
        _fieldPermissions.EnsureCanViewBalance();
        ArgumentException.ThrowIfNullOrWhiteSpace(customerId);
        var userId = _access.UserId;
        var allCustomers = _access.CanViewAll;
        await using var db = await _dbFactory.CreateDbContextAsync();
        await _access.EnsureAccessAsync(db, customerId);
        if (!await db.Customers.AnyAsync(c => c.Id == customerId))
            throw new InvalidOperationException("Клиент больше не существует. Обновите список клиентов.");

        // Share the customer summary's calculation; do not persist a second balance field.
        var finance = await _financeService.GetFinanceInfoAsync(customerId, orderId: null!);

        _fieldPermissions.EnsureCanViewBalance();
        await _access.EnsureAccessAsync(db, customerId);
        _fieldPermissions.EnsureCanViewBalance();
        if (userId != _access.UserId || allCustomers != _access.CanViewAll)
            throw new UnauthorizedAccessException("Права доступа изменились. Повторно выберите клиента.");
        return finance.Balance;
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
        _fieldPermissions.EnsureCanDeleteCustomer();
        if (string.IsNullOrWhiteSpace(customer.Id))
            throw new ArgumentException("Выберите сохранённого клиента для удаления.");
		try
		{
            await using var db = await _dbFactory.CreateDbContextAsync();
            await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            await _access.EnsureAccessAsync(db, customer.Id);
            var existingCustomer = await db.Customers.SingleOrDefaultAsync(c => c.Id == customer.Id);
            if (existingCustomer == null) return false;

            var linked = new List<string>();
            if (await db.Orders.AnyAsync(o => o.CustomerId == customer.Id)) linked.Add("заказы");
            if (await db.Payments.AnyAsync(p => p.CustomerId == customer.Id)) linked.Add("платежи");
            if (await db.Returns.AnyAsync(r => r.CustomerId == customer.Id)) linked.Add("возвраты");
            if (await db.OfficialMonthlyPlans.AnyAsync(p => p.CustomerId == customer.Id)) linked.Add("месячные планы");
            if (await db.ProductRequests.AnyAsync(r => r.CustomerId == customer.Id)) linked.Add("заявки на товары");
            if (linked.Count > 0)
                throw new InvalidOperationException("Нельзя удалить клиента: с ним связаны " + string.Join(", ", linked) + ".");

            await _access.EnsureAccessAsync(db, customer.Id);
            _fieldPermissions.EnsureCanDeleteCustomer();
            db.Customers.Remove(existingCustomer);
            await db.SaveChangesAsync();
            _fieldPermissions.EnsureCanDeleteCustomer();
            await transaction.CommitAsync();
            _monthlyPlans?.NotifyChanged();
            return true;
		}
        catch (Exception ex) when (ex is UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            throw;
        }
        catch (DbUpdateException ex) when (ex.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 547 })
        {
            throw new InvalidOperationException("Нельзя удалить клиента: в базе есть связанные с ним данные.", ex);
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
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await _access.Managers(db).AsNoTracking().OrderBy(m => m.FullName).ToListAsync();
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
