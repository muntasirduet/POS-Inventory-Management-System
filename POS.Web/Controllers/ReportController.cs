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

    // --- TAX / VAT REPORT ---
    [PermissionAuthorize("reports.view")]
    public async Task<IActionResult> TaxReport(DateTime? from, DateTime? to, int? branchId)
    {
        var f = from ?? DateTime.Today.AddMonths(-1);
        var t = to ?? DateTime.Today;
        ViewBag.From = f.ToString("yyyy-MM-dd");
        ViewBag.To = t.ToString("yyyy-MM-dd");

        var saleItems = await _db.SaleItems
            .Include(si => si.Sale)
            .Include(si => si.Product).ThenInclude(p => p.TaxRate)
            .Where(si => si.Sale.Status == POS.Core.Enums.SaleStatus.Completed
                      && si.Sale.CreatedAt >= f
                      && si.Sale.CreatedAt <= t.AddDays(1)
                      && (!branchId.HasValue || si.Sale.BranchId == branchId))
            .ToListAsync();

        var rows = saleItems
            .GroupBy(si => si.Product.TaxRate != null
                ? new { RateName = si.Product.TaxRate.Name, Rate = si.Product.TaxRate.Rate }
                : new { RateName = "No Tax", Rate = 0m })
            .Select(g => new TaxReportRow
            {
                TaxRateName = g.Key.RateName,
                Rate = g.Key.Rate,
                TaxableAmount = g.Sum(si => si.LineTotal),
                TaxAmount = g.Sum(si => si.LineTotal * g.Key.Rate)
            })
            .OrderBy(r => r.Rate)
            .ToList();

        ViewBag.Branches = await _db.Branches.ToListAsync();
        ViewBag.BranchId = branchId;
        return View(rows);
    }

    [PermissionAuthorize("reports.export")]
    public async Task<IActionResult> ExportTaxCsv(DateTime? from, DateTime? to, int? branchId)
    {
        var f = from ?? DateTime.Today.AddMonths(-1);
        var t = to ?? DateTime.Today;

        var rows = await _db.SaleItems
            .Include(si => si.Sale)
            .Include(si => si.Product).ThenInclude(p => p.TaxRate)
            .Where(si => si.Sale.Status == POS.Core.Enums.SaleStatus.Completed
                      && si.Sale.CreatedAt >= f
                      && si.Sale.CreatedAt <= t.AddDays(1)
                      && (!branchId.HasValue || si.Sale.BranchId == branchId))
            .ToListAsync();

        var csv = new System.Text.StringBuilder();
        csv.AppendLine("Sale Number,Date,Product,SKU,Qty,Unit Price,Line Total,Tax Rate,Tax Amount");
        foreach (var si in rows)
        {
            var rate = si.Product?.TaxRate?.Rate ?? 0;
            csv.AppendLine($"\"{si.Sale.SaleNumber}\",\"{si.Sale.CreatedAt:yyyy-MM-dd HH:mm}\"," +
                           $"\"{si.Product?.Name}\",\"{si.Product?.SKU}\"," +
                           $"{si.Qty},{si.UnitPrice:F4},{si.LineTotal:F4},{rate:P2},{si.LineTotal * rate:F4}");
        }

        return File(System.Text.Encoding.UTF8.GetBytes(csv.ToString()), "text/csv",
            $"TaxReport_{f:yyyyMMdd}_{t:yyyyMMdd}.csv");
    }
}

public class TaxReportRow
{
    public string TaxRateName { get; set; } = string.Empty;
    public decimal Rate { get; set; }
    public decimal TaxableAmount { get; set; }
    public decimal TaxAmount { get; set; }
}
