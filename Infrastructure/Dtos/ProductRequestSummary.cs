using Infrastructure.Entities;

namespace Infrastructure.Dtos;

public sealed class ProductRequestSummary
{
    public string ArticleNumber { get; init; } = "";
    public string ProductName { get; init; } = "";
    public string BrandName { get; init; } = "";
    public long Quantity { get; init; }
    public int RequestCount => Details.Count;
    public bool IsProcessed => Details.Count > 0 && Details.All(x => x.IsProcessed);
    public IReadOnlyList<ProductRequestEntity> Details { get; init; } = [];

    public static IReadOnlyList<ProductRequestSummary> Group(IEnumerable<ProductRequestEntity> requests) =>
        requests.GroupBy(x => x.ArticleNumber, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var details = g.OrderByDescending(x => x.CreatedAt).ToList();
                var latest = details[0];
                return new ProductRequestSummary
                {
                    ArticleNumber = latest.ArticleNumber,
                    ProductName = latest.ProductName,
                    BrandName = string.Join(", ", details.Select(x => x.BrandName)
                        .Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase)),
                    Quantity = details.Sum(x => (long)x.Quantity),
                    Details = details
                };
            }).OrderBy(x => x.ArticleNumber, StringComparer.OrdinalIgnoreCase).ToList();
}
