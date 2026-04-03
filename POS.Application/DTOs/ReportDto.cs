namespace POS.Application.DTOs;
public class SalesReportDto
{
    public decimal TotalRevenue { get; set; }
    public decimal TotalTax { get; set; }
    public decimal TotalDiscount { get; set; }
    public decimal NetRevenue { get; set; }
    public int TransactionCount { get; set; }
    public int ReturnCount { get; set; }
    public List<SaleReportRow> Rows { get; set; } = new();
}
public class SaleReportRow
{
    public string SaleNumber { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Cashier { get; set; } = string.Empty;
    public string? Customer { get; set; }
    public decimal Total { get; set; }
    public string Status { get; set; } = string.Empty;
}
public class InventoryReportDto
{
    public List<InventoryReportRow> Rows { get; set; } = new();
}
public class InventoryReportRow
{
    public string ProductName { get; set; } = string.Empty;
    public string SKU { get; set; } = string.Empty;
    public string Branch { get; set; } = string.Empty;
    public int QtyOnHand { get; set; }
    public int ReorderLevel { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal StockValue { get; set; }
}
