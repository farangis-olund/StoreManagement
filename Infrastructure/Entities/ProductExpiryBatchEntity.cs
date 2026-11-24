namespace Infrastructure.Entities;

public sealed class ProductExpiryBatchEntity
{
    public Guid Id { get; set; }
    public string ArticleNumber { get; set; } = "";
    public DateTime ManufacturedOn { get; set; }
    public DateTime ExpiresOn { get; set; }
    public string WarehousePlace { get; set; } = "";
    public int Quantity { get; set; }
    public DateTime UpdatedAt { get; set; }
    public byte[] Version { get; set; } = [];
    public ProductEntity Product { get; set; } = null!;
}
