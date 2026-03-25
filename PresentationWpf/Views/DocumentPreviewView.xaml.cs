
using Microsoft.Web.WebView2.Core;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace PresentationWpf.Views
{
    /// <summary>
    /// Shows a PDF document (e.g., invoice) inside a WebView2 viewer.
    /// </summary>
    public partial class DocumentPreviewView : UserControl
    {
        private string? _pdfPath;
        private byte[]? _pdfBytes;
        private bool _isInitialized;

        // Designer / default constructor
        public DocumentPreviewView()
        {
            InitializeComponent();
            Loaded += DocumentPreviewView_Loaded;
        }

        // Overload used at runtime to show a generated PDF by path
        public DocumentPreviewView(string pdfPath) : this()
        {
            _pdfPath = pdfPath;
        }

        // For the "bytes" case: preview.LoadPdf(bytes)
        public void LoadPdf(byte[] pdfBytes)
        {
            _pdfBytes = pdfBytes;
            if (_isInitialized)
                _ = ShowPdfAsync();
        }

        private async void DocumentPreviewView_Loaded(object sender, RoutedEventArgs e)
        {
            await InitializeWebViewAsync();
            _isInitialized = true;
            await ShowPdfAsync();
        }

        private async Task InitializeWebViewAsync()
        {
            try
            {
                // Use a writable folder for WebView2 data, not Program Files
                var basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var userDataFolder = Path.Combine(basePath, "StoreManagementSoftware", "WebView2");

                Directory.CreateDirectory(userDataFolder);

                var env = await CoreWebView2Environment.CreateAsync(userDataFolder: userDataFolder);
                await PdfViewer.EnsureCoreWebView2Async(env);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "WebView2 init error");
            }
        }

        private async Task ShowPdfAsync()
        {
            try
            {
                if (PdfViewer.CoreWebView2 == null)
                    return;

                string pathToUse;

                if (_pdfBytes != null && _pdfBytes.Length > 0)
                {
                    // Save bytes to a temp file
                    var folder = Path.Combine(Path.GetTempPath(), "Reports");
                    Directory.CreateDirectory(folder);

                    pathToUse = Path.Combine(folder, $"Preview_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
                    await File.WriteAllBytesAsync(pathToUse, _pdfBytes);
                }
                else if (!string.IsNullOrWhiteSpace(_pdfPath))
                {
                    pathToUse = _pdfPath;
                }
                else
                {
                    return; // nothing to show
                }

                if (!File.Exists(pathToUse))
                {
                    MessageBox.Show($"PDF file not found:\n{pathToUse}", "Preview error");
                    return;
                }

                var uri = new Uri(pathToUse);
                PdfViewer.CoreWebView2.Navigate(uri.AbsoluteUri);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Preview error");
            }
        }
    }
}

