using System.Security.Claims;
using System.Text.Json;
using CGM.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CGM.Api.Data;

public sealed class AuditSaveChangesInterceptor(IHttpContextAccessor httpContextAccessor) : SaveChangesInterceptor
{
    private static readonly HashSet<string> SensitiveNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "PasswordHash", "Token", "TokenHash", "OtpCode", "Code", "ReplacedByTokenHash"
    };
    private static readonly HashSet<Type> AuditedTypes =
    [
        typeof(User), typeof(PatientProfileEntity), typeof(CgmDeviceEntity),
        typeof(FamilyEntity), typeof(FamilyMemberEntity), typeof(AlertEntity),
        typeof(AlertRecipientEntity), typeof(AlertRuleEntity), typeof(NotificationEndpointEntity)
    ];

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        AddAuditRows(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void AddAuditRows(DbContext? context)
    {
        if (context is null) return;
        var http = httpContextAccessor.HttpContext;
        var claimUserId = int.TryParse(http?.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? http?.User.FindFirstValue("sub"), out var parsedUserId) ? parsedUserId : (int?)null;

        var entries = context.ChangeTracker.Entries()
            .Where(e => AuditedTypes.Contains(e.Metadata.ClrType)
                && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        foreach (var entry in entries)
        {
            var userId = claimUserId ?? ReadUserId(entry);
            context.Set<AuditLogEntity>().Add(new AuditLogEntity
            {
                UserId = userId,
                Action = entry.State.ToString(),
                Entity = entry.Metadata.ClrType.Name.Replace("Entity", string.Empty),
                EntityId = ReadPrimaryKey(entry),
                OldValue = entry.State is EntityState.Modified or EntityState.Deleted ? Serialize(entry, original: true) : null,
                NewValue = entry.State is EntityState.Added or EntityState.Modified ? Serialize(entry, original: false) : null,
                IpAddress = http?.Connection.RemoteIpAddress?.ToString(),
                CorrelationId = http?.TraceIdentifier,
                CreatedAt = DateTime.UtcNow
            });
        }
    }

    private static int? ReadUserId(EntityEntry entry)
    {
        var property = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "UserId");
        return property?.CurrentValue is int userId ? userId : null;
    }

    private static string? ReadPrimaryKey(EntityEntry entry)
    {
        var key = entry.Metadata.FindPrimaryKey();
        return key is null ? null : string.Join(',', key.Properties.Select(p => entry.Property(p.Name).CurrentValue));
    }

    private static string Serialize(EntityEntry entry, bool original)
    {
        var values = new Dictionary<string, object?>();
        foreach (var property in entry.Properties)
        {
            if (SensitiveNames.Any(name => property.Metadata.Name.Contains(name, StringComparison.OrdinalIgnoreCase)))
            {
                values[property.Metadata.Name] = "[REDACTED]";
                continue;
            }
            if (entry.State == EntityState.Modified && !property.IsModified) continue;
            values[property.Metadata.Name] = original ? property.OriginalValue : property.CurrentValue;
        }
        return JsonSerializer.Serialize(values);
    }
}
