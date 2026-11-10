using Microsoft.EntityFrameworkCore;
using POS.Core.Entities;
using POS.Infrastructure.Data;

namespace POS.Application.Services;

public class ShiftService
{
    private readonly AppDbContext _db;
    public ShiftService(AppDbContext db) => _db = db;

    /// <summary>Opens a new shift for an employee. Throws if employee already has an open shift.</summary>
    public async Task<Shift> OpenShiftAsync(int employeeId, int? branchId, int? terminalId, decimal openingCash)
    {
        var existing = await _db.Shifts.FirstOrDefaultAsync(s => s.EmployeeId == employeeId && s.EndTime == null);
        if (existing != null)
            throw new InvalidOperationException("Employee already has an open shift. Close it first.");

        var shift = new Shift
        {
            EmployeeId = employeeId,
            BranchId = branchId,
            TerminalId = terminalId,
            StartTime = DateTime.UtcNow,
            OpeningCash = openingCash
        };
        _db.Shifts.Add(shift);
        await _db.SaveChangesAsync();
        return shift;
    }

    /// <summary>Closes a shift and calculates expected cash. Optionally saves denomination breakdown.</summary>
    public async Task<Shift> CloseShiftAsync(int shiftId, decimal closingCash, string? notes,
        List<(decimal denomination, int count)>? denominations = null)
    {
        var shift = await _db.Shifts
            .Include(s => s.CashEntries)
            .FirstOrDefaultAsync(s => s.Id == shiftId && s.EndTime == null)
            ?? throw new InvalidOperationException("Shift not found or already closed.");

        // Cash sales during this shift
        var cashSales = await _db.Payments
            .Include(p => p.Sale)
            .Where(p => p.Method == Core.Enums.PaymentMethod.Cash
                     && p.Sale.CreatedAt >= shift.StartTime
                     && (shift.EndTime == null || p.Sale.CreatedAt <= shift.EndTime)
                     && p.Sale.BranchId == shift.BranchId
                     && p.Sale.Status == Core.Enums.SaleStatus.Completed)
            .SumAsync(p => p.Amount);

        var cashIn = shift.CashEntries.Where(e => e.Type == "CashIn").Sum(e => e.Amount);
        var cashOut = shift.CashEntries.Where(e => e.Type == "CashOut").Sum(e => e.Amount);

        shift.ExpectedCash = shift.OpeningCash + cashSales + cashIn - cashOut;
        shift.ClosingCash = closingCash;
        shift.EndTime = DateTime.UtcNow;
        shift.Notes = notes;

        // Save denomination breakdown
        if (denominations != null)
        {
            foreach (var (denom, count) in denominations.Where(d => d.count > 0))
                _db.ShiftDenominations.Add(new ShiftDenomination { ShiftId = shiftId, DenominationValue = denom, Count = count });
        }

        await _db.SaveChangesAsync();
        return shift;
    }

    public async Task<ShiftCashEntry> AddCashEntryAsync(int shiftId, string type, decimal amount, string? reason, string createdById)
    {
        var shift = await _db.Shifts.FirstOrDefaultAsync(s => s.Id == shiftId && s.EndTime == null)
            ?? throw new InvalidOperationException("Shift not found or already closed.");

        var entry = new ShiftCashEntry
        {
            ShiftId = shiftId, Type = type, Amount = amount,
            Reason = reason, CreatedById = createdById
        };
        _db.ShiftCashEntries.Add(entry);
        await _db.SaveChangesAsync();
        return entry;
    }

    public async Task<Shift?> GetOpenShiftAsync(int employeeId) =>
        await _db.Shifts.Include(s => s.CashEntries).Include(s => s.Branch).Include(s => s.Terminal)
            .FirstOrDefaultAsync(s => s.EmployeeId == employeeId && s.EndTime == null);

    public async Task<List<Shift>> GetShiftsAsync(int? branchId, DateTime? from, DateTime? to) =>
        await _db.Shifts
            .Include(s => s.Employee)
            .Include(s => s.Branch)
            .Include(s => s.CashEntries)
            .Where(s => (!branchId.HasValue || s.BranchId == branchId)
                     && (!from.HasValue || s.StartTime >= from.Value)
                     && (!to.HasValue || s.StartTime <= to.Value.AddDays(1)))
            .OrderByDescending(s => s.StartTime)
            .ToListAsync();
}
