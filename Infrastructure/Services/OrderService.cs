
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
		int length = 6,
		string prefix = "СЧ-",
		CancellationToken ct = default)
	{
		if (length <= 0)
			throw new ArgumentOutOfRangeException(nameof(length), "Length must be greater than 0.");

		string number;
		do
		{
			// cryptographically-strong random selection of characters
			var buf = new char[length];
			for (int i = 0; i < length; i++)
			{
				int idx = RandomNumberGenerator.GetInt32(_chars.Length);
				buf[i] = _chars[idx];
			}

			number = prefix + new string(buf);
		}
		while (await ExistOrderAsync(number, ct)); // ensure uniqueness via repo

		return number;
	}

	//public async Task<OrderEntity?> AddOrderAsync(OrderEntity orderEntity)
	//{
	//	await using var db = await _dbFactory.CreateDbContextAsync();
	//	try
	//	{
	//		// 1) Ensure Id (string key) up front
	//		if (string.IsNullOrWhiteSpace(orderEntity.Id))
	//			orderEntity.Id = await GenerateUniqueInvoiceNumberAsync(length: 6, prefix: "СЧ-");

	//		// Optional pre-check (race-prone, final guard is catch below)
	//		if (await _orderRepository.ExistsAsync(o => o.Id == orderEntity.Id))
	//			return null;

	//		// Keep child FKs consistent
	//		foreach (var d in orderEntity.OrderDetails)
	//			d.OrderId = orderEntity.Id;

	//		// 2) One transaction for: stock deduction + order insert
	//		await using var tx = await db.Database.BeginTransactionAsync();

	//		// 2a) Deduct stock (safe UPDATE ... WHERE Quentity >= @qty inside the same DbContext)
	//		var items = orderEntity.OrderDetails
	//							   .Select(d => new StockDeductionItem(d.ArticleNumber, d.Quentity))
	//							   .ToList();

	//		var stock = await _productService.DeductStockAsync(items);
	//		if (!stock.Success)
	//		{
	//			// Roll back and report which articles failed
	//			await tx.RollbackAsync();
	//			_logger.LogWarning("Недостаточно на складе: {Articles}",
	//				string.Join(", ", stock.NotEnoughArticles));
	//			return null;
	//		}

	//		// 2b) Insert order + details in one go (cascading insert)
	//		var saved = await _orderRepository.AddAsync(orderEntity); // uses same _db; SaveChanges participates in tx

	//		// 2c) Commit the lot
	//		await tx.CommitAsync();
	//		return saved;
	//	}
	//	catch (DbUpdateException ex) when (IsUniqueKeyViolation(ex))
	//	{
	//		// Retry once with a new number inside a fresh transaction
	//		await using var retryDb = await _dbFactory.CreateDbContextAsync();

	//		orderEntity.Id = await GenerateUniqueInvoiceNumberAsync(length: 6, prefix: "СЧ-");
	//		foreach (var d in orderEntity.OrderDetails)
	//			d.OrderId = orderEntity.Id;

	//		await using var tx = await retryDb.Database.BeginTransactionAsync();

	//		var items = orderEntity.OrderDetails
	//							   .Select(d => new StockDeductionItem(d.ArticleNumber, d.Quentity))
	//							   .ToList();

	//		var stock = await _productService.DeductStockAsync(items);
	//		if (!stock.Success)
	//		{
	//			await tx.RollbackAsync();
	//			_logger.LogWarning("Недостаточно на складе (артикул): {Articles}",
	//				string.Join(", ", stock.NotEnoughArticles));
	//			return null;
	//		}

	//		var saved = await _orderRepository.AddAsync(orderEntity);
	//		await tx.CommitAsync();
	//		return saved;
	//	}
	//	catch (Exception ex)
	//	{
	//		_logger.LogError(ex, "Error in AddOrderAsync");
	//		return null;
	//	}
	//}

	public async Task<OrderEntity?> AddOrderAsync(OrderEntity orderEntity)
	{
		await using var db = await _dbFactory.CreateDbContextAsync();
		try
		{
			// 1) Ensure Id (string key) up front
			if (string.IsNullOrWhiteSpace(orderEntity.Id))
				orderEntity.Id = await GenerateUniqueInvoiceNumberAsync(length: 6, prefix: "СЧ-");

			// Optional pre-check (race-prone, final guard is catch below)
			var exists = await db.Orders.AnyAsync(o => o.Id == orderEntity.Id);
			if (exists) return null;

			// Keep child FKs consistent
			foreach (var d in orderEntity.OrderDetails)
				d.OrderId = orderEntity.Id;

			// 2) One transaction for: stock deduction + order insert
			await using var tx = await db.Database.BeginTransactionAsync();

			// 2a) Deduct stock (inside the SAME db context!)
			var items = orderEntity.OrderDetails
								   .Select(d => new StockDeductionItem(d.ArticleNumber, d.Quentity))
								   .ToList();

			var stock = await _productService.DeductStockAsync(items, db); // overload that accepts db
			if (!stock.Success)
			{
				await tx.RollbackAsync();
				_logger.LogWarning("Недостаточно на складе: {Articles}",
					string.Join(", ", stock.NotEnoughArticles));
				return null;
			}

			// 2b) Insert order + details
			db.Orders.Add(orderEntity);
			await db.SaveChangesAsync();

			// 2c) Commit the lot
			await tx.CommitAsync();
			return orderEntity;
		}
		catch (DbUpdateException ex) when (IsUniqueKeyViolation(ex))
		{
			// Retry once with a new number
			await using var retryDb = await _dbFactory.CreateDbContextAsync();

			orderEntity.Id = await GenerateUniqueInvoiceNumberAsync(length: 6, prefix: "СЧ-");
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
				AmountInWords = amountInWords
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

	public async Task<string?> GetLastOrderIdForCustomerAsync(string customerId, CancellationToken ct = default)
	{
		await using var db = await _dbFactory.CreateDbContextAsync();
		return await db.Set<OrderEntity>()
						.AsNoTracking()
						.Where(o => o.CustomerId == customerId && !o.IsBarter)
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
				CustomerName = o.Customer.FullName,
				Article = d.ArticleNumber,
				ProductName = d.Product.ProductName,
				Brand = d.Product.Brand.BrandName,
				Model = d.Product.Model,
				Quantity = d.Quentity,
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
				CustomerName = o.Customer.FullName,
				Article = d.ArticleNumber,
				ProductName = d.Product.ProductName,
				Brand = d.Product.Brand.BrandName,
				Model = d.Product.Model,
				Quantity = d.Quentity,
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



}

