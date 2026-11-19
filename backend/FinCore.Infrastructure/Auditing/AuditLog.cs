using System.Text.Json;
using FinCore.Application.Common.Abstractions;
using FinCore.Infrastructure.Persistence;

namespace FinCore.Infrastructure.Auditing;

/// <summary>
/// Agrega la entrada al DbContext: se guarda en la misma transacción que el cambio auditado.
/// Completa CorrelationId, IP y fecha desde la petición.
/// </summary>
internal sealed class AuditLog(FinCoreDbContext db, IRequestContext request, TimeProvider clock) : IAuditLog
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public void Record(AuditEntry entry) =>
        db.AuditLogs.Add(new AuditLogRecord
        {
            OccurredAtUtc = clock.GetUtcNow().UtcDateTime,
            CompanyId = entry.CompanyId,
            BranchId = entry.BranchId,
            UserId = entry.UserId,
            UserName = entry.UserName,
            Action = entry.Action,
            EntityName = entry.EntityName,
            EntityId = entry.EntityId,
            NewValues = entry.Details is null ? null : JsonSerializer.Serialize(entry.Details, Json),
            OldValues = entry.OldDetails is null ? null : JsonSerializer.Serialize(entry.OldDetails, Json),
            CorrelationId = request.CorrelationId,
            IpAddress = request.IpAddress,
        });
}
