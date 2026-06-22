using Infrastructure.Constants;
using Infrastructure.Contexts;
using Infrastructure.Dtos;
using Microsoft.EntityFrameworkCore;
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
				.Where(p => p.OrderId == orderId && p.CustomerId == customerId)
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
				.Where(r => r.CustomerId == customerId &&
                r.RefundMethod == RefundMethodConstants.Balance)
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


    public async Task<CustomerFinanceInfo> GetFinanceInfoAsync(string customerId)
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
            previousPayments = db.Payments
                .Where(p => p.CustomerId == customerId)
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
                .Where(r => r.CustomerId == customerId &&
                 r.RefundMethod == RefundMethodConstants.Balance)
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
        if (string.IsNullOrEmpty(customerId))
            return 0;

        await using var db = await _contextFactory.CreateDbContextAsync();

        // 1️⃣ Load coefficients
        var coef = await db.RaschetKoefficenta.FirstOrDefaultAsync();
        if (coef == null) return 0;

        var bands = new List<RepaymentBand>
    {
        new(coef.KoefEzhPogashOstatokNach1, coef.KoefEzhPogashOstatokKon1, coef.KoefEzhPogashDin1),
        new(coef.KoefEzhPogashOstatokNach2, coef.KoefEzhPogashOstatokKon2, coef.KoefEzhPogashDin2),
        new(coef.KoefEzhPogashOstatokNach3, coef.KoefEzhPogashOstatokKon3, coef.KoefEzhPogashDin3),
        new(coef.KoefEzhPogashOstatokNach4, coef.KoefEzhPogashOstatokKon4, coef.KoefEzhPogashDin4),
        new(coef.KoefEzhPogashOstatokNach5, coef.KoefEzhPogashOstatokKon5, coef.KoefEzhPogashDin5)
    };

        // 2️⃣ Get finance info
        var info = await GetFinanceInfoAsync(customerId, orderId);
        if (info == null) return 0;

        var today = DateTime.Today;
        var contractDay = info.ContractDate ?? today;
        var balance = info.Balance;
        var currentSale = info.CurrentSale;

        // 3️⃣ Get total payments made today
        var todayPayments = await db.Payments
            .Where(p => p.CustomerId == customerId && p.Date.Date == today)
            .SumAsync(p => (decimal?)p.Amount) ?? 0m;

        var adjustedBalance = balance + todayPayments; // VBA: ostatokDlyaPogash + summaPlatezha

        // 4️⃣ Determine coefficient band
        var band = bands.FirstOrDefault(b => adjustedBalance >= b.OstatokNach && adjustedBalance <= b.OstatokKon);
        if (band == null) return 0;

        // 5️⃣ Days calculation (excluding weekends)
        var numberOfDays = DatesBetweenExcludingWeekends(contractDay, today);

        // 6️⃣ Base formula
        decimal repayment;
        if (numberOfDays <= 0 || band.Days <= 0)
        {
            repayment = 0;
        }
        else
        {
            repayment = ((adjustedBalance - currentSale) / band.Days) * numberOfDays;
        }

        // 7️⃣ Safety checks (same as VBA)
        var limit = adjustedBalance - currentSale;
        if (repayment > limit || limit < 10)
            repayment = limit;

        if (repayment < 0) repayment = 0;

        return Math.Round(repayment, 2);
    }

    public async Task<List<InactiveCustomerDto>> NeaktivOtEzhigodPogashenieAsync(
    string? managerId = null,
    string? territory = null,
    string? customerId = null)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

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

        var query = db.Customers.AsQueryable();

        if (!string.IsNullOrWhiteSpace(managerId))
            query = query.Where(c => c.SalesManagerId == managerId);

        if (!string.IsNullOrWhiteSpace(territory))
            query = query.Where(c => c.Territory == territory);

        if (!string.IsNullOrWhiteSpace(customerId))
            query = query.Where(c => c.Id == customerId);

        var customers = await query.ToListAsync();
        var today = DateTime.Today;

        var result = new List<InactiveCustomerDto>();

        foreach (var cust in customers)
        {
            var info = await GetFinanceInfoAsync(cust.Id, orderId: "");
            if (info == null) continue;

            var balance = info.Balance;
            var contractDate = info.ContractDate ?? today;

            var band = bands.FirstOrDefault(b =>
                balance >= b.OstatokNach &&
                balance <= b.OstatokKon);

            if (band == null) continue;

            var numberOfDays = DatesBetweenExcludingWeekends(contractDate, today);
            var repayment = balance * 4 / band.Days * numberOfDays;

            if (repayment >= 1 && repayment > info.TotalSales)
            {
                var suggested = Math.Round(repayment - info.TotalSales, 0);

                if (suggested > 0)
                {
                    result.Add(new InactiveCustomerDto(
                        cust.Id,
                        cust.FullName ?? "",
                        info.Balance,
                        suggested
                    ));
                }
            }
        }

        return result;
    }
    public async Task<List<InactiveCustomerDto>> NeaktivNeZakupAsync(
     string? managerId = null,
     string? territory = null,
     string? customerId = null)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        var today = DateTime.Today;
        var result = new List<InactiveCustomerDto>();

        var query = db.Customers
            .Include(c => c.Orders)
                .ThenInclude(o => o.OrderDetails)
            .Include(c => c.Returns)
            .Include(c => c.Payments)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(managerId))
            query = query.Where(c => c.SalesManagerId == managerId);

        if (!string.IsNullOrWhiteSpace(territory))
            query = query.Where(c => c.Territory == territory);

        if (!string.IsNullOrWhiteSpace(customerId))
            query = query.Where(c => c.Id == customerId);

        var customers = await query.ToListAsync();

        foreach (var cust in customers)
        {
            if (cust.ContractDate == null || cust.DailyPlannedPurchaseCoefficient == null)
                continue;

            var contractDate = cust.ContractDate.Value;
            var coef = (decimal)cust.DailyPlannedPurchaseCoefficient.Value;

            var numberOfDays = DatesBetweenExcludingWeekends(contractDate, today);
            var sumPlan = numberOfDays * coef;

            decimal sales = cust.Orders
                .SelectMany(o => o.OrderDetails)
                .Sum(d => (decimal)d.Price * d.Quentity);

            decimal returns = cust.Returns
                .Sum(r => (decimal)r.TotalAmount);

            decimal payments = cust.Payments
                .Sum(p => (decimal)p.Amount);

            decimal debt = (decimal)(cust.Debt ?? 0);

            var sumaContractov = sales - returns + debt - payments;

            if (sumPlan > sumaContractov)
            {
                var shortfall = Math.Round(sumPlan - sumaContractov, 0);

                if (shortfall > 0)
                {
                    var balance = (decimal)(cust.Debt ?? 0);

                    result.Add(new InactiveCustomerDto(
                        cust.Id,
                        cust.FullName ?? "",
                        balance,
                        shortfall
                    ));
                }
            }
        }

        return result;
    }

    public async Task<DateTime?> GetLastOrderDateAsync(string customerId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        return await db.Orders
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.Date)
            .Select(o => (DateTime?)o.Date)
            .FirstOrDefaultAsync();
    }

    public async Task<List<ReconciliationRow>> GetReconciliationAsync(string customerId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        var sales = await db.Orders
            .Where(o => o.CustomerId == customerId)
            .GroupBy(o => o.Date.Date)
            .Select(g => new
            {
                Date = g.Key,
                Sum = g.SelectMany(o => o.OrderDetails)
                       .Sum(d => d.Price * d.Quentity)
            })
            .ToListAsync();

        var payments = await db.Payments
            .Where(p => p.CustomerId == customerId)
            .GroupBy(p => p.Date.Date)
            .Select(g => new
            {
                Date = g.Key,
                Sum = g.Sum(p => p.Amount)
            })
            .ToListAsync();

        var returns = await db.Returns
            .Where(r => r.CustomerId == customerId)
            .GroupBy(r => r.Date.Date)
            .Select(g => new
            {
                Date = g.Key,
                Sum = g.Sum(r => r.TotalAmount)
            })
            .ToListAsync();

        var dates = sales.Select(x => x.Date)
            .Union(payments.Select(x => x.Date))
            .Union(returns.Select(x => x.Date))
            .OrderBy(d => d);

        var result = new List<ReconciliationRow>();

        foreach (var date in dates)
        {
            result.Add(new ReconciliationRow
            {
                Date = date,
                Sales = sales.FirstOrDefault(x => x.Date == date)?.Sum ?? 0,
                Payments = payments.FirstOrDefault(x => x.Date == date)?.Sum ?? 0,
                Returns = returns.FirstOrDefault(x => x.Date == date)?.Sum ?? 0
            });
        }

        return result;
    }

    public async Task<List<InactivesSummaryRowDto>>
    GetClientsByManagerAndPeriodAsync(
    string managerId,
    DateTime fromDate,
    DateTime toDate)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        // 1️⃣ Get customers of manager
        var customers = await db.Customers
            .Where(c => c.SalesManagerId == managerId)
            .ToListAsync();

        var customerIds = customers.Select(c => c.Id).ToList();

        // 2️⃣ Get sales (orders) in period
        var sales = await db.Orders
            .Where(o => customerIds.Contains(o.CustomerId)
                     && o.Date >= fromDate
                     && o.Date <= toDate)
            .SelectMany(o => o.OrderDetails)
            .GroupBy(d => d.Order.CustomerId)
            .Select(g => new
            {
                CustomerId = g.Key,
                Sum = g.Sum(x => x.Price * x.Quentity)
            })
            .ToListAsync();

        // 3️⃣ Get payments in period
        var payments = await db.Payments
            .Where(p => customerIds.Contains(p.CustomerId)
                     && p.Date >= fromDate
                     && p.Date <= toDate)
            .GroupBy(p => p.CustomerId)
            .Select(g => new
            {
                CustomerId = g.Key,
                Sum = g.Sum(x => x.Amount)
            })
            .ToListAsync();

        // 4️⃣ Get returns in period
        var returns = await db.Returns
            .Where(r => customerIds.Contains(r.CustomerId)
                     && r.Date >= fromDate
                     && r.Date <= toDate)
            .GroupBy(r => r.CustomerId)
            .Select(g => new
            {
                CustomerId = g.Key,
                Sum = g.Sum(x => x.TotalAmount)
            })
            .ToListAsync();

        // 5️⃣ Build result in memory (SAFE)
        var result = new List<InactivesSummaryRowDto>();

        foreach (var customer in customers)
        {
            var lastOrderDate = await GetLastOrderDateAsync(customer.Id);
            var salesSum =
                sales.FirstOrDefault(x => x.CustomerId == customer.Id)?.Sum ?? 0m;

            var paymentSum =
                payments.FirstOrDefault(x => x.CustomerId == customer.Id)?.Sum ?? 0m;

            var returnSum =
                returns.FirstOrDefault(x => x.CustomerId == customer.Id)?.Sum ?? 0m;

            // SAFE nullable handling
            decimal debt = customer.Debt.HasValue
                ? Convert.ToDecimal(customer.Debt.Value)
                : 0m;

            var balance = Math.Round(
                salesSum
                - returnSum
                + debt
                - paymentSum,
                2);
             if (balance > (decimal)(customer.Restriction ?? 0))
            {
                result.Add(new InactivesSummaryRowDto
                {
                    ClientCode = customer.Id,
                    ClientName = customer.FullName ?? string.Empty,
                    Phone = customer.MobilePhone ?? string.Empty,
                    Address = customer.Address ?? string.Empty,
                    Balance = balance,
                    MaxDate = lastOrderDate,
                    Restriction = (double)customer.Restriction,
                });
            }
        }

        return result;
    }

  
   public async Task<List<InactivesSummaryRowDto>>
    GetClientsByManagerAsync(string managerId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        // 1️⃣ Get customers of manager
        var customers = await db.Customers
            .Where(c => c.SalesManagerId == managerId)
            .ToListAsync();

        var customerIds = customers.Select(c => c.Id).ToList();

        // 2️⃣ Get ALL sales (no date filter)
        var sales = await db.Orders
            .Where(o => customerIds.Contains(o.CustomerId))
            .SelectMany(o => o.OrderDetails)
            .GroupBy(d => d.Order.CustomerId)
            .Select(g => new
            {
                CustomerId = g.Key,
                Sum = g.Sum(x => x.Price * x.Quentity)
            })
            .ToListAsync();

        // 3️⃣ Get ALL payments
        var payments = await db.Payments
            .Where(p => customerIds.Contains(p.CustomerId))
            .GroupBy(p => p.CustomerId)
            .Select(g => new
            {
                CustomerId = g.Key,
                Sum = g.Sum(x => x.Amount)
            })
            .ToListAsync();

        // 4️⃣ Get ALL returns
        var returns = await db.Returns
            .Where(r => customerIds.Contains(r.CustomerId))
            .GroupBy(r => r.CustomerId)
            .Select(g => new
            {
                CustomerId = g.Key,
                Sum = g.Sum(x => x.TotalAmount)
            })
            .ToListAsync();

        // 5️⃣ Build result in memory (SAFE)
        var result = new List<InactivesSummaryRowDto>();

        foreach (var customer in customers)
        {
            var lastOrderDate = await GetLastOrderDateAsync(customer.Id);

            var salesSum =
                sales.FirstOrDefault(x => x.CustomerId == customer.Id)?.Sum ?? 0m;

            var paymentSum =
                payments.FirstOrDefault(x => x.CustomerId == customer.Id)?.Sum ?? 0m;

            var returnSum =
                returns.FirstOrDefault(x => x.CustomerId == customer.Id)?.Sum ?? 0m;

            // SAFE nullable debt
            decimal debt = customer.Debt.HasValue
                ? Convert.ToDecimal(customer.Debt.Value)
                : 0m;

            var balance = Math.Round(
                salesSum
                - returnSum
                + debt
                - paymentSum,
                2);

            // SAFE restriction handling
            decimal restriction =
                customer.Restriction.HasValue
                ? Convert.ToDecimal(customer.Restriction.Value)
                : 0m;

            if (balance > restriction)
            {
                result.Add(new InactivesSummaryRowDto
                {
                    ClientCode = customer.Id,
                    ClientName = customer.FullName ?? string.Empty,
                    Phone = customer.MobilePhone ?? string.Empty,
                    Address = customer.Address ?? string.Empty,
                    Balance = balance,
                    MaxDate = lastOrderDate,
                    Restriction = (double)restriction
                });
            }
        }

        return result;
    }

    public async Task<DateTime?> GetLastOrderDateInPeriodAsync(
    string customerId,
    DateTime from,
    DateTime to)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        return await db.Orders
            .Where(o =>
                o.CustomerId == customerId &&
                o.Date >= from &&
                o.Date <= to)
            .OrderByDescending(o => o.Date)
            .Select(o => (DateTime?)o.Date)
            .FirstOrDefaultAsync();
    }

  

}


public record InactiveCustomerDto(
	string CustomerId,
	string FullName,
	decimal Balance,
	decimal Shortfall
);

public sealed class ReconciliationRow
{
    public DateTime Date { get; set; }
    public decimal Sales { get; set; }
    public decimal Payments { get; set; }
    public decimal Returns { get; set; }

    public decimal Delta => Sales - Payments - Returns;
}

