using QuestPDF.Infrastructure;
using QuestPDF.Fluent;
using System.Diagnostics;
using System.IO;
using PresentationWpf.ViewModels;
using PresentationWpf.Documents;
using System.Windows;


namespace PresentationWpf.Services
{
    public enum ReportType
    {
        Invoice,
        BarterInvoice,
        Payment,
        ReturnInvoice,
        Report,
        SalesSummary
    }

    public class PdfService
    {
        private static string GetReportFolder()
        {
            string folder = Path.Combine(Path.GetTempPath(), "Reports");
            Directory.CreateDirectory(folder);
            return folder;
        }

        public string GeneratePdf(ReportType type, object model)
        {
            string folder = GetReportFolder();
            string filePath = Path.Combine(folder, $"{type}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");

            try
            {
                IDocument document = type switch
                {
                    ReportType.Invoice => new InvoiceDocument((OrderInvoiceViewModel)model),
                    ReportType.ReturnInvoice => new ReturnInvoiceDocument((ReturnInvoiceViewModel)model),
                    ReportType.Payment => new PaymentDocument((PaymentReceiptViewModel)model),
                    ReportType.Report => new ReportDocument((ReportViewModel)model),
                    ReportType.BarterInvoice => new BarterInvoiceDocument((OrderInvoiceViewModel)model),
                    _ => throw new NotImplementedException($"Report type '{type}' not implemented")
                };

                document.GeneratePdf(filePath);

                return filePath;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "PDF error");
                throw;
            }
        }


        /// <summary>
        /// Generates a PDF as a byte array (useful for in-app previews with WebView2).
        /// </summary>
        public byte[] GeneratePdfBytes(ReportType type, object model)
        {
            IDocument document = type switch
            {
                ReportType.Invoice => new InvoiceDocument((OrderInvoiceViewModel)model),
                ReportType.ReturnInvoice => new ReturnInvoiceDocument((ReturnInvoiceViewModel)model),
                ReportType.Payment => new PaymentDocument((PaymentReceiptViewModel)model),
                ReportType.Report => new ReportDocument((ReportViewModel)model),
                ReportType.BarterInvoice => new BarterInvoiceDocument((OrderInvoiceViewModel)model),
                // ReportType.SalesSummary => new SalesSummaryDocument((SalesSummaryViewModel)model),
                _ => throw new NotImplementedException($"Report type '{type}' not implemented")
            };

            using var stream = new MemoryStream();
            document.GeneratePdf(stream);
            return stream.ToArray();
        }



        /// <summary>
        /// Opens the given file in the system default PDF viewer.
        /// </summary>
        public void Open(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                throw new FileNotFoundException("PDF file not found", filePath);

            Process.Start(new ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true
            });
        }

        /// <summary>
        /// Sends the PDF file to the default printer.
        /// </summary>
        public void Print(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                throw new FileNotFoundException("PDF file not found", filePath);

            Process.Start(new ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true,
                Verb = "print"
            });
        }
    }
}
