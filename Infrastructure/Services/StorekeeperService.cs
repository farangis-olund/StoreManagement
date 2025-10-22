using Infrastructure.Contexts;
using Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public class StorekeeperService
{
	private readonly DatabaseContext _context;

	public StorekeeperService(DatabaseContext context)
	{
		_context = context;
	}

	// === Get all storekeepers ===
	public async Task<List<StorekeeperEntity>> GetStorekeepersAsync()
	{
		return await _context.Storekeepers
			.OrderBy(s => s.Id)
			.ToListAsync();
	}

	// === Get one by Id ===
	public async Task<StorekeeperEntity?> GetStorekeeperByIdAsync(string id)
	{
		return await _context.Storekeepers
			.FirstOrDefaultAsync(s => s.Id == id);
	}

	// === Add new storekeeper ===
	public async Task AddStorekeeperAsync(StorekeeperEntity storekeeper)
	{
		if (string.IsNullOrWhiteSpace(storekeeper.Id))
		{
			// Find the highest existing K### code
			var lastCode = await _context.Storekeepers
				.Where(s => s.Id.StartsWith("K"))
				.OrderByDescending(s => s.Id)
				.Select(s => s.Id)
				.FirstOrDefaultAsync();

			int nextNumber = 1;

			if (!string.IsNullOrEmpty(lastCode) && lastCode.Length > 1)
			{
				// Parse numeric part (e.g. "K012" → 12)
				var numericPart = lastCode.Substring(1);
				if (int.TryParse(numericPart, out int number))
					nextNumber = number + 1;
			}

			// Format new code as K001, K002, etc.
			storekeeper.Id = $"K{nextNumber:D3}";
		}

		_context.Storekeepers.Add(storekeeper);
		await _context.SaveChangesAsync();
	}


	// === Update existing storekeeper ===
	public async Task UpdateStorekeeperAsync(StorekeeperEntity storekeeper)
	{
		var existing = await _context.Storekeepers
			.FirstOrDefaultAsync(s => s.Id == storekeeper.Id);

		if (existing != null)
		{
			existing.FullName = storekeeper.FullName;
			existing.Phone = storekeeper.Phone;
			existing.Active = storekeeper.Active;

			_context.Storekeepers.Update(existing);
			await _context.SaveChangesAsync();
		}
	}

	// === Delete by Id ===
	public async Task DeleteStorekeeperAsync(string id)
	{
		var entity = await _context.Storekeepers.FindAsync(id);
		if (entity != null)
		{
			_context.Storekeepers.Remove(entity);
			await _context.SaveChangesAsync();
		}
	}

	// === Save all (used from ViewModel SaveCommand) ===
	public async Task SaveStorekeepersAsync(IEnumerable<StorekeeperEntity> storekeepers)
	{
		foreach (var s in storekeepers)
		{
			var existing = await _context.Storekeepers.FindAsync(s.Id);
			if (existing == null)
				_context.Storekeepers.Add(s);
			else
				_context.Entry(existing).CurrentValues.SetValues(s);
		}

		await _context.SaveChangesAsync();
	}
}
