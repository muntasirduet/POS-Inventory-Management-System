using Microsoft.EntityFrameworkCore;
using POS.Core.Entities;
using POS.Application.DTOs;
using POS.Infrastructure.Data;

namespace POS.Application.Services;

public class ProductService
{
    private readonly AppDbContext _db;
    public ProductService(AppDbContext db) => _db = db;

    public async Task<List<ProductDto>> GetAllAsync(string? search = null)
    {
        var query = _db.Products
            .Include(p => p.Category)
                .ThenInclude(cat => cat.DefaultTaxGroup!)
                    .ThenInclude(dtg => dtg.Components)
                        .ThenInclude(c => c.TaxComponent)
            .Include(p => p.TaxRate)
            .Include(p => p.TaxGroup!)
                .ThenInclude(tg => tg.Components)
                    .ThenInclude(c => c.TaxComponent)
            .Include(p => p.Variants)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p => p.Name.Contains(search) || p.SKU.Contains(search) ||
                                     (p.Barcode != null && p.Barcode.Contains(search)));

        var products = await query.ToListAsync();

        // Load active category-level auto-discounts
        var now = DateTime.UtcNow;
        var catIds = products.Select(p => p.CategoryId).Distinct().ToList();
        var discounts = await _db.Discounts
            .Where(d => d.IsActive && d.CategoryId.HasValue && catIds.Contains(d.CategoryId.Value)
                && (d.StartDate == null || d.StartDate <= now)
                && (d.EndDate == null || d.EndDate >= now))
            .ToListAsync();
        var discountByCat = discounts
            .GroupBy(d => d.CategoryId!.Value)
            .ToDictionary(g => g.Key, g => g.First());

        return products.Select(p => MapToDto(p, discountByCat)).ToList();
    }

    public async Task<Product?> GetByIdAsync(int id) =>
        await _db.Products
            .Include(p => p.Category)
            .Include(p => p.TaxRate)
            .Include(p => p.TaxGroup!).ThenInclude(tg => tg.Components).ThenInclude(c => c.TaxComponent)
            .Include(p => p.Variants)
            .FirstOrDefaultAsync(p => p.Id == id);

    public async Task<Product> CreateAsync(Product product)
    {
        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        return product;
    }

    public async Task UpdateAsync(Product product)
    {
        _db.Products.Update(product);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var product = await _db.Products.FindAsync(id);
        if (product != null) { product.IsDeleted = true; product.DeletedAt = DateTime.UtcNow; await _db.SaveChangesAsync(); }
    }

    public async Task<ProductDto?> SearchByBarcodeAsync(string barcode)
    {
        var product = await _db.Products
            .Include(p => p.TaxRate)
            .Include(p => p.TaxGroup!).ThenInclude(tg => tg.Components).ThenInclude(c => c.TaxComponent)
            .Include(p => p.Category)
                .ThenInclude(cat => cat.DefaultTaxGroup!)
                    .ThenInclude(dtg => dtg.Components).ThenInclude(c => c.TaxComponent)
            .Include(p => p.Variants)
            .Where(p => p.Barcode == barcode && p.IsActive)
            .FirstOrDefaultAsync();
        return product == null ? null : MapToDto(product, new());
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    internal static ProductDto MapToDto(Product p, Dictionary<int, Discount> discountByCat)
    {
        var taxGroup = p.TaxGroup ?? p.Category?.DefaultTaxGroup;
        decimal effectiveRate = 0;
        bool isInclusive = false;
        int? tgId = null;
        string? tgName = null;

        if (taxGroup?.Components != null && taxGroup.Components.Count > 0)
        {
            tgId = taxGroup.Id;
            tgName = taxGroup.Name;
            // Exclusive rate sum for POS display (inclusive taxes are already in the price)
            effectiveRate = taxGroup.Components
                .Where(c => !c.TaxComponent.IsInclusive)
                .Sum(c => c.TaxComponent.Rate);
            isInclusive = taxGroup.Components.Any(c => c.TaxComponent.IsInclusive);
        }
        else
        {
            effectiveRate = p.TaxRate?.Rate ?? 0;
        }

        string? autoDiscType = null;
        decimal autoDiscValue = 0;
        if (discountByCat.TryGetValue(p.CategoryId, out var disc))
        {
            autoDiscType = disc.Type.ToString();
            autoDiscValue = disc.Value;
        }

        return new ProductDto
        {
            Id = p.Id, Name = p.Name, SKU = p.SKU, Barcode = p.Barcode,
            Price = p.Price, CategoryId = p.CategoryId, CategoryName = p.Category?.Name,
            TaxRateId = p.TaxRateId, TaxRateName = p.TaxRate?.Name, TaxRateValue = p.TaxRate?.Rate,
            TaxGroupId = tgId, TaxGroupName = tgName,
            EffectiveTaxRate = effectiveRate, TaxIsInclusive = isInclusive,
            ImageUrl = p.ImageUrl, IsActive = p.IsActive, Description = p.Description,
            AutoDiscountType = autoDiscType, AutoDiscountValue = autoDiscValue,
            HasVariants = p.Variants?.Any(v => v.IsActive) ?? false
        };
    }
}
