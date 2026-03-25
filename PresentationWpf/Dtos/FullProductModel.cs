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
    public decimal PriceLevel1 { get; set; }
    public decimal PriceLevel2 { get; set; }
    public decimal PriceLevel3 { get; set; }
    public decimal PriceLevel4 { get; set; }
    public decimal PriceLevel5 { get; set; }
    

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
            PriceLevel1 = dto.PriceLevel1,
            PriceLevel2 = dto.PriceLevel2,
            PriceLevel3 = dto.PriceLevel3,
            PriceLevel4 = dto.PriceLevel4,
            PriceLevel5 = dto.PriceLevel5
           
        };
}
