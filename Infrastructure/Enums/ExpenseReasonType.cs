
namespace Infrastructure.Enums;
public enum ExpenseReasonType
{
    Fuel = 1,              // Топливо
    Repair = 2,            // Ремонт
    Logistics = 4,         // Логистика

    Utilities = 6,         // Коммунальные услуги
    Internet = 7,          // Интернет / связь

    Equipment = 9,         // Оборудование / инструменты
    Maintenance = 10,      // Обслуживание (сервис)

    OfficeSupplies = 14,   // Канцелярия

    Delivery = 16,         // Доставка клиентам
    VehicleExpenses = 17,  // Расходы на авто (не топливо)
    Food = 18,             // Еда / питание

    Other = 99             // Другое
}

public class ReasonItem
{
    public ExpenseReasonType Value { get; set; }
    public string Display { get; set; } = "";
}

public static class ExpenseReasonMapper
{
    public static List<ReasonItem> GetItems() => new()
    {
        new() { Value = ExpenseReasonType.Fuel, Display = "Топливо" },
        new() { Value = ExpenseReasonType.Repair, Display = "Ремонт" },
        new() { Value = ExpenseReasonType.Logistics, Display = "Логистика" },
        new() { Value = ExpenseReasonType.Utilities, Display = "Коммунальные услуги" },
        new() { Value = ExpenseReasonType.Internet, Display = "Интернет / связь" },
        new() { Value = ExpenseReasonType.Equipment, Display = "Оборудование / инструменты" },
        new() { Value = ExpenseReasonType.Maintenance, Display = "Обслуживание" },
        new() { Value = ExpenseReasonType.OfficeSupplies, Display = "Канцелярия" },
        new() { Value = ExpenseReasonType.Delivery, Display = "Доставка" },
        new() { Value = ExpenseReasonType.VehicleExpenses, Display = "Расходы на авто" },
        new() { Value = ExpenseReasonType.Food, Display = "Еда / питание" },
        new() { Value = ExpenseReasonType.Other, Display = "Другое" }
    };

    public static string ToRussian(ExpenseReasonType value)
    {
        return GetItems()
            .FirstOrDefault(x => x.Value == value)?.Display
            ?? value.ToString();
    }
}