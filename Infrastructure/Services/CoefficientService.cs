
using Infrastructure.Contexts;
using Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public class CoefficientService
{
	private readonly DatabaseContext _db;

	public CoefficientService(DatabaseContext db)
	{
		_db = db;
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

	public async Task CalculateEzhPogashForCustomerAsync(string customerId)
	{
		var coef = await _db.RaschetKoefficenta.FirstOrDefaultAsync();
		if (coef == null) return;

		var customer = await _db.Customers
			.Include(c => c.Orders).ThenInclude(o => o.OrderDetails)
			.Include(c => c.Payments)
			.Include(c => c.Returns)
			.FirstOrDefaultAsync(c => c.Id == customerId);

		if (customer == null) return;

		decimal ordersTotal = customer.Orders
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

		customer.DailyRepaymentCoefficient = (double)Math.Round(koefEzhPogash, 2);

		await _db.SaveChangesAsync();
	}

	public async Task CalculateZakupForCustomerAsync(string customerId)
	{
		var coef = await _db.RaschetKoefficenta.FirstOrDefaultAsync();
		if (coef == null) return;

		var customer = await _db.Customers
			.Include(c => c.Orders).ThenInclude(o => o.OrderDetails)
			.Include(c => c.Payments)
			.Include(c => c.Returns)
			.FirstOrDefaultAsync(c => c.Id == customerId);

		if (customer == null) return;

		decimal ordersTotal = customer.Orders
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

		await _db.SaveChangesAsync();
	}

	public async Task CalculateZaplanZakupForCustomerAsync(string customerId)
	{
		var coef = await _db.RaschetKoefficenta.FirstOrDefaultAsync();
		if (coef == null) return;

		var customer = await _db.Customers
			.Include(c => c.Orders).ThenInclude(o => o.OrderDetails)
			.FirstOrDefaultAsync(c => c.Id == customerId);

		if (customer == null) return;

		decimal ordersTotal = customer.Orders
			.SelectMany(o => o.OrderDetails)
			.Sum(od => od.Price * od.Quentity);

		decimal result = coef.KoefZaplanZakupDni == 0
			? 0
			: Math.Round((ordersTotal + ordersTotal * coef.KoefZaplanZakup / 100m) / coef.KoefZaplanZakupDni, 2);

		customer.DailyPlannedPurchaseCoefficient = (double)result;

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
