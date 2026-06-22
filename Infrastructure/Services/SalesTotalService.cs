
using Infrastructure.Contexts;
using Infrastructure.Dtos;
using Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace Infrastructure.Services;

public class SalesTotalService
{
    private readonly IDbContextFactory<DatabaseContext> _contextFactory;

    public SalesTotalService(IDbContextFactory<DatabaseContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }
      
    // REGION / BRAND REPORT
    public async Task<List<SalesTotalDto>>
        GetTotalsByRegionAndBrandAsync(
        DateTime? fromDate = null,
        DateTime? toDate = null)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        try
        {
            var query = db.OrderDetails
                .Include(d => d.Order)
                    .ThenInclude(o => o.Customer)
                .Include(d => d.Product)
                    .ThenInclude(p => p.Brand)
                .AsQueryable();

            // DATE FILTER (FULL DAY SAFE)
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

            var result = await query
                .GroupBy(d => new
                {
                    Region = d.Order.Customer.Region,
                    Brand = d.Product.Brand.BrandName
                })
                .Select(g => new SalesTotalDto
                {
                    Region = g.Key.Region ?? "",
                    Brand = g.Key.Brand ?? "",
                    Total = g.Sum(x => (decimal)x.Price * x.Quentity)
                })
                .OrderBy(x => x.Region)
                .ThenBy(x => x.Brand)
                .ToListAsync();

            return result;
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Error in GetTotalsByRegionAndBrandAsync: " + ex);
            return new List<SalesTotalDto>();
        }
    }

    
    // GROUP / CUSTOMER REPORT
    public async Task<List<SalesByGroupCustomerDto>>
        GetSalesByGroupCustomerReportAsync(
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string? firma = null,
        string? region = null)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        try
        {
            var query = db.OrderDetails
                .Include(d => d.Order)
                    .ThenInclude(o => o.Customer)
                .Include(d => d.Product)
                    .ThenInclude(p => p.Brand)
                .Include(d => d.Product)
                    .ThenInclude(p => p.Group)
                .AsQueryable();

            // DATE FILTER (FULL DAY SAFE)
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

            // FIRMA FILTER
            if (!string.IsNullOrWhiteSpace(firma) && firma != "Всё")
                query = query.Where(d => d.Product.Brand.BrandName == firma);

            // REGION FILTER
            if (!string.IsNullOrWhiteSpace(region) && region != "Всё")
                query = query.Where(d => d.Order.Customer!.Region == region);

            var result = await query
                .GroupBy(d => new
                {
                    ProductGroup = d.Product.Group.GroupName,
                    CustomerCode = d.Order.Customer!.Id,
                    CustomerName = d.Order.Customer.FullName
                })
                .Select(g => new SalesByGroupCustomerDto
                {
                    ProductGroup = g.Key.ProductGroup ?? "",
                    CustomerCode = g.Key.CustomerCode ?? "",
                    CustomerName = g.Key.CustomerName ?? "",
                    Firma = g.First().Product.Brand.BrandName,
                    Region = g.First().Order.Customer!.Region,
                    Total = g.Sum(x => (decimal)x.Price * x.Quentity),
                    Quantity = g.Sum(x => x.Quentity)
                })
                .OrderBy(x => x.ProductGroup)
                .ThenBy(x => x.CustomerCode)
                .ToListAsync();

            return result;
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Error in GetSalesByGroupCustomerReportAsync: " + ex);
            return new List<SalesByGroupCustomerDto>();
        }
    }

 
    public async Task<List<SalesManagerReportRow>> GetManagerCommissionReportAsync(
    DateTime? fromDate = null,
    DateTime? toDate = null,
    string? firma = null,
    string? managerId = null)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        try
        {
            // =========================
            // 1. LOAD RETURNS FIRST
            // =========================
            var returnsQuery = db.Returns
                .Include(r => r.Customer)
                    .ThenInclude(c => c.ManagerCustomers)
                .Include(r => r.ReturnDetails)
                .AsQueryable();

            if (fromDate.HasValue)
            {
                var from = fromDate.Value.Date;
                returnsQuery = returnsQuery.Where(r => r.Date >= from);
            }

            if (toDate.HasValue)
            {
                var to = toDate.Value.Date.AddDays(1);
                returnsQuery = returnsQuery.Where(r => r.Date < to);
            }

            var returnsRaw = await returnsQuery
                .Select(r => new
                {
                    ReturnId = r.Id,
                    InvoiceNumber = r.InvoiceNumber,
                    CustomerId = r.CustomerId,
                    ManagerId = r.Customer != null
                        ? r.Customer.ManagerCustomers.Select(mc => mc.ManagerId).FirstOrDefault()
                        : null,
                    TotalAmount = r.TotalAmount
                })
                .ToListAsync();

            // manager filter for returns
            if (!string.IsNullOrWhiteSpace(managerId))
            {
                returnsRaw = returnsRaw
                    .Where(x => x.ManagerId == managerId)
                    .ToList();
            }

            // Returned invoice numbers for excluding sales
            var returnedInvoiceNumbers = returnsRaw
                .Where(r => !string.IsNullOrWhiteSpace(r.InvoiceNumber))
                .Select(r => r.InvoiceNumber!)
                .Distinct()
                .ToList();

            // =========================
            // 1b. LOAD RETURN DETAILS BY COMPANY
            // =========================
            var returnDetailsQuery = db.Returns
                .Include(r => r.Customer)
                    .ThenInclude(c => c.ManagerCustomers)
                .Include(r => r.ReturnDetails)
                .AsQueryable();

            if (fromDate.HasValue)
            {
                var from = fromDate.Value.Date;
                returnDetailsQuery = returnDetailsQuery.Where(r => r.Date >= from);
            }

            if (toDate.HasValue)
            {
                var to = toDate.Value.Date.AddDays(1);
                returnDetailsQuery = returnDetailsQuery.Where(r => r.Date < to);
            }

            var returnDetailsRaw = await (
                from r in returnDetailsQuery
                from rd in r.ReturnDetails
                join od in db.OrderDetails on new
                {
                    Invoice = r.InvoiceNumber,
                    Article = rd.ArticleNumber
                }
                equals new
                {
                    Invoice = od.OrderId,
                    Article = od.Product.ArticleNumber
                }
                select new
                {
                    ManagerId = r.Customer != null
                        ? r.Customer.ManagerCustomers.Select(mc => mc.ManagerId).FirstOrDefault()
                        : null,
                    Company = od.Product.Brand.CompanyName,
                    Amount = rd.Total
                })
                .ToListAsync();

            if (!string.IsNullOrWhiteSpace(managerId))
            {
                returnDetailsRaw = returnDetailsRaw
                    .Where(x => x.ManagerId == managerId)
                    .ToList();
            }

            if (!string.IsNullOrWhiteSpace(firma) && firma != "Всё")
            {
                returnDetailsRaw = returnDetailsRaw
                    .Where(x => x.Company == firma)
                    .ToList();
            }

            // =========================
            // 2. MAIN SALES QUERY
            // =========================
            var query = db.OrderDetails
                .Include(d => d.Order)
                    .ThenInclude(o => o.Customer)
                .Include(d => d.Product)
                    .ThenInclude(p => p.Brand)
                .AsQueryable();

            if (returnedInvoiceNumbers.Count > 0)
            {
                query = query.Where(d => !returnedInvoiceNumbers.Contains(d.Order.Id));
            }

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

            if (!string.IsNullOrWhiteSpace(firma) && firma != "Всё")
            {
                query = query.Where(d => d.Product.Brand.CompanyName == firma);
            }

            var rawData = await query
                .Select(d => new
                {
                    ManagerId = d.Order.Customer!.ManagerCustomers
                        .Select(mc => mc.ManagerId)
                        .FirstOrDefault(),
                    Company = d.Product.Brand.CompanyName,
                    BrandId = d.Product.Brand.Id,
                    Amount = (decimal)d.Price * d.Quentity
                })
                .ToListAsync();

            if (!string.IsNullOrWhiteSpace(managerId))
            {
                rawData = rawData
                    .Where(x => x.ManagerId == managerId)
                    .ToList();
            }

            var managers = await db.SalesManagers
                .Include(m => m.ManagerBrands)
                    .ThenInclude(mb => mb.Brand)
                .ToListAsync();

            if (!string.IsNullOrWhiteSpace(managerId))
            {
                managers = managers
                    .Where(m => m.Id == managerId)
                    .ToList();
            }

            var result = new List<SalesManagerReportRow>();

            foreach (var manager in managers)
            {
                var row = new SalesManagerReportRow
                {
                    ManagerId = manager.Id,
                    ManagerName = manager.FullName
                };

                // sales grouped by company
                var managerSales = rawData
                    .Where(x => x.ManagerId == manager.Id)
                    .GroupBy(x => x.Company);

                foreach (var companyGroup in managerSales)
                {
                    string company = companyGroup.Key ?? "Неизвестный";

                    decimal totalSales = 0;
                    decimal totalCommission = 0;
                    decimal totalReturn = 0;

                    // get returns for this manager/company
                    totalReturn = returnDetailsRaw
                        .Where(r => r.ManagerId == manager.Id && r.Company == company)
                        .Sum(r => r.Amount);

                    foreach (var sale in companyGroup)
                    {
                        var managerBrand = manager.ManagerBrands
                            .FirstOrDefault(mb => mb.BrandId == sale.BrandId);

                        if (managerBrand == null)
                            continue;

                        totalSales += sale.Amount;
                    }

                    // choose percent for this company
                    var firstBrandId = companyGroup.Select(x => x.BrandId).FirstOrDefault();
                    var companyBrand = manager.ManagerBrands
                        .FirstOrDefault(mb => mb.BrandId == firstBrandId);

                    double percent = companyBrand?.SalesPercentage ?? 0;

                    decimal netSales = totalSales - totalReturn;
                    if (netSales < 0)
                        netSales = 0;

                    totalCommission = netSales * (decimal)(percent / 100.0);

                    row.CompanyData[company] = new CompanySalesInfo
                    {
                        SalesAmount = totalSales,
                        ReturnAmount = totalReturn,
                        Percentage = percent,
                        CommissionAmount = totalCommission
                    };
                }

                // if there are return-only companies with no sales in current filtered period
                var returnOnlyCompanies = returnDetailsRaw
                    .Where(r => r.ManagerId == manager.Id)
                    .Select(r => r.Company ?? "Неизвестный")
                    .Distinct()
                    .Where(c => !row.CompanyData.ContainsKey(c));

                foreach (var company in returnOnlyCompanies)
                {
                    decimal totalReturn = returnDetailsRaw
                        .Where(r => r.ManagerId == manager.Id && (r.Company ?? "Неизвестный") == company)
                        .Sum(r => r.Amount);

                    row.CompanyData[company] = new CompanySalesInfo
                    {
                        SalesAmount = 0m,
                        ReturnAmount = totalReturn,
                        Percentage = 0,
                        CommissionAmount = 0m
                    };
                }

                row.TotalReturnAmount = row.CompanyData.Values.Sum(x => x.ReturnAmount);

                result.Add(row);
            }

            return result;
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Error in GetManagerCommissionReportAsync: " + ex);
            return new List<SalesManagerReportRow>();
        }
    }


    public async Task<List<SalesDynamicsDto>> GetSalesDynamicsAsync(
      DateTime? fromDate = null,
      DateTime? toDate = null)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        try
        {
            var query = db.OrderDetails
                .Include(d => d.Order)
                    .ThenInclude(o => o.Customer)
                .AsQueryable();

            // DATE FILTER (FULL DAY SAFE)
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

            var result = await query
                .GroupBy(d => new
                {
                    Year = d.Order.Date.Year,
                    Month = d.Order.Date.Month,
                    Region = d.Order.Customer!.Region,
                    Firma = d.Product.Brand.BrandName
                })
                .Select(g => new SalesDynamicsDto
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    Region = g.Key.Region ?? "",
                    Firma = g.Key.Firma,
                    Total = g.Sum(x => (decimal)x.Price * x.Quentity)
                })
                .OrderBy(x => x.Year)
                .ThenBy(x => x.Month)
                .ThenBy(x => x.Region)
                .ToListAsync();

            return result;
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Error in GetSalesDynamicsAsync: " + ex);
            return new List<SalesDynamicsDto>();
        }
    }

    public async Task<List<SalesPaymentDto>> GetSalesVsPaymentsReportAsync(
      DateTime? fromDate = null,
      DateTime? toDate = null,
      string? region = null,
      string? firma = null)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        try
        {
            // =========================
            // SALES (RAW LOAD FIRST)
            // =========================
            var salesRaw = await db.OrderDetails
                .Include(d => d.Order)
                    .ThenInclude(o => o.Customer)
                .Include(d => d.Product)
                    .ThenInclude(p => p.Brand)
                .Where(d => !d.Order.IsBarter)
                .ToListAsync(); // 🔥 move to memory first (fix cast issue)

            var sales = salesRaw
                .Select(d =>
                {
                    DateTime orderDate;

                    // ✅ SAFE DATE PARSE
                    if (d.Order.Date is DateTime dt)
                        orderDate = dt;
                    else
                        orderDate = Convert.ToDateTime(d.Order.Date);

                    return new SalesPaymentDto
                    {
                        CustomerCode = d.Order.CustomerId,
                        FullName = d.Order.Customer.FullName,
                        Region = d.Order.Customer?.Region ?? "",
                        Firma = d.Product?.Brand?.BrandName ?? "",

                        OrderDate = orderDate,
                        PaymentDate = DateTime.MinValue,

                        Sales = (decimal)d.Price * d.Quentity,
                        Payments = 0
                    };
                })
                // ================= FILTERS (IN MEMORY)
                .Where(x =>
                    (!fromDate.HasValue || x.OrderDate >= fromDate.Value.Date) &&
                    (!toDate.HasValue || x.OrderDate < toDate.Value.Date.AddDays(1)) &&
                    (string.IsNullOrWhiteSpace(region) || region == "Всё" || x.Region == region) &&
                    (string.IsNullOrWhiteSpace(firma) || firma == "Всё" || x.Firma == firma)
                )
                .ToList();

            // =========================
            // PAYMENTS
            // =========================
            var paymentsRaw = await db.Set<CustomerPaymentEntity>()
                .Include(p => p.Customer)
                .AsNoTracking()
                .ToListAsync();

            var payments = paymentsRaw
                .Select(p => new SalesPaymentDto
                {
                    CustomerCode = p.CustomerId,
                    FullName = p.Customer.FullName,
                    Region = p.Customer?.Region ?? "",
                    Firma = "", // payments don't have firma

                    OrderDate = DateTime.MinValue,
                    PaymentDate = p.Date,

                    Sales = 0,
                    Payments = p.Amount
                })
                // ================= FILTERS
                .Where(x =>
                    (!fromDate.HasValue || x.PaymentDate >= fromDate.Value.Date) &&
                    (!toDate.HasValue || x.PaymentDate < toDate.Value.Date.AddDays(1)) &&
                    (string.IsNullOrWhiteSpace(region) || region == "Всё" || x.Region == region)
                )
                .ToList();

            // =========================
            // MERGE
            // =========================
            var result = sales
                .Concat(payments)
                .ToList();

            return result;
        }
        catch (Exception ex)
        {
            // 👉 optional: log error
            return new List<SalesPaymentDto>();
        }
    }


}