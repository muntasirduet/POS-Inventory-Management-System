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
        var query = _db.Products.Include(p => p.Category).Include(p => p.TaxRate).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p => p.Name.Contains(search) || p.SKU.Contains(search) || (p.Barcode != null && p.Barcode.Contains(search)));
        return await query.Select(p => new ProductDto
        {
            Id = p.Id, Name = p.Name, SKU = p.SKU, Barcode = p.Barcode, Price = p.Price,
            CategoryId = p.CategoryId, CategoryName = p.Category.Name,
            TaxRateId = p.TaxRateId, TaxRateName = p.TaxRate != null ? p.TaxRate.Name : null,
            TaxRateValue = p.TaxRate != null ? p.TaxRate.Rate : null,
            ImageUrl = p.ImageUrl, IsActive = p.IsActive, Description = p.Description
        }).ToListAsync();
    }

    public async Task<Product?> GetByIdAsync(int id) =>
        await _db.Products.Include(p => p.Category).Include(p => p.TaxRate).Include(p => p.Variants).FirstOrDefaultAsync(p => p.Id == id);

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
        return await _db.Products.Include(p => p.TaxRate)
            .Where(p => p.Barcode == barcode && p.IsActive)
            .Select(p => new ProductDto { Id = p.Id, Name = p.Name, SKU = p.SKU, Barcode = p.Barcode, Price = p.Price, TaxRateValue = p.TaxRate != null ? p.TaxRate.Rate : 0 })
            .FirstOrDefaultAsync();
    }
}
