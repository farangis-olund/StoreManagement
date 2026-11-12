using Infrastructure.Constants;
using Infrastructure.Contexts;
using Infrastructure.Dtos;
using Infrastructure.Entities;
using Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public class OfficialMonthlyPlanService(IDbContextFactory<DatabaseContext> factory, IPermissionChecker permissions)
{
    public event EventHandler? Changed;
    public static DateTime MonthStart(DateTime date) => new(date.Year, date.Month, 1);

    public void NotifyChanged()
    {
        if (Changed == null) return;
        foreach (EventHandler handler in Changed.GetInvocationList())
            try { handler(this, EventArgs.Empty); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
    }

    private void Require(string permission)
    {
        if (!permissions.Has(permission)) throw new UnauthorizedAccessException("Нет права доступа к месячным планам.");
    }

    public virtual async Task<IReadOnlyList<OfficialMonthlyPlanSetting>> GetSettingsAsync(DateTime period)
    {
        Require(PermissionCatalog.OfficialMonthlyPlansManage);
        var month = MonthStart(period);
        await using var db = await factory.CreateDbContextAsync();
        await EnsurePlansForMonthAsync(db, month);
        var rows = await db.Customers.AsNoTracking().Where(c => c.OfficialCustomer).OrderBy(c => c.FullName)
            .Select(c => new OfficialMonthlyPlanSetting(c.Id, c.FullName,
                db.OfficialMonthlyPlans.Where(p => p.CustomerId == c.Id && p.Month == month)
                    .Select(p => (decimal?)p.Amount).FirstOrDefault())).ToListAsync();
        Require(PermissionCatalog.OfficialMonthlyPlansManage);
        return rows;
    }

    public virtual async Task<IReadOnlyList<OfficialMonthlyPlanSummary>> GetControlAsync(DateTime period)
    {
        Require(PermissionCatalog.OfficialMonthlyPlansView);
        await using var db = await factory.CreateDbContextAsync();
        var month = MonthStart(period);
        await EnsurePlansForMonthAsync(db, month);
        var rows = await CalculateAsync(db, month);
        Require(PermissionCatalog.OfficialMonthlyPlansView);
        return rows;
    }

    public virtual async Task<IReadOnlyList<int>> GetYearsAsync()
    {
        if (!permissions.Has(PermissionCatalog.OfficialMonthlyPlansView)) Require(PermissionCatalog.OfficialMonthlyPlansManage);
        await using var db = await factory.CreateDbContextAsync();
        return await db.OfficialMonthlyPlans.Select(p => p.Month.Year).Distinct().OrderBy(y => y).ToListAsync();
    }

    public Task SetPlanAsync(string customerId, DateTime period, decimal amount) =>
        SetPlansAsync(period, new Dictionary<string, decimal> { [customerId] = amount });

    public virtual async Task<decimal> GetNonOfficialThresholdAsync()
    {
        Require(PermissionCatalog.OfficialMonthlyPlansManage);
        await using var db = await factory.CreateDbContextAsync();
        var threshold = await ReadThresholdAsync(db);
        Require(PermissionCatalog.OfficialMonthlyPlansManage);
        return threshold;
    }

    private static async Task<decimal> ReadThresholdAsync(DatabaseContext db) =>
        await db.OfficialMonthlyPlanSettings.Where(s => s.Id == 1).Select(s => (decimal?)s.NonOfficialThreshold).SingleOrDefaultAsync() ?? 0m;

    public virtual async Task SetPlansAsync(DateTime period, IReadOnlyDictionary<string, decimal> amounts,
        decimal? nonOfficialThreshold = null)
    {
        Require(PermissionCatalog.OfficialMonthlyPlansManage);
        var changes = amounts.OrderBy(p => p.Key, StringComparer.Ordinal).ToArray();
        if (changes.Any(p => string.IsNullOrWhiteSpace(p.Key)))
            throw new ArgumentException("Не указан клиент для месячного плана.");
        if (changes.Any(p => p.Value <= 0 || p.Value > 9999999999999999.99m || decimal.Round(p.Value, 2) != p.Value))
            throw new ArgumentException("Укажите план больше нуля, не более двух знаков после запятой.");
        if (nonOfficialThreshold.HasValue && (nonOfficialThreshold < 0 || nonOfficialThreshold > 9999999999999999.99m ||
            decimal.Round(nonOfficialThreshold.Value, 2) != nonOfficialThreshold.Value))
            throw new ArgumentException("Укажите порог не меньше нуля, не более двух знаков после запятой.");
        if (changes.Length == 0 && !nonOfficialThreshold.HasValue) return;
        var month = MonthStart(period);
        var customerIds = changes.Select(p => p.Key).ToArray();
        await using var db = await factory.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        await LockPlanChangesAsync(db);
        if (changes.Length > 0 && await db.Customers.CountAsync(c => customerIds.Contains(c.Id) && c.OfficialCustomer) != changes.Length)
            throw new InvalidOperationException("План можно установить только существующему официальному клиенту.");
        await EnsurePlansForMonthAsync(db, month);
        var plans = changes.Length == 0 ? new Dictionary<string, OfficialMonthlyPlanEntity>() :
            await db.OfficialMonthlyPlans.Where(p => p.Month == month && customerIds.Contains(p.CustomerId)).ToDictionaryAsync(p => p.CustomerId);
        foreach (var (customerId, amount) in changes)
        {
            if (plans.TryGetValue(customerId, out var plan)) plan.Amount = amount;
            else db.OfficialMonthlyPlans.Add(new() { CustomerId = customerId, Month = month, Amount = amount });
        }
        if (nonOfficialThreshold.HasValue)
        {
            var settings = await db.OfficialMonthlyPlanSettings.FindAsync(1);
            if (settings == null) db.OfficialMonthlyPlanSettings.Add(new() { NonOfficialThreshold = nonOfficialThreshold.Value });
            else settings.NonOfficialThreshold = nonOfficialThreshold.Value;
        }
        Require(PermissionCatalog.OfficialMonthlyPlansManage);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        NotifyChanged();
    }

    public virtual async Task<OfficialMonthlyPlanRetailState> GetRetailStateAsync(string? customerId, DateTime date)
    {
        Require(PermissionCatalog.Orders);
        var month = MonthStart(date);
        await using var db = await factory.CreateDbContextAsync();
        await EnsurePlansForMonthAsync(db, month);
        var useOfficial = false;
        decimal? individual = null;
        decimal? officialOrderLimit = null;
        var showTotal = permissions.Has(PermissionCatalog.OfficialMonthlyPlansTotal);
        var threshold = await ReadThresholdAsync(db);
        var rows = showTotal ? await CalculateAsync(db, month, threshold) : null;
        if (!string.IsNullOrEmpty(customerId) && await db.Customers.AnyAsync(c => c.Id == customerId && c.OfficialCustomer))
        {
            var amount = await db.OfficialMonthlyPlans.Where(p => p.CustomerId == customerId && p.Month == month)
                .Select(p => (decimal?)p.Amount).SingleOrDefaultAsync();
            useOfficial = true;
            if (amount.HasValue)
            {
                var remaining = rows?.FirstOrDefault(r => r.CustomerId == customerId)?.Remaining ??
                    OfficialMonthlyPlanSummary.CalculateRemaining(amount.Value,
                        (await PurchasedAsync(db, month, customerId)).GetValueOrDefault(customerId), threshold);
                useOfficial = remaining > 0;
                if (useOfficial) officialOrderLimit = remaining;
                if (permissions.Has(PermissionCatalog.OfficialMonthlyPlansIndividual)) individual = remaining;
            }
        }
        Require(PermissionCatalog.Orders);
        return new(month, customerId, useOfficial,
            permissions.Has(PermissionCatalog.OfficialMonthlyPlansIndividual) ? individual : null,
            permissions.Has(PermissionCatalog.OfficialMonthlyPlansTotal) ? rows?.Sum(r => r.Remaining) : null, officialOrderLimit);
    }

    // Called only for a new order, inside its save transaction. Never rewrites older orders.
    public async Task ApplyToNewOrderAsync(DatabaseContext db, OrderEntity order)
    {
        if (!order.OfficialOrder || string.IsNullOrEmpty(order.CustomerId)) return;
        var month = MonthStart(order.Date);
        await EnsurePlansForMonthAsync(db, month);
        // Serialize simultaneous official orders for this customer/month before checking the plan.
        var resource = $"OfficialMonthlyPlan:{month:yyyyMM}:" + Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(order.CustomerId)));
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock @Resource={resource}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=15000;
            IF @result < 0 THROW 51001, N'Не удалось проверить месячный план. Повторите оформление заказа.', 1;
            """);
        if (!await db.Customers.AnyAsync(c => c.Id == order.CustomerId && c.OfficialCustomer))
        {
            order.OfficialOrder = false;
            return;
        }
        var amount = await db.OfficialMonthlyPlans.Where(p => p.CustomerId == order.CustomerId && p.Month == month)
            .Select(p => (decimal?)p.Amount).SingleOrDefaultAsync();
        if (amount.HasValue)
        {
            var remaining = Math.Max(0m, amount.Value - (await PurchasedAsync(db, month, order.CustomerId)).GetValueOrDefault(order.CustomerId));
            order.OfficialOrder = remaining > await ReadThresholdAsync(db);
            if (order.OfficialOrder && order.OrderDetails.Sum(d => decimal.Round(d.Price, 2, MidpointRounding.AwayFromZero) * d.Quentity) > remaining)
                throw new OfficialMonthlyPlanLimitException(remaining);
        }
    }

    private async Task EnsurePlansForMonthAsync(DatabaseContext db, DateTime month)
    {
        // No lock is needed once every previously planned official client has a plan for this month.
        var missing = await db.Customers.AnyAsync(c => c.OfficialCustomer &&
            db.OfficialMonthlyPlans.Any(p => p.CustomerId == c.Id && p.Month < month) &&
            !db.OfficialMonthlyPlans.Any(p => p.CustomerId == c.Id && p.Month == month));
        if (!missing) return;

        await using var tx = db.Database.CurrentTransaction == null ? await db.Database.BeginTransactionAsync() : null;
        // Multiple open forms or application instances may initialize the new month together.
        // Recheck inside the lock; insert missing plans only, without changing existing amounts.
        await LockPlanChangesAsync(db);
        var copied = await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO [OfficialMonthlyPlans] ([CustomerId], [Month], [Amount])
            SELECT c.[Id], {month}, previous.[Amount]
            FROM [Customers] AS c
            CROSS APPLY (
                SELECT TOP (1) p.[Amount]
                FROM [OfficialMonthlyPlans] AS p
                WHERE p.[CustomerId] = c.[Id] AND p.[Month] < {month}
                ORDER BY p.[Month] DESC
            ) AS previous
            WHERE c.[OfficialCustomer] = 1 AND NOT EXISTS (
                SELECT 1 FROM [OfficialMonthlyPlans] AS currentPlan WITH (UPDLOCK, HOLDLOCK)
                WHERE currentPlan.[CustomerId] = c.[Id] AND currentPlan.[Month] = {month}
            );
            """);
        if (tx != null)
        {
            await tx.CommitAsync();
            if (copied > 0) NotifyChanged();
        }
    }

    private static Task LockPlanChangesAsync(DatabaseContext db)
    {
        // A shared lock also orders inserts into adjacent months, whose missing-key ranges can overlap.
        const string resource = "OfficialMonthlyPlanCarryForward";
        return db.Database.ExecuteSqlInterpolatedAsync($"""
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock @Resource={resource}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=15000;
            IF @result < 0 THROW 51002, N'Не удалось подготовить месячные планы. Повторите загрузку.', 1;
            """);
    }

    private static async Task<IReadOnlyList<OfficialMonthlyPlanSummary>> CalculateAsync(DatabaseContext db, DateTime month,
        decimal? nonOfficialThreshold = null)
    {
        var plans = await db.OfficialMonthlyPlans.AsNoTracking().Where(p => p.Month == month && p.Customer.OfficialCustomer)
            .OrderBy(p => p.Customer.FullName).Select(p => new { p.CustomerId, p.Customer.FullName, p.Amount }).ToListAsync();
        if (plans.Count == 0) return [];
        var purchased = await PurchasedAsync(db, month);
        var threshold = nonOfficialThreshold ?? await ReadThresholdAsync(db);
        return plans.Select(p => new OfficialMonthlyPlanSummary(p.CustomerId, p.FullName, p.Amount,
            purchased.GetValueOrDefault(p.CustomerId), threshold)).ToArray();
    }

    private static async Task<Dictionary<string, decimal>> PurchasedAsync(DatabaseContext db, DateTime month, string? customerId = null)
    {
        var end = month.AddMonths(1);
        var orders = db.Orders.AsNoTracking().Where(o => o.OfficialOrder && o.Date >= month && o.Date < end && o.CustomerId != null);
        if (customerId != null) orders = orders.Where(o => o.CustomerId == customerId);
        var sales = await orders.Select(o => new { o.Id, o.CustomerId,
            Total = o.OrderDetails.Sum(d => (decimal?)d.Price * d.Quentity) ?? 0m }).ToListAsync();
        // Returns belong to the original order's month, including returns made in a later month.
        // Query current return records rather than the denormalized ReturnedQty counter.
        var refunds = await (from r in db.Returns.AsNoTracking()
                             join o in orders on r.InvoiceNumber equals o.Id
                             where !r.IsManual && r.CustomerId == o.CustomerId
                             group r by r.InvoiceNumber into g
                             select new { Id = g.Key!, Amount = g.Sum(r => r.TotalAmount) }).ToDictionaryAsync(r => r.Id, r => r.Amount);
        return sales.GroupBy(o => o.CustomerId!).ToDictionary(g => g.Key,
            g => g.Sum(o => Math.Max(0m, o.Total - refunds.GetValueOrDefault(o.Id))));
    }
}
