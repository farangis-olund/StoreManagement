using Infrastructure.Entities;
using Infrastructure.Contexts;
using Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Linq.Expressions;

namespace Infrastructure.Services;

public class ReturnService
{
	private readonly ReturnRepository _returnRepository;
	private readonly DatabaseContext _db;
	private readonly ILogger<ReturnService> _logger;

	private static readonly char[] _chars =
		"ABDEFGHIJLMNOPQRTUVWXYZ0123456789".ToCharArray();

	public ReturnService(
		ReturnRepository returnRepository,
		DatabaseContext db,
		ILogger<ReturnService> logger)
	{
		_returnRepository = returnRepository;
		_db = db;
		_logger = logger;
	}

	/// <summary>
	/// Генерирует уникальный номер возврата (ВОЗ-XXXXXX).
	/// </summary>
	public async Task<string> GenerateUniqueReturnNumberAsync(
		int length = 6,
		string prefix = "ВОЗ-",
		CancellationToken ct = default)
	{
		if (length <= 0)
			throw new ArgumentOutOfRangeException(nameof(length));

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
		while (await ExistsAsync(r => r.Id == number, ct));

		return number;
	}

    /// <summary>
    /// Добавить возврат вместе с деталями.
    /// </summary>
    public async Task<ReturnEntity?> AddReturnAsync(ReturnEntity returnEntity)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(returnEntity.Id))
                returnEntity.Id = await GenerateUniqueReturnNumberAsync();

            foreach (var d in returnEntity.ReturnDetails)
                d.ReturnId = returnEntity.Id;

            await using var tx = await _db.Database.BeginTransactionAsync();

            // 1. Save return itself
            var saved = await _returnRepository.AddAsync(returnEntity);
            // Reload with Product included
				saved = await _db.Set<ReturnEntity>()
                .Include(r => r.ReturnDetails)
                    .ThenInclude(d => d.Product)
                        .ThenInclude(p => p.Brand)
				.Include(c=>c.Customer)
                .FirstOrDefaultAsync(r => r.Id == returnEntity.Id);

            // 2. Reduce remaining qty in order details
            foreach (var detail in returnEntity.ReturnDetails)
            {
                var orderDetail = await _db.Set<OrderDetailEntity>()
                    .FirstOrDefaultAsync(o =>
                        o.OrderId == returnEntity.InvoiceNumber &&
                        o.ArticleNumber == detail.ArticleNumber);

                if (orderDetail != null)
                {
                    // Decrease qty
                    orderDetail.ReturnedQty = (orderDetail.ReturnedQty ?? 0) + detail.Quantity;

                    // Clamp: prevent negative
                    if (orderDetail.ReturnedQty > orderDetail.Quentity)
                        orderDetail.ReturnedQty = orderDetail.Quentity;

                    _db.Update(orderDetail);
                }
            }

            // 3. If refund method is "Зачесть в баланс", update Customer.Dept
            if (returnEntity.RefundMethod == "Зачесть в баланс"
			&& !string.IsNullOrEmpty(returnEntity.CustomerId))
            {
                var customer = await _db.Set<CustomerEntity>()
                    .FirstOrDefaultAsync(c => c.Id == returnEntity.CustomerId);

                if (customer != null)
                {
                    customer.Debt -=(double) returnEntity.TotalAmount;
                    _db.Update(customer);
                }
            }




            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            return saved;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при сохранении возврата");
            return null;
        }
    }


    /// <summary>
    /// Получить все возвраты.
    /// </summary>
    public async Task<IReadOnlyList<ReturnEntity>> GetReturnsAsync(CancellationToken ct = default)
	{
		return await _db.Set<ReturnEntity>()
			.AsNoTracking()
			.Include(r => r.Customer)
			.Include(r => r.ReturnDetails)
			.OrderByDescending(r => r.Date)
			.ToListAsync(ct);
	}

	/// <summary>
	/// Получить возвраты за период.
	/// </summary>
	public async Task<IReadOnlyList<ReturnEntity>> GetReturnsInRangeAsync(
		DateTime from,
		DateTime to,
		CancellationToken ct = default)
	{
		return await _db.Set<ReturnEntity>()
			.AsNoTracking()
			.Include(r => r.Customer)
			.Include(r => r.ReturnDetails)
			.Where(r => r.Date >= from && r.Date <= to)
			.OrderByDescending(r => r.Date)
			.ToListAsync(ct);
	}

	/// <summary>
	/// Получить возврат по Id.
	/// </summary>
	public Task<ReturnEntity?> GetReturnAsync(string id) =>
		_returnRepository.GetOneAsync(r => r.Id == id, null);

	
	/// <summary>
	/// Удалить возврат.
	/// </summary>
	public async Task<bool> DeleteReturnAsync(string id)
	{
		try
		{
			await _returnRepository.RemoveAsync(r => r.Id == id);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Ошибка при удалении возврата");
			return false;
		}
	}

	/// <summary>
	/// Проверяет, существует ли возврат по условию.
	/// </summary>
	public async Task<bool> ExistsAsync(Expression<Func<ReturnEntity, bool>> predicate, CancellationToken ct = default)
	{
		return await _db.Set<ReturnEntity>().AnyAsync(predicate, ct);
	}
}
