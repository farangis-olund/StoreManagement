namespace Infrastructure.Entities;

public class ReturnEntity
{
	public string Id { get; set; } = null!;            // Код возврата: ВОЗ-XXXXXX
	public DateTime Date { get; set; } = DateTime.Now; // Дата возврата

	public string? InvoiceNumber { get; set; }         // № накладной (если возврат по накладной)
	public decimal TotalAmount { get; set; }           // Сумма возврата
	public string? AmountInWords { get; set; }         // Сумма прописью
	public string CustomerId { get; set; } = null!;    // FK на клиента
	public bool IsManual { get; set; }                 // Ручное оформление (true = вручную, false = по накладной)

	public string RefundMethod { get; set; } = "cash"; // Способ возврата (наличные, перевод, зачёт и т.д.)
	public string? Reason { get; set; }                // Причина возврата
	public string? Comment { get; set; }               // Доп. комментарии

	public virtual CustomerEntity? Customer { get; set; }
	public virtual ICollection<ReturnDetailEntity> ReturnDetails { get; set; } = [];
}
