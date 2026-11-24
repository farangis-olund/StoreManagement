namespace Infrastructure.Entities;

public sealed class ClearanceProductEntity
{
    public string ArticleNumber { get; set; } = "";
    public ProductEntity Product { get; set; } = null!;
}
