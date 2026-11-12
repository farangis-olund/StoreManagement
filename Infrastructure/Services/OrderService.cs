
using DocumentFormat.OpenXml.Drawing.Charts;
using DocumentFormat.OpenXml.InkML;
using Infrastructure.Contexts;
using Infrastructure.Constants;
using Infrastructure.Security;
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
    private readonly IPermissionChecker _permissions;
    private readonly OfficialMonthlyPlanService? _monthlyPlans;

	// Allowed characters for the random part
	private static readonly char[] _chars =
		"ABDEFGHIJLMNOPQRTUVWXYZ0123456789".ToCharArray();
	public OrderService(OrderRepository orderRepository,
								OrderDetailRepository orderDetailRepository,
								ILogger<OrderService> logger, ProductService productService, IDbContextFactory<DatabaseContext> dbFactory,
                                IPermissionChecker permissions, OfficialMonthlyPlanService? monthlyPlans = null)
	{
		_orderRepository = orderRepository;
		_orderDetailRepository = orderDetailRepository;
		_logger = logger;
		_dbFactory = dbFactory;
		_productService = productService;
        _permissions = permissions;
        _monthlyPlans = monthlyPlans;

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
        if (orderEntity.IsBarter && !_permissions.Has(PermissionCatalog.OrdersBarter))
            throw new UnauthorizedAccessException("Нет права: Заказы.Бартер.");
        if (orderEntity.DirectFromStock && !_permissions.Has(PermissionCatalog.OrdersDirectFromStock))
            throw new UnauthorizedAccessException("Нет права: Заказы.Заказ прямо со склада.");

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
            if (_monthlyPlans != null) await _monthlyPlans.ApplyToNewOrderAsync(db, orderEntity);

            // 3a) Deduct stock ONLY if not DirectFromStock
            if (!orderEntity.DirectFromStock)
            {
                var items = orderEntity.OrderDetails
                                       .Select(d => new StockDeductionItem(d.ArticleNumber, d.Quentity))
                                       .ToList();

                var stock = await _productService.DeductStockAsync(items, db);

                if (!stock.Success)
                {
                    await tx.RollbackAsync();

                    _logger.LogWarning(
                        "Недостаточно на складе: {Articles}",
                        string.Join(", ", stock.NotEnoughArticles));

                    return null;
                }
            }

            // 3b) Insert order + details
            db.Orders.Add(orderEntity);
            await db.SaveChangesAsync();

            // 3c) Commit the lot
            await tx.CommitAsync();
            _monthlyPlans?.NotifyChanged();
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
            if (_monthlyPlans != null) await _monthlyPlans.ApplyToNewOrderAsync(retryDb, orderEntity);

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
            _monthlyPlans?.NotifyChanged();
            return orderEntity;
        }
        catch (OfficialMonthlyPlanLimitException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in AddOrderAsync");
            return null;
        }
    }

    private static bool IsUniqueKeyViolation(Exception ex)
	{
        if (ex.GetBaseException() is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 }) return true;
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

			_monthlyPlans?.NotifyChanged();
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
			_monthlyPlans?.NotifyChanged();
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
			_monthlyPlans?.NotifyChanged();
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
					ReceiptNumber = p.ReceiptNumber ?? p.Id.ToString(),
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
    
	CancellationToken ct = default,
    string? courierId = null)
	{
        for (var attempt = 0; attempt < 3; attempt++)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            try
            {
                if (amount <= 0) return null;
                // Do the candidate check before Serializable to avoid holding number-range read locks.
                var receiptNumber = await GenerateUniquePaymentReceiptNumberAsync(db, ct);
                await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
                var order = string.IsNullOrWhiteSpace(orderId) ? null
                    : await db.Orders.SingleOrDefaultAsync(o => o.Id == orderId && o.CustomerId == customerId, ct);
                if (!string.IsNullOrWhiteSpace(orderId) && order == null) return null;
                var isBarter = order?.IsBarter == true;
                if (!isBarter && (string.IsNullOrWhiteSpace(courierId) ||
                    !await db.Couriers.AnyAsync(c => c.Id == courierId, ct))) return null;
                // A settled invoice must never hide a later, independently collected receipt.
                if (order?.IsPaid == true && !isBarter) orderId = null;

                var payment = new CustomerPaymentEntity
                {
                    ReceiptNumber = receiptNumber,
                    CustomerId = customerId,
                    Date = date ?? DateTime.Today,
                    Amount = amount,
                    OrderId = orderId,
                    AmountInWords = amountInWords,
                    CourierId = isBarter ? null : courierId,
                    IsDeliveryConfirmed = isBarter,
                    Rate = (decimal)rate
                };

                db.Payments.Add(payment);
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return payment;
            }
            catch (DbUpdateException ex) when (attempt < 2 && IsUniqueKeyViolation(ex))
            {
                // The unique index also protects simultaneous writers; retry in a fresh transaction.
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AddPaymentAsync failed for customer {CustomerId}", customerId);
                return null;
            }
        }
        return null;
	}

    protected virtual string CreatePaymentReceiptNumber()
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var suffix = new char[6];
        for (var i = 0; i < suffix.Length; i++)
            suffix[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
        return "ПЛ-" + new string(suffix);
    }

    private async Task<string> GenerateUniquePaymentReceiptNumberAsync(DatabaseContext db, CancellationToken ct)
    {
        string number;
        do { number = CreatePaymentReceiptNumber(); }
        while (await db.Payments.AnyAsync(p => p.ReceiptNumber == number, ct));
        return number;
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

public virtual async Task<IReadOnlyList<OrderRowDto>> GetOrdersInRangeAsync(
	DateTime? from, DateTime? to, CancellationToken ct = default, string? customerId = null)
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

        if (!string.IsNullOrWhiteSpace(customerId))
            query = query.Where(o => o.CustomerId == customerId);

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
    /// Gets individual receipts plus orders which have no receipt for this courier.
    /// </summary>
    public async Task<IReadOnlyList<OrderWithPaymentsDto>> GetOrdersByCourierAsync(
     string courierId,
     bool onlyUnpaid = true,
     CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        try
        {
            var payments = db.Payments.AsNoTracking().Where(p => p.CourierId == courierId &&
                (p.Order == null || !p.Order.IsBarter));
            if (onlyUnpaid) payments = payments.Where(p => !p.IsDeliveryConfirmed);

            var data = await payments.Select(p => new OrderWithPaymentsDto
            {
                OrderId = p.OrderId ?? "",
                PaymentId = p.Id,
                ReceiptNumber = p.ReceiptNumber ?? p.Id.ToString(),
                Date = p.Date,
                Rate = p.Rate,
                CustomerId = p.CustomerId,
                FullName = p.Customer!.FullName,
                Address = p.Customer.Address,
                City = p.Customer.City,
                Phone = p.Customer.MobilePhone,
                IsPaid = p.IsDeliveryConfirmed,
                PaymentAmount = p.Amount
            }).ToListAsync(ct);

            var query = db.Orders.AsNoTracking()
                .Where(o => o.CourierId == courierId && !o.IsBarter);
            if (onlyUnpaid) query = query.Where(o => !o.IsPaid);
            var orders = await query
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
                    Rate = (decimal)o.Rate,
                    SaleAmount = o.OrderDetails.Sum(d => d.Price * d.Quentity),
                })
                .OrderByDescending(x => x.Date)
                .ToListAsync(ct);

            foreach (var order in orders)
            {
                // Count the sale once even when several receipts reference the same invoice.
                var receipt = data.Where(p => p.OrderId == order.OrderId)
                    .OrderBy(p => p.Date).ThenBy(p => p.PaymentId).FirstOrDefault();
                if (receipt != null) receipt.SaleAmount = order.SaleAmount;
                else data.Add(order);
            }
            return data.OrderByDescending(x => x.Date).ThenBy(x => x.PaymentId).ToList();
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

        var rows = await PendingCustomerPayments(db, customerId)
            .Select(p => new UnpaidOrderDto
            {
                Id = p.OrderId ?? "",
                Date = p.Date,
                CustomerId = p.CustomerId,
                Paid = p.Amount,
                PaymentId = p.Id,
                ReceiptNumber = p.ReceiptNumber ?? p.Id.ToString()
            }).ToListAsync(ct);

        var orders = await db.Orders.AsNoTracking()
            .Where(o => o.CustomerId == customerId && !o.IsPaid && !o.IsBarter && o.CourierId != null &&
                !db.Payments.Any(p => p.OrderId == o.Id && !p.IsDeliveryConfirmed))
            .Select(o => new UnpaidOrderDto
            {
                Id = o.Id,
                Date = o.Date,
                CustomerId = o.CustomerId,
                TotalAmount = db.Set<OrderDetailEntity>()
                    .Where(d => d.OrderId == o.Id)
                    .Sum(d => (decimal?)(d.Price * d.Quentity)) ?? 0m,
            }).ToListAsync(ct);
        rows.AddRange(orders);
        return rows.OrderBy(x => x.Date).ThenBy(x => x.PaymentId).ToList();
    }

    public async Task<decimal> GetUnpaidPaymentTotalAsync(string customerId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(customerId))
            return 0m;

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        return await PendingCustomerPayments(db, customerId)
            .Where(p => p.Amount > 0m)
            .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;
    }

    private static IQueryable<CustomerPaymentEntity> PendingCustomerPayments(DatabaseContext db, string customerId)
    {
        return db.Payments.AsNoTracking().Where(p => p.CustomerId == customerId &&
            !p.IsDeliveryConfirmed && (p.Order == null || !p.Order.IsBarter));
    }

    public async Task<bool> ConfirmDeliveryAsync(string courierId, string? orderId, int? paymentId,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            if (paymentId.HasValue)
            {
                var payment = await db.Payments.SingleOrDefaultAsync(p => p.Id == paymentId && p.CourierId == courierId, ct);
                if (payment == null) return false;
                if (payment.IsDeliveryConfirmed) return true;
                payment.IsDeliveryConfirmed = true;
                db.CourierPayments.Add(new CourierPaymentEntity
                {
                    CustomerPaymentId = payment.Id,
                    OrderId = payment.OrderId,
                    CourierId = courierId,
                    Date = DateTime.Today,
                    AmountInEuro = payment.Amount,
                    AmountInTJS = payment.Amount * payment.Rate
                });
                if (payment.OrderId != null)
                {
                    var order = await db.Orders.SingleAsync(o => o.Id == payment.OrderId, ct);
                    if (order.CourierId == courierId && !await db.Payments.AnyAsync(p =>
                        p.OrderId == order.Id && p.Id != payment.Id && !p.IsDeliveryConfirmed, ct))
                        order.IsPaid = true;
                }
            }
            else
            {
                var order = await db.Orders.SingleOrDefaultAsync(o => o.Id == orderId && o.CourierId == courierId && !o.IsBarter, ct);
                if (order == null) return false;
                // A receipt may have been added after this delivery screen was loaded.
                if (await db.Payments.AnyAsync(p => p.OrderId == order.Id && p.CourierId == courierId && !p.IsDeliveryConfirmed, ct))
                    return false;
                order.IsPaid = true;
            }
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Delivery confirmation failed for receipt {PaymentId}, order {OrderId}", paymentId, orderId);
            return false;
        }
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
