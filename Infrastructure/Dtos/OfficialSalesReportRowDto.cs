namespace Infrastructure.Dtos;

public class OfficialSalesReportRowDto
{
    public DateTime Date { get; set; }
    public string OrderId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string CustomerFullName { get; set; } = string.Empty;
    public string? CourierName { get; set; }
    public string? StorekeeperName { get; set; }
    public string ArticleNumber { get; set; } = string.Empty;
    public string? ProductName { get; set; }
    public string? BrandName { get; set; }
    public string? Marka { get; set; }
    public string? Model { get; set; }
    public int Quantity { get; set; }
    public int ReturnedQuantity { get; set; }
    public decimal Price { get; set; }
    public decimal Total { get; set; }
    public double Rate { get; set; }
    public decimal TotalSmn { get; set; }
}
