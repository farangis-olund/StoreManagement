

namespace Infrastructure.Dtos;

public class PromotionGapRowDto
{
    public string CustomerId { get; set; } = "";
    public string FullName { get; set; } = "";
    public decimal AverageLast3Months { get; set; }
    public decimal CurrentMonthAmount { get; set; }
    public decimal Difference { get; set; }
}