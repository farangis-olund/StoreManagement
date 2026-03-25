namespace Infrastructure.Entities;

public class CourierPaymentEntity
{
    public int Id { get; set; }

    public DateTime Date { get; set; }              // Дата

    public string OrderId { get; set; } = "";       // № накладной

    public decimal AmountInEuro { get; set; }             // Сумма евро
    public decimal AmountInTJS { get; set; }             // Сумма смн

    public string CourierId { get; set; } = "";     // Код доставщика

    // NAVIGATION
    public CourierEntity? Courier { get; set; }
    public OrderEntity? Order { get; set; }
}
