namespace Infrastructure.Services;

public sealed class OfficialMonthlyPlanLimitException(decimal remaining) : InvalidOperationException(
    $"Сумма официального заказа превышает остаток по месячному плану: {remaining:N2}. " +
    "Оформите товары в пределах остатка, а остальные товары — отдельным заказом.")
{
    public decimal Remaining { get; } = remaining;
}
