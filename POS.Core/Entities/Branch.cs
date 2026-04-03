namespace POS.Core.Entities;
public class Branch : BaseEntity
{
    public int StoreId { get; set; }
    public Store Store { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? ManagerId { get; set; }
    public ICollection<Terminal> Terminals { get; set; } = new List<Terminal>();
    public ICollection<StockItem> StockItems { get; set; } = new List<StockItem>();
    public ICollection<Sale> Sales { get; set; } = new List<Sale>();
}
