using LiveCharts;
using LiveCharts.Wpf;
using Infrastructure.Reporting;
using System.Windows.Media;

namespace PresentationWpf.Reporting;

public static class ChartBuilder
{
    //public static void BuildColumnChart(
    //    PivotResult pivot,
    //    SeriesCollection seriesCollection,
    //    out string[] labels)
    //{
    //    seriesCollection.Clear();

    //    labels = pivot.Columns.ToArray();

    //    var palette = new[]
    //     {
    //        Color.FromRgb(79, 129, 189),   // Muted Blue
    //        Color.FromRgb(192, 80, 77),    // Soft Brick Red
    //        Color.FromRgb(155, 187, 89),   // Olive Green
    //        Color.FromRgb(54, 161, 145),   // Teal
    //        Color.FromRgb(128, 100, 162),  // Soft Purple
    //        Color.FromRgb(75, 172, 198),   // Slate Cyan
    //        Color.FromRgb(247, 150, 70),   // Burnt Orange
    //        Color.FromRgb(118, 96, 146),   // Deep Plum
    //        Color.FromRgb(119, 147, 60),   // Moss Green
    //        Color.FromRgb(58, 83, 155),    // Steel Blue
    //        Color.FromRgb(221, 138, 88),   // Warm Sand
    //        Color.FromRgb(91, 155, 213),   // Soft Corporate Blue
    //        Color.FromRgb(166, 166, 166),  // Neutral Grey
    //        Color.FromRgb(112, 173, 71),   // Balanced Green
    //        Color.FromRgb(146, 208, 80)    // Light Olive
    //    };

    //    int colorIndex = 0;

    //    foreach (var row in pivot.Rows.Where(r => !r.IsTotalRow))
    //    {
    //        var values = new ChartValues<decimal>(
    //            row.Cells.Select(c => c.Value)
    //        );

    //        var baseColor = palette[colorIndex % palette.Length];

    //        seriesCollection.Add(new ColumnSeries
    //        {
    //            Title = row.RowKey,
    //            Values = values,
    //            Fill = new SolidColorBrush(baseColor),
    //            Stroke = new SolidColorBrush(Color.Multiply(baseColor, 0.8f)),
    //            StrokeThickness = 1
    //        });

    //        colorIndex++;
    //    }
    //}


    public static void BuildColumnChart(
    PivotResult pivot,
    SeriesCollection seriesCollection,
    out string[] labels)
    {
        seriesCollection.Clear();

        labels = pivot.Columns.ToArray();

        var totalRow = pivot.Rows.FirstOrDefault(r => r.IsTotalRow);
        decimal grandTotal = totalRow?.Total ?? 0;

        var palette = new[]
        {
        Color.FromRgb(79, 129, 189),
        Color.FromRgb(192, 80, 77),
        Color.FromRgb(155, 187, 89),
        Color.FromRgb(54, 161, 145),
        Color.FromRgb(128, 100, 162),
        Color.FromRgb(75, 172, 198),
        Color.FromRgb(247, 150, 70),
        Color.FromRgb(118, 96, 146),
        Color.FromRgb(119, 147, 60),
        Color.FromRgb(58, 83, 155),
        Color.FromRgb(221, 138, 88),
        Color.FromRgb(91, 155, 213),
        Color.FromRgb(166, 166, 166),
        Color.FromRgb(112, 173, 71),
        Color.FromRgb(146, 208, 80)
    };

        int colorIndex = 0;

        foreach (var row in pivot.Rows.Where(r => !r.IsTotalRow && !r.IsPercentRow))
        {
            var values = new ChartValues<decimal>(
                row.Cells.Select(c => c.Value)
            );

            var baseColor = palette[colorIndex % palette.Length];

            seriesCollection.Add(new ColumnSeries
            {
                Title = row.RowKey,
                Values = values,
                Fill = new SolidColorBrush(baseColor),
                Stroke = new SolidColorBrush(Color.Multiply(baseColor, 0.8f)),
                StrokeThickness = 1,
                                
                LabelPoint = point =>
                {
                    if (grandTotal == 0)
                        return $"{point.Y:N2} (0.00%)";

                    double percent = point.Y / (double)grandTotal * 100;

                    return $"{point.Y:N2} ({percent:N2}%)";
                }
            });

            colorIndex++;
        }
    }

    // NEW METHOD FOR STATISTICS LINE CHARTS
    public static void BuildLineChart(
        PivotResult pivot,
        SeriesCollection seriesCollection,
        out string[] labels)
    {
        seriesCollection.Clear();

        // X axis = pivot rows (months)
        labels = pivot.Rows
            .Where(r => !r.IsTotalRow)
            .Select(r => r.RowKey)
            .ToArray();

        var palette = new[]
        {
            Color.FromRgb(79, 129, 189),
            Color.FromRgb(192, 80, 77),
            Color.FromRgb(155, 187, 89),
            Color.FromRgb(54, 161, 145),
            Color.FromRgb(128, 100, 162),
            Color.FromRgb(75, 172, 198),
            Color.FromRgb(247, 150, 70)
        };

        for (int col = 0; col < pivot.Columns.Count; col++)
        {
            var baseColor = palette[col % palette.Length];

            var values = new ChartValues<decimal>(
                pivot.Rows
                    .Where(r => !r.IsTotalRow)
                    .Select(r => r.Cells[col].Value)
            );

            seriesCollection.Add(new LineSeries
            {
                Title = pivot.Columns[col],
                Values = values,
                Stroke = new SolidColorBrush(baseColor),
                Fill = Brushes.Transparent,
                PointGeometrySize = 7,
                StrokeThickness = 2
            });
        }
    }
}