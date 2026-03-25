
namespace Infrastructure.Reporting;

public sealed class PivotCell
{
    public string ColumnKey { get; init; } = string.Empty;

    public decimal Value { get; init; }
}