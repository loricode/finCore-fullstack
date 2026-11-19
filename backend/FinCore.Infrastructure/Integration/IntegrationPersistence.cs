using FinCore.Application.Integration;
using FinCore.Domain.Integration;
using FinCore.Domain.Operations;
using FinCore.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinCore.Infrastructure.Integration;

// ------------------------------------------------------------------ modelos de persistencia

internal sealed class BankStatementRecord
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public string Source { get; set; } = string.Empty;
    public DateOnly PeriodFrom { get; set; }
    public DateOnly PeriodTo { get; set; }
    public string? FileName { get; set; }
    public int LineCount { get; set; }
    public List<BankStatementDetailRecord> Details { get; set; } = [];
}

internal sealed class BankStatementDetailRecord
{
    public long Id { get; set; }
    public int BankStatementId { get; set; }
    public int AccountId { get; set; }
    public string ExternalMovementId { get; set; } = string.Empty;
    public string? BankReference { get; set; }
    public DateOnly MovementDate { get; set; }
    public decimal Amount { get; set; }
    public string? Description { get; set; }
    public BankStatementRecord? Statement { get; set; }
}

internal sealed class WatermarkRecord
{
    public string SourceName { get; set; } = string.Empty;
    public long LastValue { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

internal sealed class InboundEventConfiguration : IEntityTypeConfiguration<InboundEvent>
{
    public void Configure(EntityTypeBuilder<InboundEvent> b)
    {
        b.ToTable("InboundEvents", "integration");
        b.HasKey(x => x.Id);
        b.Property(x => x.Provider).HasMaxLength(50).IsUnicode(false);
        b.Property(x => x.ExternalEventId).HasMaxLength(100);
        b.Property(x => x.EventType).HasMaxLength(60).IsUnicode(false);
        b.Property(x => x.PayloadHash).HasMaxLength(64).IsFixedLength().IsUnicode(false);
        b.Property(x => x.Status).HasMaxLength(12).IsUnicode(false)
            .HasConversion(v => v.ToString().ToUpperInvariant(), v => Enum.Parse<InboundEventStatus>(v, true));
        b.Property(x => x.LastError).HasMaxLength(2000);
        b.Property(x => x.CorrelationId).HasMaxLength(64);
        b.Ignore(x => x.CanBeProcessed);
    }
}

internal sealed class BankStatementConfiguration : IEntityTypeConfiguration<BankStatementRecord>
{
    public void Configure(EntityTypeBuilder<BankStatementRecord> b)
    {
        b.ToTable("BankStatements", "rec");
        b.HasKey(x => x.Id);
        b.Property(x => x.Source).HasMaxLength(10).IsUnicode(false);
        b.Property(x => x.FileName).HasMaxLength(260);
        b.HasMany(x => x.Details).WithOne(x => x.Statement).HasForeignKey(x => x.BankStatementId);
    }
}

internal sealed class BankStatementDetailConfiguration : IEntityTypeConfiguration<BankStatementDetailRecord>
{
    public void Configure(EntityTypeBuilder<BankStatementDetailRecord> b)
    {
        b.ToTable("BankStatementDetails", "rec");
        b.HasKey(x => x.Id);
        b.Property(x => x.ExternalMovementId).HasMaxLength(60);
        b.Property(x => x.BankReference).HasMaxLength(40);
        b.Property(x => x.Description).HasMaxLength(250);
    }
}

internal sealed class WatermarkConfiguration : IEntityTypeConfiguration<WatermarkRecord>
{
    public void Configure(EntityTypeBuilder<WatermarkRecord> b)
    {
        b.ToTable("Watermarks", "etl");
        b.HasKey(x => x.SourceName);
        b.Property(x => x.SourceName).HasMaxLength(100);
    }
}

// ------------------------------------------------------------------ repositorios

internal sealed class InboundEventRepository(FinCoreDbContext db) : IInboundEventRepository
{
    public Task<InboundEvent?> GetAsync(string provider, string externalEventId, CancellationToken ct) =>
        db.Set<InboundEvent>().SingleOrDefaultAsync(e => e.Provider == provider && e.ExternalEventId == externalEventId, ct);

    public async Task<IReadOnlyList<InboundEvent>> GetStaleReceivedAsync(DateTime receivedBeforeUtc, int max, CancellationToken ct) =>
        await db.Set<InboundEvent>()
            .Where(e => e.Status == InboundEventStatus.Received && e.ReceivedAtUtc < receivedBeforeUtc && e.Attempts < InboundEvent.MaxAttempts)
            .OrderBy(e => e.Id).Take(max).ToListAsync(ct);

    public void Add(InboundEvent inboundEvent) => db.Set<InboundEvent>().Add(inboundEvent);
}

/// <summary>
/// Movimientos por webhook: un extracto WEBHOOK por cuenta y día (PeriodFrom = PeriodTo = fecha del movimiento).
/// Guarda de inmediato para devolver el Id; el caso de uso lo llama dentro de una transacción.
/// </summary>
internal sealed class BankMovementStore(FinCoreDbContext db) : IBankMovementStore
{
    public async Task<long?> AddWebhookMovementAsync(Account account, BankEventMovement movement, CancellationToken ct)
    {
        var movementId = movement.MovementId.Trim();
        var exists = await db.Set<BankStatementDetailRecord>()
            .AnyAsync(d => d.AccountId == account.Id && d.ExternalMovementId == movementId, ct);
        if (exists) return null;

        var statement = await db.Set<BankStatementRecord>()
            .SingleOrDefaultAsync(s => s.AccountId == account.Id && s.Source == "WEBHOOK" && s.PeriodFrom == movement.Date, ct);
        if (statement is null)
        {
            statement = new BankStatementRecord
            {
                AccountId = account.Id, Source = "WEBHOOK", PeriodFrom = movement.Date, PeriodTo = movement.Date,
                FileName = $"webhook {movement.Date:yyyy-MM-dd}",
            };
            db.Set<BankStatementRecord>().Add(statement);
        }

        var detail = new BankStatementDetailRecord
        {
            Statement = statement,
            AccountId = account.Id,
            ExternalMovementId = movementId,
            BankReference = Normalize(movement.Reference),   // misma regla que rec.fn_NormalizeReference
            MovementDate = movement.Date,
            Amount = movement.Amount,
            Description = string.IsNullOrWhiteSpace(movement.Description) ? null : movement.Description.Trim(),
        };
        statement.LineCount++;
        db.Set<BankStatementDetailRecord>().Add(detail);
        await db.SaveChangesAsync(ct);
        return detail.Id;
    }

    private static string? Normalize(string? reference) =>
        string.IsNullOrWhiteSpace(reference) ? null : reference.Trim().ToUpperInvariant();
}

internal sealed class EtlWatermarkStore(FinCoreDbContext db) : IEtlWatermarkStore
{
    public async Task<long> GetAsync(string sourceName, CancellationToken ct) =>
        (await db.Set<WatermarkRecord>().AsNoTracking().SingleOrDefaultAsync(w => w.SourceName == sourceName, ct))?.LastValue ?? 0;

    public async Task SetAsync(string sourceName, long value, CancellationToken ct)
    {
        var record = await db.Set<WatermarkRecord>().SingleOrDefaultAsync(w => w.SourceName == sourceName, ct);
        if (record is null) db.Set<WatermarkRecord>().Add(record = new WatermarkRecord { SourceName = sourceName });
        record.LastValue = value;
        record.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }
}
