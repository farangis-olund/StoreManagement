
namespace Infrastructure.Reporting;

public sealed class PivotRow
{
    public string RowKey { get; init; } = string.Empty;

    public List<PivotCell> Cells { get; } = [];
    public bool IsTotalRow { get; init; }
    public bool IsPercentRow { get; set; }

    public decimal Total => Cells.Sum(c => c.Value);
}