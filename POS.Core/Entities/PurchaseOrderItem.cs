namespace POS.Core.Entities;
public class PurchaseOrderItem : BaseEntity
{
    public int PurchaseOrderId { get; set; }
    public PurchaseOrder PurchaseOrder { get; set; } = null!;
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public int Qty { get; set; }
    public decimal UnitCost { get; set; }
    public decimal LineTotal { get; set; }
    public int ReceivedQty { get; set; } = 0;
}
