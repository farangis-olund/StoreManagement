using Infrastructure.Contexts;
using Infrastructure.Dtos;
using Infrastructure.Helpers;
using Microsoft.EntityFrameworkCore;
using System;
using System.Diagnostics;
using static Infrastructure.Helpers.DateHelper;

namespace Infrastructure.Services;

public class CustomerFinanceService
{
	private readonly IDbContextFactory<DatabaseContext> _contextFactory;

	public CustomerFinanceService(IDbContextFactory<DatabaseContext> contextFactory)
	{
		_contextFactory = contextFactory;
	}

	public async Task<CustomerFinanceInfo> GetFinanceInfoAsync(string customerId, string orderId)
	{
		await using var db = await _contextFactory.CreateDbContextAsync();

		decimal currentSale = 0;
		decimal currentPayment = 0;
		decimal previousPayments = 0;
		decimal totalSales = 0;
		decimal totalReturns = 0;
		decimal customerDebt = 0;
		decimal creditLimit = 0;
		DateTime? contractDate = null;

		try
		{
			// Sum of the current order's sale
			currentSale = await db.OrderDetails
				.Where(d => d.OrderId == orderId)
				.Select(d => (d.Price) * (d.Quentity))
				.SumAsync();
		}
		catch (Exception ex)
		{
			Debug.WriteLine("Error in currentSale query: " + ex);
		}

		try
		{
			currentPayment = db.Payments
				.Where(p => p.OrderId == orderId)
				.AsEnumerable() 
				.Sum(p => (decimal)(p.Amount));
		}
		catch (Exception ex)
		{
			Debug.WriteLine("Error in currentPayment query: " + ex);
		}



		try
		{
			previousPayments = db.Payments
				.Where(p => p.CustomerId == customerId && p.OrderId != orderId)
				.AsEnumerable()
				.Sum(p => (decimal)(p.Amount));
		}
		catch (Exception ex)
		{
			Debug.WriteLine("Error in previousPayments query: " + ex);
		}
				

		try
		{
			// Customer info
			var cust = await db.Customers.FirstOrDefaultAsync(c => c.Id == customerId);
			if (cust != null)
			{
				customerDebt = (decimal)(cust.Debt ?? 0);
				creditLimit = (decimal)(cust.Restriction ?? 0);
				contractDate = cust.ContractDate;
			}
		}
		catch (Exception ex)
		{
			Debug.WriteLine("Error in customer query: " + ex);
		}

		try
		{
			totalSales = db.OrderDetails
				.Where(d => d.Order.CustomerId == customerId)
				.AsEnumerable() // move to LINQ-to-Objects
				.Sum(d => (decimal)(d.Price) * (d.Quentity));
		}
		catch (Exception ex)
		{
			Debug.WriteLine("Error in totalSales query: " + ex);
		}


		try
		{
			totalReturns = db.Returns
				.Where(r => r.CustomerId == customerId)
				.AsEnumerable()
				.Sum(r => (decimal)(r.TotalAmount));
		}
		catch (Exception ex)
		{
			Debug.WriteLine("Error in totalReturns query: " + ex);
		}


		decimal oldDebt = (totalSales - totalReturns) - currentSale - previousPayments + customerDebt;
		decimal balance = totalSales + customerDebt - previousPayments - currentPayment - totalReturns;

		return new CustomerFinanceInfo
		{
			CurrentSale = currentSale,
			CurrentPayment = currentPayment,
			PreviousPayments = previousPayments,
			CustomerDebt = customerDebt,
			CreditLimit = creditLimit,
			TotalSales = totalSales,
			TotalReturns = totalReturns,
			OldDebt = oldDebt,
			Balance = balance,
			ContractDate = contractDate
		};
	}
	public async Task<decimal> EzhigodPogashenieAsync(string customerId, string orderId)
	{
		// 1. Get full finance info
		var info = await GetFinanceInfoAsync(customerId, orderId);
		if (info == null) return 0;

		await using var db = await _contextFactory.CreateDbContextAsync();

		// 2. Load coefficients
		var coef = await db.RaschetKoefficenta.FirstOrDefaultAsync();
		if (coef == null) return 0;

		var bands = new List<RepaymentBand>
	{
		new(coef.KoefEzhPogashOstatokNach1, coef.KoefEzhPogashOstatokKon1, coef.KoefEzhPogashDin1),
		new(coef.KoefEzhPogashOstatokNach2, coef.KoefEzhPogashOstatokKon2, coef.KoefEzhPogashDin2),
		new(coef.KoefEzhPogashOstatokNach3, coef.KoefEzhPogashOstatokKon3, coef.KoefEzhPogashDin3),
		new(coef.KoefEzhPogashOstatokNach4, coef.KoefEzhPogashOstatokKon4, coef.KoefEzhPogashDin4),
		new(coef.KoefEzhPogashOstatokNach5, coef.KoefEzhPogashOstatokKon5, coef.KoefEzhPogashDin5),
	};

		// 3. Determine contract date and balance
		var today = DateTime.Today;
		var contractDate = info.ContractDate ?? today;
		var balance = info.Balance;

		// If balance is below minimum → no repayment
		var band = bands.FirstOrDefault(b => balance >= b.OstatokNach && balance <= b.OstatokKon);
		if (band == null)
			return 0;

		// 4. Calculate number of working days
		var numberOfDays = DatesBetweenExcludingWeekends(contractDate, today);

		// 5. Repayment calculation (use balance, not balance - CurrentSale)
		var repayment = (balance / band.Days) * numberOfDays;

		// 6. Safety checks
		if (repayment > balance || balance < 10)
			repayment = balance;

		return Math.Max(0, Math.Round(repayment, 2));
	}

	public async Task<List<InactiveCustomerDto>> NeaktivOtEzhigodPogashenieAsync(
	string managerId, string? territory = null)
	{
		await using var db = await _contextFactory.CreateDbContextAsync();

		// load coefficients
		var coef = await db.RaschetKoefficenta.FirstOrDefaultAsync();
		if (coef == null) return new();

		var bands = new List<RepaymentBand>
	{
		new(coef.KoefEzhPogashOstatokNach1, coef.KoefEzhPogashOstatokKon1, coef.KoefEzhPogashDin1),
		new(coef.KoefEzhPogashOstatokNach2, coef.KoefEzhPogashOstatokKon2, coef.KoefEzhPogashDin2),
		new(coef.KoefEzhPogashOstatokNach3, coef.KoefEzhPogashOstatokKon3, coef.KoefEzhPogashDin3),
		new(coef.KoefEzhPogashOstatokNach4, coef.KoefEzhPogashOstatokKon4, coef.KoefEzhPogashDin4),
		new(coef.KoefEzhPogashOstatokNach5, coef.KoefEzhPogashOstatokKon5, coef.KoefEzhPogashDin5)
	};

		// query all customers of the manager (optionally filter by territory)
		var query = db.Customers
			.Where(c => c.SalesManagerId == managerId);

		if (!string.IsNullOrWhiteSpace(territory))
			query = query.Where(c => c.Territory == territory);

		var customers = await query.ToListAsync();
		var today = DateTime.Today;

		var result = new List<InactiveCustomerDto>();

		foreach (var cust in customers)
		{
			// reuse finance info for this customer
			var info = await GetFinanceInfoAsync(cust.Id, orderId: "");
			if (info == null) continue;

			var balance = info.Balance;
			var contractDate = info.ContractDate ?? today;

			var band = bands.FirstOrDefault(b => balance >= b.OstatokNach && balance <= b.OstatokKon);
			if (band == null) continue;

			var numberOfDays = DatesBetweenExcludingWeekends(contractDate, today);
			var repayment = balance * 4 / band.Days * numberOfDays;

			if (repayment >= 1 && repayment > info.TotalSales)
			{
				var suggested = Math.Round(repayment - info.TotalSales, 0);
				if (suggested > 0)
					result.Add(new InactiveCustomerDto(cust.Id, cust.FullName ?? "", info.Balance, suggested));

			}
		}

		return result;
	}

	public async Task<List<InactiveCustomerDto>> NeaktivNeZakupAsync(
	string managerId,
	string? territory = null)
	{
		await using var db = await _contextFactory.CreateDbContextAsync();
		var today = DateTime.Today;
		var result = new List<InactiveCustomerDto>();

		// 1. Select customers for this manager (optionally filter by territory)
		var query = db.Customers
			.Include(c => c.Orders)
				.ThenInclude(o => o.OrderDetails)
			.Include(c => c.Returns)
			.Include(c => c.Payments)
			.Where(c => c.SalesManagerId == managerId);

		if (!string.IsNullOrWhiteSpace(territory))
			query = query.Where(c => c.Territory == territory);

		var customers = await query.ToListAsync();

		foreach (var cust in customers)
		{
			if (cust.ContractDate == null || cust.DailyPlannedPurchaseCoefficient == null)
				continue;

			var contractDate = cust.ContractDate.Value;
			var coef = (decimal)cust.DailyPlannedPurchaseCoefficient.Value;

			// 2. Planned sum = days since contract × coefficient
			var numberOfDays = DatesBetweenExcludingWeekends(contractDate, today);
			var sumPlan = numberOfDays * coef;

			// 3. Actual sum = (sales - returns + debt - payments)
			decimal sales = cust.Orders
				.SelectMany(o => o.OrderDetails)
				.Sum(d => (decimal)d.Price * d.Quentity);

			decimal returns = cust.Returns
				.Sum(r => (decimal)r.TotalAmount);

			decimal payments = cust.Payments
				.Sum(p => (decimal)p.Amount);

			decimal debt = (decimal)(cust.Debt ?? 0);

			var sumaContractov = sales - returns + debt - payments;

			// 4. Compare planned vs actual
			if (sumPlan > sumaContractov)
			{
				var shortfall = Math.Round(sumPlan - sumaContractov, 0);
				if (shortfall > 0)
				{
					var balance = (decimal)(cust.Debt ?? 0); // or info.Balance if you want the finance calculation
					result.Add(new InactiveCustomerDto(cust.Id, cust.FullName ?? "", balance, shortfall));
				}
			}


		}

		return result;
	}



}


public record InactiveCustomerDto(
	string CustomerId,
	string FullName,
	decimal Balance,
	decimal Shortfall
);


