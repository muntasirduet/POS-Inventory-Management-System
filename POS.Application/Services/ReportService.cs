using Microsoft.EntityFrameworkCore;
using POS.Core.Enums;
using POS.Application.DTOs;
using POS.Infrastructure.Data;

namespace POS.Application.Services;

public class ReportService
{
    private readonly AppDbContext _db;
    public ReportService(AppDbContext db) => _db = db;

    public async Task<SalesReportDto> GetSalesReportAsync(DateTime? from, DateTime? to, int? branchId)
    {
        var query = _db.Sales.Include(s => s.Customer).AsQueryable();
        if (from.HasValue) query = query.Where(s => s.CreatedAt >= from.Value);
        if (to.HasValue) query = query.Where(s => s.CreatedAt <= to.Value.AddDays(1));
        if (branchId.HasValue) query = query.Where(s => s.BranchId == branchId.Value);
        var sales = await query.OrderByDescending(s => s.CreatedAt).ToListAsync();
        var users = await _db.Users.ToDictionaryAsync(u => u.Id, u => u.UserName ?? u.Email ?? u.Id);
        return new SalesReportDto
        {
            TotalRevenue = sales.Where(s => s.Status == SaleStatus.Completed).Sum(s => s.Total),
            TotalTax = sales.Where(s => s.Status == SaleStatus.Completed).Sum(s => s.Tax),
            TotalDiscount = sales.Sum(s => s.Discount),
            NetRevenue = sales.Where(s => s.Status == SaleStatus.Completed).Sum(s => s.Total - s.Tax),
            TransactionCount = sales.Count(s => s.Status == SaleStatus.Completed),
            ReturnCount = sales.Count(s => s.Status == SaleStatus.Returned),
            Rows = sales.Select(s => new SaleReportRow { SaleNumber = s.SaleNumber, Date = s.CreatedAt, Cashier = users.TryGetValue(s.CashierId, out var cn) ? cn! : s.CashierId, Customer = s.Customer?.Name, Total = s.Total, Status = s.Status.ToString() }).ToList()
        };
    }

    public async Task<InventoryReportDto> GetInventoryReportAsync(int? branchId)
    {
        var query = _db.StockItems.Include(s => s.Product).Include(s => s.Branch).AsQueryable();
        if (branchId.HasValue) query = query.Where(s => s.BranchId == branchId.Value);
        var items = await query.ToListAsync();
        return new InventoryReportDto { Rows = items.Select(s => new InventoryReportRow { ProductName = s.Product.Name, SKU = s.Product.SKU, Branch = s.Branch.Name, QtyOnHand = s.QtyOnHand, ReorderLevel = s.ReorderLevel, UnitPrice = s.Product.Price, StockValue = s.QtyOnHand * s.Product.Price }).ToList() };
    }
}
