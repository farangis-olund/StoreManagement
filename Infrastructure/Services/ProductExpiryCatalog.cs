using Infrastructure.Contexts;
using Infrastructure.Dtos;
using Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public sealed class ProductExpiryCatalog
{
    public event EventHandler<EventArgs>? Changed;
    private IReadOnlyDictionary<string, ProductExpiryAnnotation> _snapshot =
        new Dictionary<string, ProductExpiryAnnotation>(StringComparer.OrdinalIgnoreCase);
    public ProductExpiryAnnotation? Get(string article) => _snapshot.GetValueOrDefault(article);

    public void Publish(IReadOnlyList<ProductExpiryAnnotation> rows)
    {
        _snapshot = rows.ToDictionary(x => x.ArticleNumber, StringComparer.OrdinalIgnoreCase);
        if (Changed == null) return;
        foreach (EventHandler<EventArgs> handler in Changed.GetInvocationList())
            try { handler(this, EventArgs.Empty); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
    }

    public static async Task<IReadOnlyList<ProductExpiryAnnotation>> ReadAsync(DatabaseContext db, CancellationToken ct = default)
    {
        var batches = await db.ProductExpiryBatches.AsNoTracking().Where(x => x.Quantity > 0)
            .OrderBy(x => x.ExpiresOn).ThenBy(x => x.WarehousePlace)
            .Select(x => new { x.ArticleNumber, x.ManufacturedOn, x.ExpiresOn, x.WarehousePlace, x.Quantity }).ToListAsync(ct);
        return batches.GroupBy(x => x.ArticleNumber, StringComparer.OrdinalIgnoreCase).Select(g =>
        {
            var first = g.First();
            return new ProductExpiryAnnotation(g.Key, first.ExpiresOn,
                string.Join("\n", g.Select(x => $"Производство: {x.ManufacturedOn:dd.MM.yyyy}; годен до {x.ExpiresOn:dd.MM.yyyy}; место: {x.WarehousePlace}; в наличии: {x.Quantity}")));
        }).ToArray();
    }

    public static async Task AnnotateAsync(DatabaseContext db, IEnumerable<ProductEntity> products, CancellationToken ct = default)
    {
        var annotations = (await ReadAsync(db, ct)).ToDictionary(x => x.ArticleNumber, StringComparer.OrdinalIgnoreCase);
        foreach (var product in products)
        {
            var annotation = annotations.GetValueOrDefault(product.ArticleNumber);
            product.NearestExpiryDate = annotation?.ExpiresOn;
            product.ExpiryHint = annotation?.Hint ?? "";
        }
    }
}
