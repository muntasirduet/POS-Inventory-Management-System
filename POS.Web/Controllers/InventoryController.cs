using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using POS.Application.Services;
using POS.Core.Entities;
using POS.Infrastructure.Data;
using POS.Infrastructure.Identity;
using POS.Web.Authorization;

namespace POS.Web.Controllers;

[Authorize]
public class InventoryController : Controller
{
    private readonly InventoryService _inventoryService;
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public InventoryController(InventoryService inventoryService, AppDbContext db, UserManager<ApplicationUser> userManager)
    {
        _inventoryService = inventoryService; _db = db; _userManager = userManager;
    }

    [PermissionAuthorize("inventory.view")]
    public async Task<IActionResult> Index()
    {
        var branchId = HttpContext.Session.GetInt32("BranchId");
        return View(await _inventoryService.GetStockAsync(branchId));
    }

    [PermissionAuthorize("inventory.view")]
    public async Task<IActionResult> LowStock()
    {
        var branchId = HttpContext.Session.GetInt32("BranchId");
        return View(await _inventoryService.GetLowStockAsync(branchId));
    }

    [PermissionAuthorize("inventory.transfer")]
    public async Task<IActionResult> Transfer()
    {
        ViewBag.Products = new SelectList(await _db.Products.ToListAsync(), "Id", "Name");
        ViewBag.Branches = new SelectList(await _db.Branches.ToListAsync(), "Id", "Name");
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken, PermissionAuthorize("inventory.transfer")]
    public async Task<IActionResult> Transfer(int productId, int fromBranchId, int toBranchId, int qty)
    {
        try
        {
            var user = await _userManager.GetUserAsync(User);
            await _inventoryService.TransferStockAsync(productId, fromBranchId, toBranchId, qty, user!.Id);
            TempData["Success"] = "Stock transferred.";
        }
        catch (Exception ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }

    [PermissionAuthorize("inventory.adjust")]
    public async Task<IActionResult> Adjust()
    {
        ViewBag.Products = new SelectList(await _db.Products.ToListAsync(), "Id", "Name");
        ViewBag.Branches = new SelectList(await _db.Branches.ToListAsync(), "Id", "Name");
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken, PermissionAuthorize("inventory.adjust")]
    public async Task<IActionResult> Adjust(int productId, int branchId, int qty, string notes)
    {
        var user = await _userManager.GetUserAsync(User);
        await _inventoryService.AdjustStockAsync(productId, branchId, qty, notes, user!.Id);
        TempData["Success"] = "Stock adjusted.";
        return RedirectToAction(nameof(Index));
    }

    [PermissionAuthorize("inventory.view")]
    public async Task<IActionResult> Movements()
    {
        var movements = await _db.StockMovements.Include(m => m.Product).Include(m => m.FromBranch).Include(m => m.ToBranch)
            .OrderByDescending(m => m.CreatedAt).Take(200).ToListAsync();
        return View(movements);
    }

    [PermissionAuthorize("inventory.purchase")]
    public async Task<IActionResult> PurchaseOrders()
    {
        var orders = await _db.PurchaseOrders.Include(po => po.Supplier).Include(po => po.Branch).OrderByDescending(po => po.CreatedAt).ToListAsync();
        return View(orders);
    }

    [PermissionAuthorize("inventory.purchase")]
    public async Task<IActionResult> CreatePurchaseOrder()
    {
        ViewBag.Suppliers = new SelectList(await _db.Suppliers.ToListAsync(), "Id", "Name");
        ViewBag.Branches = new SelectList(await _db.Branches.ToListAsync(), "Id", "Name");
        ViewBag.Products = await _db.Products.ToListAsync();
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken, PermissionAuthorize("inventory.purchase")]
    public async Task<IActionResult> CreatePurchaseOrder(PurchaseOrder order, List<int> productIds, List<int> qtys, List<decimal> costs)
    {
        ModelState.Remove("Supplier"); ModelState.Remove("Branch"); ModelState.Remove("Items");
        var user = await _userManager.GetUserAsync(User);
        order.OrderedById = user!.Id;
        _db.PurchaseOrders.Add(order);
        await _db.SaveChangesAsync();
        for (int i = 0; i < productIds.Count; i++)
            _db.PurchaseOrderItems.Add(new PurchaseOrderItem { PurchaseOrderId = order.Id, ProductId = productIds[i], Qty = qtys[i], UnitCost = costs[i], LineTotal = qtys[i] * costs[i] });
        order.Total = qtys.Zip(costs, (q, c) => q * c).Sum();
        await _db.SaveChangesAsync();
        TempData["Success"] = "Purchase order created.";
        return RedirectToAction(nameof(PurchaseOrders));
    }
}
