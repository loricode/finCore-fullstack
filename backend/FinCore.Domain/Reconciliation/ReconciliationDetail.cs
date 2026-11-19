using FinCore.Domain.Common;

namespace FinCore.Domain.Reconciliation;

/// <summary>Resultados del motor de conciliación. Los valores son los Id de rec.ReconciliationResultTypes.</summary>
public enum ReconciliationResultType : byte
{
    Matched = 1,
    AmountMismatch = 2,
    DateMismatch = 3,
    MissingInBank = 4,
    MissingInternal = 5,
    DuplicateReference = 6,
    PendingReview = 7,
}

/// <summary>
/// Resultado de comparar una operación y/o un movimiento bancario. Tabla rec.ReconciliationDetails.
/// Lo crea el motor de conciliación (SQL); aquí solo vive la regla de resolución (RC-07):
/// una diferencia se resuelve una vez, con nota, y el resultado original no cambia.
/// </summary>
public sealed class ReconciliationDetail
{
    public long Id { get; private set; }
    public int ReconciliationId { get; private set; }
    public ReconciliationResultType ResultType { get; private set; }
    public long? TransactionId { get; private set; }
    public long? BankStatementDetailId { get; private set; }
    public decimal? InternalAmount { get; private set; }
    public decimal? BankAmount { get; private set; }
    public decimal DifferenceAmount { get; private set; }
    public string? Notes { get; private set; }
    public DateTime? ResolvedAtUtc { get; private set; }
    public int? ResolvedByUserId { get; private set; }
    public string? ResolutionNote { get; private set; }

    public const int MaxNoteLength = 500;

    private ReconciliationDetail() { } // EF Core

    public bool IsResolved => ResolvedAtUtc is not null;

    public bool RequiresAction => ResultType != ReconciliationResultType.Matched;

    public void Resolve(string note, int userId, DateTime nowUtc)
    {
        if (!RequiresAction)
            throw new DomainException("reconciliation.matched_not_resolvable", "Un resultado conciliado no requiere resolución.");
        if (IsResolved)
            throw new DomainException("reconciliation.already_resolved", "Esta diferencia ya fue resuelta.");
        if (string.IsNullOrWhiteSpace(note))
            throw new DomainException("reconciliation.note_required", "La nota de resolución es obligatoria.");

        var trimmed = note.Trim();
        ResolutionNote = trimmed.Length <= MaxNoteLength ? trimmed : trimmed[..MaxNoteLength];
        ResolvedByUserId = userId;
        ResolvedAtUtc = nowUtc;
    }
}
