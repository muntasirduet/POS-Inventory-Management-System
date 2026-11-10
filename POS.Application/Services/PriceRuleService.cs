using Microsoft.EntityFrameworkCore;
using POS.Core.Entities;
using POS.Infrastructure.Data;

namespace POS.Application.Services;

public class PriceRuleService
{
    private readonly AppDbContext _db;
    public PriceRuleService(AppDbContext db) => _db = db;

    /// <summary>
    /// Returns the best (highest-priority) applicable price rule for a product/customer group.
    /// Checks: product-specific rule first, then category-level rule.
    /// </summary>
    public async Task<PriceRule?> GetBestRuleAsync(int productId, int categoryId, int? customerGroupId)
    {
        if (customerGroupId == null) return null;
        var now = DateTime.UtcNow;
        return await _db.PriceRules
            .Where(pr => pr.IsActive
                && pr.CustomerGroupId == customerGroupId
                && (pr.StartDate == null || pr.StartDate <= now)
                && (pr.EndDate == null || pr.EndDate >= now)
                && (pr.ProductId == productId || pr.CategoryId == categoryId || (pr.ProductId == null && pr.CategoryId == null)))
            .OrderByDescending(pr => pr.Priority)
            .ThenBy(pr => pr.ProductId == null ? 1 : 0) // product-specific beats category
            .FirstOrDefaultAsync();
    }

    /// <summary>Applies a price rule to a unit price. Returns adjusted unit price.</summary>
    public static decimal ApplyRule(PriceRule rule, decimal unitPrice)
    {
        return rule.DiscountType switch
        {
            "Percentage"   => Math.Max(0, unitPrice * (1 - rule.Value / 100)),
            "FixedAmount"  => Math.Max(0, unitPrice - rule.Value),
            "FixedPrice"   => rule.Value,
            _              => unitPrice
        };
    }
}
