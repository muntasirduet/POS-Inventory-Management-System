namespace POS.Core.Entities;
public class Warehouse : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public int BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    public string? Location { get; set; }
}
