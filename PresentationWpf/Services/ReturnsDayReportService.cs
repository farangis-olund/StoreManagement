using Infrastructure.Dtos;
using Infrastructure.Services;
using PdfiumViewer;
using PresentationWpf.Documents;
using PresentationWpf.Views;
using QuestPDF.Fluent;
using System.IO;
using System.Windows;
using System.Drawing.Printing;

namespace PresentationWpf.Services;

public class ReturnsDayReportService
{
    private readonly ReturnService _returnService;
    private readonly OrganizationInfoService _orgService;

    public ReturnsDayReportService(
        ReturnService returnService,
        OrganizationInfoService orgService)
    {
        _returnService = returnService;
        _orgService = orgService;
    }

    private async Task<string?> GenerateReportPdfAsync(DateTime reportDate)
    {
        var returns = await _returnService.GetReturnsByDateAsync(reportDate.Date);

        if (returns == null || returns.Count == 0)
            return null;

        var rows = returns
            .SelectMany(r => r.ReturnDetails.Select(d => new ReturnDayReportRowDto
            {
                Place = d.Product?.WarehousePlace ?? "",
                ArticleNumber = d.ArticleNumber,
                ProductName = d.Product?.ProductName ?? "",
                BrandName = d.Product?.Brand?.BrandName ?? "",
                Quantity = d.Quantity
            }))
            .ToList();

        if (rows.Count == 0)
        {
            MessageBox.Show("Нет данных для отчета.", "Печать",
                MessageBoxButton.OK, MessageBoxImage.Information);

            return null;
        }

        decimal totalReturn = rows.Sum(x => x.Quantity);

        var shopName = await _orgService.GetShopDisplayAsync() ?? "";

        var document = new ReturnDayReportDocument(
            rows,
            reportDate.Date,
            shopName,
            totalReturn);

        string folder = Path.Combine(Path.GetTempPath(), "ReturnDayReports");
        Directory.CreateDirectory(folder);

        string file = Path.Combine(
            folder,
            $"ReturnDayReport_{reportDate:yyyyMMdd}_{DateTime.Now:HHmmss}.pdf");

        document.GeneratePdf(file);

        return file;
    }

    public async Task ShowReturnsDayReportAsync(DateTime reportDate)
    {
        var file = await GenerateReportPdfAsync(reportDate);

        if (file == null)
        {
            MessageBox.Show($"Нет возвратов за {reportDate:dd.MM.yyyy}.", "Печать",
                MessageBoxButton.OK, MessageBoxImage.Information);

            return;
        }

        var preview = new DocumentPreviewView(file);

        var window = new Window
        {
            Title = $"Отчет о возвратах за {reportDate:dd.MM.yyyy}",
            Content = preview,
            Width = 900,
            Height = 1000,
            WindowStartupLocation = WindowStartupLocation.CenterScreen
        };

        window.ShowDialog();
    }

    public async Task ShowReturnsDayReportAsync()
    {
        await ShowReturnsDayReportAsync(DateTime.Today);
    }

    public async Task PrintReturnsDayReportAsync(DateTime reportDate)
    {
        var file = await GenerateReportPdfAsync(reportDate);

        if (file == null)
        {
            MessageBox.Show($"Нет возвратов за {reportDate:dd.MM.yyyy}.", "Печать",
                MessageBoxButton.OK, MessageBoxImage.Information);

            return;
        }

        try
        {
            using var document = PdfDocument.Load(file);
            using var printDocument = document.CreatePrintDocument();

            printDocument.PrinterSettings = new PrinterSettings
            {
                PrinterName = new PrinterSettings().PrinterName
            };

            printDocument.PrintController = new StandardPrintController();
            printDocument.Print();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Ошибка печати: {ex.Message}",
                "Печать",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    public async Task PrintReturnsDayReportAsync()
    {
        await PrintReturnsDayReportAsync(DateTime.Today);
    }
}