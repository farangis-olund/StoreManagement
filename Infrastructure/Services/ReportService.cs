using Infrastructure.Constants;
using Infrastructure.Contexts;
using Infrastructure.Dtos;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace Infrastructure.Services;

public class ReportService
{
    private readonly IDbContextFactory<DatabaseContext> _contextFactory;

    public ReportService(IDbContextFactory<DatabaseContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }


    public async Task<List<InactiveWarehouseProductDto>>
      GetInactiveWarehouseProductsAsync(
          int quantityLessThan,
          DateTime? fromDate = null,
          DateTime? toDate = null)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        try
        {
            var query = db.Products
                .Include(p => p.Brand)
                .Select(p => new
                {
                    Product = p,

                    SoldQuantity = p.OrderDetails
                        .Where(d =>
                            (!fromDate.HasValue || d.Order.Date >= fromDate.Value.Date) &&
                            (!toDate.HasValue || d.Order.Date < toDate.Value.Date.AddDays(1)))
                        .Sum(d => (int?)d.Quentity) ?? 0
                });

            var result = await query
                .Where(x => x.SoldQuantity <= quantityLessThan)
                .Select(x => new InactiveWarehouseProductDto
                {
                    ArticleNumber = x.Product.ArticleNumber,
                    ProductName = x.Product.ProductName,
                    BrandName = x.Product.Brand.BrandName,
                    Marka = x.Product.Marka,
                    Model = x.Product.Model,
                    Quentity = x.SoldQuantity
                })
                .OrderBy(x => x.Quentity)
                .ThenBy(x => x.ProductName)
                .ToListAsync();

            return result;
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Error in GetInactiveWarehouseProductsAsync: " + ex);
            return new List<InactiveWarehouseProductDto>();
        }
    }

    public async Task<(List<CourierStorekeeperReportDto> couriers,
                    List<CourierStorekeeperReportDto> storekeepers)>
 GetCourierStorekeeperReportAsync(
     DateTime? fromDate,
     DateTime? toDate,
     decimal percent)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        try
        {
            var ordersQuery = db.Orders
                .Include(o => o.Courier)
                .Include(o => o.Storekeeper)
                .Include(o => o.OrderDetails)
                .AsQueryable();

            if (fromDate.HasValue)
            {
                var from = fromDate.Value.Date;
                ordersQuery = ordersQuery.Where(o => o.Date >= from);
            }

            if (toDate.HasValue)
            {
                var to = toDate.Value.Date.AddDays(1);
                ordersQuery = ordersQuery.Where(o => o.Date < to);
            }


            var orders = await ordersQuery
                .Select(o => new
                {
                    o.Id,
                    Courier = o.Courier!.FullName,
                    Storekeeper = o.Storekeeper!.FullName,
                    OrderTotal = o.OrderDetails
                        .Sum(d => (decimal)d.Price * d.Quentity)
                })
                .ToListAsync();


            // RETURNS PER ORDER
            var returns = await db.Returns
                .Where(r => r.InvoiceNumber != null)
                .GroupBy(r => r.InvoiceNumber!)
                .Select(g => new
                {
                    OrderId = g.Key,
                    ReturnTotal = g.Sum(x => x.TotalAmount)
                })
                .ToDictionaryAsync(x => x.OrderId, x => x.ReturnTotal);


            var orderData = orders
                .Select(o =>
                {
                    decimal returnAmount = 0;

                    if (returns.TryGetValue(o.Id, out var r))
                        returnAmount = r;

                    var net = o.OrderTotal - returnAmount;

                    if (net < 0)
                        net = 0;

                    return new
                    {
                        o.Courier,
                        o.Storekeeper,
                        OrdersSum = o.OrderTotal,
                        ReturnSum = returnAmount,
                        NetSum = net
                    };
                })
                .ToList();


            // COURIERS
            var couriers = orderData
                .GroupBy(x => x.Courier)
                .Select(g =>
                {
                    var ordersSum = g.Sum(x => x.OrdersSum);
                    var returnSum = g.Sum(x => x.ReturnSum);
                    var net = g.Sum(x => x.NetSum);

                    return new CourierStorekeeperReportDto
                    {
                        Name = g.Key,
                        OrdersSum = ordersSum,
                        ReturnSum = returnSum,
                        NetSum = net,
                        PercentAmount = net * percent / 100m
                    };
                })
                .OrderBy(x => x.Name)
                .ToList();


            // STOREKEEPERS
            var storekeepers = orderData
                .GroupBy(x => x.Storekeeper)
                .Select(g =>
                {
                    var ordersSum = g.Sum(x => x.OrdersSum);
                    var returnSum = g.Sum(x => x.ReturnSum);
                    var net = g.Sum(x => x.NetSum);

                    return new CourierStorekeeperReportDto
                    {
                        Name = g.Key,
                        OrdersSum = ordersSum,
                        ReturnSum = returnSum,
                        NetSum = net,
                        PercentAmount = net * percent / 100m
                    };
                })
                .OrderBy(x => x.Name)
                .ToList();


            return (couriers, storekeepers);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            return ([], []);
        }
    }

    public async Task<List<WarehousePlaceDto>> GetWarehousePlaceReportAsync(
     string? section,
     int row,
     int place,
     bool includeZeroQuantity)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        try
        {
            var query = db.Products
                .AsNoTracking()
                .Include(p => p.Brand)
                .AsQueryable();

            // quantity filter
            if (!includeZeroQuantity)
                query = query.Where(p => p.Quentity > 0);

            // section filter
            if (!string.IsNullOrWhiteSpace(section))
                query = query.Where(p => p.WarehousePlace.StartsWith(section));

            // exact row filter
            if (row > 0)
            {
                if (!string.IsNullOrWhiteSpace(section))
                {
                    query = query.Where(p =>
                        EF.Functions.Like(p.WarehousePlace, $"{section}{row}/%"));
                }
                else
                {
                    // one-letter section, then row, then slash
                    query = query.Where(p =>
                        EF.Functions.Like(p.WarehousePlace, $"_{row}/%"));
                }
            }

            // exact place filter
            if (place > 0)
                query = query.Where(p => p.WarehousePlace.EndsWith($"/{place}"));

            var list = await query
                .Select(p => new WarehousePlaceDto
                {
                    PlaceCode = p.WarehousePlace,
                    Article = p.ArticleNumber,
                    Name = p.ProductName,
                    Brand = p.Brand.BrandName,
                    Mark = p.Marka,
                    Model = p.Model,
                    Quantity = p.Quentity
                })
                .ToListAsync();

            var result = list
                .OrderBy(x => GetSection(x.PlaceCode))
                .ThenBy(x => GetRow(x.PlaceCode))
                .ThenBy(x => GetPlace(x.PlaceCode))
                .ThenBy(x => x.Name)
                .ThenBy(x => x.Brand)
                .ToList();

            return result;
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Error in GetWarehousePlaceReportAsync: " + ex);
            return new List<WarehousePlaceDto>();
        }
    }

    public async Task<List<WarehousePlaceInfo>> GetWarehousePlacesRawAsync()
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        try
        {
            var rawData = await db.Products
                .AsNoTracking()
                .Where(p => !string.IsNullOrEmpty(p.WarehousePlace))
                .Select(p => p.WarehousePlace)
                .Distinct()
                .ToListAsync();

            var parsed = rawData
                .Select(text =>
                {
                    try
                    {
                        // Example: A12/7
                        var section = text.Substring(0, 1);
                        var rest = text.Substring(1).Split('/');

                        if (rest.Length != 2)
                            return null;

                        if (!int.TryParse(rest[0], out var row))
                            return null;

                        if (!int.TryParse(rest[1], out var place))
                            return null;

                        return new WarehousePlaceInfo
                        {
                            Section = section,
                            Row = row,
                            Place = place
                        };
                    }
                    catch
                    {
                        return null;
                    }
                })
                .Where(x => x != null)
                .GroupBy(x => new { x!.Section, x.Row, x.Place })
                .Select(g => g.First()!)
                .OrderBy(x => x.Section)
                .ThenBy(x => x.Row)
                .ThenBy(x => x.Place)
                .ToList();

            return parsed;
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Error in GetWarehousePlacesRawAsync: " + ex);
            return new List<WarehousePlaceInfo>();
        }
    }


    public async Task<List<TotalSalesReportRowDto>> GetFullTotalSalesReportAsync(
    DateTime? fromDate,
    DateTime? toDate)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        try
        {
            var orderDetailsQuery = db.OrderDetails
                .AsNoTracking()
                .AsQueryable();

            var paymentsQuery = db.Payments
                .AsNoTracking()
                .AsQueryable();

            var returnsQuery = db.Returns
                .AsNoTracking()
                .AsQueryable();

            var courierPaymentsQuery = db.CourierPayments
                .AsNoTracking()
                .AsQueryable();

            var expensesQuery = db.Expenses
                .AsNoTracking()
                .AsQueryable();

            var today = DateTime.Today;

            var isTodayOnly =
                fromDate.HasValue &&
                toDate.HasValue &&
                fromDate.Value.Date == today &&
                toDate.Value.Date == today;

            // default range = what user selected
            DateTime? commonFrom = fromDate?.Date;
            DateTime? commonToExclusive = toDate?.Date.AddDays(1);

            DateTime? courierFrom = fromDate?.Date;
            DateTime? courierToExclusive = toDate?.Date.AddDays(1);

            // special rule:
            // if user selected only today,
            // then all queries except courier payments should use yesterday
            if (isTodayOnly)
            {
                var yesterday = today.AddDays(-1);

                commonFrom = yesterday;
                commonToExclusive = today; // yesterday only: >= yesterday and < today

                courierFrom = today;
                courierToExclusive = today.AddDays(1); // today only
            }

            // =========================
            // APPLY COMMON FILTERS
            // =========================
            if (commonFrom.HasValue)
            {
                var from = commonFrom.Value;

                orderDetailsQuery = orderDetailsQuery.Where(x => x.Order.Date >= from);
                paymentsQuery = paymentsQuery.Where(x => x.Date >= from);
                returnsQuery = returnsQuery.Where(x => x.Date >= from);
                expensesQuery = expensesQuery.Where(x => x.Date >= from);
            }

            if (commonToExclusive.HasValue)
            {
                var to = commonToExclusive.Value;

                orderDetailsQuery = orderDetailsQuery.Where(x => x.Order.Date < to);
                paymentsQuery = paymentsQuery.Where(x => x.Date < to);
                returnsQuery = returnsQuery.Where(x => x.Date < to);
                expensesQuery = expensesQuery.Where(x => x.Date < to);
            }

            // =========================
            // APPLY COURIER FILTERS
            // =========================
            if (courierFrom.HasValue)
            {
                var from = courierFrom.Value;
                courierPaymentsQuery = courierPaymentsQuery.Where(x => x.Date >= from);
            }

            if (courierToExclusive.HasValue)
            {
                var to = courierToExclusive.Value;
                courierPaymentsQuery = courierPaymentsQuery.Where(x => x.Date < to);
            }

            // =========================
            // SALES
            // =========================
            var totalSalesEuro = await orderDetailsQuery
                .Where(x => !x.Order.IsBarter)
                .SumAsync(x => (decimal?)x.Price * x.Quentity) ?? 0m;

            var totalSalesSmn = await orderDetailsQuery
                .Where(x => !x.Order.IsBarter)
                .SumAsync(x => (decimal?)(x.Price * x.Quentity * (decimal)x.Order.Rate)) ?? 0m;

            // =========================
            // RETURNS
            // =========================
            var totalReturnEuro = await returnsQuery
                .Where(x => !x.IsManual)
                .SumAsync(x => (decimal?)x.TotalAmount) ?? 0m;

            var totalReturnSmn = await returnsQuery
                .Where(x => !x.IsManual)
                .SumAsync(x => (decimal?)(x.TotalAmount * x.Rate)) ?? 0m;

            // =========================
            // OLD RETURNS
            // =========================
            var totalOldReturnEuro = await returnsQuery
                .Where(x => x.IsManual)
                .SumAsync(x => (decimal?)x.TotalAmount) ?? 0m;

            var totalOldReturnSmn = await returnsQuery
                .Where(x => x.IsManual)
                .SumAsync(x => (decimal?)(x.TotalAmount * x.Rate)) ?? 0m;

            // =========================
            // PAYMENTS
            // =========================
            var totalPaymentsEuro = await paymentsQuery
                .SumAsync(x => (decimal?)x.Amount) ?? 0m;

            var totalPaymentsSmn = await paymentsQuery
                .SumAsync(x => (decimal?)(x.Amount * x.Rate)) ?? 0m;

            // =========================
            // COURIER PAYMENTS
            // =========================
            var totalCourierPaymentsEuro = await courierPaymentsQuery
                .SumAsync(x => (decimal?)x.AmountInEuro) ?? 0m;

            var totalCourierPaymentsSmn = await courierPaymentsQuery
                .SumAsync(x => (decimal?)x.AmountInTJS) ?? 0m;

            // =========================
            // EXPENSES
            // =========================
            var totalExpensesEuro = await expensesQuery
                .SumAsync(x => (decimal?)x.AmountEuro) ?? 0m;

            var totalExpensesSmn = await expensesQuery
                .SumAsync(x => (decimal?)x.AmountSmn) ?? 0m;

            // =========================
            // RETURN BY CASH / CARD
            // =========================
            var returnCashOrCardEuro = await returnsQuery
                .Where(x => !x.IsManual &&
                    (x.RefundMethod == RefundMethodConstants.Cash ||
                     x.RefundMethod == RefundMethodConstants.Card))
                .SumAsync(x => (decimal?)x.TotalAmount) ?? 0m;

            var returnCashOrCardSmn = await returnsQuery
                .Where(x => !x.IsManual &&
                    (x.RefundMethod == RefundMethodConstants.Cash ||
                     x.RefundMethod == RefundMethodConstants.Card))
                .SumAsync(x => (decimal?)(x.TotalAmount * x.Rate)) ?? 0m;

            var oldReturnCashOrCardEuro = await returnsQuery
                .Where(x => x.IsManual &&
                    (x.RefundMethod == RefundMethodConstants.Cash ||
                     x.RefundMethod == RefundMethodConstants.Card))
                .SumAsync(x => (decimal?)x.TotalAmount) ?? 0m;

            var oldReturnCashOrCardSmn = await returnsQuery
                .Where(x => x.IsManual &&
                    (x.RefundMethod == RefundMethodConstants.Cash ||
                     x.RefundMethod == RefundMethodConstants.Card))
                .SumAsync(x => (decimal?)(x.TotalAmount * x.Rate)) ?? 0m;

            // =========================
            // KASSA
            // =========================
            var kasaEuro =
                totalCourierPaymentsEuro
                - returnCashOrCardEuro
                - oldReturnCashOrCardEuro
                - totalExpensesEuro;

            var kasaSmn =
                totalCourierPaymentsSmn
                - returnCashOrCardSmn
                - oldReturnCashOrCardSmn
                - totalExpensesSmn;

            var rows = new List<TotalSalesReportRowDto>
        {
            new()
            {
                Name = "ПРОДАЖА",
                Euro = totalSalesEuro,
                Smn = totalSalesSmn
            },
            new()
            {
                Name = "ВОЗВРАТ",
                Euro = totalReturnEuro,
                Smn = totalReturnSmn
            },
            new()
            {
                Name = "ПРОШЛОГОДНИЙ ВОЗВРАТ",
                Euro = totalOldReturnEuro,
                Smn = totalOldReturnSmn
            },
            new()
            {
                Name = "ПЛАТЕЖИ",
                Euro = totalPaymentsEuro,
                Smn = totalPaymentsSmn
            },
            new()
            {
                Name = "ОПЛАТА ДОСТАВЩИКУ",
                Euro = totalCourierPaymentsEuro,
                Smn = totalCourierPaymentsSmn
            },
            new()
            {
                Name = "РАСХОДЫ",
                Euro = totalExpensesEuro,
                Smn = totalExpensesSmn
            },
            new()
            {
                Name = "КАССА",
                Euro = kasaEuro,
                Smn = kasaSmn
            }
        };

            return rows;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            return new List<TotalSalesReportRowDto>();
        }
    }

    //public async Task<List<TotalSalesReportRowDto>> GetFullTotalSalesReportAsync(
    // DateTime? fromDate,
    // DateTime? toDate)
    //{
    //    await using var db = await _contextFactory.CreateDbContextAsync();

    //    try
    //    {
    //        var orderDetailsQuery = db.OrderDetails
    //            .AsNoTracking()
    //            .AsQueryable();

    //        var paymentsQuery = db.Payments
    //            .AsNoTracking()
    //            .AsQueryable();

    //        var returnsQuery = db.Returns
    //            .AsNoTracking()
    //            .AsQueryable();

    //        var courierPaymentsQuery = db.CourierPayments
    //            .AsNoTracking()
    //            .AsQueryable();

    //        var expensesQuery = db.Expenses
    //            .AsNoTracking()
    //            .AsQueryable();

    //        if (fromDate.HasValue)
    //        {
    //            var from = fromDate.Value.Date;

    //            orderDetailsQuery = orderDetailsQuery.Where(x => x.Order.Date >= from);
    //            paymentsQuery = paymentsQuery.Where(x => x.Date >= from);
    //            returnsQuery = returnsQuery.Where(x => x.Date >= from);
    //            courierPaymentsQuery = courierPaymentsQuery.Where(x => x.Date >= from);
    //            expensesQuery = expensesQuery.Where(x => x.Date >= from);
    //        }

    //        if (toDate.HasValue)
    //        {
    //            var to = toDate.Value.Date.AddDays(1);

    //            orderDetailsQuery = orderDetailsQuery.Where(x => x.Order.Date < to);
    //            paymentsQuery = paymentsQuery.Where(x => x.Date < to);
    //            returnsQuery = returnsQuery.Where(x => x.Date < to);
    //            courierPaymentsQuery = courierPaymentsQuery.Where(x => x.Date < to);
    //            expensesQuery = expensesQuery.Where(x => x.Date < to);
    //        }

    //        // =========================
    //        // SALES
    //        // =========================
    //        var totalSalesEuro = await orderDetailsQuery
    //            .Where(x => !x.Order.IsBarter)
    //            .SumAsync(x => (decimal?)x.Price * x.Quentity) ?? 0m;

    //        var totalSalesSmn = await orderDetailsQuery
    //            .Where(x => !x.Order.IsBarter)
    //            .SumAsync(x => (decimal?)(x.Price * x.Quentity * (decimal)x.Order.Rate)) ?? 0m;

    //        // =========================
    //        // RETURNS
    //        // assumes ReturnEntity has Rate
    //        // =========================
    //        var totalReturnEuro = await returnsQuery
    //            .Where(x => !x.IsManual)
    //            .SumAsync(x => (decimal?)x.TotalAmount) ?? 0m;

    //        var totalReturnSmn = await returnsQuery
    //            .Where(x => !x.IsManual)
    //            .SumAsync(x => (decimal?)(x.TotalAmount * x.Rate)) ?? 0m;

    //        // =========================
    //        // OLD RETURNS
    //        // assumes ReturnEntity has Rate
    //        // =========================
    //        var totalOldReturnEuro = await returnsQuery
    //            .Where(x => x.IsManual)
    //            .SumAsync(x => (decimal?)x.TotalAmount) ?? 0m;

    //        var totalOldReturnSmn = await returnsQuery
    //            .Where(x => x.IsManual)
    //            .SumAsync(x => (decimal?)(x.TotalAmount * x.Rate)) ?? 0m;

    //        // =========================
    //        // PAYMENTS
    //        // assumes PaymentEntity has Rate
    //        // =========================
    //        var totalPaymentsEuro = await paymentsQuery
    //            .SumAsync(x => (decimal?)x.Amount) ?? 0m;

    //        var totalPaymentsSmn = await paymentsQuery
    //            .SumAsync(x => (decimal?)(x.Amount * x.Rate)) ?? 0m;

    //        // =========================
    //        // COURIER PAYMENTS
    //        // saved in both currencies already
    //        // =========================
    //        var totalCourierPaymentsEuro = await courierPaymentsQuery
    //            .SumAsync(x => (decimal?)x.AmountInEuro) ?? 0m;

    //        var totalCourierPaymentsSmn = await courierPaymentsQuery
    //            .SumAsync(x => (decimal?)x.AmountInTJS) ?? 0m;

    //        // =========================
    //        // EXPENSES
    //        // saved in both currencies already
    //        // =========================
    //        var totalExpensesEuro = await expensesQuery
    //            .SumAsync(x => (decimal?)x.AmountEuro) ?? 0m;

    //        var totalExpensesSmn = await expensesQuery
    //            .SumAsync(x => (decimal?)x.AmountSmn) ?? 0m;

    //        // =========================
    //        // RETURN BY CASH / CARD
    //        // assumes ReturnEntity has Rate
    //        // =========================
    //        var returnCashOrCardEuro = await returnsQuery
    //            .Where(x => !x.IsManual &&
    //                (x.RefundMethod == RefundMethodConstants.Cash ||
    //                 x.RefundMethod == RefundMethodConstants.Card))
    //            .SumAsync(x => (decimal?)x.TotalAmount) ?? 0m;

    //        var returnCashOrCardSmn = await returnsQuery
    //            .Where(x => !x.IsManual &&
    //                (x.RefundMethod == RefundMethodConstants.Cash ||
    //                 x.RefundMethod == RefundMethodConstants.Card))
    //            .SumAsync(x => (decimal?)(x.TotalAmount * x.Rate)) ?? 0m;

    //        var oldReturnCashOrCardEuro = await returnsQuery
    //            .Where(x => x.IsManual &&
    //                (x.RefundMethod == RefundMethodConstants.Cash ||
    //                 x.RefundMethod == RefundMethodConstants.Card))
    //            .SumAsync(x => (decimal?)x.TotalAmount) ?? 0m;

    //        var oldReturnCashOrCardSmn = await returnsQuery
    //            .Where(x => x.IsManual &&
    //                (x.RefundMethod == RefundMethodConstants.Cash ||
    //                 x.RefundMethod == RefundMethodConstants.Card))
    //            .SumAsync(x => (decimal?)(x.TotalAmount * x.Rate)) ?? 0m;

    //        // =========================
    //        // KASSA
    //        // use saved euro/tjs totals separately
    //        // =========================
    //        var kasaEuro =
    //            totalCourierPaymentsEuro
    //            - returnCashOrCardEuro
    //            - oldReturnCashOrCardEuro
    //            - totalExpensesEuro;

    //        var kasaSmn =
    //            totalCourierPaymentsSmn
    //            - returnCashOrCardSmn
    //            - oldReturnCashOrCardSmn
    //            - totalExpensesSmn;

    //        var rows = new List<TotalSalesReportRowDto>
    //    {
    //        new()
    //        {
    //            Name = "ПРОДАЖА",
    //            Euro = totalSalesEuro,
    //            Smn = totalSalesSmn
    //        },
    //        new()
    //        {
    //            Name = "ВОЗВРАТ",
    //            Euro = totalReturnEuro,
    //            Smn = totalReturnSmn
    //        },
    //        new()
    //        {
    //            Name = "ПРОШЛОГОДНИЙ ВОЗВРАТ",
    //            Euro = totalOldReturnEuro,
    //            Smn = totalOldReturnSmn
    //        },
    //        new()
    //        {
    //            Name = "ПЛАТЕЖИ",
    //            Euro = totalPaymentsEuro,
    //            Smn = totalPaymentsSmn
    //        },
    //        new()
    //        {
    //            Name = "ОПЛАТА ДОСТАВЩИКУ",
    //            Euro = totalCourierPaymentsEuro,
    //            Smn = totalCourierPaymentsSmn
    //        },
    //        new()
    //        {
    //            Name = "РАСХОДЫ",
    //            Euro = totalExpensesEuro,
    //            Smn = totalExpensesSmn
    //        },
    //        new()
    //        {
    //            Name = "КАССА",
    //            Euro = kasaEuro,
    //            Smn = kasaSmn
    //        }
    //    };

    //        return rows;
    //    }
    //    catch (Exception ex)
    //    {
    //        Debug.WriteLine(ex);
    //        return new List<TotalSalesReportRowDto>();
    //    }
    //}

    public async Task<List<OfficialSalesReportRowDto>> GetOfficialSalesReportAsync(
        DateTime? fromDate,
        DateTime? toDate)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        try
        {
            var query = db.OrderDetails
                .AsNoTracking()
                .Where(d =>
                    d.Order.OfficialOrder &&
                    d.Order.Customer != null &&
                    d.Order.Customer.OfficialCustomer);

            if (fromDate.HasValue)
            {
                var from = fromDate.Value.Date;
                query = query.Where(d => d.Order.Date >= from);
            }

            if (toDate.HasValue)
            {
                var to = toDate.Value.Date.AddDays(1);
                query = query.Where(d => d.Order.Date < to);
            }

            return await query
                .OrderBy(d => d.Order.Date)
                .ThenBy(d => d.Order.Customer!.FullName)
                .ThenBy(d => d.Order.Id)
                .ThenBy(d => d.ArticleNumber)
                .Select(d => new OfficialSalesReportRowDto
                {
                    Date = d.Order.Date,
                    OrderId = d.Order.Id,
                    CustomerId = d.Order.CustomerId ?? string.Empty,
                    CustomerFullName = d.Order.Customer!.FullName,
                    CourierName = d.Order.Courier != null ? d.Order.Courier.FullName : null,
                    StorekeeperName = d.Order.Storekeeper != null ? d.Order.Storekeeper.FullName : null,
                    ArticleNumber = d.ArticleNumber,
                    ProductName = d.Product.ProductName,
                    BrandName = d.Product.Brand.BrandName,
                    Marka = d.Product.Marka,
                    Model = d.Product.Model,
                    Quantity = d.Quentity,
                    ReturnedQuantity = d.ReturnedQty ?? 0,
                    Price = d.Price,
                    Total = d.Price * d.Quentity,
                    Rate = d.Order.Rate,
                    TotalSmn = d.Price * d.Quentity * (decimal)d.Order.Rate
                })
                .ToListAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Error in GetOfficialSalesReportAsync: " + ex);
            return new List<OfficialSalesReportRowDto>();
        }
    }



    private static string GetSection(string code)
    {
        return string.IsNullOrEmpty(code) ? "" : code.Substring(0, 1);
    }

    private static int GetRow(string code)
    {
        if (string.IsNullOrEmpty(code)) return 0;

        var slashIndex = code.IndexOf('/');
        if (slashIndex <= 1) return 0;

        var rowPart = code.Substring(1, slashIndex - 1);
        return int.TryParse(rowPart, out var r) ? r : 0;
    }

    private static int GetPlace(string code)
    {
        if (string.IsNullOrEmpty(code)) return 0;

        var slashIndex = code.IndexOf('/');
        if (slashIndex < 0 || slashIndex == code.Length - 1) return 0;

        var placePart = code.Substring(slashIndex + 1);
        return int.TryParse(placePart, out var p) ? p : 0;
    }
}

public class WarehousePlaceInfo
{
    public string Section { get; set; } = "";
    public int Row { get; set; }
    public int Place { get; set; }
}
