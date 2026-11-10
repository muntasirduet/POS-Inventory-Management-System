using Microsoft.EntityFrameworkCore;
using POS.Core.Entities;
using POS.Infrastructure.Data;

namespace POS.Application.Services;

public class StoreCreditService
{
    private readonly AppDbContext _db;
    public StoreCreditService(AppDbContext db) => _db = db;

    public async Task<StoreCreditAccount> GetOrCreateAsync(int customerId)
    {
        var account = await _db.StoreCreditAccounts
            .Include(sc => sc.Transactions)
            .FirstOrDefaultAsync(sc => sc.CustomerId == customerId);
        if (account == null)
        {
            account = new StoreCreditAccount { CustomerId = customerId };
            _db.StoreCreditAccounts.Add(account);
            await _db.SaveChangesAsync();
        }
        return account;
    }

    public async Task<StoreCreditAccount> IssueCreditAsync(int customerId, decimal amount, int? saleId, string notes = "Store credit issued")
    {
        var account = await GetOrCreateAsync(customerId);
        account.Balance += amount;
        account.TotalIssued += amount;
        _db.StoreCreditTransactions.Add(new StoreCreditTransaction
        {
            StoreCreditAccountId = account.Id,
            Type = "Issue",
            Amount = amount,
            BalanceAfter = account.Balance,
            SaleId = saleId,
            Notes = notes
        });
        await _db.SaveChangesAsync();
        return account;
    }

    /// <summary>Redeems up to <paramref name="amountNeeded"/>. Returns actual amount redeemed.</summary>
    public async Task<decimal> RedeemAsync(int customerId, decimal amountNeeded, int? saleId)
    {
        var account = await _db.StoreCreditAccounts.FirstOrDefaultAsync(sc => sc.CustomerId == customerId);
        if (account == null || account.Balance <= 0)
            throw new InvalidOperationException("No store credit available.");
        var redeemed = Math.Min(account.Balance, amountNeeded);
        account.Balance -= redeemed;
        _db.StoreCreditTransactions.Add(new StoreCreditTransaction
        {
            StoreCreditAccountId = account.Id,
            Type = "Redeem",
            Amount = redeemed,
            BalanceAfter = account.Balance,
            SaleId = saleId,
            Notes = $"Redeemed on sale"
        });
        await _db.SaveChangesAsync();
        return redeemed;
    }

    public async Task<StoreCreditAccount?> GetAsync(int customerId)
        => await _db.StoreCreditAccounts
            .Include(sc => sc.Transactions.OrderByDescending(t => t.CreatedAt).Take(30))
            .FirstOrDefaultAsync(sc => sc.CustomerId == customerId);
}
