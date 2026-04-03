using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using POS.Application.Services;
using POS.Core.Entities;
using POS.Infrastructure.Data;
using POS.Web.Authorization;

namespace POS.Web.Controllers;

[Authorize]
public class ProductController : Controller
{
    private readonly ProductService _productService;
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;

    public ProductController(ProductService productService, AppDbContext db, IWebHostEnvironment env)
    {
        _productService = productService; _db = db; _env = env;
    }

    [PermissionAuthorize("products.view")]
    public async Task<IActionResult> Index(string? search)
    {
        var products = await _productService.GetAllAsync(search);
        ViewBag.Search = search;
        return View(products);
    }

    [PermissionAuthorize("products.view")]
    public async Task<IActionResult> Details(int id)
    {
        var product = await _productService.GetByIdAsync(id);
        if (product == null) return NotFound();
        return View(product);
    }

    [PermissionAuthorize("products.create")]
    public async Task<IActionResult> Create()
    {
        await PopulateSelectLists();
        return View(new Product());
    }

    [HttpPost, ValidateAntiForgeryToken, PermissionAuthorize("products.create")]
    public async Task<IActionResult> Create(Product product, IFormFile? imageFile)
    {
        ModelState.Remove("Category"); ModelState.Remove("TaxRate"); ModelState.Remove("Variants"); ModelState.Remove("SaleItems"); ModelState.Remove("StockItems");
        if (!ModelState.IsValid) { await PopulateSelectLists(); return View(product); }
        if (imageFile != null && imageFile.Length > 0) product.ImageUrl = await SaveImageAsync(imageFile);
        await _productService.CreateAsync(product);
        TempData["Success"] = "Product created successfully.";
        return RedirectToAction(nameof(Index));
    }

    [PermissionAuthorize("products.edit")]
    public async Task<IActionResult> Edit(int id)
    {
        var product = await _productService.GetByIdAsync(id);
        if (product == null) return NotFound();
        await PopulateSelectLists(product.CategoryId, product.TaxRateId);
        return View(product);
    }

    [HttpPost, ValidateAntiForgeryToken, PermissionAuthorize("products.edit")]
    public async Task<IActionResult> Edit(int id, Product product, IFormFile? imageFile)
    {
        if (id != product.Id) return BadRequest();
        ModelState.Remove("Category"); ModelState.Remove("TaxRate"); ModelState.Remove("Variants"); ModelState.Remove("SaleItems"); ModelState.Remove("StockItems");
        if (!ModelState.IsValid) { await PopulateSelectLists(); return View(product); }
        if (imageFile != null && imageFile.Length > 0) product.ImageUrl = await SaveImageAsync(imageFile);
        await _productService.UpdateAsync(product);
        TempData["Success"] = "Product updated successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken, PermissionAuthorize("products.delete")]
    public async Task<IActionResult> Delete(int id)
    {
        await _productService.DeleteAsync(id);
        TempData["Success"] = "Product deleted.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Search(string q)
    {
        var products = await _productService.GetAllAsync(q);
        return Json(products.Take(10));
    }

    [HttpGet]
    public async Task<IActionResult> ByBarcode(string barcode)
    {
        var product = await _productService.SearchByBarcodeAsync(barcode);
        if (product == null) return NotFound();
        return Json(product);
    }

    private async Task PopulateSelectLists(int? categoryId = null, int? taxRateId = null)
    {
        ViewBag.Categories = new SelectList(await _db.Categories.ToListAsync(), "Id", "Name", categoryId);
        ViewBag.TaxRates = new SelectList(await _db.TaxRates.ToListAsync(), "Id", "Name", taxRateId);
    }

    private async Task<string> SaveImageAsync(IFormFile file)
    {
        var uploadsDir = Path.Combine(_env.WebRootPath, "uploads", "products");
        Directory.CreateDirectory(uploadsDir);
        var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
        var filePath = Path.Combine(uploadsDir, fileName);
        await using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream);
        return $"/uploads/products/{fileName}";
    }
}
