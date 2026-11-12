using PdfiumViewer;
using PresentationWpf.Services;

internal static class PdfPrintProbe
{
    public static int Run(string path)
    {
        try
        {
            Console.WriteLine($"Bundled={string.IsNullOrEmpty(typeof(PdfDocument).Assembly.Location)}; Architecture={System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}");
            using var document = PdfPrinter.OpenDocument(path);
            Console.WriteLine($"PASS: PDF loaded for printing; pages={document.PageCount}. No printer job was submitted.");
            return 0;
        }
        catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
    }
}
