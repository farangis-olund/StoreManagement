namespace Infrastructure.Dtos;

public sealed record WarehousePlaceImportRow(int RowNumber, string ArticleNumber, string WarehousePlace);

public enum WarehousePlaceImportIssueKind { InvalidData, ArticleNotFound }

public sealed record WarehousePlaceImportIssue(int RowNumber, string ArticleNumber, string WarehousePlace,
    string Reason, WarehousePlaceImportIssueKind Kind = WarehousePlaceImportIssueKind.InvalidData);

public sealed record WarehousePlaceImportFile(IReadOnlyList<WarehousePlaceImportRow> Rows,
    IReadOnlyList<WarehousePlaceImportIssue> Issues, bool HasHeader)
{
    public int RowCount => Rows.Count + Issues.Count;
}

public sealed record WarehousePlaceImportResult(int UpdatedCount, int DuplicateCount,
    IReadOnlyList<WarehousePlaceImportIssue> Issues)
{
    public IReadOnlyList<Product> UpdatedProducts { get; init; } = [];
}
