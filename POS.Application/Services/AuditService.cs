using POS.Core.Entities;
using POS.Infrastructure.Data;

namespace POS.Application.Services;

public class AuditService
{
    private readonly AppDbContext _db;
    public AuditService(AppDbContext db) => _db = db;

    public async Task LogAsync(string userId, string action, string entityType, string? entityId = null, string? oldValues = null, string? newValues = null, string? ipAddress = null)
    {
        _db.AuditLogs.Add(new AuditLog { UserId = userId, Action = action, EntityType = entityType, EntityId = entityId, OldValues = oldValues, NewValues = newValues, IpAddress = ipAddress, Timestamp = DateTime.UtcNow });
        await _db.SaveChangesAsync();
    }
}
