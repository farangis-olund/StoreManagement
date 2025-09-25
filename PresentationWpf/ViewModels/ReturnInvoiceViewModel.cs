
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows;
using PresentationWpf.Services;
using PresentationWpf.Views;

namespace PresentationWpf.ViewModels;

public partial class ReturnInvoiceViewModel : ObservableObject
{
    public ReturnInvoiceViewModel()
    {
        Lines.CollectionChanged += (_, __) =>
        {
            TotalItems = Lines.Count;
        };
        LoadInvoice();

    }

    public FrameworkElement? ReturnInvoiceViewReference { get; set; }

    // === Lines (table) ===
    public ObservableCollection<ReturnInvoiceLine> Lines { get; set; } = new();
    [ObservableProperty]
    private int totalItems;
    // === Totals ===
    [ObservableProperty] private decimal totalAmount;
    [ObservableProperty] private string totalAmountWords = "";

    // === Debt/Balance ===
    [ObservableProperty] private decimal oldDebt;
    [ObservableProperty] private decimal returnedAmount;
    [ObservableProperty] private decimal remainingDebt;
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RefundNote))]
    private string refundMethod = "";

    public string RefundNote => RefundMethod switch
    {
        "Зачесть в баланс" => "Оплата возврата зачтена в баланс клиента",
        "Наличные" => "Оплата возврата наличными",
        "Карта" => "Оплата возврата через карту",
        _ => "Оплата возврата с остатка клиента"
    };


    // === Meta ===
    [ObservableProperty] private string id = "";
    [ObservableProperty] private DateTime date;
    [ObservableProperty] private string customerName = "";
    [ObservableProperty] private decimal total;
    [ObservableProperty] private string shopName = "";
    [ObservableProperty] private string invoiceNumber = "";

    public void LoadInvoice()
    {
        // Create a fresh view and bind to this VM
        var invoiceView = new ReturnInvoiceView
        {
            DataContext = this
        };

        ReturnInvoiceViewReference = invoiceView;
    }

    [RelayCommand]
    private void Print()
    {
        if (ReturnInvoiceViewReference == null)
        {
            MessageBox.Show("Печать чека недоступна: визуал не создан.",
                            "Печать", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        PrintHelper.Print(ReturnInvoiceViewReference, "Возвратный чек");
    }


}

public class ReturnInvoiceLine
{
    public string Article { get; set; } = "";
    public string Name { get; set; } = "";
    public string Brand { get; set; } = "";
    public string Marka { get; set; } = "";
    public string Model { get; set; } = "";
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal Total { get; set; }
}

