using Infrastructure.Dtos;
using System.ComponentModel;


namespace PresentationWpf.Dtos;

public class FullProductModel : INotifyPropertyChanged
{
    public string ArticleNumber { get; set; } = null!;
    public string ProductName { get; set; } = null!;
    public string Model { get; set; } = null!;
    public string Marka { get; set; } = null!;
    public string Alternative { get; set; } = null!;
    public int GroupId { get; set; }
    public int BrandId { get; set; }
    public int Quentity { get; set; }
    public string WarehousePlace { get; set; } = null!;
    public int MinRemainingQuantity { get; set; }

    // 💶 Все цены (обязательные для каталога)
    public decimal RetailPriceEuro { get; set; }
    public decimal WholesalePriceEuro { get; set; }
    public decimal ServicePriceEuro { get; set; }
    public decimal WholesalePrice1Euro { get; set; }
    public decimal NetPrice { get; set; }
    public decimal SmallWholesalePrice { get; set; }

    // INotifyPropertyChanged
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    // 🔄 Маппинг из DTO (Entity → Model)
    public static implicit operator FullProductModel(Product dto) =>
        new FullProductModel
        {
            ArticleNumber = dto.ArticleNumber,
            ProductName = dto.ProductName,
            Model = dto.Model,
            Marka = dto.Marka,
            Alternative = dto.Alternative,
            GroupId = dto.GroupId,
            BrandId = dto.BrandId,
            Quentity = dto.Quentity,
            WarehousePlace = dto.WarehousePlace,
            MinRemainingQuantity = dto.MinRemainingQuantity,
            RetailPriceEuro = dto.RetailPriceEuro,
            WholesalePriceEuro = dto.WholesalePriceEuro,
            ServicePriceEuro = dto.ServicePriceEuro,
            WholesalePrice1Euro = dto.WholesalePrice1Euro,
            NetPrice = dto.NetPrice,
            SmallWholesalePrice = dto.SmallWholesalePrice
        };
}
