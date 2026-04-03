using Microsoft.EntityFrameworkCore;
using POS.Core.Entities;
using POS.Core.Enums;
using POS.Application.DTOs;
using POS.Infrastructure.Data;

namespace POS.Application.Services;

public class SaleService
{
    private readonly AppDbContext _db;
    public SaleService(AppDbContext db) => _db = db;

    public async Task<Sale> CreateSaleAsync(List<CartItemDto> cartItems, List<PaymentDto> payments,
        string cashierId, int? customerId, int? branchId, decimal discount = 0, string? couponCode = null)
    {
        decimal subTotal = cartItems.Sum(i => i.LineTotal);
        decimal taxAmount = 0;
        foreach (var item in cartItems)
        {
            var product = await _db.Products.Include(p => p.TaxRate).FirstOrDefaultAsync(p => p.Id == item.ProductId);
            if (product?.TaxRate != null) taxAmount += item.LineTotal * product.TaxRate.Rate;
        }
        decimal total = subTotal + taxAmount - discount;
        var sale = new Sale
        {
            SaleNumber = GenerateSaleNumber(), CashierId = cashierId, CustomerId = customerId,
            BranchId = branchId, SubTotal = subTotal, Tax = taxAmount, Discount = discount,
            Total = Math.Max(0, total), Status = SaleStatus.Completed
        };
        _db.Sales.Add(sale);
        await _db.SaveChangesAsync();

        foreach (var item in cartItems)
        {
            _db.SaleItems.Add(new SaleItem { SaleId = sale.Id, ProductId = item.ProductId, VariantId = item.VariantId, Qty = item.Qty, UnitPrice = item.UnitPrice, Discount = item.Discount, LineTotal = item.LineTotal });
            var stock = await _db.StockItems.FirstOrDefaultAsync(s => s.ProductId == item.ProductId && s.BranchId == branchId);
            if (stock != null) stock.QtyOnHand = Math.Max(0, stock.QtyOnHand - item.Qty);
            if (branchId.HasValue)
                _db.StockMovements.Add(new StockMovement { ProductId = item.ProductId, ToBranchId = branchId, Qty = item.Qty, Type = StockMovementType.Sale, Reference = sale.SaleNumber, CreatedById = cashierId });
        }

        foreach (var p in payments)
            _db.Payments.Add(new Payment { SaleId = sale.Id, Method = p.Method, Amount = p.Amount, Change = p.Change, Reference = p.Reference });

        _db.Receipts.Add(new Receipt { SaleId = sale.Id, ReceiptNumber = "RCP-" + sale.SaleNumber });

        if (customerId.HasValue)
        {
            var loyalty = await _db.LoyaltyAccounts.FirstOrDefaultAsync(la => la.CustomerId == customerId.Value);
            if (loyalty != null)
            {
                var config = await _db.SystemConfigs.FirstOrDefaultAsync(c => c.Key == "LoyaltyPointsPerDollar");
                int pts = int.TryParse(config?.Value, out var p2) ? p2 : 1;
                loyalty.Points += (int)(total * pts);
                loyalty.TotalSpend += total;
            }
        }

        if (!string.IsNullOrWhiteSpace(couponCode))
        {
            var coupon = await _db.Coupons.FirstOrDefaultAsync(c => c.Code == couponCode && !c.IsUsed);
            if (coupon != null) coupon.IsUsed = true;
        }

        await _db.SaveChangesAsync();
        return sale;
    }

    public async Task<Sale> HoldSaleAsync(List<CartItemDto> cartItems, string cashierId, int? customerId, int? branchId)
    {
        var sale = new Sale { SaleNumber = GenerateSaleNumber(), CashierId = cashierId, CustomerId = customerId, BranchId = branchId, SubTotal = cartItems.Sum(i => i.LineTotal), Total = cartItems.Sum(i => i.LineTotal), Status = SaleStatus.OnHold };
        _db.Sales.Add(sale);
        await _db.SaveChangesAsync();
        foreach (var item in cartItems)
            _db.SaleItems.Add(new SaleItem { SaleId = sale.Id, ProductId = item.ProductId, VariantId = item.VariantId, Qty = item.Qty, UnitPrice = item.UnitPrice, Discount = item.Discount, LineTotal = item.LineTotal });
        await _db.SaveChangesAsync();
        return sale;
    }

    public async Task<SaleDto?> GetSaleAsync(int id)
    {
        var sale = await _db.Sales.Include(s => s.Items).ThenInclude(i => i.Product).Include(s => s.Payments).Include(s => s.Customer).FirstOrDefaultAsync(s => s.Id == id);
        return sale == null ? null : MapToDto(sale);
    }

    public async Task<List<SaleDto>> GetOnHoldSalesAsync(string cashierId)
    {
        var sales = await _db.Sales.Include(s => s.Items).ThenInclude(i => i.Product).Include(s => s.Customer)
            .Where(s => s.CashierId == cashierId && s.Status == SaleStatus.OnHold).ToListAsync();
        return sales.Select(MapToDto).ToList();
    }

    public async Task<List<SaleDto>> GetSalesAsync(DateTime? from, DateTime? to, int? branchId, string? cashierId)
    {
        var query = _db.Sales.Include(s => s.Customer).AsQueryable();
        if (from.HasValue) query = query.Where(s => s.CreatedAt >= from.Value);
        if (to.HasValue) query = query.Where(s => s.CreatedAt <= to.Value.AddDays(1));
        if (branchId.HasValue) query = query.Where(s => s.BranchId == branchId.Value);
        if (!string.IsNullOrWhiteSpace(cashierId)) query = query.Where(s => s.CashierId == cashierId);
        var sales = await query.OrderByDescending(s => s.CreatedAt).ToListAsync();
        return sales.Select(MapToDto).ToList();
    }

    private static string GenerateSaleNumber() => $"SALE-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";

    private static SaleDto MapToDto(Sale sale) => new()
    {
        Id = sale.Id, SaleNumber = sale.SaleNumber, CashierId = sale.CashierId, CustomerId = sale.CustomerId,
        CustomerName = sale.Customer?.Name, BranchId = sale.BranchId, SubTotal = sale.SubTotal,
        Tax = sale.Tax, Discount = sale.Discount, Total = sale.Total, Status = sale.Status, CreatedAt = sale.CreatedAt,
        Items = sale.Items.Select(i => new SaleItemDto { ProductId = i.ProductId, ProductName = i.Product?.Name ?? "", VariantId = i.VariantId, Qty = i.Qty, UnitPrice = i.UnitPrice, Discount = i.Discount, LineTotal = i.LineTotal }).ToList(),
        Payments = sale.Payments.Select(p => new PaymentDto { Method = p.Method, Amount = p.Amount, Change = p.Change, Reference = p.Reference }).ToList()
    };
}
