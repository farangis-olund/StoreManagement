using System.Data;
using System.IO;
using PresentationWpf.Documents;
using PresentationWpf.Services;
using PresentationWpf.ViewModels;
using QuestPDF.Fluent;

internal static class PdfPrinterTests
{
    public static void Run()
    {
        var table = new DataTable();
        foreach (var column in new[] { "Артикул", "Наименование", "Группа", "Бренд", "Модель" }) table.Columns.Add(column);
        table.Columns.Add("Количество", typeof(int));
        table.Rows.Add("0092S40130", "Аккумулятор", "АКБ", "BOSCH", "C9309252", 10);
        table.Rows.Add("0241229676", "Свеча зажигания", "СВЧ", "BOSCH", "W201", 1);
        table.Rows.Add("F002DG1534", "Комплект фильтров", "ДГН", "BOSCH", "Для кондиционеров", 24);
        var report = new ReportViewModel(null!, null!);
        report.Initialize(table, new DateTime(2026, 11, 10), "Обновление товаров", "Opel");
        var path = Path.Combine(AppContext.BaseDirectory, "import-print-sample.pdf");
        new ReportDocument(report).GeneratePdf(path);
        using (var pdf = PdfPrinter.OpenDocument(path))
            Check(pdf.PageCount == 1, "The actual import report must load into the printing library.");

        var missingLibrary = new FakePrinter(new DllNotFoundException("test missing PDFium"));
        Check(!missingLibrary.TryPrint(path, out var nativeError) && nativeError!.Contains("pdfium.dll") && missingLibrary.Calls == 1,
            "A native printing failure must be returned as a useful warning without escaping to the report command.");
        var failedPrinter = new FakePrinter(new InvalidOperationException("test printer unavailable"));
        Check(!failedPrinter.TryPrint(path, out var printerError) && printerError == "test printer unavailable",
            "A printer failure must not prevent the caller from showing the generated PDF.");
        var workingPrinter = new FakePrinter(null);
        Check(workingPrinter.TryPrint(path, out var successError) && successError == null && workingPrinter.Calls == 1,
            "A successful automatic print must be submitted exactly once.");
        Check(File.Exists(path), "A failed print must leave the PDF available for preview or retry.");
        Console.WriteLine("PASS: actual import PDF loads for printing; native/printer failures become recoverable warnings; success submits once; no physical printer jobs used in tests");
    }
    private sealed class FakePrinter(Exception? error) : PdfPrinter
    {
        public int Calls;
        public override void Print(string filePath)
        {
            Calls++;
            if (error != null) throw error;
        }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
}
