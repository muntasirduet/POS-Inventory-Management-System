namespace POS.Application.DTOs;
public class DashboardDto
{
    public decimal TodayRevenue { get; set; }
    public int TodayTransactions { get; set; }
    public decimal AvgOrderValue { get; set; }
    public string TopProduct { get; set; } = string.Empty;
    public List<DailySalesDto> DailySales { get; set; } = new();
    public List<LowStockDto> LowStockItems { get; set; } = new();
    public int ActiveProducts { get; set; }
    public int PendingPurchaseOrders { get; set; }
}
public class DailySalesDto
{
    public string Date { get; set; } = string.Empty;
    public decimal Total { get; set; }
}
public class LowStockDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int QtyOnHand { get; set; }
    public int ReorderLevel { get; set; }
    public string BranchName { get; set; } = string.Empty;
}
