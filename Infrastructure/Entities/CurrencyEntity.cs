using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Entities;

public partial class CurrencyEntity
{
    [Key]
    [StringLength(10)]
    [Unicode(false)]
    public string Code { get; set; } = null!;

    [StringLength(20)]
    public string CurrencyName { get; set; } = null!;

    //public virtual ICollection<PriceEntity> Prices { get; set; } = [];
    
}
