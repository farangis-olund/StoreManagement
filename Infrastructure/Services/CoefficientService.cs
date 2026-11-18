
using Infrastructure.Contexts;
using Infrastructure.Constants;
using Infrastructure.Security;
using Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public class CoefficientService
{
	private readonly DatabaseContext _db;
	private readonly IDbContextFactory<DatabaseContext> _contextFactory;
	private readonly CustomerFieldPermissionPolicy _fieldPermissions;
    private readonly CustomerAccessPolicy _access;

	public CoefficientService(DatabaseContext db, IDbContextFactory<DatabaseContext> contextFactory,
		CustomerFieldPermissionPolicy fieldPermissions, CustomerAccessPolicy access)
	{
		_db = db;
		_contextFactory = contextFactory;
		_fieldPermissions = fieldPermissions;
        _access = access;
	}

	public async Task<CustomerEntity> CalculateForCustomerAsync(string customerId)
	{
		await using var db = await _contextFactory.CreateDbContextAsync();
		await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        await _access.EnsureAccessAsync(db, customerId);
		var coef = await db.RaschetKoefficenta.AsNoTracking().FirstOrDefaultAsync()
			?? throw new InvalidOperationException("Настройки расчета коэффициентов не найдены.");
		var customer = await db.Customers
			.Include(c => c.Orders).ThenInclude(o => o.OrderDetails)
			.Include(c => c.Payments).Include(c => c.Returns)
			.FirstOrDefaultAsync(c => c.Id == customerId)
			?? throw new InvalidOperationException("Клиент не найден.");

		var ordersTotal = customer.Orders.SelectMany(o => o.OrderDetails).Sum(d => d.Price * d.Quentity);
		var debtSales = customer.Orders.Where(o => !o.IsBarter)
			.SelectMany(o => o.OrderDetails).Sum(d => d.Price * d.Quentity);
		var balance = debtSales - customer.Returns.Sum(r => r.TotalAmount)
			+ ((decimal?)customer.Debt ?? 0) - customer.Payments.Sum(p => p.Amount);
		var brackets = new[]
		{
			(coef.KoefEzhPogashOstatokNach1, coef.KoefEzhPogashOstatokKon1, coef.KoefEzhPogashDin1),
			(coef.KoefEzhPogashOstatokNach2, coef.KoefEzhPogashOstatokKon2, coef.KoefEzhPogashDin2),
			(coef.KoefEzhPogashOstatokNach3, coef.KoefEzhPogashOstatokKon3, coef.KoefEzhPogashDin3),
			(coef.KoefEzhPogashOstatokNach4, coef.KoefEzhPogashOstatokKon4, coef.KoefEzhPogashDin4),
			(coef.KoefEzhPogashOstatokNach5, coef.KoefEzhPogashOstatokKon5, coef.KoefEzhPogashDin5)
		};
		decimal repayment = 0;
		foreach (var (minimum, maximum, days) in brackets)
		{
			if (balance < minimum || balance > maximum) continue;
			if (days <= 0)
				throw new InvalidOperationException("Количество дней погашения должно быть больше нуля.");
			repayment = Math.Round(balance / days, 2);
			break;
		}

		var calculated = new CustomerEntity
		{
			DailyPurchaseCoefficient = coef.KoefZakupaDni == 0 ? 0
				: (double)Math.Round(balance * coef.KoefZakupa / coef.KoefZakupaDni, 2),
			DailyRepaymentCoefficient = (double)repayment,
			DailyPlannedPurchaseCoefficient = coef.KoefZaplanZakupDni == 0 ? 0
				: (double)Math.Round((ordersTotal + ordersTotal * coef.KoefZaplanZakup / 100m) / coef.KoefZaplanZakupDni, 2)
		};
		// Validate every result before changing any of the three saved values.
		_fieldPermissions.EnsureCanChange(CustomerField.Coefficients, customer, calculated);
		customer.DailyPurchaseCoefficient = calculated.DailyPurchaseCoefficient;
		customer.DailyRepaymentCoefficient = calculated.DailyRepaymentCoefficient;
		customer.DailyPlannedPurchaseCoefficient = calculated.DailyPlannedPurchaseCoefficient;
		await db.SaveChangesAsync();
		await transaction.CommitAsync();
		return customer;
	}

	/// <summary>
	/// Mass-calculates Ежедневное погашение for all customers 
	/// </summary>
	public async Task CalculateEzhPogashForAllAsync()
	{
		var coef = await _db.RaschetKoefficenta.FirstOrDefaultAsync();
		if (coef == null) return;

		var customers = await _db.Customers
			.Include(c => c.Orders).ThenInclude(o => o.OrderDetails)
			.Include(c => c.Payments)
			.Include(c => c.Returns)
			.ToListAsync();

		foreach (var customer in customers)
		{
			decimal ordersTotal = customer.Orders
				.Where(o => !o.IsBarter)
				.SelectMany(o => o.OrderDetails)
				.Sum(od => od.Price * od.Quentity);

			decimal returnsTotal = customer.Returns.Sum(r => r.TotalAmount);
			decimal paymentsTotal = customer.Payments.Sum(p => p.Amount);
			decimal debt = (decimal?)customer.Debt ?? 0;

			decimal balance = ordersTotal - returnsTotal + debt - paymentsTotal;

			decimal koefEzhPogash = 0;

			if (balance >= coef.KoefEzhPogashOstatokNach1 && balance <= coef.KoefEzhPogashOstatokKon1)
				koefEzhPogash = balance / coef.KoefEzhPogashDin1;
			else if (balance >= coef.KoefEzhPogashOstatokNach2 && balance <= coef.KoefEzhPogashOstatokKon2)
				koefEzhPogash = balance / coef.KoefEzhPogashDin2;
			else if (balance >= coef.KoefEzhPogashOstatokNach3 && balance <= coef.KoefEzhPogashOstatokKon3)
				koefEzhPogash = balance / coef.KoefEzhPogashDin3;
			else if (balance >= coef.KoefEzhPogashOstatokNach4 && balance <= coef.KoefEzhPogashOstatokKon4)
				koefEzhPogash = balance / coef.KoefEzhPogashDin4;
			else if (balance >= coef.KoefEzhPogashOstatokNach5 && balance <= coef.KoefEzhPogashOstatokKon5)
				koefEzhPogash = balance / coef.KoefEzhPogashDin5;

			// Save result into Customer (add a property in entity: KoefEzhPogashenie)
			customer.DailyRepaymentCoefficient = (double)Math.Round(koefEzhPogash, 2);
		}

		await _db.SaveChangesAsync();
	}

	public async Task CalculateZakupForAllAsync()
	{
		var coef = await _db.RaschetKoefficenta.FirstOrDefaultAsync();
		if (coef == null) return;

		var customers = await _db.Customers
			.Include(c => c.Orders).ThenInclude(o => o.OrderDetails)
			.Include(c => c.Payments)
			.Include(c => c.Returns)
			.ToListAsync();

		foreach (var customer in customers)
		{
			decimal ordersTotal = customer.Orders
				.Where(o => !o.IsBarter)
				.SelectMany(o => o.OrderDetails)
				.Sum(od => od.Price * od.Quentity);

			decimal returnsTotal = customer.Returns.Sum(r => r.TotalAmount);
			decimal paymentsTotal = customer.Payments.Sum(p => p.Amount);
			decimal debt = (decimal?)customer.Debt ?? 0;

			decimal balance = ordersTotal - returnsTotal + debt - paymentsTotal;

			decimal result = coef.KoefZakupaDni == 0
				? 0
				: Math.Round(balance * coef.KoefZakupa / coef.KoefZakupaDni, 2);

			customer.DailyPurchaseCoefficient = (double)result; 
		}

		await _db.SaveChangesAsync();
	}

	public async Task CalculateZaplanZakupForAllAsync()
	{
		var coef = await _db.RaschetKoefficenta.FirstOrDefaultAsync();
		if (coef == null) return;

		var customers = await _db.Customers
			.Include(c => c.Orders).ThenInclude(o => o.OrderDetails)
			.ToListAsync();

		foreach (var customer in customers)
		{
			decimal ordersTotal = customer.Orders
				.SelectMany(o => o.OrderDetails)
				.Sum(od => od.Price * od.Quentity);

			decimal result = coef.KoefZaplanZakupDni == 0
				? 0
				: Math.Round((ordersTotal + ordersTotal * coef.KoefZaplanZakup / 100m) / coef.KoefZaplanZakupDni, 2);

			customer.DailyPlannedPurchaseCoefficient = (double)result; 
		}

		await _db.SaveChangesAsync();

	}


	/// <summary>
	/// Возвращает первую запись RaschetKoefficenta или создаёт её с дефолтными значениями.
	/// </summary>
	public async Task<RaschetKoefficentaEntity> GetOrCreateAsync()
	{
		var entity = await _db.RaschetKoefficenta.FirstOrDefaultAsync();

		if (entity == null)
		{
			entity = new RaschetKoefficentaEntity
			{
				// === Коэффициент закупа ===
				KoefZakupa = 4,
				KoefZakupaDni = 288,

				// === Ежедневное погашение ===
				KoefEzhPogashOstatokNach1 = 1000,
				KoefEzhPogashOstatokKon1 = 4999,
				KoefEzhPogashDin1 = 288,

				KoefEzhPogashOstatokNach2 = 5000,
				KoefEzhPogashOstatokKon2 = 9999,
				KoefEzhPogashDin2 = 576,

				KoefEzhPogashOstatokNach3 = 10000,
				KoefEzhPogashOstatokKon3 = 19999,
				KoefEzhPogashDin3 = 720,

				KoefEzhPogashOstatokNach4 = 20000,
				KoefEzhPogashOstatokKon4 = 29999,
				KoefEzhPogashDin4 = 864,

				KoefEzhPogashOstatokNach5 = 30000,
				KoefEzhPogashOstatokKon5 = 49999,
				KoefEzhPogashDin5 = 1152,

				// === Запланированный закуп ===
				KoefZaplanZakup = 5,
				KoefZaplanZakupDni = 288
			};

			_db.RaschetKoefficenta.Add(entity);
			await _db.SaveChangesAsync();
		}

		return entity;
	}

	public async Task SaveAsync(RaschetKoefficentaEntity entity)
	{
		var existing = await _db.Set<RaschetKoefficentaEntity>().FirstOrDefaultAsync();
		if (existing == null)
		{
			_db.Add(entity);
		}
		else
		{
			_db.Entry(existing).CurrentValues.SetValues(entity);
		}
		await _db.SaveChangesAsync();
	}
}
