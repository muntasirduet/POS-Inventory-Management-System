using Microsoft.EntityFrameworkCore;
using POS.Core.Entities;
using POS.Core.Enums;
using POS.Application.DTOs;
using POS.Infrastructure.Data;

namespace POS.Application.Services;

public class InventoryService
{
    private readonly AppDbContext _db;
    public InventoryService(AppDbContext db) => _db = db;

    public async Task<List<StockItem>> GetStockAsync(int? branchId = null)
    {
        var query = _db.StockItems.Include(s => s.Product).Include(s => s.Branch).AsQueryable();
        if (branchId.HasValue) query = query.Where(s => s.BranchId == branchId.Value);
        return await query.ToListAsync();
    }

    public async Task<List<LowStockDto>> GetLowStockAsync(int? branchId = null)
    {
        var query = _db.StockItems.Include(s => s.Product).Include(s => s.Branch).Where(s => s.QtyOnHand <= s.ReorderLevel);
        if (branchId.HasValue) query = query.Where(s => s.BranchId == branchId.Value);
        return await query.Select(s => new LowStockDto { ProductId = s.ProductId, ProductName = s.Product.Name, QtyOnHand = s.QtyOnHand, ReorderLevel = s.ReorderLevel, BranchName = s.Branch.Name }).ToListAsync();
    }

    public async Task TransferStockAsync(int productId, int fromBranchId, int toBranchId, int qty, string userId)
    {
        var fromStock = await _db.StockItems.FirstOrDefaultAsync(s => s.ProductId == productId && s.BranchId == fromBranchId);
        if (fromStock == null || fromStock.QtyOnHand < qty) throw new InvalidOperationException("Insufficient stock");
        fromStock.QtyOnHand -= qty;
        var toStock = await _db.StockItems.FirstOrDefaultAsync(s => s.ProductId == productId && s.BranchId == toBranchId);
        if (toStock == null) _db.StockItems.Add(new StockItem { ProductId = productId, BranchId = toBranchId, QtyOnHand = qty });
        else toStock.QtyOnHand += qty;
        _db.StockMovements.Add(new StockMovement { ProductId = productId, FromBranchId = fromBranchId, ToBranchId = toBranchId, Qty = qty, Type = StockMovementType.Transfer, CreatedById = userId });
        await _db.SaveChangesAsync();
    }

    public async Task AdjustStockAsync(int productId, int branchId, int qty, string notes, string userId)
    {
        var stock = await _db.StockItems.FirstOrDefaultAsync(s => s.ProductId == productId && s.BranchId == branchId);
        if (stock == null) _db.StockItems.Add(new StockItem { ProductId = productId, BranchId = branchId, QtyOnHand = qty });
        else { int diff = qty - stock.QtyOnHand; stock.QtyOnHand = qty; _db.StockMovements.Add(new StockMovement { ProductId = productId, ToBranchId = branchId, Qty = diff, Type = StockMovementType.Adjustment, Notes = notes, CreatedById = userId }); }
        await _db.SaveChangesAsync();
    }
}
