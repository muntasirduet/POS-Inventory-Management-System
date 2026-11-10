using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using POS.Application.Services;
using POS.Core.Entities;
using POS.Core.Enums;
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

    [PermissionAuthorize("inventory.purchase")]
    public async Task<IActionResult> PurchaseOrderDetails(int id)
    {
        var order = await _db.PurchaseOrders
            .Include(po => po.Supplier)
            .Include(po => po.Branch)
            .Include(po => po.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(po => po.Id == id);
        if (order == null) return NotFound();
        return View(order);
    }

    [HttpPost, ValidateAntiForgeryToken, PermissionAuthorize("inventory.purchase")]
    public async Task<IActionResult> SubmitPurchaseOrder(int id)
    {
        var order = await _db.PurchaseOrders.FindAsync(id);
        if (order == null) return NotFound();
        if (order.Status != PurchaseOrderStatus.Draft)
        {
            TempData["Error"] = "Only Draft orders can be submitted.";
            return RedirectToAction(nameof(PurchaseOrderDetails), new { id });
        }
        order.Status = PurchaseOrderStatus.Ordered;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Purchase order submitted to supplier.";
        return RedirectToAction(nameof(PurchaseOrderDetails), new { id });
    }

    [PermissionAuthorize("inventory.purchase")]
    public async Task<IActionResult> ReceivePurchaseOrder(int id)
    {
        var order = await _db.PurchaseOrders
            .Include(po => po.Supplier)
            .Include(po => po.Branch)
            .Include(po => po.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(po => po.Id == id);
        if (order == null) return NotFound();
        if (order.Status != PurchaseOrderStatus.Ordered && order.Status != PurchaseOrderStatus.PartiallyReceived)
        {
            TempData["Error"] = "Only Ordered or Partially Received orders can receive stock.";
            return RedirectToAction(nameof(PurchaseOrderDetails), new { id });
        }
        return View(order);
    }

    [HttpPost, ValidateAntiForgeryToken, PermissionAuthorize("inventory.purchase")]
    public async Task<IActionResult> ReceivePurchaseOrder(int id, List<int> itemIds, List<int> receivedQtys)
    {
        var order = await _db.PurchaseOrders
            .Include(po => po.Items)
            .FirstOrDefaultAsync(po => po.Id == id);
        if (order == null) return NotFound();
        if (order.Status != PurchaseOrderStatus.Ordered && order.Status != PurchaseOrderStatus.PartiallyReceived)
        {
            TempData["Error"] = "Cannot receive stock for this order.";
            return RedirectToAction(nameof(PurchaseOrderDetails), new { id });
        }

        var user = await _userManager.GetUserAsync(User);
        for (int i = 0; i < itemIds.Count; i++)
        {
            var item = order.Items.FirstOrDefault(x => x.Id == itemIds[i]);
            if (item == null) continue;
            int incoming = Math.Max(0, Math.Min(receivedQtys[i], item.Qty - item.ReceivedQty));
            if (incoming == 0) continue;
            item.ReceivedQty += incoming;

            // Update Product.CostPrice to reflect latest purchase cost (last-cost method)
            var product = await _db.Products.FindAsync(item.ProductId);
            if (product != null && item.UnitCost > 0)
                product.CostPrice = item.UnitCost;

            // Update stock
            var stock = await _db.StockItems.FirstOrDefaultAsync(s => s.ProductId == item.ProductId && s.BranchId == order.BranchId);
            if (stock == null)
                _db.StockItems.Add(new StockItem { ProductId = item.ProductId, BranchId = order.BranchId, QtyOnHand = incoming });
            else
                stock.QtyOnHand += incoming;

            _db.StockMovements.Add(new StockMovement
            {
                ProductId = item.ProductId,
                ToBranchId = order.BranchId,
                Qty = incoming,
                Type = StockMovementType.Restock,
                Reference = $"PO-{order.Id}",
                Notes = $"Received from PO #{order.Id}",
                CreatedById = user!.Id
            });
        }

        bool allReceived = order.Items.All(x => x.ReceivedQty >= x.Qty);
        bool anyReceived = order.Items.Any(x => x.ReceivedQty > 0);
        order.Status = allReceived ? PurchaseOrderStatus.Received
                     : anyReceived ? PurchaseOrderStatus.PartiallyReceived
                     : order.Status;

        await _db.SaveChangesAsync();
        TempData["Success"] = allReceived ? "All items received. Order completed." : "Stock partially received.";
        return RedirectToAction(nameof(PurchaseOrderDetails), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken, PermissionAuthorize("inventory.purchase")]
    public async Task<IActionResult> CancelPurchaseOrder(int id)
    {
        var order = await _db.PurchaseOrders.FindAsync(id);
        if (order == null) return NotFound();
        if (order.Status == PurchaseOrderStatus.Received)
        {
            TempData["Error"] = "Received orders cannot be cancelled.";
            return RedirectToAction(nameof(PurchaseOrderDetails), new { id });
        }
        order.Status = PurchaseOrderStatus.Cancelled;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Purchase order cancelled.";
        return RedirectToAction(nameof(PurchaseOrders));
    }
}
