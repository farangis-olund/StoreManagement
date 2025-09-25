

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
        StockLocationViewModel stockLocationViewModel)
    {
        _productViewModel = productViewModel;
        _importViewModel = importViewModel;
        _movementViewModel = movementViewModel;
        _transferViewModel = transferViewModel;
        _stockLocationViewModel = stockLocationViewModel;

        // 🔹 listen for ProductViewModel close request
        _productViewModel.RequestClose += () =>
        {
            CurrentViewModel = null; // hide content
            IsCatalogSelected = false;
        };

        _importViewModel.RequestClose += () =>
        {
            CurrentViewModel = null;
            IsImportSelected = false;
        };
    }

    [RelayCommand]
    private void OpenCatalog()
    {
        CurrentViewModel = _productViewModel;
        IsCatalogSelected = true;
        IsImportSelected = false;
        IsMovementSelected = false;
        IsTransferSelected = false;
        IsStockLocationSelected = false;
    }

    [RelayCommand]
    private void OpenImport()
    {
        CurrentViewModel = _importViewModel;
        IsCatalogSelected = false;
        IsImportSelected = true;
        IsMovementSelected = false;
        IsTransferSelected = false;
        IsStockLocationSelected = false;
    }

    [RelayCommand]
    private void OpenMovement()
    {
        CurrentViewModel = _movementViewModel;
        IsCatalogSelected = false;
        IsImportSelected = false;
        IsMovementSelected = true;
        IsTransferSelected = false;
        IsStockLocationSelected = false;
    }

    [RelayCommand]
    private void OpenTransfer()
    {
        CurrentViewModel = _transferViewModel;
        IsCatalogSelected = false;
        IsImportSelected = false;
        IsMovementSelected = false;
        IsTransferSelected = true;
        IsStockLocationSelected = false;
    }

    [RelayCommand]
    private void OpenStockLocation()
    {
        CurrentViewModel = _stockLocationViewModel;
        IsCatalogSelected = false;
        IsImportSelected = false;
        IsMovementSelected = false;
        IsTransferSelected = false;
        IsStockLocationSelected = true;
    }
}
