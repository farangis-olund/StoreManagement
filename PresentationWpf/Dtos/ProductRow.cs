
using CommunityToolkit.Mvvm.ComponentModel;
using Infrastructure.Dtos;

namespace PresentationWpf.Dtos;

public partial class ProductRow : ObservableObject
{
    private readonly Product _dto;

    public ProductRow(Product dto)
    {
        _dto = dto;
        _quentity = dto.Quentity;
    }

    public string ArticleNumber => _dto.ArticleNumber;
    public string ProductName => _dto.ProductName;
    public string GroupName => _dto.GroupName;
    public string BrandName => _dto.BrandName;
    public string Marka => _dto.Marka;
    public string Model => _dto.Model;
    public decimal RetailPriceEuro => _dto.RetailPriceEuro;
    public decimal WholesalePriceEuro => _dto.WholesalePriceEuro;
    public string WarehousePlace => _dto.WarehousePlace;

    [ObservableProperty] private int _quentity;
}