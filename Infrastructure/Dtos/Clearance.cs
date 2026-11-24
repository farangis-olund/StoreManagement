namespace Infrastructure.Dtos;

public sealed record ClearanceProductRow(string ArticleNumber, string ProductName, string BrandName,
    int StockQuantity, long SoldQuantity);

public sealed record ClearanceBonusRow(int UserId, string SellerName, string ArticleNumber,
    long Quantity, decimal Amount, decimal Bonus);

public sealed record ClearanceBonusReport(Guid Id, DateTime FromDate, DateTime ToDate, decimal Percent,
    DateTime CreatedAt, IReadOnlyList<ClearanceBonusRow> Rows)
{
    public long TotalQuantity => Rows.Sum(x => x.Quantity);
    public decimal TotalAmount => Rows.Sum(x => x.Amount);
    public decimal TotalBonus => Rows.Sum(x => x.Bonus);
}

public sealed record ClearanceReportSummary(Guid Id, DateTime FromDate, DateTime ToDate, decimal Percent,
    decimal TotalAmount, decimal TotalBonus, DateTime CreatedAt)
{
    public string Display => $"{FromDate:dd.MM.yyyy} — {ToDate:dd.MM.yyyy} · {Percent:0.##}% · бонус {TotalBonus:N2}";
}
