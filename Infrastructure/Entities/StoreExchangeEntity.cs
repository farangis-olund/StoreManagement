using System.ComponentModel.DataAnnotations.Schema;

namespace Infrastructure.Entities
{
    public class StoreExchangeEntity
    {
        public int Id { get; set; }

        public string StoreCode { get; set; } = null!;
        public string ArticleNumber { get; set; } = null!;
        public int Quantity { get; set; }
        public string ExchangeType { get; set; } = null!;

        [ForeignKey(nameof(StoreCode))]
        public virtual StoreEntity Store { get; set; } = null!;

        [ForeignKey(nameof(ArticleNumber))]
        public virtual ProductEntity Product { get; set; } = null!;
    }
}
