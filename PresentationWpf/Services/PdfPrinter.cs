using PdfiumViewer;
using System.Drawing.Printing;
using System.IO;

namespace PresentationWpf.Services;

public class PdfPrinter
{
    static PdfPrinter()
    {
        // PdfiumViewer 2.x relies on Assembly.Location, which is empty in a single-file application.
        PdfiumResolver.Resolve += (_, args) =>
        {
            var directories = new[] { AppContext.BaseDirectory }.Concat(
                (AppContext.GetData("NATIVE_DLL_SEARCH_DIRECTORIES") as string ?? "")
                    .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries));
            foreach (var directory in directories.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var platform = Environment.Is64BitProcess ? "x64" : "x86";
                foreach (var candidate in new[] { Path.Combine(directory, platform, "pdfium.dll"), Path.Combine(directory, "pdfium.dll") })
                {
                    if (!File.Exists(candidate)) continue;
                    args.PdfiumFileName = Path.GetFullPath(candidate);
                    return;
                }
            }
        };
    }

    public static PdfDocument OpenDocument(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            throw new FileNotFoundException("PDF-файл не найден.", filePath);
        return PdfDocument.Load(filePath);
    }

    public virtual void Print(string filePath)
    {
        using var document = OpenDocument(filePath);
        using var printDocument = document.CreatePrintDocument();
        if (!printDocument.PrinterSettings.IsValid)
            throw new InvalidPrinterException(printDocument.PrinterSettings);
        printDocument.PrintController = new StandardPrintController();
        printDocument.Print();
    }

    public bool TryPrint(string filePath, out string? error)
    {
        try
        {
            Print(filePath);
            error = null;
            return true;
        }
        catch (Exception exception)
        {
            var cause = exception.GetBaseException();
            error = cause is InvalidPrinterException
                ? "Не найден доступный принтер. Выберите принтер по умолчанию в настройках Windows."
                : cause is DllNotFoundException
                ? "Не удалось загрузить библиотеку печати PDF (pdfium.dll). Проверьте установочную сборку программы."
                : cause.Message;
            System.Diagnostics.Debug.WriteLine(exception);
            return false;
        }
    }
}
