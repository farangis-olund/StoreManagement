using System.Data;
using Infrastructure.Constants;
using Infrastructure.Contexts;
using Infrastructure.Dtos;
using Infrastructure.Entities;
using Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public sealed class ProductRequestService(IDbContextFactory<DatabaseContext> factory, IPermissionChecker permissions)
{
    public async Task<ProductRequestEntity> CreateAsync(Guid id, string article, int quantity, int userId,
        string customerId, bool automaticShortage = false)
    {
        article = ValidateText(article, 50, "Артикул", required: true);
        if (quantity <= 0) throw new ArgumentException("Укажите целое количество больше нуля.");
        if (id == Guid.Empty) throw new ArgumentException("Не указан идентификатор заявки.");
        await using var db = await factory.CreateDbContextAsync();
        // Protect an absent article from being inserted between the lookup and saving its draft.
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId)
            ?? throw new InvalidOperationException("Пользователь не найден. Войдите в систему повторно.");
        var customer = await db.Customers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == customerId)
            ?? throw new InvalidOperationException("Выберите клиента перед созданием заявки.");
        var existing = await db.ProductRequests.SingleOrDefaultAsync(x => x.Id == id);
        if (existing != null)
        {
            if (existing.UserId != userId || existing.CustomerId != customerId ||
                !string.Equals(existing.ArticleNumber, article, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Идентификатор принадлежит другой заявке.");
            // One automatic request per cart line. Recommitting or retrying must not add demand twice.
            if (automaticShortage && existing.Kind == ProductRequestKind.Shortage)
            {
                existing.Quantity = quantity;
                await db.SaveChangesAsync();
            }
            await transaction.CommitAsync();
            return existing;
        }

        var product = await db.Products.AsNoTracking().Include(x => x.Brand).Include(x => x.Group)
            .SingleOrDefaultAsync(x => x.ArticleNumber == article);
        if (automaticShortage && product == null)
            throw new InvalidOperationException("Товар больше не существует. Создайте заявку на новый артикул.");
        var kind = product == null ? ProductRequestKind.NewArticle
            : !automaticShortage && product.Quentity <= 0 ? ProductRequestKind.OutOfStock
            : ProductRequestKind.Shortage;
        var now = DateTime.Now;
        var request = new ProductRequestEntity
        {
            Id = id, CreatedAt = now, UserId = user.Id,
            UserName = string.IsNullOrWhiteSpace(user.FullName) ? user.UserName : user.FullName,
            CustomerId = customer.Id, CustomerName = customer.FullName,
            ArticleNumber = product?.ArticleNumber ?? article, Quantity = quantity, Kind = kind,
            IsSubmitted = kind != ProductRequestKind.NewArticle,
            SubmittedAt = kind != ProductRequestKind.NewArticle ? now : null,
            ProductName = product?.ProductName ?? "", BrandName = product?.Brand.BrandName ?? "",
            Marka = product?.Marka ?? "", Model = product?.Model ?? "",
            GroupName = product?.Group.GroupName ?? "", Alternative = product?.Alternative ?? ""
        };
        db.ProductRequests.Add(request);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return request;
    }

    public async Task<IReadOnlyList<ProductRequestEntity>> GetDraftsAsync(int userId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.ProductRequests.AsNoTracking()
            .Where(x => x.UserId == userId && x.Kind == ProductRequestKind.NewArticle && !x.IsSubmitted)
            .OrderByDescending(x => x.CreatedAt).ToListAsync();
    }

    public async Task SaveDraftAsync(ProductRequestEntity draft, int userId, bool submit)
    {
        await using var db = await factory.CreateDbContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var saved = await db.ProductRequests.SingleOrDefaultAsync(x => x.Id == draft.Id && x.UserId == userId
            && x.Kind == ProductRequestKind.NewArticle && !x.IsSubmitted)
            ?? throw new InvalidOperationException("Черновик уже отправлен или удалён. Обновите список заявок.");
        if (await db.Products.AnyAsync(x => x.ArticleNumber == saved.ArticleNumber))
            throw new InvalidOperationException("Артикул уже появился в базе товаров. Обновите список заявок.");

        saved.ProductName = ValidateText(draft.ProductName, 100, "Наименование", submit);
        saved.BrandName = ValidateText(draft.BrandName, 100, "Бренд", submit);
        saved.Marka = ValidateText(draft.Marka, 300, "Марка", submit);
        saved.Model = ValidateText(draft.Model, 500, "Модель", submit);
        saved.GroupName = ValidateText(draft.GroupName, 100, "Группа", submit);
        saved.Alternative = ValidateText(draft.Alternative, 300, "Альтернатива");
        saved.Notes = ValidateText(draft.Notes, 2000, "Примечание");
        saved.IsSubmitted = submit;
        saved.SubmittedAt = submit ? DateTime.Now : null;
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    public async Task<IReadOnlyList<ProductRequestSummary>> GetJournalAsync(ProductRequestKind kind,
        DateTime? from, DateTime? through, string? brand, bool includeProcessed)
    {
        RequireJournalAccess();
        if (from?.Date > through?.Date) throw new ArgumentException("Начало периода позже окончания.");
        await using var db = await factory.CreateDbContextAsync();
        var query = db.ProductRequests.AsNoTracking().Where(x => x.IsSubmitted && x.Kind == kind);
        if (from.HasValue) query = query.Where(x => x.CreatedAt >= from.Value.Date);
        if (through.HasValue)
        {
            var end = through.Value.Date.AddDays(1);
            query = query.Where(x => x.CreatedAt < end);
        }
        if (!string.IsNullOrWhiteSpace(brand)) query = query.Where(x => x.BrandName == brand);
        if (!includeProcessed && kind == ProductRequestKind.NewArticle) query = query.Where(x => !x.IsProcessed);
        return ProductRequestSummary.Group(await query.ToListAsync());
    }

    public async Task<IReadOnlyList<string>> GetBrandsAsync()
    {
        RequireJournalAccess();
        await using var db = await factory.CreateDbContextAsync();
        return await db.ProductRequests.AsNoTracking().Where(x => x.IsSubmitted && x.BrandName != "")
            .Select(x => x.BrandName).Distinct().OrderBy(x => x).ToListAsync();
    }

    public async Task DeleteAsync(IEnumerable<Guid> ids)
    {
        RequireJournalAccess();
        var selected = ids.Distinct().ToArray();
        await using var db = await factory.CreateDbContextAsync();
        await db.ProductRequests.Where(x => selected.Contains(x.Id) && x.IsSubmitted &&
            x.Kind != ProductRequestKind.NewArticle).ExecuteDeleteAsync();
    }

    public async Task SetProcessedAsync(IEnumerable<Guid> ids, bool processed)
    {
        RequireJournalAccess();
        var selected = ids.Distinct().ToArray();
        await using var db = await factory.CreateDbContextAsync();
        await db.ProductRequests.Where(x => selected.Contains(x.Id) && x.IsSubmitted &&
            x.Kind == ProductRequestKind.NewArticle)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsProcessed, processed));
    }

    private void RequireJournalAccess()
    {
        if (!permissions.Has(PermissionCatalog.ProductRequests))
            throw new UnauthorizedAccessException("Нет права доступа к разделу «Заявки на товары».");
    }

    private static string ValidateText(string? text, int limit, string name, bool required = false)
    {
        var value = text?.Trim() ?? "";
        if (required && value.Length == 0) throw new ArgumentException($"Заполните поле «{name}».");
        if (value.Length > limit) throw new ArgumentException($"Поле «{name}»: максимум {limit} символов.");
        return value;
    }
}
