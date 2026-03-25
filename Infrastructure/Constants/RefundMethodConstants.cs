
namespace Infrastructure.Constants; 

public static class RefundMethodConstants
{
    public const string Cash = "Наличные";
    public const string Card = "Карта";
    public const string Balance = "Зачесть в баланс";

    public static readonly string[] CashOrCard =
    {
        Cash,
        Card
    };
}