namespace POS.Core.Entities;
public class StockItem : BaseEntity
{
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public int BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    public int QtyOnHand { get; set; }
    public int ReorderLevel { get; set; } = 10;
}
