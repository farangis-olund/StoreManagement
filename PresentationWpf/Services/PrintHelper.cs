using System.Windows;               // <-- FrameworkElement, Size, Rect, Point
using System.Windows.Controls;
using System.Windows.Media;      // <-- PrintDialog


namespace PresentationWpf.Services;
public static class PrintHelper
{
    public static void Print(FrameworkElement? view, string title)
    {
        if (view == null)
        {
            MessageBox.Show("Печать документа недоступна: визуал не создан.", "Печать",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // We keep PrintableHeader visible and hide only the action buttons
        var actions = view.FindName("ActionsBar") as FrameworkElement;
        var grid = view.FindName("ReportGrid") as DataGrid;

        var oldActionsVis = actions?.Visibility ?? Visibility.Visible;
        var oldTransform = view.LayoutTransform;

        try
        {
            if (actions != null)
            {
                actions.Visibility = Visibility.Collapsed;
                view.UpdateLayout();
            }

            var dlg = new PrintDialog();
            try { dlg.PrintTicket.PageOrientation = System.Printing.PageOrientation.Landscape; } catch { }

            double pageW = dlg.PrintableAreaWidth;
            double pageH = dlg.PrintableAreaHeight;

            // Let view expand to its natural size
            view.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            view.Arrange(new Rect(view.DesiredSize));
            view.UpdateLayout();

            // Compute full table width for scaling
            double fullWidth = view.DesiredSize.Width;
            if (grid != null && grid.Columns.Count > 0)
            {
                grid.UpdateLayout();
                fullWidth = grid.Columns.Sum(c => c.ActualWidth) + 50; // headers/rowheader margin
            }

            double scale = fullWidth > 0 ? pageW / fullWidth : 1.0;
            if (scale > 1.0) scale = 1.0; // don’t upscale

            view.LayoutTransform = new ScaleTransform(scale, scale);
            view.Measure(new Size(pageW, pageH));
            view.Arrange(new Rect(new Point(0, 0), new Size(pageW, pageH)));
            view.UpdateLayout();

            dlg.PrintVisual(view, title);
        }
        finally
        {
            if (actions != null) actions.Visibility = oldActionsVis;
            view.LayoutTransform = oldTransform;
            view.UpdateLayout();
        }
    }
}