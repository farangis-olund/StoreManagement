
using Infrastructure.Contexts;
using Infrastructure.Dtos;
using Infrastructure.Entities;
using Infrastructure.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;


public class ExpenseService
{
    private readonly IDbContextFactory<DatabaseContext> _contextFactory;
    public ExpenseService(IDbContextFactory<DatabaseContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<List<ExpenseDto>> GetExpensesAsync(DateTime? fromDate, DateTime? toDate)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        var query = db.Set<ExpenseEntity>()
            .AsNoTracking()
            .Include(x => x.User)
            .Include(x => x.Courier)
            .AsQueryable();

        if (fromDate.HasValue)
            query = query.Where(x => x.Date.Date >= fromDate.Value.Date);

        if (toDate.HasValue)
            query = query.Where(x => x.Date.Date <= toDate.Value.Date);

        return await query
            .OrderByDescending(x => x.Date)
            .Select(x => new ExpenseDto
            {
                Id = x.Id,
                Date = x.Date,

                UserId = x.UserId,
                CourierId = x.CourierId,

                UserFullName = x.User != null
                    ? (x.User.FirstName + " " + x.User.LastName).Trim()
                    : null,

                CourierFullName = x.Courier != null
                    ? x.Courier.FullName
                    : null,

                AmountEuro = x.AmountEuro,
                AmountTjs = x.AmountSmn,

                Reason = x.Reason.ToRussian(),
                Note = x.Note
            })
            .ToListAsync();
    }
}
