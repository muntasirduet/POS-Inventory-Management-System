using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using POS.Application.Services;
using POS.Infrastructure.Data;
using POS.Web.Authorization;

namespace POS.Web.Controllers;

[Authorize]
public class ReportController : Controller
{
    private readonly ReportService _reportService;
    private readonly AppDbContext _db;

    public ReportController(ReportService reportService, AppDbContext db)
    {
        _reportService = reportService; _db = db;
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
    }

    [PermissionAuthorize("reports.view")]
    public IActionResult Index() => View();

    [PermissionAuthorize("reports.view")]
    public async Task<IActionResult> Sales(DateTime? from, DateTime? to, int? branchId)
    {
        var report = await _reportService.GetSalesReportAsync(from ?? DateTime.Today.AddDays(-30), to ?? DateTime.Today, branchId);
        ViewBag.From = from?.ToString("yyyy-MM-dd") ?? DateTime.Today.AddDays(-30).ToString("yyyy-MM-dd");
        ViewBag.To = to?.ToString("yyyy-MM-dd") ?? DateTime.Today.ToString("yyyy-MM-dd");
        return View(report);
    }

    [PermissionAuthorize("reports.view")]
    public async Task<IActionResult> Inventory(int? branchId)
    {
        return View(await _reportService.GetInventoryReportAsync(branchId));
    }

    [PermissionAuthorize("reports.audit")]
    public async Task<IActionResult> AuditLogs(string? userId, string? entityType, int page = 1)
    {
        int pageSize = 50;
        var query = _db.AuditLogs.AsQueryable();
        if (!string.IsNullOrWhiteSpace(userId)) query = query.Where(l => l.UserId == userId);
        if (!string.IsNullOrWhiteSpace(entityType)) query = query.Where(l => l.EntityType == entityType);
        var total = await query.CountAsync();
        var logs = await query.OrderByDescending(l => l.Timestamp).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        ViewBag.Page = page;
        ViewBag.TotalPages = (int)Math.Ceiling(total / (double)pageSize);
        ViewBag.EntityTypeFilter = entityType ?? string.Empty;
        return View(logs);
    }

    [PermissionAuthorize("reports.export")]
    public async Task<IActionResult> ExportSalesExcel(DateTime? from, DateTime? to, int? branchId)
    {
        var report = await _reportService.GetSalesReportAsync(from ?? DateTime.Today.AddDays(-30), to ?? DateTime.Today, branchId);
        using var package = new ExcelPackage();
        var ws = package.Workbook.Worksheets.Add("Sales Report");
        string[] headers = { "Sale Number", "Date", "Cashier", "Customer", "Total", "Status" };
        for (int i = 0; i < headers.Length; i++) ws.Cells[1, i + 1].Value = headers[i];
        int row = 2;
        foreach (var r in report.Rows)
        {
            ws.Cells[row, 1].Value = r.SaleNumber; ws.Cells[row, 2].Value = r.Date.ToString("yyyy-MM-dd HH:mm");
            ws.Cells[row, 3].Value = r.Cashier; ws.Cells[row, 4].Value = r.Customer;
            ws.Cells[row, 5].Value = r.Total; ws.Cells[row, 6].Value = r.Status; row++;
        }
        ws.Cells.AutoFitColumns();
        return File(package.GetAsByteArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "SalesReport.xlsx");
    }

    [PermissionAuthorize("reports.export")]
    public async Task<IActionResult> ExportInventoryExcel(int? branchId)
    {
        var report = await _reportService.GetInventoryReportAsync(branchId);
        using var package = new ExcelPackage();
        var ws = package.Workbook.Worksheets.Add("Inventory");
        string[] headers = { "Product", "SKU", "Branch", "Qty On Hand", "Reorder Level", "Unit Price", "Stock Value" };
        for (int i = 0; i < headers.Length; i++) ws.Cells[1, i + 1].Value = headers[i];
        int row = 2;
        foreach (var r in report.Rows)
        {
            ws.Cells[row, 1].Value = r.ProductName; ws.Cells[row, 2].Value = r.SKU; ws.Cells[row, 3].Value = r.Branch;
            ws.Cells[row, 4].Value = r.QtyOnHand; ws.Cells[row, 5].Value = r.ReorderLevel;
            ws.Cells[row, 6].Value = r.UnitPrice; ws.Cells[row, 7].Value = r.StockValue; row++;
        }
        ws.Cells.AutoFitColumns();
        return File(package.GetAsByteArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "InventoryReport.xlsx");
    }
}
