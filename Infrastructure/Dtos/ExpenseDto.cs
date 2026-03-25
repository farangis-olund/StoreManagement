
namespace Infrastructure.Dtos;
public class ExpenseDto
{
    public int Id { get; set; }

    public DateTime Date { get; set; }

    public int? UserId { get; set; }
    public string? CourierId { get; set; }

    public string? UserFullName { get; set; }
    public string? CourierFullName { get; set; }

    public decimal AmountEuro { get; set; }
    public decimal AmountTjs { get; set; }

    public string Reason { get; set; } = "";
    public string? Note { get; set; }
}