using POS.Core.Enums;
namespace POS.Core.Entities;
public class PurchaseOrder : BaseEntity
{
    public int SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;
    public int BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Draft;
    public decimal Total { get; set; }
    public string? Notes { get; set; }
    public string OrderedById { get; set; } = string.Empty;
    public ICollection<PurchaseOrderItem> Items { get; set; } = new List<PurchaseOrderItem>();
}
