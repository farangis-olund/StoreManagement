# Import report printing

The Import report generates a QuestPDF document, automatically prints it through
PdfiumViewer and opens it in the WebView2 preview. Printing and preview use different
components.

## Confirmed single-file failure

PdfiumViewer 2.13.0 looks for `x64/pdfium.dll` relative to its managed assembly's
`Assembly.Location`. In the installed single-file executable, that property is empty.
The native library is bundled, but is extracted to the runtime's native directory.
The original report command called the printer before creating the preview window,
without catching the printing exception, so this failure also stopped the preview.

`PdfPrinter` registers PdfiumViewer's supported resolver and searches the application
directory and `NATIVE_DLL_SEARCH_DIRECTORIES`, including the architecture subfolder.
Both import and daily-return printing use this service. `TryPrint` reports a failure
without discarding the PDF; the import preview is opened independently of printing.
Report files have distinct names, and the automatic return report uses the same
selected date as the import report.

## Verification

The `PaymentInput.Tests` executable accepts `--pdf-probe <path>` before any application
startup or database initialization. It loads a PDF through the production printing
loader, reads its page count and exits without submitting a print job.

Publish this runner with the application's settings (`win-x64`, self-contained,
`PublishSingleFile=true`, `IncludeNativeLibrariesForSelfExtract=true`) to verify the
installed-file layout. The original published runner reproduced `DllNotFoundException`
in `FPDF_AddRef`; the corrected published runner opened the same one-page PDF.
`PdfPrinterTests` also exercises the actual Import report document and simulated printer
and native-library failures without sending physical printer jobs.

The preview still requires the client's installed WebView2 Runtime. That prerequisite
is separate from the confirmed PdfiumViewer loading failure; the client's physical
printer and WebView2 installation cannot be verified by this local test.
