namespace POS.Core.Entities;
public class Terminal : BaseEntity
{
    public int BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public ICollection<Shift> Shifts { get; set; } = new List<Shift>();
}
