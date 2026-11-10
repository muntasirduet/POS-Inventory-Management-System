using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using POS.Core.Entities;
using POS.Infrastructure.Data;

namespace POS.Web.Controllers;

[Authorize(Roles = "SuperAdmin,StoreOwner,StoreManager")]
public class CategoryController : Controller
{
    private readonly AppDbContext _db;
    public CategoryController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index() => View(await _db.Categories.Include(c => c.ParentCategory).ToListAsync());

    public async Task<IActionResult> Create()
    {
        ViewBag.Parents = new SelectList(await _db.Categories.ToListAsync(), "Id", "Name");
        ViewBag.TaxGroups = new SelectList(await _db.TaxGroups.OrderBy(t => t.Name).ToListAsync(), "Id", "Name");
        return View(new Category());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Category category)
    {
        ModelState.Remove("ParentCategory"); ModelState.Remove("SubCategories"); ModelState.Remove("Products"); ModelState.Remove("DefaultTaxGroup"); ModelState.Remove("AttributeTemplates");
        if (!ModelState.IsValid)
        {
            ViewBag.Parents = new SelectList(await _db.Categories.ToListAsync(), "Id", "Name");
            ViewBag.TaxGroups = new SelectList(await _db.TaxGroups.OrderBy(t => t.Name).ToListAsync(), "Id", "Name");
            return View(category);
        }
        _db.Categories.Add(category);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Category created.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var category = await _db.Categories.FindAsync(id);
        if (category == null) return NotFound();
        ViewBag.Parents = new SelectList(await _db.Categories.Where(c => c.Id != id).ToListAsync(), "Id", "Name", category.ParentCategoryId);
        ViewBag.TaxGroups = new SelectList(await _db.TaxGroups.OrderBy(t => t.Name).ToListAsync(), "Id", "Name", category.DefaultTaxGroupId);
        
        // ← NEW: Get all attributes and which ones are linked to this category
        ViewBag.AllAttributes = await _db.AttributeDefinitions
            .OrderBy(a => a.Name)
            .ToListAsync();
        
        ViewBag.LinkedAttributeIds = await _db.CategoryAttributeTemplates
            .Where(cat => cat.CategoryId == id)
            .Select(cat => cat.AttributeDefinitionId)
            .ToListAsync();
        
        return View(category);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Category category)
    {
        if (id != category.Id) return BadRequest();
        ModelState.Remove("ParentCategory"); ModelState.Remove("SubCategories"); ModelState.Remove("Products"); ModelState.Remove("DefaultTaxGroup"); ModelState.Remove("AttributeTemplates");
        if (!ModelState.IsValid)
        {
            ViewBag.Parents = new SelectList(await _db.Categories.Where(c => c.Id != id).ToListAsync(), "Id", "Name");
            ViewBag.TaxGroups = new SelectList(await _db.TaxGroups.OrderBy(t => t.Name).ToListAsync(), "Id", "Name");
            return View(category);
        }
        _db.Categories.Update(category);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Category updated.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>JSON API: Save category-attribute template links</summary>
    [HttpPost]
    public async Task<IActionResult> SaveCategoryAttributes(int categoryId, [FromBody] int[] attributeIds)
    {
        if (attributeIds == null) attributeIds = Array.Empty<int>();

        // Remove old templates
        var oldTemplates = await _db.CategoryAttributeTemplates
            .Where(cat => cat.CategoryId == categoryId)
            .ToListAsync();
        _db.CategoryAttributeTemplates.RemoveRange(oldTemplates);

        // Add new templates
        int order = 0;
        foreach (var attrId in attributeIds)
        {
            _db.CategoryAttributeTemplates.Add(new CategoryAttributeTemplate
            {
                CategoryId = categoryId,
                AttributeDefinitionId = attrId,
                DisplayOrder = order++
            });
        }

        await _db.SaveChangesAsync();
        return Json(new { success = true, message = "Category attributes saved" });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var category = await _db.Categories.FindAsync(id);
        if (category != null) { category.IsDeleted = true; category.DeletedAt = DateTime.UtcNow; await _db.SaveChangesAsync(); }
        TempData["Success"] = "Category deleted.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>JSON API: Get all attributes for a category via templates</summary>
    [HttpGet, AllowAnonymous]
    public async Task<IActionResult> GetCategoryAttributes(int categoryId)
    {
        var attributes = await _db.CategoryAttributeTemplates
            .Where(cat => cat.CategoryId == categoryId)
            .Include(cat => cat.AttributeDefinition).ThenInclude(ad => ad.Options)
            .OrderBy(cat => cat.DisplayOrder)
            .Select(cat => new {
                id = cat.AttributeDefinition.Id,
                name = cat.AttributeDefinition.Name,
                dataType = cat.AttributeDefinition.DataType,
                isVariantLevel = cat.AttributeDefinition.IsVariantLevel,
                options = cat.AttributeDefinition.Options
                    .OrderBy(o => o.SortOrder)
                    .Select(o => new { value = o.Value, sortOrder = o.SortOrder })
                    .ToList()
            })
            .ToListAsync();

        return Json(attributes);
    }
}
