using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using PresentationWpf.ViewModels;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--pdf-probe") return PdfPrintProbe.Run(args[1]);
        if (args.Contains("--product-requests")) return ProductRequestUiTests.Run();
        try
        {
            foreach (var cultureName in new[] { "en-US", "ru-RU", "sv-SE" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
                var vm = new SummaryViewModel(null!, null!, null!, null!, null!, null!);
                var input = new TextBox { DataContext = vm };
                input.SetBinding(TextBox.TextProperty, new Binding(nameof(vm.PaymentAmountText))
                {
                    Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                });
                void Enter(string text)
                {
                    input.Text = text;
                    input.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
                    input.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                    Check(input.Text == text && vm.PaymentAmountText == text, "Typing must preserve the exact text.");
                }
                foreach (var separator in new[] { ",", "." })
                {
                    Enter("2");
                    Enter("20");
                    Enter("20" + separator);
                    Enter("20" + separator + "5");
                    Check(vm.TryGetPaymentAmount(out var amount) && amount == 20.5m, "Wrong decimal amount.");
                    Enter("20" + separator + "50");
                    Check(vm.TryGetPaymentAmount(out amount) && amount == 20.5m, "Trailing zero changed the value.");
                    Enter(separator + "5");
                    Check(vm.TryGetPaymentAmount(out amount) && amount == .5m, "Leading separator not accepted.");
                }
                Enter(" 1234,56 ");
                Check(vm.TryGetPaymentAmount(out var pasted) && pasted == 1234.56m, "Pasted amount parsed incorrectly.");
                vm.PaymentAmount = 75.25m;
                input.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                Check(input.Text == "75.25", "Suggested payment did not update the input.");
                foreach (var invalid in new[] { "", "abc", ",", ".", "20,5.1", "20..5", "1,234,56", "1 234.56", "1e3", "999999999999999999999999999999999999" })
                {
                    Enter(invalid);
                    Check(!vm.TryGetPaymentAmount(out _), "Invalid input must not reuse the last valid amount: " + invalid);
                }
                Enter("0");
                Check(vm.TryGetPaymentAmount(out var zero) && zero == 0, "Zero parsing failed.");
                Enter("-20,5");
                Check(vm.TryGetPaymentAmount(out var negative) && negative == -20.5m, "Negative value must remain negative for validation.");
                Console.WriteLine($"PASS: payment typing, both separators, pasted amounts, invalid input, and suggestions ({cultureName})");
            }
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
