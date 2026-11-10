namespace POS.Core.Entities;

/// <summary>Records each denomination counted during shift close (e.g. 20x $10 = $200).</summary>
public class ShiftDenomination
{
    public int Id { get; set; }
    public int ShiftId { get; set; }
    public Shift Shift { get; set; } = null!;
    public decimal DenominationValue { get; set; }
    public int Count { get; set; }
    public decimal SubTotal => DenominationValue * Count;
}
