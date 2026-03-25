using Infrastructure.Enums;

namespace Infrastructure.Entities;

public class ExpenseEntity
{
    public int Id { get; set; }

    public DateTime Date { get; set; }                  // Дата

    public decimal AmountEuro { get; set; }             // Сумма в евро
    public decimal AmountSmn { get; set; }              // Сумма в СМН

    public ExpenseReasonType Reason { get; set; }       // Причина

    public string? Note { get; set; }                   // Примечание (если "Другое")

    // WHO CREATED EXPENSE
    public int? UserId { get; set; }
    public string? CourierId { get; set; }

    // NAVIGATION
    public UserEntity? User { get; set; }
    public CourierEntity? Courier { get; set; }
}