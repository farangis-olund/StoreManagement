using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using Infrastructure.Dtos;

namespace PresentationWpf.Services;

public static class ProductRequestPrintDocument
{
    public static FlowDocument Create(string title, string filters, IReadOnlyList<ProductRequestSummary> rows, Size pageSize)
    {
        var width = Math.Max(100, pageSize.Width - 48);
        var document = new FlowDocument
        {
            PageWidth = pageSize.Width, PageHeight = pageSize.Height,
            PagePadding = new Thickness(24), ColumnWidth = width,
            FontFamily = new FontFamily("Segoe UI"), FontSize = 11
        };
        document.Blocks.Add(new Paragraph(new Run($"Заявки на товары — {title}"))
            { FontSize = 18, FontWeight = FontWeights.Bold });
        document.Blocks.Add(new Paragraph(new Run(filters)));
        var table = new Table { CellSpacing = 0 };
        foreach (var weight in new[] { 2d, 4d, 2d, 1.5d })
            table.Columns.Add(new TableColumn { Width = new GridLength(weight / 9.5 * width) });
        var group = new TableRowGroup();
        table.RowGroups.Add(group);
        void AddRow(params string[] values)
        {
            var row = new TableRow();
            foreach (var value in values)
                row.Cells.Add(new TableCell(new Paragraph(new Run(value)))
                { Padding = new Thickness(5), BorderBrush = Brushes.Gray, BorderThickness = new Thickness(0.5) });
            group.Rows.Add(row);
        }
        AddRow("Артикул", "Наименование", "Бренд", "Количество");
        group.Rows[0].FontWeight = FontWeights.Bold;
        foreach (var row in rows) AddRow(row.ArticleNumber, row.ProductName, row.BrandName, row.Quantity.ToString());
        document.Blocks.Add(table);
        document.Blocks.Add(new Paragraph(new Run($"Итого: {rows.Sum(x => x.Quantity)} шт.")));
        return document;
    }
}
