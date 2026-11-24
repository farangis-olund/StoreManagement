namespace Infrastructure.Entities;

public sealed class OrderExpiryAllocationEntity
{
    public Guid Id { get; set; }
    public string OrderId { get; set; } = "";
    public string ArticleNumber { get; set; } = "";
    public Guid BatchId { get; set; }
    public int Quantity { get; set; }
    public int ReturnedQuantity { get; set; }
    public ProductExpiryBatchEntity Batch { get; set; } = null!;
    public OrderDetailEntity OrderDetail { get; set; } = null!;
}
