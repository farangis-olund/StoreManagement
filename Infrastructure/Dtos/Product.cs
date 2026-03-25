

using Infrastructure.Entities;


namespace Infrastructure.Dtos;

public class Product 
{
	public int Numbering { get; set; }
	public string ArticleNumber { get; set; } = null!;
    public string ProductName { get; set; } = null!;
    public string Model { get; set; } = null!;
	public string Marka { get; set; } = null!;
	public string Alternative { get; set; } = null!;
    public string GroupName { get; set; } = null!;
    public string BrandName { get; set; } = null!;
   
    public int Quentity { get; set; }
	public int OrderQuentity { get; set; }
	public decimal Total { get; set; } = 0;
	public string WarehousePlace { get; set; } = null!;
	public int MinRemainingQuantity { get; set; }
	public double? ExchangeRate { get; set; }
    public double CustomerDiscountPercentage { get; set; } = 0;

	public decimal PriceLevel1 { get; set; }   
    
	public decimal PriceLevel2 { get; set; }   
    	
	public decimal PriceLevel3 { get; set; }   
    	
	public decimal PriceLevel4 { get; set; }  

	public decimal PriceLevel5 { get; set; }   
     public string? Display {  get; set; }
    public int GroupId { get; set; }
    public int BrandId { get; set; }

    public static implicit operator Product(ProductEntity entity)
    {
        return new Product
        {
            Numbering = entity.Numbering,
            ArticleNumber = entity.ArticleNumber,
            ProductName = entity.ProductName,
            Model = entity.Model,
            Marka = entity.Marka,
            Alternative = entity.Alternative,
            GroupName = entity.Group?.GroupName ?? string.Empty,   // safe
            BrandName = entity.Brand?.BrandName ?? string.Empty,
            Quentity = entity.Quentity,
            WarehousePlace = entity.WarehousePlace,
            MinRemainingQuantity = entity.MinRemainingQuantity,
            PriceLevel1 = entity.PriceLevel1,
            PriceLevel2 = entity.PriceLevel2,
            PriceLevel3 = entity.PriceLevel3,
            PriceLevel4 = entity.PriceLevel4,
            PriceLevel5 = entity.PriceLevel5,
            Display = $"{entity.ArticleNumber} · {entity.Brand?.BrandName} · {entity.ProductName}",
            GroupId = entity.GroupId,
            BrandId = entity.BrandId,
			OrderQuentity = 0,
            Total = 0,
            
        };
    }
}
