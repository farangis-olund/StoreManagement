using Infrastructure.Contexts;
using Infrastructure.Dtos;
using Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using PresentationWpf.Dtos;

namespace PresentationWpf.Services;

public class BonusService
{
    private readonly IDbContextFactory<DatabaseContext> _contextFactory;

    public BonusService(IDbContextFactory<DatabaseContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<List<string>> GetBrandsAsync()
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        return await db.Brands
            .AsNoTracking()
            .Select(x => x.BrandName)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync();
    }

    public async Task<(List<CustomerLevelReviewDto> Lower, List<CustomerLevelReviewDto> Higher)>
        GetCustomerLevelReviewAsync()
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        var monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var nextMonthStart = monthStart.AddMonths(1);

        var levels = await db.Set<PriceLevelEntity>()
            .AsNoTracking()
            .Where(x => x.Code.HasValue)
            .OrderBy(x => x.Code)
            .ToListAsync();

        var customers = await db.Customers
             .AsNoTracking()
             .Include(x => x.PriceLevel)
             .Select(x => new
             {
                 x.Id,
                 x.FullName,
                 x.PriceLevelId,
                 CurrentLevelName = x.PriceLevel != null ? x.PriceLevel.PriceType : null,
                 CurrentLevelCode = x.PriceLevel != null ? x.PriceLevel.Code : null
             })
     .ToListAsync();

        var currentMonthTotals = await db.OrderDetails
            .AsNoTracking()
            .Where(x =>
                x.Order.CustomerId != null &&
                x.Order.Date >= monthStart &&
                x.Order.Date < nextMonthStart &&
                !x.Order.IsBarter)
            .GroupBy(x => x.Order.CustomerId!)
            .Select(g => new
            {
                CustomerId = g.Key,
                Total = g.Sum(x => x.Price * x.Quentity)
            })
            .ToListAsync();

        var totalsMap = currentMonthTotals.ToDictionary(x => x.CustomerId, x => x.Total);

        var lower = new List<CustomerLevelReviewDto>();
        var higher = new List<CustomerLevelReviewDto>();

        foreach (var c in customers)
        {
            var total = totalsMap.TryGetValue(c.Id, out var sum) ? sum : 0m;

            var suggested = FindLevelByAmount(levels, (double)total);
            var suggestedCode = suggested?.Code ?? 0;
            var currentCode = c.CurrentLevelCode ?? 0;

            if (suggestedCode == currentCode)
                continue;

            var row = new CustomerLevelReviewDto
            {
                CustomerId = c.Id,
                FullName = c.FullName,
                CurrentMonthTotal = total,

                CurrentLevel = c.CurrentLevelName ?? c.PriceLevelId ?? "",
                CurrentLevelCode = currentCode,
                CurrentLevelId = c.PriceLevelId,

                SuggestedLevel = suggested?.PriceType ?? "Нет уровня",
                SuggestedLevelCode = suggestedCode,
                SuggestedLevelId = suggested?.Level
            };

            if (suggestedCode < currentCode)
                lower.Add(row);
            else if (suggestedCode > currentCode)
                higher.Add(row);
        }

        return (
            lower.OrderByDescending(x => x.CurrentLevelCode - x.SuggestedLevelCode)
                 .ThenByDescending(x => x.CurrentMonthTotal)
                 .ToList(),

            higher.OrderByDescending(x => x.SuggestedLevelCode - x.CurrentLevelCode)
                  .ThenByDescending(x => x.CurrentMonthTotal)
                  .ToList()
        );
    }

    public async Task<List<BrandBonusRowDto>> GetBrandBonusAsync(
        string? brandName,
        DateTime? fromDate,
        DateTime? toDate)
    {
        if (string.IsNullOrWhiteSpace(brandName))
            return new List<BrandBonusRowDto>();

        await using var db = await _contextFactory.CreateDbContextAsync();

        var query = db.OrderDetails
            .AsNoTracking()
            .Where(x =>
                x.Order.CustomerId != null &&
                !x.Order.IsBarter &&
                x.Product.Brand.BrandName == brandName);

        if (fromDate.HasValue)
        {
            var from = fromDate.Value.Date;
            query = query.Where(x => x.Order.Date >= from);
        }

        if (toDate.HasValue)
        {
            var toExclusive = toDate.Value.Date.AddDays(1);
            query = query.Where(x => x.Order.Date < toExclusive);
        }

        var result = await query
            .GroupBy(x => new
            {
                CustomerId = x.Order.CustomerId!,
                FullName = x.Order.Customer!.FullName
            })
            .Select(g => new BrandBonusRowDto
            {
                CustomerId = g.Key.CustomerId,
                FullName = g.Key.FullName,
                TotalAmount = g.Sum(x => x.Price * x.Quentity)
            })
            .OrderByDescending(x => x.TotalAmount)
            .ToListAsync();

        return result;
    }

    public async Task<List<PromotionGapRowDto>> GetPromotionGapAsync()
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        var currentMonthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var nextMonthStart = currentMonthStart.AddMonths(1);

        var month1Start = currentMonthStart.AddMonths(-1);
        var month2Start = currentMonthStart.AddMonths(-2);
        var month3Start = currentMonthStart.AddMonths(-3);

        var historicalTotals = await db.OrderDetails
        .AsNoTracking()
        .Where(x =>
            x.Order.CustomerId != null &&
            x.Order.Date >= month3Start &&
            x.Order.Date < currentMonthStart &&
            !x.Order.IsBarter)
        .GroupBy(x => new
        {
            CustomerId = x.Order.CustomerId!,
            FullName = x.Order.Customer!.FullName
        })
        .Select(g => new
        {
            g.Key.CustomerId,
            g.Key.FullName,
            Total3Months = g.Sum(x => x.Price * x.Quentity)
        })
    .ToListAsync();

        var currentMonthTotals = await db.OrderDetails
            .AsNoTracking()
            .Where(x =>
                x.Order.CustomerId != null &&
                x.Order.Date >= currentMonthStart &&
                x.Order.Date < nextMonthStart &&
                !x.Order.IsBarter)
            .GroupBy(x => x.Order.CustomerId!)
            .Select(g => new
            {
                CustomerId = g.Key,
                CurrentTotal = g.Sum(x => x.Price * x.Quentity)
            })
            .ToListAsync();

        var currentMap = currentMonthTotals.ToDictionary(x => x.CustomerId, x => x.CurrentTotal);

        var rows = historicalTotals
    .Select(x =>
    {
        var current = currentMap.TryGetValue(x.CustomerId, out var cur) ? cur : 0m;
        var avg = x.Total3Months / 3m;

        var diff = current - avg; 

        return new PromotionGapRowDto
        {
            CustomerId = x.CustomerId,
            FullName = x.FullName,
            AverageLast3Months = avg,
            CurrentMonthAmount = current,
            Difference = diff
        };
    })
    .Where(x => x.Difference > 0) // ✅ only positive
    .OrderByDescending(x => x.Difference)
    
    .ToList();

        return rows;
    }

    private static PriceLevelEntity? FindLevelByAmount(List<PriceLevelEntity> levels, double amount)
    {
        var ordered = levels
            .Where(x => x.Code.HasValue)
            .OrderBy(x => x.Code)
            .ToList();

        for (int i = 0; i < ordered.Count; i++)
        {
            var level = ordered[i];

            bool minOk = !level.MinLimit.HasValue || amount >= level.MinLimit.Value;

            bool maxOk;
            if (i == ordered.Count - 1)
                maxOk = !level.MaxLimit.HasValue || amount <= level.MaxLimit.Value;
            else
                maxOk = !level.MaxLimit.HasValue || amount < level.MaxLimit.Value;

            if (minOk && maxOk)
                return level;
        }

        return null;
    }

    public async Task<int> UpdateCustomersToSuggestedLevelAsync(IEnumerable<CustomerLevelReviewDto> selectedCustomers)
    {
        var items = selectedCustomers
            .Where(x => x.IsSelected)
            .Where(x => !string.IsNullOrWhiteSpace(x.CustomerId))
            .Where(x => !string.IsNullOrWhiteSpace(x.SuggestedLevelId))
            .ToList();

        if (items.Count == 0)
            return 0;

        await using var db = await _contextFactory.CreateDbContextAsync();

        var customerIds = items.Select(x => x.CustomerId).Distinct().ToList();

        var customers = await db.Customers
            .Where(x => customerIds.Contains(x.Id))
            .ToListAsync();

        int updated = 0;

        foreach (var customer in customers)
        {
            var dto = items.FirstOrDefault(x => x.CustomerId == customer.Id);
            if (dto == null || string.IsNullOrWhiteSpace(dto.SuggestedLevelId))
                continue;

            if (customer.PriceLevelId == dto.SuggestedLevelId)
                continue;

            customer.PriceLevelId = dto.SuggestedLevelId;
            updated++;
        }

        await db.SaveChangesAsync();
        return updated;
    }

    public async Task<List<ShopBonusRowDto>> GetShopBonusAsync(
    DateTime fromDate,
    DateTime toDate,
    decimal percent,
    int? userId = null)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        var from = fromDate.Date;
        var to = toDate.Date.AddDays(1);

        var query = db.StoreTransferSummaries
            .AsNoTracking()
            .Where(x => x.Date >= from && x.Date < to);

        if (userId.HasValue)
            query = query.Where(x => x.UserId == userId.Value);

        var rows = await query
         .GroupBy(x => new
         {
             Date = x.Date.Date,
             x.UserId,
             x.User.FirstName,
             x.User.LastName
         })
         .Select(g => new ShopBonusRowDto
         {
             Date = g.Key.Date,

             UserId = g.Key.UserId,
             UserFullName = (g.Key.FirstName + " " + g.Key.LastName).Trim(),

             TotalQuantity = g.Sum(x => x.TotalQuantity),
             TotalAmount = g.Sum(x => x.TotalAmount),
             BonusAmount = g.Sum(x => x.TotalAmount) * percent / 100m
         })
         .OrderBy(x => x.Date)
         .ThenBy(x => x.UserFullName)
         .ToListAsync();

        return rows;
    }

    public async Task<List<ShopBonusRowDto>> GetShopBonusUsersAsync(
     DateTime? fromDate,
     DateTime? toDate)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        var query = db.StoreTransferSummaries
            .AsNoTracking()
            .AsQueryable();

        if (fromDate.HasValue)
        {
            var from = fromDate.Value.Date;
            query = query.Where(x => x.Date >= from);
        }

        if (toDate.HasValue)
        {
            var toExclusive = toDate.Value.Date.AddDays(1);
            query = query.Where(x => x.Date < toExclusive);
        }

        var users = await query
            .GroupBy(x => new
            {
                x.UserId,
                x.User.FirstName,
                x.User.LastName
            })
            .Select(g => new ShopBonusRowDto
            {
                UserId = g.Key.UserId,
                UserFullName = g.Key.FirstName + " " + g.Key.LastName
            })
            .OrderBy(x => x.UserFullName)
            .ToListAsync();

        return users;
    }
}