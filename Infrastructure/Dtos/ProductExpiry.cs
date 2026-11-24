namespace Infrastructure.Dtos;

public sealed record ExpiryBatchRow(Guid Id, string ArticleNumber, string ProductName, int BrandId, string BrandName,
    int StockQuantity, DateTime ManufacturedOn, DateTime ExpiresOn, string WarehousePlace, int Quantity, byte[] Version, DateTime AsOf)
{
    public bool IsActive => Quantity > 0;
    public int DaysRemaining => (ExpiresOn.Date - AsOf.Date).Days;
    public bool IsExpired => IsActive && ExpiresOn.Date < AsOf.Date;
    public bool IsNearExpiry => IsActive && !IsExpired && ExpiresOn.Date <= AsOf.Date.AddYears(1);
    public bool IsExpiringWithinMonth => IsActive && !IsExpired && ExpiresOn.Date < AsOf.Date.AddMonths(1);
    public bool IsCriticalExpiry => IsExpired || IsExpiringWithinMonth;
    public string Status => !IsActive ? "Партия закончилась" : IsExpired ? "Срок истёк" : IsExpiringWithinMonth
        ? "Меньше месяца" : IsNearExpiry ? "Осталось не больше года" : "Срок в норме";
}

public sealed record ExpiryProductOption(string ArticleNumber, string ProductName, string BrandName, string WarehousePlace)
{
    public string Display => $"{ArticleNumber} · {BrandName} · {ProductName}";
}
public sealed record ExpiryBrandOption(int? Id, string Name);
public sealed record ExpiryBatchInput(Guid Id, string ArticleNumber, DateTime ManufacturedOn, DateTime ExpiresOn,
    string WarehousePlace, int Quantity, byte[]? Version = null);
public sealed record ExpiryImportRow(int RowNumber, string ArticleNumber, DateTime ManufacturedOn, DateTime ExpiresOn,
    string WarehousePlace, int Quantity);
public sealed record ExpiryImportIssue(int RowNumber, string ArticleNumber, string Message);
public sealed record ExpiryImportFile(IReadOnlyList<ExpiryImportRow> Rows, IReadOnlyList<ExpiryImportIssue> Issues);
public sealed record ExpiryImportResult(int AddedCount, int UpdatedCount, int DuplicateCount, IReadOnlyList<ExpiryImportIssue> Issues);
public sealed record ProductExpiryAnnotation(string ArticleNumber, DateTime ExpiresOn, string Hint);
