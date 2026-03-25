

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Infrastructure.Contexts;
using Infrastructure.Dtos;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Windows;
using System.Linq.Expressions;
using System.Globalization;
using System.Reflection;
namespace PresentationWpf.ViewModels;

public partial class DataBaseViewModel : ObservableObject
{
    private readonly DatabaseContext _db;

    public DataBaseViewModel(DatabaseContext db)
    {
        _db = db;

        LoadTableNames();
    }


    // ======================== TABLE GROUPS ========================

    public ObservableCollection<TableItem> TablesA_G { get; } = new();
    public ObservableCollection<TableItem> TablesD_K { get; } = new();
    public ObservableCollection<TableItem> TablesL_P { get; } = new();
    public ObservableCollection<TableItem> TablesR_Ya { get; } = new();

    private void LoadTableNames()
    {
        TablesA_G.Clear();
        TablesD_K.Clear();
        TablesL_P.Clear();
        TablesR_Ya.Clear();

        void AddToGroup(string display, string tableName)
        {
            var item = new TableItem(display, tableName);
            char c = display[0];

            if ("АБВГ".Contains(c))
                TablesA_G.Add(item);
            else if ("ДЕЁЖЗИЙК".Contains(c))
                TablesD_K.Add(item);
            else if ("ЛМНОПР".Contains(c))
                TablesL_P.Add(item);
            else
                TablesR_Ya.Add(item);
        }

        var culture = new CultureInfo("ru-RU");
        //------------------------------------------------------------
        // 1) Automatic discovery of ALL DbSet<T> in your DbContext
        //------------------------------------------------------------
        var dbSets = _db.GetType()
            .GetProperties()
            .Where(p =>
                p.PropertyType.IsGenericType &&
                p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>))
            .ToList();

        //------------------------------------------------------------
        // 2) Load DISPLAY NAMES for all known entities
        //------------------------------------------------------------
        Dictionary<string, string> displayNames = new Dictionary<string, string>()
    {
        // --- manually readable names ---
        { "Brands", "Бренды" },
        { "ManagerBrands", "Бренды менеджеров" },
        { "Currencies", "Валюты" },
        { "Returns", "Возвраты" },
        { "Groups", "Группы" },

        { "StockMovements", "Движение склада" },
        { "ReturnDetails", "Детали возвратов" },
        { "OrderDetails", "Детали заказов" },
        { "Couriers", "Доставщики" },
        { "Orders", "Заказы" },
        { "Categories", "Категории" },
        { "Customers", "Клиенты" },
        { "ManagerCustomers", "Клиенты менеджеров" },
        { "RaschetKoefficenta", "Коэфф. расчёта" },
        { "ExchangeRates", "Курсы валют" },

        { "StockUpdateLog", "Лог обновления склада" },
        { "SalesManagers", "Менеджеры продаж" },
        { "Stores", "Магазины" },
        { "Payments", "Платежи" },
        { "Users", "Пользователи" },

        { "Roles", "Роли" },
        { "UserRoles", "Роль пользователей" },
        { "Storekeepers", "Складчики" },
        { "Products", "Товары" },
        { "PriceLevels", "Уровни цен" },

        { "RolePermissions", "Разрешения ролей" },
        { "Permissions", "Разрешения" },
        { "ReturnReasons", "Причины возврата" },
        { "OrganizationInfo", "Организация" },
        { "StockImportErrors", "Ошибки импорта" },
        { "StoreExchanges", "Обмены магазинов" }

    };

        //------------------------------------------------------------
        // 3) Add all DbSet tables automatically to the sidebar
        //------------------------------------------------------------
        foreach (var prop in dbSets)
        {
            string tableName = prop.Name;

            // If no display name defined → use a readable Russian fallback
            string display =
                displayNames.ContainsKey(tableName)
                ? displayNames[tableName]
                : tableName; // fallback (you may add transliteration later)

            AddToGroup(display, tableName);
        }

        SortCollection(TablesA_G, culture);
        SortCollection(TablesD_K, culture);
        SortCollection(TablesL_P, culture);
        SortCollection(TablesR_Ya, culture);


    }

    private void SortCollection(ObservableCollection<TableItem> collection, CultureInfo culture)
    {
        var sorted = collection
            .OrderBy(x => x.Display, StringComparer.Create(culture, ignoreCase: false))
            .ToList();

        collection.Clear();

        foreach (var item in sorted)
            collection.Add(item);
    }

   

    [ObservableProperty]
    private TableItem selectedTable;

    partial void OnSelectedTableChanged(TableItem value)
    {
        if (value != null)
            SelectedTableName = value.TableName;
    }


    [ObservableProperty]
    private string selectedTableName;

    partial void OnSelectedTableNameChanged(string value)
    {
        if (value != null)
            LoadTable();
    }


    // ======================== TABLE DATA ========================

    [ObservableProperty]
    private ObservableCollection<object> tableData = new();

    public bool HasDummyRow { get; set; } = false;


    private void LoadTable()
    {
        HasDummyRow = false;

        var prop = _db.GetType().GetProperty(SelectedTableName);
        if (prop == null) return;

        var dbSet = prop.GetValue(_db) as IQueryable<object>;
        if (dbSet == null) return;

        var list = dbSet.ToList();

        // Add dummy row if empty → generate columns
        if (list.Count == 0)
        {
            var entityType = prop.PropertyType.GetGenericArguments()[0];
            var dummy = Activator.CreateInstance(entityType);

            list.Add(dummy);
            HasDummyRow = false;
        }

        TableData = new ObservableCollection<object>(list);
    }


    // ======================== CRUD (optional) ========================

    [ObservableProperty]
    private object selectedRow;


    [RelayCommand]
    private void Delete()
    {
        if (HasDummyRow) return;
        if (SelectedRow == null) return;

        var result = MessageBox.Show(
            "Вы уверены, что хотите удалить выбранную запись?",
            "Подтверждение удаления",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
            return;

        _db.Remove(SelectedRow);
        _db.SaveChanges();

        MessageBox.Show("Запись успешно удалена.", "Успех",
            MessageBoxButton.OK, MessageBoxImage.Information);

        LoadTable();
    }

    [RelayCommand]
    private void Update()
    {
        if (SelectedRow == null)
            return;

        var result = MessageBox.Show(
            "Вы хотите сохранить изменения?",
            "Подтверждение",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
            return;

        try
        {
            // Get DbSet dynamically
            var tableProp = _db.GetType().GetProperty(SelectedTableName);
            var tableSet = tableProp.GetValue(_db);

            foreach (var row in TableData.ToList())
            {
                // Skip empty rows (same as before)
                bool isEmpty = row.GetType().GetProperties()
                    .Where(p => p.Name != "Id")
                    .All(p =>
                    {
                        var v = p.GetValue(row);
                        return v == null || (v is string s && string.IsNullOrWhiteSpace(s));
                    });

                if (isEmpty)
                    continue;

                // Get ID value
                var idProp = row.GetType().GetProperty("Id");
                var idValue = idProp?.GetValue(row);

                bool exists = !(idValue is int intId && intId == 0);

                if (!exists)
                {
                    _db.Add(row); // new
                }
                else
                {
                    _db.Entry(row).State = EntityState.Modified; // update
                }
            }

            // TRY SAVING — ERRORS HANDLED IN CATCH
            _db.SaveChanges();

            MessageBox.Show("Сохранено!", "Успех",
                MessageBoxButton.OK, MessageBoxImage.Information);

            LoadTable();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Ошибка при сохранении данных.\n" +
                "Проверьте правильность введённых значений.",
                "Ошибка",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );

            // OPTIONAL: show details in output window
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }
    [RelayCommand]
    private void Refresh()
    {
        // Block refresh if dummy row is visible (table empty case)
        if (HasDummyRow)
            return;

        // No table selected -> nothing to refresh
        if (string.IsNullOrWhiteSpace(SelectedTableName))
            return;

        LoadTable();   // simply reload current table
    }

    public object CreateNewRow()
    {
        var prop = _db.GetType().GetProperty(SelectedTableName);
        var entityType = prop.PropertyType.GetGenericArguments()[0];

        var newRow = Activator.CreateInstance(entityType);

        // If table uses auto-increment ID → leave it null
        // If table uses manual ID (Customers), user will paste it
        var idProp = entityType.GetProperty("Id");
        if (idProp != null)
        {
            if (idProp.PropertyType == typeof(int))
                idProp.SetValue(newRow, 0); // EF will generate
        }

        TableData.Add(newRow);
        return newRow;
    }


    [RelayCommand]
    private void Add()
    {
        if (string.IsNullOrWhiteSpace(SelectedTableName))
            return;

        var prop = _db.GetType().GetProperty(SelectedTableName);
        if (prop == null) return;

        var entityType = prop.PropertyType.GetGenericArguments()[0];

        var newRow = Activator.CreateInstance(entityType);

        var idProp = entityType.GetProperty("Id");

        // 🔥 DO NOT touch ID for identity int columns
        if (idProp != null)
        {
            // If it's string ID (like Customers) → set empty
            if (idProp.PropertyType == typeof(string))
            {
                idProp.SetValue(newRow, string.Empty);
            }
            // If it's int identity → leave it completely untouched
        }

        TableData.Add(newRow);
        SelectedRow = newRow;
    }

   
}

public class TableItem
{
    public string Display { get; }
    public string TableName { get; }

    public TableItem(string display, string tableName)
    {
        Display = display;
        TableName = tableName;
    }

    public override string ToString() => Display;
}