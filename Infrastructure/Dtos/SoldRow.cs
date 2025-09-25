

namespace Infrastructure.Dtos;

public sealed class SoldRow
{
    public string ArticleNumber { get; set; } = "";
    public string ProductName { get; set; } = "";
    public string BrandName { get; set; } = "";
    public string GroupName { get; set; } = "";
    public string Model { get; set; } = "";
    public int Quantity { get; set; }
}
