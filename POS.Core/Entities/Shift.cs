namespace POS.Core.Entities;
public class Shift : BaseEntity
{
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public int? TerminalId { get; set; }
    public Terminal? Terminal { get; set; }
    public int? BranchId { get; set; }
    public Branch? Branch { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public decimal OpeningCash { get; set; } = 0;
    public decimal? ClosingCash { get; set; }
    public decimal? ExpectedCash { get; set; }
    public string? Notes { get; set; }
    public bool IsClosed => EndTime.HasValue;
    public ICollection<ShiftCashEntry> CashEntries { get; set; } = new List<ShiftCashEntry>();
    public ICollection<ShiftDenomination> Denominations { get; set; } = new List<ShiftDenomination>();
}

/// <summary>Cash-in / cash-out entries within a shift (e.g. paid-in, paid-out, float refill).</summary>
public class ShiftCashEntry
{
    public int Id { get; set; }
    public int ShiftId { get; set; }
    public Shift Shift { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedById { get; set; } = string.Empty;
    /// <summary>CashIn | CashOut</summary>
    public string Type { get; set; } = "CashIn";
    public decimal Amount { get; set; }
    public string? Reason { get; set; }
}
