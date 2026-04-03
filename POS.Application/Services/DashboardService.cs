using Microsoft.EntityFrameworkCore;
using POS.Core.Enums;
using POS.Application.DTOs;
using POS.Infrastructure.Data;

namespace POS.Application.Services;

public class DashboardService
{
    private readonly AppDbContext _db;
    public DashboardService(AppDbContext db) => _db = db;

    public async Task<DashboardDto> GetDashboardAsync(int? branchId = null)
    {
        var today = DateTime.UtcNow.Date;
        var salesQuery = _db.Sales.Where(s => s.Status == SaleStatus.Completed).AsQueryable();
        if (branchId.HasValue) salesQuery = salesQuery.Where(s => s.BranchId == branchId.Value);

        var todaySales = await salesQuery.Where(s => s.CreatedAt >= today).ToListAsync();
        var todayRevenue = todaySales.Sum(s => s.Total);
        var todayCount = todaySales.Count;

        var thirtyDaysAgo = today.AddDays(-29);
        var recentSales = await salesQuery.Where(s => s.CreatedAt >= thirtyDaysAgo).ToListAsync();
        var dailySales = recentSales.GroupBy(s => s.CreatedAt.Date).OrderBy(g => g.Key)
            .Select(g => new DailySalesDto { Date = g.Key.ToString("MMM dd"), Total = g.Sum(s => s.Total) }).ToList();

        var topProductId = await _db.SaleItems
            .Where(si => si.Sale.CreatedAt >= today && (branchId == null || si.Sale.BranchId == branchId))
            .GroupBy(si => si.ProductId).OrderByDescending(g => g.Sum(i => i.Qty))
            .Select(g => g.Key).FirstOrDefaultAsync();
        var topProduct = topProductId > 0 ? (await _db.Products.FindAsync(topProductId))?.Name ?? "" : "";

        var lowStockQuery = _db.StockItems.Include(s => s.Product).Include(s => s.Branch).Where(s => s.QtyOnHand <= s.ReorderLevel);
        if (branchId.HasValue) lowStockQuery = lowStockQuery.Where(s => s.BranchId == branchId.Value);
        var lowStock = await lowStockQuery.Take(10).Select(s => new LowStockDto { ProductId = s.ProductId, ProductName = s.Product.Name, QtyOnHand = s.QtyOnHand, ReorderLevel = s.ReorderLevel, BranchName = s.Branch.Name }).ToListAsync();

        var activeProducts = await _db.Products.CountAsync(p => p.IsActive);
        var pendingPOs = await _db.PurchaseOrders.CountAsync(po => po.Status == POS.Core.Enums.PurchaseOrderStatus.Draft || po.Status == POS.Core.Enums.PurchaseOrderStatus.Ordered);

        return new DashboardDto { TodayRevenue = todayRevenue, TodayTransactions = todayCount, AvgOrderValue = todayCount > 0 ? todayRevenue / todayCount : 0, TopProduct = topProduct, DailySales = dailySales, LowStockItems = lowStock, ActiveProducts = activeProducts, PendingPurchaseOrders = pendingPOs };
    }
}
