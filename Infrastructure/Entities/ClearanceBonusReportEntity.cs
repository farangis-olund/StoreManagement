namespace Infrastructure.Entities;

public sealed class ClearanceBonusReportEntity
{
    public Guid Id { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public decimal Percent { get; set; }
    public DateTime CreatedAt { get; set; }
    public long TotalQuantity { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal TotalBonus { get; set; }
    public ICollection<ClearanceBonusLineEntity> Lines { get; set; } = [];
}

// Names and articles are snapshots: an old report must survive catalogue/user changes.
public sealed class ClearanceBonusLineEntity
{
    public Guid ReportId { get; set; }
    public int UserId { get; set; }
    public string ArticleNumber { get; set; } = "";
    public string SellerName { get; set; } = "";
    public long Quantity { get; set; }
    public decimal Amount { get; set; }
    public decimal Bonus { get; set; }
    public ClearanceBonusReportEntity Report { get; set; } = null!;
}
