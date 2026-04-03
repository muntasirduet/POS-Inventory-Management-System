namespace POS.Core.Entities;
public class Shift : BaseEntity
{
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public int? TerminalId { get; set; }
    public Terminal? Terminal { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public decimal? OpeningCash { get; set; }
    public decimal? ClosingCash { get; set; }
}
