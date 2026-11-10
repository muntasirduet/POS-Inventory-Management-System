using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs;
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
    private readonly PriceRuleService _priceRuleService;

    public ProductController(ProductService productService, AppDbContext db, IWebHostEnvironment env, PriceRuleService priceRuleService)
    {
        _productService = productService; _db = db; _env = env; _priceRuleService = priceRuleService;
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
        var product = await _db.Products
            .Include(p => p.Category)
            .Include(p => p.TaxRate)
            .Include(p => p.TaxGroup)
            .Include(p => p.Variants).ThenInclude(v => v.AttributeValues).ThenInclude(av => av.AttributeDefinition)
            .Include(p => p.Variants).ThenInclude(v => v.BranchStockItems).ThenInclude(bsi => bsi.Branch)
            .Include(p => p.AttributeValues).ThenInclude(av => av.AttributeDefinition)
            .Include(p => p.StockItems).ThenInclude(si => si.Branch)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (product == null) return NotFound();

        // Variant-level attribute definitions (for the Add Variant form)
        var variantAttrs = await _db.CategoryAttributeTemplates
            .Where(cat => cat.CategoryId == product.CategoryId)
            .Include(cat => cat.AttributeDefinition).ThenInclude(ad => ad.Options)
            .Where(cat => cat.AttributeDefinition.IsVariantLevel)
            .OrderBy(cat => cat.DisplayOrder)
            .Select(cat => cat.AttributeDefinition)
            .ToListAsync();
        ViewBag.VariantAttributes = variantAttrs;

        // ALL attribute definitions (WooCommerce-style: user can attach any to this product)
        ViewBag.AllAttributes = await _db.AttributeDefinitions
            .Include(a => a.Options)
            .OrderBy(a => a.SortOrder).ThenBy(a => a.Name)
            .ToListAsync();

        // All branches for the inventory management section
        ViewBag.Branches = await _db.Branches.OrderBy(b => b.Name).ToListAsync();

        // Recent sales of this product (last 20 SaleItems)
        ViewBag.RecentSales = await _db.SaleItems
            .Where(si => si.ProductId == id)
            .Include(si => si.Sale)
            .OrderByDescending(si => si.Id)
            .Take(20)
            .ToListAsync();

        // Stock movements for this product (last 100)
        ViewBag.StockMovements = await _db.StockMovements
            .Where(sm => sm.ProductId == id)
            .Include(sm => sm.FromBranch)
            .Include(sm => sm.ToBranch)
            .OrderByDescending(sm => sm.CreatedAt)
            .Take(100)
            .ToListAsync();

        // Purchase order items for this product
        ViewBag.PurchaseOrderItems = await _db.PurchaseOrderItems
            .Where(poi => poi.ProductId == id)
            .Include(poi => poi.PurchaseOrder).ThenInclude(po => po.Supplier)
            .Include(poi => poi.PurchaseOrder).ThenInclude(po => po.Branch)
            .OrderByDescending(poi => poi.PurchaseOrderId)
            .ToListAsync();

        return View(product);
    }

    [HttpPost, ValidateAntiForgeryToken, PermissionAuthorize("inventory.manage")]
    public async Task<IActionResult> SetStockItem(int productId, int branchId, int qtyOnHand, int reorderLevel = 10)
    {
        var stock = await _db.StockItems
            .FirstOrDefaultAsync(s => s.ProductId == productId && s.BranchId == branchId);
        if (stock == null)
        {
            stock = new StockItem { ProductId = productId, BranchId = branchId };
            _db.StockItems.Add(stock);
        }
        stock.QtyOnHand = Math.Max(0, qtyOnHand);
        stock.ReorderLevel = Math.Max(0, reorderLevel);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Stock updated.";
        return RedirectToAction(nameof(Details), new { id = productId, tab = "inventory" });
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

    [HttpPost, ValidateAntiForgeryToken, PermissionAuthorize("products.edit")]
    public async Task<IActionResult> AddVariant(ProductVariant variant)
    {
        // Manually parse attrValues[id]=value from the form to avoid Dictionary<int,string>
        // binding fallback that tries to parse non-prefixed form keys (e.g. "ProductId") as int keys.
        var attrValues = new Dictionary<int, string>();
        foreach (var key in Request.Form.Keys)
        {
            if (key.StartsWith("attrValues[") && key.EndsWith("]"))
            {
                var inner = key["attrValues[".Length..^1];
                if (int.TryParse(inner, out var attrId))
                    attrValues[attrId] = Request.Form[key].ToString();
            }
        }

        ModelState.Remove("Product");
        if (ModelState.IsValid)
        {
            // Auto-build label from submitted attribute values
            if (string.IsNullOrWhiteSpace(variant.Label) && attrValues.Any())
            {
                var attrDefs = await _db.AttributeDefinitions
                    .Where(a => attrValues.Keys.Contains(a.Id))
                    .OrderBy(a => a.SortOrder).ToListAsync();
                variant.Label = string.Join(" / ", attrDefs
                    .Where(a => attrValues.ContainsKey(a.Id) && !string.IsNullOrWhiteSpace(attrValues[a.Id]))
                    .Select(a => attrValues[a.Id]));
            }
            _db.ProductVariants.Add(variant);
            await _db.SaveChangesAsync();

            foreach (var kv in attrValues.Where(kv => !string.IsNullOrWhiteSpace(kv.Value)))
                _db.VariantAttributeValues.Add(new VariantAttributeValue
                {
                    ProductVariantId = variant.Id,
                    AttributeDefinitionId = kv.Key,
                    Value = kv.Value
                });
            await _db.SaveChangesAsync();
            TempData["Success"] = "Variant added.";
        }
        return RedirectToAction(nameof(Details), new { id = variant.ProductId });
    }

    [HttpPost, ValidateAntiForgeryToken, PermissionAuthorize("products.edit")]
    public async Task<IActionResult> EditVariant(int id, int productId, string? label, string? sku,
        decimal price, int stockQty, string? barcode, bool isActive)
    {
        var variant = await _db.ProductVariants.FindAsync(id);
        if (variant == null) return NotFound();
        variant.Label  = string.IsNullOrWhiteSpace(label) ? null : label.Trim();
        variant.SKU    = string.IsNullOrWhiteSpace(sku)   ? null : sku.Trim();
        variant.Price  = Math.Max(0, price);
        variant.StockQty = Math.Max(0, stockQty);
        variant.Barcode = string.IsNullOrWhiteSpace(barcode) ? null : barcode.Trim();
        variant.IsActive = isActive;
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Variant '{variant.Label ?? variant.SKU}' updated.";
        return RedirectToAction(nameof(Details), new { id = productId, tab = "variants" });
    }

    [HttpPost, ValidateAntiForgeryToken, PermissionAuthorize("products.edit")]
    public async Task<IActionResult> SaveProductAttribute(int productId, int attributeDefinitionId, string value)
    {
        var existing = await _db.ProductAttributeValues
            .FirstOrDefaultAsync(pav => pav.ProductId == productId && pav.AttributeDefinitionId == attributeDefinitionId);
        if (existing == null)
            _db.ProductAttributeValues.Add(new ProductAttributeValue
            {
                ProductId = productId, AttributeDefinitionId = attributeDefinitionId, Value = value ?? string.Empty
            });
        else
            existing.Value = value ?? string.Empty;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Attribute saved.";
        return RedirectToAction(nameof(Details), new { id = productId });
    }

    [HttpPost, ValidateAntiForgeryToken, PermissionAuthorize("products.edit")]
    public async Task<IActionResult> DeleteProductAttribute(int id, int productId)
    {
        var pav = await _db.ProductAttributeValues.FindAsync(id);
        if (pav != null) { _db.ProductAttributeValues.Remove(pav); await _db.SaveChangesAsync(); TempData["Success"] = "Attribute cleared."; }
        return RedirectToAction(nameof(Details), new { id = productId });
    }

    [HttpPost, ValidateAntiForgeryToken, PermissionAuthorize("products.edit")]
    public async Task<IActionResult> DeleteVariant(int id, int productId)
    {
        var variant = await _db.ProductVariants.FindAsync(id);
        if (variant == null)
        {
            TempData["Error"] = "Variant not found.";
            return RedirectToAction(nameof(Details), new { id = productId });
        }

        // Check if this variant has been used in any sales
        var hasBeenSold = await _db.SaleItems.AnyAsync(si => si.VariantId == id);
        if (hasBeenSold)
        {
            TempData["Error"] = "Cannot delete this variant because it has been sold. You can deactivate it instead.";
            return RedirectToAction(nameof(Details), new { id = productId, tab = "variants" });
        }

        try
        {
            _db.ProductVariants.Remove(variant);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Variant removed.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Could not delete variant: {ex.InnerException?.Message ?? ex.Message}";
        }

        return RedirectToAction(nameof(Details), new { id = productId, tab = "variants" });
    }

    [HttpPost, ValidateAntiForgeryToken, PermissionAuthorize("products.edit")]
    public async Task<IActionResult> GenerateVariants(int productId)
    {
        var product = await _db.Products
            .Include(p => p.AttributeValues).ThenInclude(av => av.AttributeDefinition)
            .Include(p => p.Variants).ThenInclude(v => v.AttributeValues)
            .FirstOrDefaultAsync(p => p.Id == productId);
        if (product == null) return NotFound();

        // Only variant-level attributes that have values selected
        var variantAttrs = product.AttributeValues
            .Where(av => av.AttributeDefinition.IsVariantLevel && !string.IsNullOrWhiteSpace(av.Value))
            .Select(av => new {
                AttrDef = av.AttributeDefinition,
                Values = av.Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                              .Distinct().ToList()
            })
            .Where(x => x.Values.Count > 0)
            .ToList();

        if (!variantAttrs.Any())
        {
            TempData["Error"] = "No variant attributes configured. Add attributes with 'Used for Variants' = Yes first.";
            return RedirectToAction(nameof(Details), new { id = productId, tab = "attributes" });
        }

        // Compute cartesian product of all selected values across all variant attributes
        IEnumerable<List<(int AttrDefId, string AttrDefName, string Value)>> combinations =
            new List<List<(int, string, string)>> { new() };

        foreach (var attr in variantAttrs)
        {
            combinations = combinations.SelectMany(combo =>
                attr.Values.Select(val =>
                    combo.Concat(new[] { (attr.AttrDef.Id, attr.AttrDef.Name, val) }).ToList()
                )
            );
        }

        int created = 0, skipped = 0;
        foreach (var combo in combinations)
        {
            // Check if a variant already exists with the exact same attribute combination
            bool exists = product.Variants.Any(v =>
                v.AttributeValues.Count == combo.Count &&
                combo.All(c => v.AttributeValues.Any(av =>
                    av.AttributeDefinitionId == c.AttrDefId && av.Value == c.Value)));
            if (exists) { skipped++; continue; }

            var label = string.Join(" / ", combo.Select(c => c.Value));
            var skuSuffix = string.Join("-", combo.Select(c => c.Value.Replace(" ", "").ToUpperInvariant()));
            var variant = new ProductVariant
            {
                ProductId = productId,
                Label = label,
                SKU = $"{product.SKU}-{skuSuffix}",
                Price = product.Price,
                StockQty = 0,
                IsActive = true
            };
            _db.ProductVariants.Add(variant);
            await _db.SaveChangesAsync();

            foreach (var (attrDefId, _, val) in combo)
                _db.VariantAttributeValues.Add(new VariantAttributeValue
                {
                    ProductVariantId = variant.Id,
                    AttributeDefinitionId = attrDefId,
                    Value = val
                });
            await _db.SaveChangesAsync();
            created++;
        }

        TempData["Success"] = $"{created} variant(s) generated. {(skipped > 0 ? $"{skipped} already existed and were skipped." : "")}";
        return RedirectToAction(nameof(Details), new { id = productId, tab = "variants" });
    }

    [HttpGet]
    public async Task<IActionResult> Search(string q)
    {
        var products = await _productService.GetAllAsync(q);
        return Json(products.Take(10));
    }

    [HttpGet]
    public async Task<IActionResult> GetVariants(int productId)
    {
        var variants = await _db.ProductVariants
            .Where(v => v.ProductId == productId && v.IsActive)
            .Include(v => v.AttributeValues).ThenInclude(av => av.AttributeDefinition)
            .OrderBy(v => v.Label)
            .ToListAsync();

        var result = variants.Select(v => new {
            id = v.Id,
            label = v.Label ?? string.Join(" / ", v.AttributeValues
                .OrderBy(a => a.AttributeDefinition?.SortOrder)
                .Select(a => a.Value)),
            sku  = v.SKU,
            price = v.Price,
            stockQty = v.StockQty,
            attributes = v.AttributeValues
                .OrderBy(a => a.AttributeDefinition?.SortOrder)
                .Select(a => new { name = a.AttributeDefinition?.Name, value = a.Value })
        });
        return Json(result);
    }

    /// <summary>Returns variants with stock availability per branch for the POS terminal.</summary>
    [HttpGet]
    public async Task<IActionResult> GetVariantsWithBranchStock(int productId)
    {
        var variants = await _db.ProductVariants
            .Where(v => v.ProductId == productId && v.IsActive)
            .Include(v => v.AttributeValues).ThenInclude(av => av.AttributeDefinition)
            .Include(v => v.BranchStockItems).ThenInclude(bsi => bsi.Branch)
            .OrderBy(v => v.Label)
            .ToListAsync();

        var result = variants.Select(v => new VariantWithStockDto
        {
            Id = v.Id,
            Label = v.Label,
            SKU = v.SKU,
            Barcode = v.Barcode,
            Price = v.Price,
            IsActive = v.IsActive,
            Attributes = v.AttributeValues
                .OrderBy(a => a.AttributeDefinition?.SortOrder)
                .Select(a => new VariantAttributeDto
                {
                    Name = a.AttributeDefinition?.Name ?? "Attribute",
                    Value = a.Value ?? ""
                }).ToList(),
            StockByBranch = v.BranchStockItems
                .OrderBy(bsi => bsi.Branch.Name)
                .Select(bsi => new BranchVariantStockDto
                {
                    BranchId = bsi.BranchId,
                    BranchName = bsi.Branch.Name,
                    QtyOnHand = bsi.QtyOnHand,
                    ReorderLevel = bsi.ReorderLevel
                }).ToList()
        }).ToList();
        return Json(result);
    }

    [HttpGet]
    public async Task<IActionResult> ByBarcode(string barcode)
    {
        var product = await _productService.SearchByBarcodeAsync(barcode);
        if (product == null) return NotFound();
        return Json(product);
    }

    /// <summary>Returns the price rule-adjusted unit price for a product given a customer context.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAdjustedPrice(int productId, int? customerId)
    {
        var product = await _db.Products.FindAsync(productId);
        if (product == null) return NotFound();
        if (!customerId.HasValue)
            return Json(new { price = product.Price, discountApplied = false });

        var customer = await _db.Customers.FindAsync(customerId.Value);
        if (customer?.CustomerGroupId == null)
            return Json(new { price = product.Price, discountApplied = false });

        var rule = await _priceRuleService.GetBestRuleAsync(productId, product.CategoryId, customer.CustomerGroupId);
        if (rule == null)
            return Json(new { price = product.Price, discountApplied = false });

        var adjusted = PriceRuleService.ApplyRule(rule, product.Price);
        return Json(new { price = adjusted, discountApplied = true, ruleName = rule.Name, originalPrice = product.Price });
    }

    private async Task PopulateSelectLists(int? categoryId = null, int? taxRateId = null)
    {
        ViewBag.Categories = new SelectList(await _db.Categories.ToListAsync(), "Id", "Name", categoryId);
        ViewBag.TaxRates = new SelectList(await _db.TaxRates.ToListAsync(), "Id", "Name", taxRateId);
        ViewBag.TaxGroups = new SelectList(await _db.TaxGroups.ToListAsync(), "Id", "Name");
    }

    /// <summary>JSON API: Get all product attributes</summary>
    [HttpGet, PermissionAuthorize("products.view")]
    public async Task<IActionResult> GetProductAttributes(int productId)
    {
        var attributes = await _db.ProductAttributeValues
            .Where(av => av.ProductId == productId)
            .Include(av => av.AttributeDefinition)
            .Select(av => new {
                id = av.Id,
                attributeDefinitionId = av.AttributeDefinitionId,
                attributeName = av.AttributeDefinition.Name,
                value = av.Value
            })
            .ToListAsync();
        return Json(attributes);
    }

    /// <summary>Form-based: Set product attribute (from Details view form)</summary>
    [HttpPost, ValidateAntiForgeryToken, PermissionAuthorize("products.edit")]
    public async Task<IActionResult> SetProductAttribute(int productId, int attributeDefinitionId, string value)
    {
        var existing = await _db.ProductAttributeValues
            .FirstOrDefaultAsync(av => av.ProductId == productId && av.AttributeDefinitionId == attributeDefinitionId);
        
        if (existing == null)
        {
            _db.ProductAttributeValues.Add(new ProductAttributeValue
            {
                ProductId = productId,
                AttributeDefinitionId = attributeDefinitionId,
                Value = value ?? string.Empty
            });
        }
        else
        {
            existing.Value = value ?? string.Empty;
        }
        
        await _db.SaveChangesAsync();
        TempData["Success"] = "Attribute saved.";
        return RedirectToAction(nameof(Details), new { id = productId });
    }

    /// <summary>JSON API: Set/Create product attribute (from Edit view Fetch API)</summary>
    [HttpPost, PermissionAuthorize("products.edit"), Route("SetProductAttributeJson")]
    public async Task<IActionResult> SetProductAttributeJson([FromBody] SetProductAttributeRequest request)
    {
        if (request == null || request.ProductId <= 0 || request.AttributeDefinitionId <= 0)
            return Json(new { success = false, message = "Invalid request" });

        var existing = await _db.ProductAttributeValues
            .FirstOrDefaultAsync(av => av.ProductId == request.ProductId && 
                                       av.AttributeDefinitionId == request.AttributeDefinitionId);
        
        if (existing == null)
        {
            _db.ProductAttributeValues.Add(new ProductAttributeValue
            {
                ProductId = request.ProductId,
                AttributeDefinitionId = request.AttributeDefinitionId,
                Value = request.Value ?? string.Empty
            });
        }
        else
        {
            existing.Value = request.Value ?? string.Empty;
        }
        
        await _db.SaveChangesAsync();
        return Json(new { success = true, message = "Attribute saved" });
    }

    /// <summary>JSON API: Remove product attribute</summary>
    [HttpDelete, PermissionAuthorize("products.edit")]
    public async Task<IActionResult> RemoveProductAttribute(int productId, int attributeDefinitionId)
    {
        var attribute = await _db.ProductAttributeValues
            .FirstOrDefaultAsync(av => av.ProductId == productId && 
                                       av.AttributeDefinitionId == attributeDefinitionId);
        
        if (attribute != null)
        {
            _db.ProductAttributeValues.Remove(attribute);
            await _db.SaveChangesAsync();
            return Json(new { success = true, message = "Attribute removed" });
        }
        
        return Json(new { success = false, message = "Attribute not found" });
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

/// <summary>Request model for setting product attributes via JSON API</summary>
public class SetProductAttributeRequest
{
    public int ProductId { get; set; }
    public int AttributeDefinitionId { get; set; }
    public string? Value { get; set; }
}
