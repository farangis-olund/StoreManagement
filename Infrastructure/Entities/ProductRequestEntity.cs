namespace Infrastructure.Entities;

public enum ProductRequestKind
{
    Shortage = 1,
    OutOfStock = 2,
    NewArticle = 3
}

/// <summary>Demand history keeps snapshots even if a customer or user is subsequently changed.</summary>
public class ProductRequestEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public int UserId { get; set; }
    public string UserName { get; set; } = "";
    public string CustomerId { get; set; } = "";
    public string CustomerName { get; set; } = "";
    public string ArticleNumber { get; set; } = "";
    public int Quantity { get; set; }
    public ProductRequestKind Kind { get; set; }
    public bool IsSubmitted { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public bool IsProcessed { get; set; }
    public string ProductName { get; set; } = "";
    public string BrandName { get; set; } = "";
    public string Marka { get; set; } = "";
    public string Model { get; set; } = "";
    public string GroupName { get; set; } = "";
    public string Alternative { get; set; } = "";
    public string Notes { get; set; } = "";
}
