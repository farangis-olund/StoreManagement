
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
    private readonly ICurrentUserContext? _session;
    private readonly OrderOperationsAccessPolicy _operationsAccess;
    private readonly ProductExpiryCatalog? _expiryCatalog;

	// Allowed characters for the random part
	private static readonly char[] _chars =
		"ABDEFGHIJLMNOPQRTUVWXYZ0123456789".ToCharArray();
	public OrderService(OrderRepository orderRepository,
								OrderDetailRepository orderDetailRepository,
								ILogger<OrderService> logger, ProductService productService, IDbContextFactory<DatabaseContext> dbFactory,
                                IPermissionChecker permissions, OfficialMonthlyPlanService? monthlyPlans = null,
                                ICurrentUserContext? session = null, ProductExpiryCatalog? expiryCatalog = null)
	{
		_orderRepository = orderRepository;
		_orderDetailRepository = orderDetailRepository;
		_logger = logger;
		_dbFactory = dbFactory;
		_productService = productService;
        _permissions = permissions;
        _monthlyPlans = monthlyPlans;
        _session = session;
        _operationsAccess = new(permissions);
        _expiryCatalog = expiryCatalog;

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

    public virtual async Task<OrderEntity?> AddOrderAsync(OrderEntity orderEntity)
    {
        if (orderEntity.IsBarter && !_permissions.Has(PermissionCatalog.OrdersBarter))
            throw new UnauthorizedAccessException("Нет права: Заказы.Бартер.");
        if (orderEntity.DirectFromStock && !_permissions.Has(PermissionCatalog.OrdersDirectFromStock))
            throw new UnauthorizedAccessException("Нет права: Заказы.Заказ прямо со склада.");
        if (orderEntity.IsBarter) orderEntity.OfficialOrder = false;

        for (var attempt = 0; attempt < 3; attempt++)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            CustomerPaymentEntity? barterPayment = null;
            var committed = false;
            try
            {
                if (attempt > 0 || string.IsNullOrWhiteSpace(orderEntity.Id))
                    orderEntity.Id = await GenerateUniqueInvoiceNumberAsync(isBarter: orderEntity.IsBarter);

                if (await db.Orders.AnyAsync(o => o.Id == orderEntity.Id)) return null;

                foreach (var detail in orderEntity.OrderDetails)
                    detail.OrderId = orderEntity.Id;

                var receiptNumber = orderEntity.IsBarter
                    ? await GenerateUniquePaymentReceiptNumberAsync(db, CancellationToken.None) : null;
                await using var tx = await db.Database.BeginTransactionAsync();
                if (_monthlyPlans != null) await _monthlyPlans.ApplyToNewOrderAsync(db, orderEntity);

                if (!orderEntity.DirectFromStock)
                {
                    var items = orderEntity.OrderDetails.OrderBy(x => x.ArticleNumber, StringComparer.OrdinalIgnoreCase)
                        .Select(d => new StockDeductionItem(d.ArticleNumber, d.Quentity)).ToList();
                    var stock = await _productService.DeductStockAsync(items, db);
                    if (!stock.Success)
                    {
                        _logger.LogWarning("Недостаточно на складе: {Articles}", string.Join(", ", stock.NotEnoughArticles));
                        return null;
                    }
                }

                await ExpiryBatchStock.AllocateAsync(db, orderEntity);
                if (orderEntity.IsBarter) orderEntity.IsPaid = true;
                db.Orders.Add(orderEntity);
                await db.SaveChangesAsync();

                if (orderEntity.IsBarter)
                {
                    // Use the persisted prices so the receipt exactly matches the saved invoice.
                    var amount = await db.OrderDetails.Where(d => d.OrderId == orderEntity.Id)
                        .SumAsync(d => (decimal?)(d.Price * d.Quentity)) ?? 0m;
                    barterPayment = new CustomerPaymentEntity
                    {
                        ReceiptNumber = receiptNumber,
                        CustomerId = orderEntity.CustomerId!,
                        OrderId = orderEntity.Id,
                        Date = orderEntity.Date,
                        Amount = amount,
                        AmountInWords = Infrastructure.Utilities.NumberToWordsConverter.ConvertToRussianWords(amount),
                        Rate = (decimal)orderEntity.Rate,
                        IsDeliveryConfirmed = true
                    };
                    db.Payments.Add(barterPayment);
                    await db.SaveChangesAsync();
                }

                var expiryAnnotations = _expiryCatalog == null ? null : await ProductExpiryCatalog.ReadAsync(db);
                await tx.CommitAsync();
                committed = true;
                if (expiryAnnotations != null) _expiryCatalog!.Publish(expiryAnnotations);
                _monthlyPlans?.NotifyChanged();
                return orderEntity;
            }
            catch (DbUpdateException ex) when (attempt < 2 && IsUniqueKeyViolation(ex))
            {
                // Both document numbers have unique indexes; retry the whole transaction.
            }
            catch (OfficialMonthlyPlanLimitException) { throw; }
            catch (ExpiryBatchStockException) { throw; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AddOrderAsync");
                return null;
            }
            finally
            {
                // EF fixes up this navigation even when a failed transaction is rolled back.
                if (!committed && barterPayment != null)
                    orderEntity.Payments.Remove(barterPayment);
            }
        }
        return null;
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

	public virtual Task<OrderEntity?> GetOrderAsync(string id) =>
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

            await using var guardDb = await _dbFactory.CreateDbContextAsync();
            if (await guardDb.OrderExpiryAllocations.AnyAsync(x => x.OrderId == existingOrder.Id) &&
                (orderEntity.DirectFromStock != existingOrder.DirectFromStock || orderEntity.OrderDetails.Count != existingOrder.OrderDetails.Count ||
                 orderEntity.OrderDetails.Any(x => !existingOrder.OrderDetails.Any(y => y.ArticleNumber == x.ArticleNumber && y.Quentity == x.Quentity))))
                throw new InvalidOperationException("Количество в накладной со списанными партиями изменять нельзя. Оформите возврат.");
            foreach (var detail in orderEntity.OrderDetails)
                detail.WarehousePlaceSnapshot = existingOrder.OrderDetails.FirstOrDefault(x => x.ArticleNumber == detail.ArticleNumber)?.WarehousePlaceSnapshot;
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
            await using var db = await _dbFactory.CreateDbContextAsync();
            if (await db.OrderExpiryAllocations.AnyAsync(x => x.OrderId == orderEntity.Id)) return false;
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
            await using var db = await _dbFactory.CreateDbContextAsync();
            if (await db.OrderExpiryAllocations.AnyAsync(x => x.OrderId == orderEntity.Id && x.ArticleNumber == article)) return false;
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
						   .Where(o => o.CustomerId == customerId);

			if (from.HasValue)
				query = query.Where(o => o.Date >= from.Value);

			if (to.HasValue)
				query = query.Where(o => o.Date < to.Value);

			var data = await query
				.Select(o => new OrderSummaryDto
				{
					Id = o.Id,
					Date = o.Date,
					IsBarter = o.IsBarter,
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
    public virtual async Task<IReadOnlyList<OrderWithPaymentsDto>> GetOrdersByCourierAsync(
     string courierId,
     bool onlyUnpaid = true,
     CancellationToken ct = default)
    {
        var userId = EnsureOrderOperationAccess(PermissionCatalog.OrdersDeliveryView);
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
            EnsureOrderOperationAccess(PermissionCatalog.OrdersDeliveryView, userId);
            return data.OrderByDescending(x => x.Date).ThenBy(x => x.PaymentId).ToList();
        }
        catch (UnauthorizedAccessException) { throw; }
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
        var userId = EnsureOrderOperationAccess(PermissionCatalog.OrdersDeliveryUpdate);
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        try
        {
            var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == orderId, ct);
            if (order == null)
                return false;

            order.IsPaid = isPaid;
            EnsureOrderOperationAccess(PermissionCatalog.OrdersDeliveryUpdate, userId);
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (UnauthorizedAccessException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating payment status for order {OrderId}", orderId);
            return false;
        }
    }

    public virtual async Task<IReadOnlyList<PendingOrderDto>> GetUnsentOrdersAsync(string? managerId = null)
    {
        EnsurePendingOrdersAccess();
        var userId = _session!.UserId;
        var allManagers = _permissions.Has(PermissionCatalog.OrdersViewAllManagers);
        await using var db = await _dbFactory.CreateDbContextAsync();

        var query = db.Orders.AsNoTracking()
            .Where(o => !o.IsSent && !o.IsBarter && (allManagers || db.SalesManagers.Any(m =>
                m.Active && m.UserId == userId && m.User!.IsManager && m.Id == o.Customer!.SalesManagerId)));
        if (!string.IsNullOrWhiteSpace(managerId))
            query = query.Where(o => o.Customer != null && o.Customer.SalesManagerId == managerId);

        var orders = await query
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

        EnsurePendingOrdersAccess();
        if (_session.UserId != userId || allManagers != _permissions.Has(PermissionCatalog.OrdersViewAllManagers))
            throw new UnauthorizedAccessException("Пользователь или права доступа изменились. Повторно откройте список заказов.");
        return orders;
    }

    private void EnsurePendingOrdersAccess()
    {
        if (_session?.IsLoggedIn != true || !_permissions.Has(PermissionCatalog.Orders))
            throw new UnauthorizedAccessException("Нет доступа к списку неотправленных заказов.");
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

    public virtual async Task<IReadOnlyList<AssignPickerDto>> GetOrdersInRangeForPickersAsync(
     DateTime from,
     DateTime to,
     CancellationToken ct = default)
    {
        var userId = EnsureOrderOperationAccess(PermissionCatalog.OrdersAssignPickers);
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

            EnsureOrderOperationAccess(PermissionCatalog.OrdersAssignPickers, userId);
            return data;
        }
        catch (UnauthorizedAccessException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetOrdersInRangeForPickersAsync({From}, {To})", from, to);
            return Array.Empty<AssignPickerDto>();
        }
    }

    public virtual async Task<bool> UpdateAssignPickerAsync(string orderId, string? pickerId, CancellationToken ct = default)
    {
        var userId = EnsureOrderOperationAccess(PermissionCatalog.OrdersAssignPickers);
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == orderId, ct);
            if (order == null)
                return false;

            order.StorekeeperId = pickerId;  // ✅ set by Id
            EnsureOrderOperationAccess(PermissionCatalog.OrdersAssignPickers, userId);
            await db.SaveChangesAsync(ct);
            EnsureOrderOperationAccess(PermissionCatalog.OrdersAssignPickers, userId);
            await transaction.CommitAsync(ct);
            return true;
        }
        catch (UnauthorizedAccessException) { throw; }
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

    public virtual async Task<bool> ConfirmDeliveryAsync(string courierId, string? orderId, int? paymentId,
        CancellationToken ct = default)
    {
        var userId = EnsureOrderOperationAccess(PermissionCatalog.OrdersDeliveryUpdate);
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
            EnsureOrderOperationAccess(PermissionCatalog.OrdersDeliveryUpdate, userId);
            await db.SaveChangesAsync(ct);
            EnsureOrderOperationAccess(PermissionCatalog.OrdersDeliveryUpdate, userId);
            await transaction.CommitAsync(ct);
            return true;
        }
        catch (UnauthorizedAccessException) { throw; }
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
        var userId = EnsureOrderOperationAccess(PermissionCatalog.OrdersDeliveryUpdate);
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
        EnsureOrderOperationAccess(PermissionCatalog.OrdersDeliveryUpdate, userId);
        await db.SaveChangesAsync(ct);
    }

    private int EnsureOrderOperationAccess(string permission, int? expectedUserId = null)
    {
        if (_session?.IsLoggedIn != true || (expectedUserId.HasValue && _session.UserId != expectedUserId))
            throw new UnauthorizedAccessException("Пользователь изменился. Повторно откройте форму заказов.");
        _operationsAccess.Ensure(permission);
        return _session.UserId;
    }
}
