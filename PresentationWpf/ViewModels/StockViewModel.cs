

//using CommunityToolkit.Mvvm.ComponentModel;
//using CommunityToolkit.Mvvm.Input;
//using Infrastructure.Services;

//namespace PresentationWpf.ViewModels;
//public partial class StockViewModel : ObservableObject
//{
//    private readonly ProductViewModel _productViewModel;
//    private readonly ImportViewModel _importViewModel;
//    private readonly MovementViewModel _movementViewModel;
//    private readonly TransferViewModel _transferViewModel;
//    private readonly StockLocationViewModel _stockLocationViewModel;
//    private readonly ReturnDebtRepaymentViewModel _returnDebtRepaymentViewModel;
//    private readonly TransferReceiveViewModel _transferReceiveViewModel;


//    [ObservableProperty]
//    private ObservableObject currentViewModel = null!;

//    [ObservableProperty] private bool isCatalogSelected;
//    [ObservableProperty] private bool isImportSelected;
//    [ObservableProperty] private bool isMovementSelected;
//    [ObservableProperty] private bool isTransferSelected;
//    [ObservableProperty] private bool isStockLocationSelected;

//    public StockViewModel(
//        ImportViewModel importViewModel,
//        MovementViewModel movementViewModel,
//        ProductViewModel productViewModel,
//        TransferViewModel transferViewModel,
//        StockLocationViewModel stockLocationViewModel,
//        ReturnDebtRepaymentViewModel returnDebtRepaymentViewModel,
//        TransferReceiveViewModel transferReceiveViewModel)
//    {
//        _productViewModel = productViewModel;
//        _importViewModel = importViewModel;
//        _movementViewModel = movementViewModel;
//        _transferViewModel = transferViewModel;
//        _stockLocationViewModel = stockLocationViewModel;
//        _returnDebtRepaymentViewModel = returnDebtRepaymentViewModel;
//        _transferReceiveViewModel = transferReceiveViewModel;

//        // 🔹 listen for ProductViewModel close request
//        _productViewModel.RequestClose += () =>
//        {
//            CurrentViewModel = null; // hide content
//            IsCatalogSelected = false;
//        };

//        _importViewModel.RequestClose += () =>
//        {
//            CurrentViewModel = null;
//            IsImportSelected = false;
//        };

//        _stockLocationViewModel.RequestClose += () =>
//        {
//            CurrentViewModel = null;
//            IsStockLocationSelected = false;
//        };

//        _movementViewModel.RequestClose += () =>
//        {
//            CurrentViewModel = null;
//            IsMovementSelected = false;
//        };
//    }

//    [RelayCommand]
//    private void OpenCatalog()
//    {
//        CurrentViewModel = _productViewModel;
//        IsCatalogSelected = true;
//        IsImportSelected = false;
//        IsMovementSelected = false;
//        IsTransferSelected = false;
//        IsStockLocationSelected = false;
//    }

//    [RelayCommand]
//    private void OpenImport()
//    {
//        CurrentViewModel = _importViewModel;
//        IsCatalogSelected = false;
//        IsImportSelected = true;
//        IsMovementSelected = false;
//        IsTransferSelected = false;
//        IsStockLocationSelected = false;
//    }

//    [RelayCommand]
//    private void OpenMovement()
//    {
//        CurrentViewModel = _movementViewModel;
//        IsCatalogSelected = false;
//        IsImportSelected = false;
//        IsMovementSelected = true;
//        IsTransferSelected = false;
//        IsStockLocationSelected = false;
//    }

//    [RelayCommand]
//    private void OpenTransfer()
//    {
//        CurrentViewModel = _transferViewModel;
//        IsCatalogSelected = false;
//        IsImportSelected = false;
//        IsMovementSelected = false;
//        IsTransferSelected = true;
//        IsStockLocationSelected = false;
//    }

//    [RelayCommand]
//    private void OpenStockLocation()
//    {
//        CurrentViewModel = _stockLocationViewModel;
//        IsCatalogSelected = false;
//        IsImportSelected = false;
//        IsMovementSelected = false;
//        IsTransferSelected = false;
//        IsStockLocationSelected = true;
//    }
//}

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Infrastructure.Services;

namespace PresentationWpf.ViewModels;

public partial class StockViewModel : ObservableObject
{
    private readonly ProductViewModel _productViewModel;
    private readonly ImportViewModel _importViewModel;
    private readonly MovementViewModel _movementViewModel;
    private readonly TransferViewModel _transferViewModel;
    private readonly StockLocationViewModel _stockLocationViewModel;
    private readonly ReturnDebtRepaymentViewModel _returnDebtRepaymentViewModel;
    private readonly TransferReceiveViewModel _transferReceiveViewModel;

    [ObservableProperty]
    private ObservableObject currentViewModel = null!;

    [ObservableProperty] private bool isCatalogSelected;
    [ObservableProperty] private bool isImportSelected;
    [ObservableProperty] private bool isMovementSelected;
    [ObservableProperty] private bool isTransferSelected;
    [ObservableProperty] private bool isStockLocationSelected;

    public StockViewModel(
        ImportViewModel importViewModel,
        MovementViewModel movementViewModel,
        ProductViewModel productViewModel,
        TransferViewModel transferViewModel,
        StockLocationViewModel stockLocationViewModel,
        ReturnDebtRepaymentViewModel returnDebtRepaymentViewModel,
        TransferReceiveViewModel transferReceiveViewModel)
    {
        _productViewModel = productViewModel;
        _importViewModel = importViewModel;
        _movementViewModel = movementViewModel;
        _transferViewModel = transferViewModel;
        _stockLocationViewModel = stockLocationViewModel;
        _returnDebtRepaymentViewModel = returnDebtRepaymentViewModel;
        _transferReceiveViewModel = transferReceiveViewModel;

        // listen for close requests to reset tab state
        _productViewModel.RequestClose += () => ResetTab(ref isCatalogSelected);
        _importViewModel.RequestClose += () => ResetTab(ref isImportSelected);
        _movementViewModel.RequestClose += () => ResetTab(ref isMovementSelected);
        _stockLocationViewModel.RequestClose += () => ResetTab(ref isStockLocationSelected);

        // 🔹 Add new ones
         _returnDebtRepaymentViewModel.RequestClose += () => ResetTab(ref isTransferSelected);
        _transferReceiveViewModel.RequestClose += () => ResetTab(ref isTransferSelected);
    }

    // === Catalog ===
    [RelayCommand]
    private void OpenCatalog()
    {
        CurrentViewModel = _productViewModel;
        SetActiveTab(catalog: true);
    }

    // === Import ===
    [RelayCommand]
    private void OpenImport()
    {
        CurrentViewModel = _importViewModel;
        SetActiveTab(import: true);
    }

    // === Movement ===
    [RelayCommand]
    private void OpenMovement()
    {
        CurrentViewModel = _movementViewModel;
        SetActiveTab(movement: true);
    }

    // === Transfer Main Tab ===
    [RelayCommand]
    private void OpenTransfer()
    {
        CurrentViewModel = _transferViewModel;
        SetActiveTab(transfer: true);
    }

    // === Stock Location ===
    [RelayCommand]
    private void OpenStockLocation()
    {
        CurrentViewModel = _stockLocationViewModel;
        SetActiveTab(stockLocation: true);
    }

    // === Dropdown Option 1: Возврат и погащение полученных товаров ===
    [RelayCommand]
    private void OpenReturnDebtRepayment()
    {
        CurrentViewModel = _returnDebtRepaymentViewModel;
        SetActiveTab(transfer: true);
    }

    // === Dropdown Option 2: Получение и передача товаров ===
    [RelayCommand]
    private void OpenReceiveTransfer()
    {
        CurrentViewModel = _transferReceiveViewModel;
        SetActiveTab(transfer: true);
    }

    // === Helper to clear all and activate one tab ===
    private void SetActiveTab(bool catalog = false, bool import = false, bool movement = false, bool transfer = false, bool stockLocation = false)
    {
        IsCatalogSelected = catalog;
        IsImportSelected = import;
        IsMovementSelected = movement;
        IsTransferSelected = transfer;
        IsStockLocationSelected = stockLocation;
    }

    private void ResetTab(ref bool tabFlag)
    {
        CurrentViewModel = null;
        tabFlag = false;
    }
}
