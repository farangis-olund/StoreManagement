using Infrastructure.Contexts;
using Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public class CourierService
{
    private readonly DatabaseContext _context;

    public CourierService(DatabaseContext context)
    {
        _context = context;
    }

    // === Get all couriers ===
    public async Task<List<CourierEntity>> GetCouriersAsync()
    {
        return await _context.Couriers
            .OrderBy(c => c.Id)
            .ToListAsync();
    }

    // === Get one by Id ===
    public async Task<CourierEntity?> GetCourierByIdAsync(string id)
    {
        return await _context.Couriers
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    // === Add new courier ===
    public async Task AddCourierAsync(CourierEntity courier)
    {
        if (string.IsNullOrWhiteSpace(courier.Id))
        {
            // Find the highest existing C### code
            var lastCode = await _context.Couriers
                .Where(c => c.Id.StartsWith("C"))
                .OrderByDescending(c => c.Id)
                .Select(c => c.Id)
                .FirstOrDefaultAsync();

            int nextNumber = 1;

            if (!string.IsNullOrEmpty(lastCode) && lastCode.Length > 1)
            {
                // Parse numeric part (e.g. "C012" → 12)
                var numericPart = lastCode.Substring(1);
                if (int.TryParse(numericPart, out int number))
                    nextNumber = number + 1;
            }

            // Format new code as C001, C002, etc.
            courier.Id = $"C{nextNumber:D3}";
        }

        _context.Couriers.Add(courier);
        await _context.SaveChangesAsync();
    }

    // === Update existing courier ===
    public async Task UpdateCourierAsync(CourierEntity courier)
    {
        var existing = await _context.Couriers
            .FirstOrDefaultAsync(c => c.Id == courier.Id);

        if (existing != null)
        {
            existing.FullName = courier.FullName;
            existing.Phone = courier.Phone;
            existing.Active = courier.Active;

            _context.Couriers.Update(existing);
            await _context.SaveChangesAsync();
        }
    }

    // === Delete by Id ===
    public async Task DeleteCourierAsync(string id)
    {
        var entity = await _context.Couriers.FindAsync(id);
        if (entity != null)
        {
            _context.Couriers.Remove(entity);
            await _context.SaveChangesAsync();
        }
    }

    // === Save all (used for batch updates) ===
    public async Task SaveCouriersAsync(IEnumerable<CourierEntity> couriers)
    {
        foreach (var c in couriers)
        {
            var existing = await _context.Couriers.FindAsync(c.Id);
            if (existing == null)
                _context.Couriers.Add(c);
            else
                _context.Entry(existing).CurrentValues.SetValues(c);
        }

        await _context.SaveChangesAsync();
    }
}
