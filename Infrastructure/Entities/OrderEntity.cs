namespace Infrastructure.Entities;

public class OrderEntity
{
	public string Id { get; set; } =null!;
	public DateTime Date { get; set; } = DateTime.Now;
	public double Rate { get; set; }
	public string? CustomerId { get; set; }
	public int UserId { get; set; }
	public bool WithoutInvoice { get; set; } = false;
	public bool Wholesale { get; set; }
	public bool Stock {  get; set; }
	public bool DirectFromStock { get; set; }
	public bool IsPaid { get; set; }
    public bool IsSent { get; set; }
    public string SuminWords { get; set; } = null!;
	public bool IsBarter { get; set; }
	public string? CourierId { get; set; }                // Доставщик
	public CourierEntity? Courier { get; set; }

	public string? StorekeeperId { get; set; }            // Складчик
	public StorekeeperEntity? Storekeeper { get; set; }
	public virtual UserEntity? User { get; set; }
	public virtual CustomerEntity? Customer { get; set; }
	public virtual ICollection<OrderDetailEntity> OrderDetails { get; set; } = [];
    public virtual ICollection<CustomerPaymentEntity> Payments { get; set; } = [];

}

