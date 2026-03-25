namespace Infrastructure.Reporting;

public static class PivotEngine<T>
{
    public static PivotResult Create(
        IEnumerable<T> source,
        Func<T, string> rowSelector,
        Func<T, string> columnSelector,
        Func<T, decimal> valueSelector)
    {
        var result = new PivotResult();

        if (source == null)
            return result;

        var data = source.ToList();
        if (data.Count == 0)
            return result;

        // ==========================
        // Columns
        // ==========================
        var columns = data
            .Select(columnSelector)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct()
            .OrderBy(c => c)
            .ToList();

        result.Columns.AddRange(columns);

        // ==========================
        // Row Groups
        // ==========================
        var rowGroups = data
            .GroupBy(rowSelector)
            .OrderBy(g => g.Key);

        foreach (var group in rowGroups)
        {
            var row = new PivotRow
            {
                RowKey = group.Key ?? string.Empty
            };

            foreach (var column in columns)
            {
                var sum = group
                    .Where(x => columnSelector(x) == column)
                    .Sum(valueSelector);

                row.Cells.Add(new PivotCell
                {
                    ColumnKey = column,
                    Value = sum
                });
            }

            result.Rows.Add(row);
        }

        // ==========================
        // TOTAL ROW (NEW)
        // ==========================
        var totalRow = new PivotRow
        {
            RowKey = "ВСЕГО",
            IsTotalRow = true
        };

        foreach (var column in columns)
        {
            var columnTotal = data
                .Where(x => columnSelector(x) == column)
                .Sum(valueSelector);

            totalRow.Cells.Add(new PivotCell
            {
                ColumnKey = column,
                Value = columnTotal
            });
        }

        result.Rows.Add(totalRow);

        return result;
    }
}