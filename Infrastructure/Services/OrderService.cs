
using DocumentFormat.OpenXml.Drawing.Charts;
using DocumentFormat.OpenXml.InkML;
using Infrastructure.Contexts;
using Infrastructure.Dtos;
using Infrastructure.Entities;
using Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Security.Cryptography;

namespace Infrastructure.Services;

public class OrderService
{
	private readonly OrderRepository _orderRepository;
	private readonly OrderDetailRepository _orderDetailRepository;
	private readonly ILogger<OrderService> _logger;
	private readonly IDbContextFactory<DatabaseContext> _dbFactory;
	private readonly ProductService _productService;

	// Allowed characters for the random part
	private static readonly char[] _chars =
		"ABDEFGHIJLMNOPQRTUVWXYZ0123456789".ToCharArray();
	public OrderService(OrderRepository orderRepository,
								OrderDetailRepository orderDetailRepository,
								ILogger<OrderService> logger, ProductService productService, IDbContextFactory<DatabaseContext> dbFactory)
	{
		_orderRepository = orderRepository;
		_orderDetailRepository = orderDetailRepository;
		_logger = logger;
		_dbFactory = dbFactory;
		_productService = productService;

	}

    /// <summary>
    /// Generates a unique order/invoice id (e.g. "СЧ-ABC123") using EF-backed existence checks.
    /// </summary>
    public async Task<string> GenerateUniqueInvoiceNumberAsync(
    bool isBarter = false,
    int length = 6,
    string? prefix = null,
    CancellationToken ct = default)
    {
        if (length <= 0)
            throw new ArgumentOutOfRangeException(nameof(length), "Length must be greater than 0.");

        // ✅ Automatically select correct prefix
        prefix ??= isBarter ? "БТ-" : "СЧ-";

        string number;
        do
        {
            var buf = new char[length];
            for (int i = 0; i < length; i++)
            {
                int idx = RandomNumberGenerator.GetInt32(_chars.Length);
                buf[i] = _chars[idx];
            }

            number = prefix + new string(buf);
        }
        while (await ExistOrderAsync(number, ct));

        return number;
    }

    public async Task<OrderEntity?> AddOrderAsync(OrderEntity orderEntity)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        try
        {
            // 1️⃣ Ensure Id (invoice number) with correct prefix
            if (string.IsNullOrWhiteSpace(orderEntity.Id))
            {
                string prefix = orderEntity.IsBarter ? "БТ-" : "СЧ-";
                orderEntity.Id = await GenerateUniqueInvoiceNumberAsync(
                    isBarter: orderEntity.IsBarter,
                    length: 6,
                    prefix: prefix
                );
            }

            // Optional pre-check
            var exists = await db.Orders.AnyAsync(o => o.Id == orderEntity.Id);
            if (exists)
                return null;

            // 2️⃣ Keep child FKs consistent
            foreach (var d in orderEntity.OrderDetails)
                d.OrderId = orderEntity.Id;

            // 3️⃣ Start transaction
            await using var tx = await db.Database.BeginTransactionAsync();

            // 3a) Deduct stock
            var items = orderEntity.OrderDetails
                                   .Select(d => new StockDeductionItem(d.ArticleNumber, d.Quentity))
                                   .ToList();

            var stock = await _productService.DeductStockAsync(items, db); // overload using same context
            if (!stock.Success)
            {
                await tx.RollbackAsync();
                _logger.LogWarning("Недостаточно на складе: {Articles}",
                    string.Join(", ", stock.NotEnoughArticles));
                return null;
            }

            // 3b) Insert order + details
            db.Orders.Add(orderEntity);
            await db.SaveChangesAsync();

            // 3c) Commit the lot
            await tx.CommitAsync();
            return orderEntity;
        }
        catch (DbUpdateException ex) when (IsUniqueKeyViolation(ex))
        {
            // 4️⃣ Retry once with new number
            await using var retryDb = await _dbFactory.CreateDbContextAsync();

            string retryPrefix = orderEntity.IsBarter ? "БТ-" : "СЧ-";
            orderEntity.Id = await GenerateUniqueInvoiceNumberAsync(
                isBarter: orderEntity.IsBarter,
                length: 6,
                prefix: retryPrefix
            );

            foreach (var d in orderEntity.OrderDetails)
                d.OrderId = orderEntity.Id;

            await using var tx = await retryDb.Database.BeginTransactionAsync();

            var items = orderEntity.OrderDetails
                                   .Select(d => new StockDeductionItem(d.ArticleNumber, d.Quentity))
                                   .ToList();

            var stock = await _productService.DeductStockAsync(items, retryDb);
            if (!stock.Success)
            {
                await tx.RollbackAsync();
                _logger.LogWarning("Недостаточно на складе (артикул): {Articles}",
                    string.Join(", ", stock.NotEnoughArticles));
                return null;
            }

            retryDb.Orders.Add(orderEntity);
            await retryDb.SaveChangesAsync();

            await tx.CommitAsync();
            return orderEntity;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in AddOrderAsync");
            return null;
        }
    }

    private static bool IsUniqueKeyViolation(Exception ex)
	{
		// SQL Server: 2601/2627; SQLite: "UNIQUE constraint failed"; PostgreSQL: 23505
		var msg = ex.GetBaseException().Message;
		return msg.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase)
			|| msg.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
			|| msg.Contains("23505"); // pg
	}

	public Task<OrderEntity?> GetOrderAsync(string id) =>
	_orderRepository.GetOneAsync(o => o.Id == id, null);


	public Task<bool> ExistOrderAsync(string id, CancellationToken ct = default)
	{
		if (string.IsNullOrWhiteSpace(id))
			throw new ArgumentException("Order id is required.", nameof(id));

		return _orderRepository.ExistsAsync(o => o.Id == id, ct);
	}


	public async Task<OrderEntity> UpdateOrderAsync(OrderEntity orderEntity)
	{
		try
		{
			var existingOrder = await _orderRepository.GetOneAsync(pp => pp.Id == orderEntity.Id);

			if (existingOrder == null)
			{
				_logger.LogWarning($"Order with ID {orderEntity.Id} not found.");
				return null!;
			}

			await _orderRepository.UpdateAsync(p => p.Id == existingOrder.Id, orderEntity);

			foreach (var orderDetail in orderEntity.OrderDetails)
			{
				var existingOrderDetail = await _orderDetailRepository.GetOneAsync(p => p.OrderId == orderDetail.OrderId && p.ArticleNumber == orderDetail.ArticleNumber);

				if (existingOrderDetail != null)
				{
					orderDetail.OrderId = existingOrderDetail.OrderId;
					await _orderDetailRepository.UpdateAsync(p => p.OrderId == existingOrderDetail.OrderId, orderDetail);
				}
				else
				{
					await _orderDetailRepository.AddAsync(orderDetail);
				}
			}

			return orderEntity;
		}
		catch (Exception ex)
		{
			_logger.LogError($"Error in updating order: {ex.Message}");
			Debug.WriteLine(ex.Message);
			return null!;
		}
	}

	public async Task<bool> DeleteOrderByIdAsync(OrderEntity orderEntity)
	{
		try
		{
			await _orderRepository.RemoveAsync(pp => pp.Id == orderEntity.Id);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError($"Error in deleting product Order by Product variant: {ex.Message}");
			Debug.WriteLine(ex.Message);
			return false;
		}
	}

	public async Task<bool> DeleteOrderDetailByArticleAsync(OrderEntity orderEntity, string article)
	{
		try
		{
			var existingOrderDetail = await _orderDetailRepository.GetOneAsync(pp => pp.OrderId == orderEntity.Id && pp.ArticleNumber == article);
			if (existingOrderDetail == null)
			{
				_logger.LogWarning($"Order not found.");
				return false!;
			}
			await _orderDetailRepository.RemoveAsync(pp => pp.OrderId == orderEntity.Id && pp.ArticleNumber == article);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError($"Error in deleting product Order by Product variant: {ex.Message}");
			Debug.WriteLine(ex.Message);
			return false;
		}
	}

	public async Task<IReadOnlyList<OrderSummaryDto>> GetOrdersByCustomerAsync(
	string customerId,
	DateTime? from = null,
	DateTime? to = null,
	CancellationToken ct = default)
	{
		await using var db = await _dbFactory.CreateDbContextAsync();
		try
		{
			var query = db.Set<OrderEntity>()
						   .AsNoTracking()
						   .Where(o => o.CustomerId == customerId && !o.IsBarter); // 🚩 exclude barter

			if (from.HasValue)
				query = query.Where(o => o.Date >= from.Value);

			if (to.HasValue)
				query = query.Where(o => o.Date < to.Value);

			var data = await query
				.Select(o => new OrderSummaryDto
				{
					Id = o.Id,
					Date = o.Date,
					TotalAmount = o.OrderDetails.Sum(d => (decimal?)(d.Price * d.Quentity)) ?? 0m
				})
				.OrderByDescending(x => x.Date)
				.ToListAsync(ct);

			return data;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error in GetOrdersByCustomerAsync for customerId {CustomerId}", customerId);
			return Array.Empty<OrderSummaryDto>();
		}
	}

	public async Task<IReadOnlyList<CustomerPaymentDto>> GetPaymentsByCustomerAsync(
	string customerId,
	DateTime? from = null,
	DateTime? to = null,
	CancellationToken ct = default)
	{
		await using var db = await _dbFactory.CreateDbContextAsync();
		try
		{
			var query = db.Set<CustomerPaymentEntity>()
						   .AsNoTracking()
						   .Where(p => p.CustomerId == customerId);

			if (from.HasValue)
				query = query.Where(p => p.Date >= from.Value);

			if (to.HasValue)
				query = query.Where(p => p.Date < to.Value);

			var data = await query
				.OrderByDescending(p => p.Date)
				.Select(p => new CustomerPaymentDto
				{
					Id = p.Id,
					Date = p.Date,
					Amount = p.Amount,
					OrderId = p.OrderId
				})
				.ToListAsync(ct);

			return data;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error in GetPaymentsByCustomerAsync for customerId {CustomerId}", customerId);
			return Array.Empty<CustomerPaymentDto>();
		}
	}

	public async Task<CustomerPaymentEntity?> AddPaymentAsync(
	string customerId,
	decimal amount,
    double rate,
    DateTime? date = null,
	string? orderId = null,
	string? amountInWords = null,
    
	CancellationToken ct = default)
	{
		await using var db = await _dbFactory.CreateDbContextAsync();
		try
		{
			if (amount <= 0) return null;

			var p = new CustomerPaymentEntity
			{
				CustomerId = customerId,
				Date = (date ?? DateTime.Today),
				Amount = amount,
				OrderId = orderId,
				AmountInWords = amountInWords,
                Rate = (decimal)rate
			};

			db.Set<CustomerPaymentEntity>().Add(p);
			await db.SaveChangesAsync(ct);
			return p;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "AddPaymentAsync failed for customer {CustomerId}", customerId);
			return null;
		}
	}

    
    public async Task<string?> GetTodayOrderIdForCustomerAsync(string customerId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var today = DateTime.Today;

        return await db.Set<OrderEntity>()
            .AsNoTracking()
            .Where(o =>
                o.CustomerId == customerId &&
                !o.IsBarter &&
                o.Date.Date == today)                 // 🔹 limit to today
            .OrderByDescending(o => o.Date)
            .Select(o => o.Id)
            .FirstOrDefaultAsync(ct);
    }


    public async Task<IReadOnlyList<OrderRowDto>> GetOrdersAsync(CancellationToken ct = default)
{
		await using var db = await _dbFactory.CreateDbContextAsync();
		try
	{
		var data = await db.Set<OrderEntity>()
			.AsNoTracking()
			.Include(o => o.Customer)
			.Include(o => o.OrderDetails)
			.Where(o => !o.IsBarter)
			.SelectMany(o => o.OrderDetails.Select(d => new OrderRowDto
			{
				Date = o.Date,
				OrderId = o.Id,
				CustomerId = o.CustomerId,
				CustomerName = o.Customer.FullName,
				Article = d.ArticleNumber,
				ProductName = d.Product.ProductName,
				Brand = d.Product.Brand.BrandName,
				Model = d.Product.Model,
				Marka =d.Product.Marka,
				Quantity = d.Quentity,
				ReturnedQty = d.ReturnedQty,
				Price = d.Price
			}))
			.OrderByDescending(x => x.Date)
			.ToListAsync(ct);

		return data;
	}
	catch (Exception ex)
	{
		_logger.LogError(ex, "Error in GetOrdersAsync()");
		return Array.Empty<OrderRowDto>();
	}
}

public async Task<IReadOnlyList<OrderRowDto>> GetOrdersInRangeAsync(
	DateTime? from, DateTime? to, CancellationToken ct = default)
{
		await using var db = await _dbFactory.CreateDbContextAsync();
		try
	{
		var query = db.Set<OrderEntity>()
			.AsNoTracking()
			.Include(o => o.Customer)
			.Include(o => o.OrderDetails)
			.Where(o => !o.IsBarter)
			.AsQueryable();

		if (from.HasValue)
			query = query.Where(o => o.Date >= from.Value);
		if (to.HasValue)
			query = query.Where(o => o.Date <= to.Value);

		var data = await query
			.SelectMany(o => o.OrderDetails.Select(d => new OrderRowDto
			{
				Date = o.Date,
				OrderId = o.Id,
				CustomerId = o.CustomerId,
				CustomerName = o.Customer.FullName,
				Article = d.ArticleNumber,
				ProductName = d.Product.ProductName,
				Brand = d.Product.Brand.BrandName,
				Model = d.Product.Model,
				Marka =d.Product.Marka,
				Quantity = d.Quentity,
				ReturnedQty = d.ReturnedQty,
				Price = d.Price
			}))
			.OrderByDescending(x => x.Date)
			.ToListAsync(ct);

		return data;
	}
	catch (Exception ex)
	{
		_logger.LogError(ex, "Error in GetOrdersInRangeAsync({From}, {To})", from, to);
		return Array.Empty<OrderRowDto>();
	}
}



    public async Task<List<DateTime>> GetSoldDatesAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        // If you track returns (ReturnedQty int?):
        return await db.Orders
            .SelectMany(o => o.OrderDetails.Select(od => new
            {
                Day = o.Date.Date,
                Qty = od.Quentity,
                Ret = od.ReturnedQty
            }))
            .GroupBy(x => x.Day)
            .Where(g => g.Sum(x => x.Qty - (x.Ret ?? 0)) > 0)   // keep dates with net > 0
            .Select(g => g.Key)
            .OrderBy(d => d)
            .ToListAsync(ct);

     
    }


    // Aggregated per article for a given day
    public async Task<List<SoldRow>> GetSoldByDateAsync(DateTime date, CancellationToken ct = default)
    {
        date = date.Date;

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var list = await db.Orders
            .Where(o => o.Date.Date == date)
            .SelectMany(o => o.OrderDetails)
            .GroupBy(d => new
            {
                d.Product.ArticleNumber,
                d.Product.ProductName,
                BrandName = d.Product.Brand.BrandName,
                GroupName = d.Product.Group.GroupName,
                d.Product.Model
            })
            .Select(g => new SoldRow
            {
                ArticleNumber = g.Key.ArticleNumber,
                ProductName = g.Key.ProductName,
                BrandName = g.Key.BrandName,
                GroupName = g.Key.GroupName,
                Model = g.Key.Model,
                Quantity = g.Sum(x => x.Quentity - (x.ReturnedQty ?? 0))
            })
            .Where(r => r.Quantity > 0)
            .OrderBy(r => r.ArticleNumber)
            .ToListAsync(ct);

        return list;
    }

    /// <summary>
    /// Gets all orders for a given courier (optionally only unpaid).
    /// </summary>
    public async Task<IReadOnlyList<OrderWithPaymentsDto>> GetOrdersByCourierAsync(
     string courierId,
     bool onlyUnpaid = true,
     CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        try
        {
            var query = db.Orders
                .AsNoTracking()
                .Include(o => o.Customer)
                .Include(o => o.OrderDetails)
                .Where(o => o.CourierId == courierId);

            if (onlyUnpaid)
                query = query.Where(o => !o.IsPaid);

            var data = await query
                .Select(o => new OrderWithPaymentsDto
                {
                    OrderId = o.Id,
                    Date = o.Date,
                    CustomerId = o.CustomerId,
                    FullName = o.Customer.FullName,
                    Address = o.Customer.Address,
					City = o.Customer.City,
                    IsPaid = o.IsPaid,
					Phone = o.Customer.MobilePhone,
                    // ✅ Total order sum (like "Продажа" in Access)
                    SaleAmount = o.OrderDetails.Sum(d => d.Price * d.Quentity),

                    // ✅ Total payments (like "Сумма платежа" in Access)
                    PaymentAmount = db.Payments
                                      .Where(p => p.OrderId == o.Id)
                                      .Sum(p => (decimal?)p.Amount) ?? 0m
                })
                .OrderByDescending(x => x.Date)
                .ToListAsync(ct);

            return data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetOrdersByCourierAsync for courier {CourierId}", courierId);
            return Array.Empty<OrderWithPaymentsDto>();
        }
    }


    /// <summary>
    /// Updates payment status and payment amount of an order.
    /// </summary>
    public async Task<bool> UpdatePaymentStatusAsync(
        string orderId,
        bool isPaid,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        try
        {
            var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == orderId, ct);
            if (order == null)
                return false;

            order.IsPaid = isPaid;
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating payment status for order {OrderId}", orderId);
            return false;
        }
    }

    public async Task<IReadOnlyList<PendingOrderDto>> GetUnsentOrdersAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.Orders
            .Where(o => !o.IsSent && !o.IsBarter)
          
            .OrderByDescending(o => o.Date) // ✅ latest first
            .Select(o => new PendingOrderDto
            {
                Invoice = o.Id,
                InvoiceNumber = o.Id,
                Date = o.Date,
                CustomerId = o.CustomerId,
                IsSent = o.IsSent
            })
            .ToListAsync();
    }

    public async Task UpdateSentStatusAsync(string invoiceNumber, bool isSent)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == invoiceNumber);
        if (order != null)
        {
            order.IsSent = isSent;
            await db.SaveChangesAsync();
        }
    }

    public async Task<IReadOnlyList<AssignPickerDto>> GetOrdersInRangeForPickersAsync(
     DateTime from,
     DateTime to,
     CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        try
        {
            var query = db.Orders
                .AsNoTracking()
                .Include(o => o.Customer)
                .Include(o => o.Storekeeper) // ✅ join storekeeper
                .Where(o => o.Date >= from && o.Date < to && !o.IsBarter);

            var data = await query
                .Select(o => new AssignPickerDto
                {
                    OrderId = o.Id,
                    CustomerName = o.Customer.FullName,
                    Date = o.Date,
                    PickerId = o.StorekeeperId,          // ✅ Id
                    PickerName = o.Storekeeper.FullName // ✅ Display name
                })
                .OrderByDescending(o => o.Date)
                .ToListAsync(ct);

            return data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetOrdersInRangeForPickersAsync({From}, {To})", from, to);
            return Array.Empty<AssignPickerDto>();
        }
    }

    public async Task<bool> UpdateAssignPickerAsync(string orderId, string? pickerId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        try
        {
            var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == orderId, ct);
            if (order == null)
                return false;

            order.StorekeeperId = pickerId;  // ✅ set by Id
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in UpdateAssignPickerAsync for order {OrderId}", orderId);
            return false;
        }
    }

    /// <summary>
    /// Checks if the specified customer already has a payment recorded on the given date.
    /// </summary>
    public async Task<bool> HasPaymentForCustomerOnDateAsync(string customerId, DateTime date, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(customerId))
            return false;

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        // normalize date to just the day (no time)
        var targetDate = date.Date;

        return await db.Set<CustomerPaymentEntity>()
            .AsNoTracking()
            .AnyAsync(p =>
                p.CustomerId == customerId &&
                p.Date.Date == targetDate, ct);
    }

    public async Task<decimal> GetCustomerPaymentsSumAsync(string customerId, DateTime date, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        return await db.Set<CustomerPaymentEntity>()
            .Where(p => p.CustomerId == customerId && p.Date.Date == date.Date)
            .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;
    }

    public async Task<decimal> GetOrderPaymentsSumAsync(string orderId, DateTime date, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        return await db.Set<CustomerPaymentEntity>()
            .Where(p => p.OrderId == orderId && p.Date.Date == date.Date)
            .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;
    }


    /// <summary>
    /// Gets the exchange rate from the Orders table
    /// for a specific order ID and date.
    /// Returns 1m if not found.
    /// </summary>
    public async Task<decimal> GetExchangeRateByDateAsync(string orderId, DateTime date, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        try
        {
            var targetDate = date.Date;

            // ✅ Query the Orders table for matching orderId and date
            var rate = await db.Orders
                .AsNoTracking()
                .Where(o => o.Id == orderId && o.Date.Date == targetDate)
                .Select(o => (decimal?)o.Rate)
                .FirstOrDefaultAsync(ct);

            // ✅ If not found, return 1 as safe default
            return rate ?? 1m;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetExchangeRateByDateAsync for Order {OrderId} on {Date}", orderId, date);
            return 1m;
        }
    }


    public async Task<List<UnpaidOrderDto>> GetUnpaidOrdersAsync(string customerId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var query =
            from o in db.Set<OrderEntity>().AsNoTracking()
            where !o.IsPaid
                  && o.CustomerId == customerId
                  && !o.IsBarter
            let totalAmount = db.Set<OrderDetailEntity>()
                .Where(d => d.OrderId == o.Id)
                .Sum(d => (decimal?)(d.Price * d.Quentity)) ?? 0m
            let paid = db.Set<CustomerPaymentEntity>()
                .Where(p => p.OrderId == o.Id)
                .Sum(p => (decimal?)p.Amount) ?? 0m
            where paid > 0m // ✅ Only include if the customer has paid something
            select new UnpaidOrderDto
            {
                Id = o.Id,
                Date = o.Date,
                CustomerId = o.CustomerId,
                TotalAmount = totalAmount,
                Paid = paid,
                PaymentId = db.Set<CustomerPaymentEntity>()
                    .Where(p => p.OrderId == o.Id)
                    .OrderByDescending(p => p.Date)
                    .Select(p => p.Id)
                    .FirstOrDefault()
            };

        return await query
            .OrderBy(x => x.Date)
            .ToListAsync(ct);
    }

    public async Task<bool> HasUnpaidOrdersAsync(string customerId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.Orders
            .AnyAsync(o => o.CustomerId == customerId && !o.IsPaid && !o.IsBarter);
    }

    public async Task AddCourierPaymentAsync(
     string courierId,
     string orderId,
     decimal amountEuro,
     decimal amountTjs,
     DateTime date,
     CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var exists = await db.Set<CourierPaymentEntity>()
            .AnyAsync(x => x.OrderId == orderId && x.CourierId == courierId, ct);

        if (exists)
            return;

        var entity = new CourierPaymentEntity
        {
            CourierId = courierId,
            OrderId = orderId,
            Date = date,
            AmountInEuro = amountEuro,
            AmountInTJS = amountTjs
        };

        db.Set<CourierPaymentEntity>().Add(entity);
        await db.SaveChangesAsync(ct);
    }
}

