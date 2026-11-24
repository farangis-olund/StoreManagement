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
    private readonly OfficialMonthlyPlanService? _monthlyPlans;
    private readonly ProductExpiryCatalog? _expiryCatalog;

	private static readonly char[] _chars =
		"ABDEFGHIJLMNOPQRTUVWXYZ0123456789".ToCharArray();

	public ReturnService(
		ReturnRepository returnRepository,
		DatabaseContext db,
		ILogger<ReturnService> logger, OfficialMonthlyPlanService? monthlyPlans = null, ProductExpiryCatalog? expiryCatalog = null)
	{
		_returnRepository = returnRepository;
		_db = db;
		_logger = logger;
        _monthlyPlans = monthlyPlans;
        _expiryCatalog = expiryCatalog;
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
    public virtual async Task<ReturnEntity?> AddReturnAsync(ReturnEntity returnEntity)
    {
        _db.ChangeTracker.Clear();
        try
        {
            if (string.IsNullOrWhiteSpace(returnEntity.Id))
                returnEntity.Id = await GenerateUniqueReturnNumberAsync();

            foreach (var d in returnEntity.ReturnDetails)
                d.ReturnId = returnEntity.Id;

            await using var tx = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            if (await _db.Returns.AnyAsync(x => x.Id == returnEntity.Id)) return null;
            _db.Returns.Add(returnEntity);
            await ExpiryBatchStock.RestoreReturnAsync(_db, returnEntity);

            await _db.SaveChangesAsync();
            var saved = await _db.Returns.Include(x => x.Customer).Include(x => x.ReturnDetails).ThenInclude(x => x.Product).ThenInclude(x => x.Brand)
                .SingleAsync(x => x.Id == returnEntity.Id);
            var annotations = _expiryCatalog == null ? null : await ProductExpiryCatalog.ReadAsync(_db);
            await tx.CommitAsync();
            if (annotations != null) _expiryCatalog!.Publish(annotations);
            _monthlyPlans?.NotifyChanged();

            return saved;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при сохранении возврата");
            _db.ChangeTracker.Clear();
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
            _db.ChangeTracker.Clear();
            await using var tx = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var document = await _db.Returns.Include(x => x.ReturnDetails).SingleOrDefaultAsync(x => x.Id == id);
            if (document == null) return false;
            await ExpiryBatchStock.UndoReturnAsync(_db, document);
            _db.Returns.Remove(document); await _db.SaveChangesAsync();
            var annotations = _expiryCatalog == null ? null : await ProductExpiryCatalog.ReadAsync(_db);
            await tx.CommitAsync(); if (annotations != null) _expiryCatalog!.Publish(annotations);
			_monthlyPlans?.NotifyChanged();
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Ошибка при удалении возврата");
            _db.ChangeTracker.Clear();
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

    /// <summary>
    /// Получить все возвраты за сегодня.
    /// </summary>
    public async Task<IReadOnlyList<ReturnEntity>> GetTodayReturnsAsync(CancellationToken ct = default)
    {
        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);

        return await _db.Set<ReturnEntity>()
            .AsNoTracking()
            .Include(r => r.Customer)
            .Include(r => r.ReturnDetails)
                .ThenInclude(d => d.Product)
                    .ThenInclude(p => p.Brand)
            .Where(r => r.Date >= today && r.Date < tomorrow)
            .OrderByDescending(r => r.Date)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<DateTime>> GetReturnDatesAsync(CancellationToken ct = default)
    {
        return await _db.Set<ReturnEntity>()
            .AsNoTracking()
            .Select(r => r.Date.Date)
            .Distinct()
            .OrderByDescending(d => d)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ReturnEntity>> GetReturnsByDateAsync(
        DateTime date,
        CancellationToken ct = default)
    {
        var from = date.Date;
        var to = from.AddDays(1);

        return await _db.Set<ReturnEntity>()
            .AsNoTracking()
            .Include(r => r.Customer)
            .Include(r => r.ReturnDetails)
                .ThenInclude(d => d.Product)
                    .ThenInclude(p => p.Brand)
            .Where(r => r.Date >= from && r.Date < to)
            .OrderByDescending(r => r.Date)
            .ToListAsync(ct);
    }
}
