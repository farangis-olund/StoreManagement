
//using Infrastructure.Entities;

//namespace Infrastructure.Dtos;

//public class Price
//{
//    public string PriceType { get; set; } = null!;
//    public decimal PriceAmount { get; set; }
//    public decimal? DiscountPrice { get; set; }
//    public decimal? DicountPercentage { get; set; }
//    public string CurrencyCode { get; set; } = null!;
//    public string CurrencyName { get; set; } = null!;

//    public static implicit operator Price(PriceEntity entity)
//    {
//        return new Price
//        {
//            PriceType = entity.PriceType,
//            PriceAmount = entity.Price,
//            DicountPercentage = entity.DicountPercentage,
//            DiscountPrice = entity.DiscountPrice,
//            CurrencyCode = entity.CurrencyCode,
//            CurrencyName = entity.CurrencyCode,
            
//        };
//    }
//}
