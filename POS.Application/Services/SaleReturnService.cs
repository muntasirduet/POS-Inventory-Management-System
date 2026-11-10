using Microsoft.EntityFrameworkCore;
using POS.Core.Entities;
using POS.Core.Enums;
using POS.Infrastructure.Data;

namespace POS.Application.Services;

public class SaleReturnService
{
    private readonly AppDbContext _db;
    public SaleReturnService(AppDbContext db) => _db = db;

    /// <summary>
    /// Creates a return for specified sale items. Restocks if RestockItems=true.
    /// </summary>
    public async Task<SaleReturn> CreateReturnAsync(
        int saleId,
        string requestedById,
        string reason,
        string refundMethod,
        bool restockItems,
        string? notes,
        List<(int saleItemId, int returnQty)> returnLines)
    {
        var sale = await _db.Sales
            .Include(s => s.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(s => s.Id == saleId)
            ?? throw new InvalidOperationException("Sale not found.");

        if (sale.Status != SaleStatus.Completed)
            throw new InvalidOperationException("Only completed sales can be returned.");

        // Build return items and validate quantities
        decimal totalRefund = 0;
        var returnItems = new List<SaleReturnItem>();
        foreach (var (saleItemId, returnQty) in returnLines)
        {
            if (returnQty <= 0) continue;
            var saleItem = sale.Items.FirstOrDefault(i => i.Id == saleItemId)
                ?? throw new InvalidOperationException($"SaleItem {saleItemId} not in sale.");

            // Check how many have already been returned for this item
            var alreadyReturned = await _db.SaleReturnItems
                .Where(sri => sri.SaleItemId == saleItemId)
                .SumAsync(sri => sri.ReturnQty);
            int available = saleItem.Qty - alreadyReturned;
            if (returnQty > available)
                throw new InvalidOperationException($"Cannot return {returnQty} of '{saleItem.Product?.Name}' — only {available} available.");

            var lineRefund = saleItem.UnitPrice * returnQty;
            totalRefund += lineRefund;
            returnItems.Add(new SaleReturnItem
            {
                SaleItemId = saleItemId,
                ProductId = saleItem.ProductId,
                VariantId = saleItem.VariantId,
                ReturnQty = returnQty,
                UnitPrice = saleItem.UnitPrice
            });
        }

        if (!returnItems.Any())
            throw new InvalidOperationException("No valid return lines.");

        var saleReturn = new SaleReturn
        {
            SaleId = saleId,
            RequestedById = requestedById,
            RefundAmount = totalRefund,
            Reason = reason,
            RefundMethod = refundMethod,
            Notes = notes,
            RestockItems = restockItems,
            IsApproved = true,
            ApprovedById = requestedById,
            ApprovedAt = DateTime.UtcNow,
            Items = returnItems
        };
        _db.SaleReturns.Add(saleReturn);

        if (restockItems)
        {
            foreach (var item in returnItems)
            {
                var stock = await _db.StockItems.FirstOrDefaultAsync(s => s.ProductId == item.ProductId && s.BranchId == sale.BranchId);
                if (stock != null)
                    stock.QtyOnHand += item.ReturnQty;
                else if (sale.BranchId.HasValue)
                    _db.StockItems.Add(new StockItem { ProductId = item.ProductId, BranchId = sale.BranchId.Value, QtyOnHand = item.ReturnQty });

                if (sale.BranchId.HasValue)
                    _db.StockMovements.Add(new StockMovement
                    {
                        ProductId = item.ProductId,
                        ToBranchId = sale.BranchId,
                        Qty = item.ReturnQty,
                        Type = StockMovementType.Return,
                        Reference = sale.SaleNumber,
                        Notes = $"Return: {reason}",
                        CreatedById = requestedById
                    });
            }
        }

        await _db.SaveChangesAsync();

        // --- Issue store credit if selected ---
        if (refundMethod == "StoreCredit" && sale.CustomerId.HasValue)
        {
            var account = await _db.StoreCreditAccounts
                .FirstOrDefaultAsync(sc => sc.CustomerId == sale.CustomerId.Value);
            if (account == null)
            {
                account = new StoreCreditAccount { CustomerId = sale.CustomerId.Value };
                _db.StoreCreditAccounts.Add(account);
                await _db.SaveChangesAsync();
            }
            account.Balance += totalRefund;
            account.TotalIssued += totalRefund;
            _db.StoreCreditTransactions.Add(new StoreCreditTransaction
            {
                StoreCreditAccountId = account.Id, Type = "Issue",
                Amount = totalRefund, BalanceAfter = account.Balance,
                SaleId = saleId, Notes = $"Refund from return — {reason}"
            });
            await _db.SaveChangesAsync();
        }

        return saleReturn;
    }

    public async Task<List<SaleReturn>> GetReturnsAsync(int? branchId, DateTime? from, DateTime? to)
    {
        var query = _db.SaleReturns
            .Include(r => r.Sale)
            .Include(r => r.Items).ThenInclude(ri => ri.Product)
            .AsQueryable();

        if (from.HasValue) query = query.Where(r => r.CreatedAt >= from.Value);
        if (to.HasValue) query = query.Where(r => r.CreatedAt <= to.Value.AddDays(1));
        if (branchId.HasValue) query = query.Where(r => r.Sale.BranchId == branchId.Value);

        return await query.OrderByDescending(r => r.CreatedAt).ToListAsync();
    }

    public async Task<SaleReturn?> GetReturnAsync(int id) =>
        await _db.SaleReturns
            .Include(r => r.Sale).ThenInclude(s => s.Items).ThenInclude(i => i.Product)
            .Include(r => r.Items).ThenInclude(ri => ri.Product)
            .FirstOrDefaultAsync(r => r.Id == id);
}
