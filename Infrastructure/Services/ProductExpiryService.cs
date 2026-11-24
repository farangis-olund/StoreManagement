using System.Data;
using Infrastructure.Constants;
using Infrastructure.Contexts;
using Infrastructure.Dtos;
using Infrastructure.Entities;
using Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Infrastructure.Services;

public class ProductExpiryService(IDbContextFactory<DatabaseContext> factory, IPermissionChecker permissions,
    ProductExpiryCatalog? catalog = null)
{
    private void EnsureAccess(bool priorityOnly = false)
    {
        if (!permissions.Has(PermissionCatalog.Inventory) && !(priorityOnly && permissions.Has(PermissionCatalog.Orders)))
            throw new UnauthorizedAccessException("Нет права доступа к срокам годности.");
    }

    public virtual async Task<IReadOnlyList<ExpiryBatchRow>> GetBatchesAsync(bool priorityOnly, DateTime? asOf = null)
    {
        EnsureAccess(priorityOnly);
        var today = (asOf ?? DateTime.Today).Date;
        var until = today.AddYears(1);
        await using var db = await factory.CreateDbContextAsync();
        var query = db.ProductExpiryBatches.AsNoTracking();
        if (priorityOnly) query = query.Where(x => x.Quantity > 0 && x.ExpiresOn <= until && x.Product.Quentity > 0);
        var rows = await query.OrderBy(x => x.ExpiresOn).ThenBy(x => x.ArticleNumber).ThenBy(x => x.WarehousePlace)
            .Select(x => new ExpiryBatchRow(x.Id, x.ArticleNumber, x.Product.ProductName, x.Product.BrandId,
                x.Product.Brand.BrandName, x.Product.Quentity, x.ManufacturedOn, x.ExpiresOn, x.WarehousePlace,
                x.Quantity, x.Version, today)).ToListAsync();
        EnsureAccess(priorityOnly);
        return rows;
    }

    public virtual async Task<IReadOnlyList<ExpiryProductOption>> GetProductsAsync()
    {
        EnsureAccess();
        await using var db = await factory.CreateDbContextAsync();
        var rows = await db.Products.AsNoTracking().OrderBy(x => x.ArticleNumber)
            .Select(x => new ExpiryProductOption(x.ArticleNumber, x.ProductName, x.Brand.BrandName, x.WarehousePlace)).ToListAsync();
        EnsureAccess();
        return rows;
    }

    public static void Validate(string article, DateTime manufactured, DateTime expires, string place, int quantity)
    {
        if (string.IsNullOrWhiteSpace(article) || article.Trim().Length > 50)
            throw new ArgumentException("Укажите артикул длиной до 50 символов.");
        if (manufactured.Date > expires.Date)
            throw new ArgumentException("Дата производства должна быть не позже окончания срока годности.");
        if (string.IsNullOrWhiteSpace(place) || place.Trim().Length > 200)
            throw new ArgumentException("Укажите место на складе длиной до 200 символов.");
        if (quantity < 0) throw new ArgumentException("В наличии должно быть целое количество не меньше 0.");
    }

    public virtual async Task SaveAsync(ExpiryBatchInput input)
    {
        EnsureAccess();
        Validate(input.ArticleNumber, input.ManufacturedOn, input.ExpiresOn, input.WarehousePlace, input.Quantity);
        var article = input.ArticleNumber.Trim(); var place = input.WarehousePlace.Trim();
        await using var db = await factory.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var product = await db.Products.SingleOrDefaultAsync(x => x.ArticleNumber == article)
            ?? throw new InvalidOperationException("Товар не найден. Сначала добавьте его в каталог.");
        var otherQuantity = await db.ProductExpiryBatches.Where(x => x.ArticleNumber == product.ArticleNumber && x.Id != input.Id)
            .SumAsync(x => (long?)x.Quantity) ?? 0;
        if (otherQuantity + input.Quantity > product.Quentity)
            throw new InvalidOperationException($"Сумма остатков партий превышает общий остаток товара ({product.Quentity}).");
        if (await db.ProductExpiryBatches.AnyAsync(x => x.Id != input.Id && x.ArticleNumber == product.ArticleNumber &&
            x.ManufacturedOn == input.ManufacturedOn.Date && x.ExpiresOn == input.ExpiresOn.Date && x.WarehousePlace == place))
            throw new InvalidOperationException("Такая партия уже есть: совпадают артикул, даты и место.");
        ProductExpiryBatchEntity batch;
        if (input.Id == Guid.Empty)
        {
            batch = new() { Id = Guid.NewGuid() };
            db.ProductExpiryBatches.Add(batch);
        }
        else
        {
            batch = await db.ProductExpiryBatches.SingleOrDefaultAsync(x => x.Id == input.Id)
                ?? throw new InvalidOperationException("Партия удалена. Обновите список.");
            CheckVersion(batch.Version, input.Version);
            if (!string.Equals(batch.ArticleNumber, product.ArticleNumber, StringComparison.OrdinalIgnoreCase) &&
                await db.OrderExpiryAllocations.AnyAsync(x => x.BatchId == batch.Id))
                throw new InvalidOperationException("Артикул проданной партии менять нельзя. Создайте новую партию.");
        }
        batch.ArticleNumber = product.ArticleNumber; batch.ManufacturedOn = input.ManufacturedOn.Date;
        batch.ExpiresOn = input.ExpiresOn.Date; batch.WarehousePlace = place; batch.Quantity = input.Quantity;
        batch.UpdatedAt = DateTime.UtcNow;
        EnsureAccess();
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException) { throw new InvalidOperationException("Партия изменена другим пользователем. Обновите список."); }
        await CommitAsync(db, tx);
    }

    public virtual async Task DeleteAsync(Guid id, byte[] version)
    {
        EnsureAccess();
        await using var db = await factory.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        var batch = await db.ProductExpiryBatches.SingleOrDefaultAsync(x => x.Id == id)
            ?? throw new InvalidOperationException("Партия уже удалена. Обновите список.");
        CheckVersion(batch.Version, version);
        if (await db.OrderExpiryAllocations.AnyAsync(x => x.BatchId == id))
            throw new InvalidOperationException("Партия использована в накладной. Сохраните её для истории; закончившейся партии укажите количество 0.");
        db.ProductExpiryBatches.Remove(batch);
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException) { throw new InvalidOperationException("Партия изменена другим пользователем. Обновите список."); }
        await CommitAsync(db, tx);
    }

    private static void CheckVersion(byte[] current, byte[]? original)
    {
        if (original == null || !current.SequenceEqual(original))
            throw new InvalidOperationException("Партия изменена другим пользователем. Обновите список перед редактированием.");
    }

    public virtual async Task<ExpiryImportResult> ImportAsync(ExpiryImportFile file)
    {
        EnsureAccess();
        var issues = file.Issues.ToList();
        await using var db = await factory.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var products = (await db.Products.Select(x => new { x.ArticleNumber, x.Quentity }).ToListAsync())
            .ToDictionary(x => x.ArticleNumber, StringComparer.OrdinalIgnoreCase);
        var batches = await db.ProductExpiryBatches.ToListAsync();
        static string Key(string article, DateTime made, DateTime expires, string place) =>
            $"{article.ToUpperInvariant()}\0{made:yyyyMMdd}\0{expires:yyyyMMdd}\0{place.ToUpperInvariant()}";
        var existing = batches.ToDictionary(x => Key(x.ArticleNumber, x.ManufacturedOn, x.ExpiresOn, x.WarehousePlace));
        var effective = new Dictionary<string, ExpiryImportRow>();
        var added = 0; var updated = 0; var duplicates = 0;
        foreach (var row in file.Rows)
        {
            try
            {
                Validate(row.ArticleNumber, row.ManufacturedOn, row.ExpiresOn, row.WarehousePlace, row.Quantity);
                if (!products.TryGetValue(row.ArticleNumber.Trim(), out var product))
                    throw new ArgumentException("Артикул отсутствует в каталоге.");
                var article = product.ArticleNumber;
                var place = row.WarehousePlace.Trim(); var key = Key(article, row.ManufacturedOn, row.ExpiresOn, place);
                if (effective.ContainsKey(key)) duplicates++;
                effective[key] = row with { ArticleNumber = article, WarehousePlace = place };
            }
            catch (ArgumentException ex) { issues.Add(new(row.RowNumber, row.ArticleNumber, ex.Message)); }
        }
        foreach (var group in effective.GroupBy(x => x.Value.ArticleNumber, StringComparer.OrdinalIgnoreCase))
        {
            var incomingKeys = group.Select(x => x.Key).ToHashSet();
            var total = batches.Where(x => string.Equals(x.ArticleNumber, group.Key, StringComparison.OrdinalIgnoreCase) &&
                !incomingKeys.Contains(Key(x.ArticleNumber, x.ManufacturedOn, x.ExpiresOn, x.WarehousePlace))).Sum(x => (long)x.Quantity)
                + group.Sum(x => (long)x.Value.Quantity);
            if (total > products[group.Key].Quentity)
            {
                foreach (var entry in group) issues.Add(new(entry.Value.RowNumber, group.Key,
                    $"Сумма партий ({total}) превышает общий остаток товара ({products[group.Key].Quentity}). Партии артикула не изменены."));
                continue;
            }
            foreach (var entry in group)
            {
                var key = entry.Key; var row = entry.Value;
                if (!existing.TryGetValue(key, out var batch))
                {
                    batch = new() { Id = Guid.NewGuid(), ArticleNumber = row.ArticleNumber, ManufacturedOn = row.ManufacturedOn.Date,
                        ExpiresOn = row.ExpiresOn.Date, WarehousePlace = row.WarehousePlace };
                    db.ProductExpiryBatches.Add(batch); existing.Add(key, batch); added++;
                }
                else updated++;
                batch.Quantity = row.Quantity; batch.UpdatedAt = DateTime.UtcNow;
            }
        }
        EnsureAccess(); await db.SaveChangesAsync(); await CommitAsync(db, tx);
        return new(added, updated, duplicates, issues);
    }

    private async Task CommitAsync(DatabaseContext db, IDbContextTransaction transaction)
    {
        var annotations = catalog == null ? null : await ProductExpiryCatalog.ReadAsync(db);
        EnsureAccess();
        await transaction.CommitAsync();
        if (annotations != null) catalog!.Publish(annotations);
    }
}
