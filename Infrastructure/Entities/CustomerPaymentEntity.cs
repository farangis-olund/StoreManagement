namespace Infrastructure.Entities;

public class CustomerPaymentEntity
{
	public int Id { get; set; }                 // № Платежа (Long Integer)
	public DateTime Date { get; set; }          // Дата
	public string CustomerId { get; set; } = null!;   // Код клиента (FK -> Customer)
	public decimal Amount { get; set; }         // Сумма платена
	public string? AmountInWords { get; set; }  // Прописью
	public string? OrderId { get; set; }        // № Накладной (FK -> OrderEntity.Id), optional
	public string? Note { get; set; }           // (optional) примечание

    public decimal Rate { get; set; }
  
    // Navs (optional but useful)
    public CustomerEntity? Customer { get; set; }
	public OrderEntity? Order { get; set; }
}

