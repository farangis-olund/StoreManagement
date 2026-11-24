using System.Data;
using Infrastructure.Constants;
using Infrastructure.Contexts;
using Infrastructure.Dtos;
using Infrastructure.Entities;
using Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public class ClearanceService(IDbContextFactory<DatabaseContext> factory, AdminAccessPolicy access, ClearanceCatalog? catalog = null)
{
    private void EnsureAccess() => access.EnsureCanOpen(PermissionCatalog.AdminClearance);

    private static IQueryable<ClearanceProductRow> ProductRows(IQueryable<ProductEntity> products) =>
        products.AsNoTracking().Select(p => new ClearanceProductRow(p.ArticleNumber, p.ProductName,
            p.Brand.BrandName, p.Quentity,
            p.OrderDetails.Sum(d => (long?)d.Quentity) ?? 0));

    public virtual async Task<IReadOnlyList<string>> GetArticleNumbersAsync()
    {
        EnsureAccess();
        await using var db = await factory.CreateDbContextAsync();
        var articles = await db.Products.AsNoTracking().OrderBy(p => p.ArticleNumber)
            .Select(p => p.ArticleNumber).ToListAsync();
        EnsureAccess();
        return articles;
    }

    public virtual async Task<IReadOnlyList<ClearanceProductRow>> GetProductsAsync()
    {
        EnsureAccess();
        await using var db = await factory.CreateDbContextAsync();
        var rows = await ProductRows(db.Products.Where(p => db.ClearanceProducts.Any(c => c.ArticleNumber == p.ArticleNumber))
            .OrderBy(p => p.ArticleNumber)).ToListAsync();
        EnsureAccess();
        return rows;
    }

    public virtual async Task<IReadOnlyList<ClearanceProductRow>> GenerateProductsAsync(int threshold)
    {
        EnsureAccess();
        if (threshold < 0) throw new ArgumentException("Количество продаж не может быть отрицательным.");
        await using var db = await factory.CreateDbContextAsync();
        // AutoMir counts all historical sale quantities, without subtracting returns.
        var rows = await ProductRows(db.Products.Where(p => p.Quentity > 0 &&
                (p.OrderDetails.Sum(d => (long?)d.Quentity) ?? 0) <= threshold)
            .OrderBy(p => p.ArticleNumber)).ToListAsync();
        EnsureAccess();
        return rows;
    }

    public virtual async Task<ClearanceProductRow> FindProductAsync(string article)
    {
        EnsureAccess();
        article = article.Trim();
        if (article.Length is 0 or > 50) throw new ArgumentException("Укажите артикул длиной до 50 символов.");
        await using var db = await factory.CreateDbContextAsync();
        var row = await ProductRows(db.Products.Where(x => x.ArticleNumber == article)).SingleOrDefaultAsync();
        EnsureAccess();
        return row ?? throw new InvalidOperationException("Товар с таким артикулом не найден.");
    }

    public virtual async Task SaveProductsAsync(IEnumerable<string> articles)
    {
        EnsureAccess();
        var keys = articles.Select(x => x.Trim()).ToArray();
        if (keys.Any(x => x.Length is 0 or > 50) || keys.Distinct(StringComparer.OrdinalIgnoreCase).Count() != keys.Length)
            throw new ArgumentException("В списке есть пустые, повторяющиеся или слишком длинные артикулы.");
        await using var db = await factory.CreateDbContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        // Read once and validate before deleting, including lists beyond SQL Server's parameter limit.
        var known = (await db.Products.Select(x => x.ArticleNumber).ToListAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (keys.Any(x => !known.Contains(x))) throw new InvalidOperationException("Некоторые товары удалены. Обновите список.");
        EnsureAccess();
        await db.ClearanceProducts.ExecuteDeleteAsync();
        db.ClearanceProducts.AddRange(keys.Select(x => new ClearanceProductEntity { ArticleNumber = x }));
        await db.SaveChangesAsync();
        EnsureAccess();
        await transaction.CommitAsync();
        catalog?.Publish(keys);
    }

    public virtual async Task<ClearanceBonusReport> CalculateBonusAsync(DateTime fromDate, DateTime toDate, decimal percent)
    {
        EnsureAccess();
        ValidatePeriod(fromDate, toDate, percent);
        var from = fromDate.Date;
        var toExclusive = toDate.Date.AddDays(1);
        await using var db = await factory.CreateDbContextAsync();
        var sales = await db.OrderDetails.AsNoTracking()
            .Where(d => d.Order.Date >= from && d.Order.Date < toExclusive &&
                db.ClearanceProducts.Any(c => c.ArticleNumber == d.ArticleNumber))
            .GroupBy(d => new { d.Order.UserId, d.Order.User!.FirstName, d.Order.User.LastName, d.ArticleNumber })
            .Select(g => new
            {
                g.Key.UserId, g.Key.FirstName, g.Key.LastName, g.Key.ArticleNumber,
                Quantity = g.Sum(d => (long)d.Quentity), Amount = g.Sum(d => d.Quentity * d.Price)
            }).ToListAsync();
        EnsureAccess();
        var rows = sales.Select(s => new ClearanceBonusRow(s.UserId, ($"{s.FirstName} {s.LastName}").Trim(),
                s.ArticleNumber, s.Quantity, s.Amount, Math.Round(s.Amount * percent / 100m, 2, MidpointRounding.AwayFromZero)))
            .OrderBy(x => x.SellerName, StringComparer.CurrentCultureIgnoreCase).ThenBy(x => x.UserId)
            .ThenBy(x => x.ArticleNumber, StringComparer.OrdinalIgnoreCase).ToArray();
        return new ClearanceBonusReport(Guid.NewGuid(), from, toDate.Date, percent, DateTime.Now, Array.AsReadOnly(rows));
    }

    public virtual async Task SaveReportAsync(ClearanceBonusReport report)
    {
        EnsureAccess();
        ValidatePeriod(report.FromDate, report.ToDate, report.Percent);
        if (report.Id == Guid.Empty || report.Rows.Count == 0)
            throw new InvalidOperationException("Сначала сформируйте непустой отчёт о бонусах.");
        if (report.Rows.Any(r => r.Bonus != Math.Round(r.Amount * report.Percent / 100m, 2, MidpointRounding.AwayFromZero)))
            throw new InvalidOperationException("Данные отчёта изменены. Сформируйте его заново.");
        await using var db = await factory.CreateDbContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        if (await db.ClearanceBonusReports.AnyAsync(x => x.Id == report.Id)) return; // Retry is idempotent.
        if (await db.ClearanceBonusReports.AnyAsync(x => x.FromDate == report.FromDate && x.ToDate == report.ToDate))
            throw new InvalidOperationException("Отчёт за этот период уже сохранён. Откройте его в списке сохранённых отчётов.");
        db.ClearanceBonusReports.Add(new ClearanceBonusReportEntity
        {
            Id = report.Id, FromDate = report.FromDate, ToDate = report.ToDate, Percent = report.Percent,
            CreatedAt = report.CreatedAt, TotalQuantity = report.TotalQuantity,
            TotalAmount = report.TotalAmount, TotalBonus = report.TotalBonus,
            Lines = report.Rows.Select(r => new ClearanceBonusLineEntity
            {
                UserId = r.UserId, SellerName = r.SellerName, ArticleNumber = r.ArticleNumber,
                Quantity = r.Quantity, Amount = r.Amount, Bonus = r.Bonus
            }).ToList()
        });
        await db.SaveChangesAsync();
        EnsureAccess();
        await transaction.CommitAsync();
    }

    public virtual async Task<IReadOnlyList<ClearanceReportSummary>> GetHistoryAsync()
    {
        EnsureAccess();
        await using var db = await factory.CreateDbContextAsync();
        var rows = await db.ClearanceBonusReports.AsNoTracking().OrderByDescending(x => x.ToDate).ThenByDescending(x => x.CreatedAt)
            .Select(x => new ClearanceReportSummary(x.Id, x.FromDate, x.ToDate, x.Percent, x.TotalAmount, x.TotalBonus, x.CreatedAt))
            .ToListAsync();
        EnsureAccess();
        return rows;
    }

    public virtual async Task<ClearanceBonusReport> GetReportAsync(Guid id)
    {
        EnsureAccess();
        await using var db = await factory.CreateDbContextAsync();
        var report = await db.ClearanceBonusReports.AsNoTracking().Include(x => x.Lines).SingleAsync(x => x.Id == id);
        EnsureAccess();
        return new ClearanceBonusReport(report.Id, report.FromDate, report.ToDate, report.Percent, report.CreatedAt,
            Array.AsReadOnly(report.Lines.OrderBy(x => x.SellerName).ThenBy(x => x.UserId).ThenBy(x => x.ArticleNumber)
                .Select(x => new ClearanceBonusRow(x.UserId, x.SellerName, x.ArticleNumber, x.Quantity, x.Amount, x.Bonus)).ToArray()));
    }

    public static void ValidatePeriod(DateTime from, DateTime to, decimal percent)
    {
        if (from.Date > to.Date || to.Date == DateTime.MaxValue.Date)
            throw new ArgumentException("Укажите корректный период: дата начала не позже даты окончания.");
        if (percent <= 0 || percent > 100 || decimal.Round(percent, 2) != percent)
            throw new ArgumentException("Укажите процент больше 0 и не больше 100, до двух знаков после запятой.");
    }
}
