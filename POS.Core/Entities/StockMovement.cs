using POS.Core.Enums;
namespace POS.Core.Entities;
public class StockMovement : BaseEntity
{
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public int? FromBranchId { get; set; }
    public Branch? FromBranch { get; set; }
    public int? ToBranchId { get; set; }
    public Branch? ToBranch { get; set; }
    public int Qty { get; set; }
    public StockMovementType Type { get; set; }
    public string? Reference { get; set; }
    public string? Notes { get; set; }
    public string CreatedById { get; set; } = string.Empty;
}
