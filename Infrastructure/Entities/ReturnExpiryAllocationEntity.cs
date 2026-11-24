namespace Infrastructure.Entities;

public sealed class ReturnExpiryAllocationEntity
{
    public Guid Id { get; set; }
    public int ReturnDetailId { get; set; }
    public Guid SaleAllocationId { get; set; }
    public int Quantity { get; set; }
    public ReturnDetailEntity ReturnDetail { get; set; } = null!;
    public OrderExpiryAllocationEntity SaleAllocation { get; set; } = null!;
}
