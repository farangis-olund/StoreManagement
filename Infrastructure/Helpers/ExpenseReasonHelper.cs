using Infrastructure.Enums;

namespace Infrastructure.Helpers;

public static class ExpenseReasonHelper
{
    public static string ToRussian(this ExpenseReasonType reason)
    {
        return reason switch
        {
            ExpenseReasonType.Fuel => "Топливо",
            ExpenseReasonType.Repair => "Ремонт",
            ExpenseReasonType.Logistics => "Логистика",
            ExpenseReasonType.Utilities => "Коммунальные услуги",
            ExpenseReasonType.Internet => "Интернет / связь",
            ExpenseReasonType.Equipment => "Оборудование / инструменты",
            ExpenseReasonType.Maintenance => "Обслуживание",
            ExpenseReasonType.OfficeSupplies => "Канцелярия",
            ExpenseReasonType.Delivery => "Доставка",
            ExpenseReasonType.VehicleExpenses => "Расходы на авто",
            ExpenseReasonType.Food => "Еда / питание",
            ExpenseReasonType.Other => "Другое",
            _ => reason.ToString()
        };
    }
}