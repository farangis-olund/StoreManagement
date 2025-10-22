using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Infrastructure.Entities
{
    [Table("Stores")]
    public class StoreEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)] // important since StoreCode is not numeric
        public string StoreCode { get; set; } = null!;

        public string? Name { get; set; }
        public string? Address { get; set; }
        public string? Phone { get; set; }

        public virtual ICollection<StoreExchangeEntity> StoreExchanges { get; set; } = new List<StoreExchangeEntity>();
    }
}
