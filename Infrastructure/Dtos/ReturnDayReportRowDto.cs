

namespace Infrastructure.Dtos;

public class ReturnDayReportRowDto
{
    public string Place { get; set; } = "";
    public string ArticleNumber { get; set; } = "";
    public string ProductName { get; set; } = "";
    public string BrandName { get; set; } = "";
    public decimal Quantity { get; set; }
}