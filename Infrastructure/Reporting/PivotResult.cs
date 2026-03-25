
namespace Infrastructure.Reporting;

public sealed class PivotResult
{
    public List<string> Columns { get; } = new();

    public List<PivotRow> Rows { get; } = new();

    public decimal GrandTotal => Rows.Sum(r => r.Total);
}