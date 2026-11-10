using Microsoft.EntityFrameworkCore;
using POS.Core.Entities;
using POS.Core.Enums;
using POS.Application.DTOs;
using POS.Infrastructure.Data;

namespace POS.Application.Services;

public class SaleService
{
    private readonly AppDbContext _db;
    private readonly PriceRuleService _priceRuleService;
    public SaleService(AppDbContext db, PriceRuleService priceRuleService)
    {
        _db = db;
        _priceRuleService = priceRuleService;
    }

    /// <summary>Calculates total tax for a line amount from a TaxGroup's components.
    /// Handles inclusive, exclusive, and compound components in sort-order sequence.
    /// </summary>
    private static decimal CalculateTaxFromGroup(TaxGroup taxGroup, decimal lineAmount)
    {
        decimal taxTotal = 0;
        var components = taxGroup.Components
            .OrderBy(c => c.TaxComponent.SortOrder)
            .Select(c => c.TaxComponent)
            .ToList();
        foreach (var comp in components)
        {
            if (comp.IsInclusive)
                taxTotal += lineAmount - lineAmount / (1 + comp.Rate);   // extract inclusive tax
            else if (comp.IsCompound)
                taxTotal += (lineAmount + taxTotal) * comp.Rate;          // compound on running base
            else
                taxTotal += lineAmount * comp.Rate;                        // simple exclusive
        }
        return taxTotal;
    }

    private static decimal GetEffectiveTaxRate(TaxGroup? tg, TaxRate? tr)
    {
        if (tg?.Components != null && tg.Components.Count > 0)
            return tg.Components.Where(c => !c.TaxComponent.IsInclusive).Sum(c => c.TaxComponent.Rate);
        return tr?.Rate ?? 0;
    }

    public async Task<Sale> CreateSaleAsync(List<CartItemDto> cartItems, List<PaymentDto> payments,
        string cashierId, int? customerId, int? branchId, decimal discount = 0, string? couponCode = null)
    {
        // --- Resolve customer and group ---
        int? customerGroupId = null;
        if (customerId.HasValue)
        {
            var cust = await _db.Customers.FindAsync(customerId.Value);
            customerGroupId = cust?.CustomerGroupId;
        }

        // --- Pre-load products once (used for price rules, tax calc & stock) ---
        var productCache = new Dictionary<int, Product?>();
        foreach (var item in cartItems)
        {
            if (!productCache.ContainsKey(item.ProductId))
            {
                var p = await _db.Products
                    .Include(pr => pr.TaxRate)
                    .Include(pr => pr.TaxGroup!).ThenInclude(tg => tg.Components).ThenInclude(c => c.TaxComponent)
                    .Include(pr => pr.Category).ThenInclude(cat => cat.DefaultTaxGroup!)
                        .ThenInclude(dtg => dtg.Components).ThenInclude(c => c.TaxComponent)
                    .FirstOrDefaultAsync(pr => pr.Id == item.ProductId);
                productCache[item.ProductId] = p;
            }
        }

        // --- Apply price rules per item (server-side authoritative) ---
        foreach (var item in cartItems)
        {
            if (customerGroupId.HasValue && item.ProductId > 0)
            {
                var catId = productCache.TryGetValue(item.ProductId, out var pp) ? pp?.CategoryId ?? 0 : 0;
                var rule = await _priceRuleService.GetBestRuleAsync(item.ProductId, catId, customerGroupId);
                if (rule != null) item.UnitPrice = PriceRuleService.ApplyRule(rule, item.UnitPrice);
            }
        }

        decimal subTotal = cartItems.Sum(i => i.LineTotal);

        // --- Calculate tax using TaxGroup components or legacy TaxRate ---
        decimal taxAmount = 0;
        foreach (var item in cartItems)
        {
            if (productCache.TryGetValue(item.ProductId, out var product))
            {
                var taxGroup = product?.TaxGroup ?? product?.Category?.DefaultTaxGroup;
                if (taxGroup?.Components is { Count: > 0 })
                    taxAmount += CalculateTaxFromGroup(taxGroup, item.LineTotal);
                else if (product?.TaxRate != null)
                    taxAmount += item.LineTotal * product.TaxRate.Rate;
            }
        }

        // --- Apply CustomerGroup default discount if no explicit discount ---
        if (discount == 0 && customerGroupId.HasValue)
        {
            var grp = await _db.CustomerGroups.FindAsync(customerGroupId.Value);
            if (grp != null && grp.DefaultDiscountPct > 0)
                discount = subTotal * grp.DefaultDiscountPct / 100;
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

        // --- Save items + update inventory ---
        foreach (var item in cartItems)
        {
            // Compute tax-rate snapshot for this item
            productCache.TryGetValue(item.ProductId, out var prod);
            var tg2 = prod?.TaxGroup ?? prod?.Category?.DefaultTaxGroup;
            decimal taxSnap = GetEffectiveTaxRate(tg2, prod?.TaxRate);

            _db.SaleItems.Add(new SaleItem
            {
                SaleId = sale.Id, ProductId = item.ProductId, VariantId = item.VariantId,
                Qty = item.Qty, UnitPrice = item.UnitPrice, Discount = item.Discount,
                LineTotal = item.LineTotal, TaxRateSnapshot = taxSnap,
                UnitCostSnapshot = prod?.CostPrice ?? 0
            });

            // ── Variant-level stock deduction ─────────────────────────────────
            if (item.VariantId.HasValue)
            {
                var variant = await _db.ProductVariants.FindAsync(item.VariantId.Value);
                if (variant != null)
                    variant.StockQty = Math.Max(0, variant.StockQty - item.Qty);
                
                // Deduct from per-branch variant stock if available
                if (branchId.HasValue)
                {
                    var variantStock = await _db.VariantStockItems
                        .FirstOrDefaultAsync(vs => vs.VariantId == item.VariantId.Value && vs.BranchId == branchId.Value);
                    if (variantStock != null)
                        variantStock.QtyOnHand = Math.Max(0, variantStock.QtyOnHand - item.Qty);
                }
            }

            // ── Branch StockItem deduction (product-level stock) ──────────────
            if (branchId.HasValue && !item.VariantId.HasValue)  // Only for non-variant sales
            {
                var stock = await _db.StockItems
                    .FirstOrDefaultAsync(s => s.ProductId == item.ProductId && s.BranchId == branchId.Value);
                if (stock == null)
                {
                    // Auto-create stock record at 0; correct level should come from a PO/restock
                    stock = new StockItem { ProductId = item.ProductId, BranchId = branchId.Value, QtyOnHand = 0 };
                    _db.StockItems.Add(stock);
                    await _db.SaveChangesAsync();
                }
                stock.QtyOnHand = Math.Max(0, stock.QtyOnHand - item.Qty);
            }
            else if (!branchId.HasValue && !item.VariantId.HasValue)
            {
                // No branch context — deduct from any stock record for this product
                var stock = await _db.StockItems
                    .Where(s => s.ProductId == item.ProductId && s.QtyOnHand > 0)
                    .FirstOrDefaultAsync();
                if (stock != null)
                    stock.QtyOnHand = Math.Max(0, stock.QtyOnHand - item.Qty);
            }

            // ── Stock movement (FromBranchId = stock leaves the branch) ───────
            _db.StockMovements.Add(new StockMovement
            {
                ProductId = item.ProductId,
                FromBranchId = branchId,   // FIXED: was incorrectly ToBranchId
                Qty = item.Qty,
                Type = StockMovementType.Sale,
                Reference = sale.SaleNumber,
                CreatedById = cashierId,
                Notes = item.VariantId.HasValue ? $"Variant #{item.VariantId}" : null
            });
        }

        // --- Payments: handle GiftCard / StoreCredit ---
        foreach (var p in payments)
        {
            if (p.Method == PaymentMethod.GiftCard && !string.IsNullOrWhiteSpace(p.Reference))
            {
                var card = await _db.GiftCards.FirstOrDefaultAsync(gc => gc.Code == p.Reference && gc.IsActive && gc.Balance > 0);
                if (card != null)
                {
                    var redeemed = Math.Min(card.Balance, p.Amount);
                    card.Balance -= redeemed;
                    if (card.Balance == 0) card.IsActive = false;
                }
            }
            else if (p.Method == PaymentMethod.StoreCredit && customerId.HasValue)
            {
                var scAccount = await _db.StoreCreditAccounts.FirstOrDefaultAsync(sc => sc.CustomerId == customerId.Value);
                if (scAccount != null && scAccount.Balance > 0)
                {
                    var redeemed = Math.Min(scAccount.Balance, p.Amount);
                    scAccount.Balance -= redeemed;
                    _db.StoreCreditTransactions.Add(new StoreCreditTransaction
                    {
                        StoreCreditAccountId = scAccount.Id, Type = "Redeem",
                        Amount = redeemed, BalanceAfter = scAccount.Balance,
                        SaleId = sale.Id, Notes = "Redeemed on sale " + sale.SaleNumber
                    });
                }
            }
            _db.Payments.Add(new Payment { SaleId = sale.Id, Method = p.Method, Amount = p.Amount, Change = p.Change, Reference = p.Reference });
        }

        _db.Receipts.Add(new Receipt { SaleId = sale.Id, ReceiptNumber = "RCP-" + sale.SaleNumber });

        if (customerId.HasValue)
        {
            var loyalty = await _db.LoyaltyAccounts.FirstOrDefaultAsync(la => la.CustomerId == customerId.Value);
            if (loyalty != null)
            {
                var config = await _db.SystemConfigs.FirstOrDefaultAsync(c => c.Key == "LoyaltyPointsPerDollar");
                int pts = int.TryParse(config?.Value, out var p2) ? p2 : 1;
                int earned = (int)(total * pts);
                loyalty.Points += earned;
                loyalty.TotalSpend += total;

                // Tier thresholds from SystemConfig (fallback to hardcoded)
                int silverThreshold  = await GetConfigInt("LoyaltyTierSilver",  500);
                int goldThreshold    = await GetConfigInt("LoyaltyTierGold",   2000);
                int platThreshold    = await GetConfigInt("LoyaltyTierPlatinum", 5000);
                loyalty.Tier = (int)loyalty.TotalSpend switch
                {
                    var s when s >= platThreshold   => "Platinum",
                    var s when s >= goldThreshold   => "Gold",
                    var s when s >= silverThreshold => "Silver",
                    _                               => "Standard"
                };

                _db.LoyaltyTransactions.Add(new LoyaltyTransaction
                {
                    LoyaltyAccountId = loyalty.Id, Type = "Earn",
                    Points = earned, BalanceAfter = loyalty.Points,
                    SaleId = sale.Id, Notes = $"Earned from sale ${total:F2}"
                });
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

    public async Task<Sale> HoldSaleAsync(
        List<CartItemDto> cartItems, string cashierId, int? customerId, int? branchId,
        int? terminalId = null, string? holdLabel = null, string? holdNotes = null,
        string? couponCode = null, decimal manualDiscount = 0)
    {
        // Auto-expire held sales after configured hours (default 8)
        var cfg = await _db.SystemConfigs.FirstOrDefaultAsync(c => c.Key == "HoldExpiryHours");
        int holdHours = int.TryParse(cfg?.Value, out var h) ? h : 8;

        var sale = new Sale
        {
            SaleNumber = GenerateSaleNumber(),
            CashierId = cashierId,
            CustomerId = customerId,
            BranchId = branchId,
            TerminalId = terminalId,
            SubTotal = cartItems.Sum(i => i.LineTotal),
            Total = cartItems.Sum(i => i.LineTotal),
            Status = SaleStatus.OnHold,
            HoldLabel = holdLabel,
            HoldNotes = holdNotes,
            HoldCouponCode = couponCode,
            HoldDiscountAmount = manualDiscount,
            HoldExpiresAt = DateTime.UtcNow.AddHours(holdHours)
        };
        _db.Sales.Add(sale);
        await _db.SaveChangesAsync();

        foreach (var item in cartItems)
        {
            // Resolve effective tax rate snapshot from TaxGroup or legacy TaxRate
            decimal taxRate = 0;
            if (item.ProductId > 0)
            {
                var prod = await _db.Products
                    .Include(p => p.TaxRate)
                    .Include(p => p.TaxGroup!).ThenInclude(tg => tg.Components).ThenInclude(c => c.TaxComponent)
                    .Include(p => p.Category).ThenInclude(cat => cat.DefaultTaxGroup!)
                        .ThenInclude(dtg => dtg.Components).ThenInclude(c => c.TaxComponent)
                    .FirstOrDefaultAsync(p => p.Id == item.ProductId);
                var tg = prod?.TaxGroup ?? prod?.Category?.DefaultTaxGroup;
                taxRate = GetEffectiveTaxRate(tg, prod?.TaxRate);
            }
            _db.SaleItems.Add(new SaleItem
            {
                SaleId = sale.Id, ProductId = item.ProductId, VariantId = item.VariantId,
                Qty = item.Qty, UnitPrice = item.UnitPrice, Discount = item.Discount,
                LineTotal = item.LineTotal, TaxRateSnapshot = taxRate
            });
        }
        await _db.SaveChangesAsync();
        return sale;
    }

    public async Task<bool> VoidHeldSaleAsync(int saleId, string userId)
    {
        var sale = await _db.Sales.FirstOrDefaultAsync(s => s.Id == saleId && s.Status == SaleStatus.OnHold);
        if (sale == null) return false;
        sale.Status = SaleStatus.Cancelled;
        sale.UpdatedAt = DateTime.UtcNow;
        _db.AuditLogs.Add(new POS.Core.Entities.AuditLog
        {
            UserId = userId, Action = "VoidHeld", EntityType = "Sale",
            EntityId = saleId.ToString(), Timestamp = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<SaleDto?> GetSaleAsync(int id)
    {
        var sale = await _db.Sales.Include(s => s.Items).ThenInclude(i => i.Product).Include(s => s.Payments).Include(s => s.Customer).FirstOrDefaultAsync(s => s.Id == id);
        return sale == null ? null : MapToDto(sale);
    }

    public async Task<List<SaleDto>> GetOnHoldSalesAsync(string cashierId)
    {
        // Auto-cancel expired holds
        var expired = await _db.Sales
            .Where(s => s.Status == SaleStatus.OnHold && s.HoldExpiresAt.HasValue && s.HoldExpiresAt < DateTime.UtcNow)
            .ToListAsync();
        foreach (var e in expired) { e.Status = SaleStatus.Cancelled; e.UpdatedAt = DateTime.UtcNow; }
        if (expired.Any()) await _db.SaveChangesAsync();

        var sales = await _db.Sales
            .Include(s => s.Items).ThenInclude(i => i.Product)
            .Include(s => s.Customer)
            .Where(s => s.CashierId == cashierId && s.Status == SaleStatus.OnHold)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();
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

    private async Task<int> GetConfigInt(string key, int fallback)
    {
        var cfg = await _db.SystemConfigs.FirstOrDefaultAsync(c => c.Key == key);
        return int.TryParse(cfg?.Value, out var v) ? v : fallback;
    }

    private static SaleDto MapToDto(Sale sale) => new()
    {
        Id = sale.Id, SaleNumber = sale.SaleNumber, CashierId = sale.CashierId, CustomerId = sale.CustomerId,
        CustomerName = sale.Customer?.Name, BranchId = sale.BranchId, SubTotal = sale.SubTotal,
        Tax = sale.Tax, Discount = sale.Discount, Total = sale.Total, Status = sale.Status, CreatedAt = sale.CreatedAt,
        HoldLabel = sale.HoldLabel, HoldNotes = sale.HoldNotes,
        HoldCouponCode = sale.HoldCouponCode, HoldDiscountAmount = sale.HoldDiscountAmount,
        HoldExpiresAt = sale.HoldExpiresAt,
        Items = sale.Items.Select(i => new SaleItemDto
        {
            ProductId = i.ProductId, ProductName = i.Product?.Name ?? "",
            VariantId = i.VariantId, Qty = i.Qty, UnitPrice = i.UnitPrice,
            Discount = i.Discount, LineTotal = i.LineTotal, TaxRateSnapshot = i.TaxRateSnapshot
        }).ToList(),
        Payments = sale.Payments.Select(p => new PaymentDto { Method = p.Method, Amount = p.Amount, Change = p.Change, Reference = p.Reference }).ToList()
    };
}
