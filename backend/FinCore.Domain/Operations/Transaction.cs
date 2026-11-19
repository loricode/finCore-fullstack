using FinCore.Domain.Common;

namespace FinCore.Domain.Operations;

/// <summary>Datos para registrar una operación (ya validados en formato por Application).</summary>
public sealed record NewTransaction(
    Account Account,
    TransactionType Type,
    string Reference,
    DateOnly OperationDate,
    decimal Amount,
    string? Description,
    string? CounterpartyName,
    string? CounterpartyAccount,
    string? IdempotencyKey);

/// <summary>Cambios permitidos sobre una operación pendiente.</summary>
public sealed record PendingTransactionChanges(
    DateOnly OperationDate,
    decimal Amount,
    string? Description,
    string? CounterpartyName,
    string? CounterpartyAccount);

/// <summary>
/// Operación financiera. Tabla fin.Transactions. Es el dueño de la máquina de estados (spec operaciones-financieras):
///
///   Pending ──process──▶ Processed ──(motor de conciliación)──▶ Reconciled
///      │                    │                                      │
///      └─reject─▶ Rejected  └──────reverse──▶ Reversed ◀──reverse──┘
///
/// La base de datos protege lo irreversible (trigger fin.trg_Transactions_Guard); aquí están las reglas completas.
/// </summary>
public sealed class Transaction
{
    public long Id { get; private set; }
    public Guid PublicId { get; private set; }
    public int CompanyId { get; private set; }
    public int BranchId { get; private set; }
    public int AccountId { get; private set; }
    public TransactionType Type { get; private set; }
    public TransactionStatus Status { get; private set; }
    public string Reference { get; private set; } = string.Empty;
    public DateOnly OperationDate { get; private set; }
    public decimal Amount { get; private set; }
    public short Direction { get; private set; }
    public string Currency { get; private set; } = "COP";
    public string? Description { get; private set; }
    public string? CounterpartyName { get; private set; }
    public string? CounterpartyAccount { get; private set; }
    public long? OriginalTransactionId { get; private set; }
    public string? RejectionReason { get; private set; }
    public string Source { get; private set; } = "MANUAL";
    public string? IdempotencyKey { get; private set; }
    public int CreatedByUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public int? UpdatedByUserId { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }
    public DateTime? ProcessedAtUtc { get; private set; }
    public DateTime? ReconciledAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public decimal SignedAmount => Amount * Direction;

    private Transaction() { } // EF Core

    /// <summary>Registra una operación nueva en estado Pendiente.</summary>
    public static Transaction Register(NewTransaction data, int userId, DateTime nowUtc)
    {
        if (data.Type == TransactionType.Reversal)
            throw new DomainException("transaction.reversal_not_allowed", "Una reversión solo se crea revirtiendo una operación.");
        if (!data.Account.IsActive)
            throw new DomainException("transaction.account_inactive", "La cuenta está inactiva.");

        EnsureValidAmount(data.Amount);
        EnsureValidDate(data.OperationDate, nowUtc);
        EnsureCounterparty(data.Type, data.CounterpartyName, data.CounterpartyAccount);

        return new Transaction
        {
            PublicId = Guid.CreateVersion7(),
            CompanyId = data.Account.CompanyId,
            BranchId = data.Account.BranchId,
            AccountId = data.Account.Id,
            Type = data.Type,
            Status = TransactionStatus.Pending,
            Reference = TransactionRules.NormalizeReference(data.Reference),
            OperationDate = data.OperationDate,
            Amount = data.Amount,
            Direction = data.Type.DefaultDirection(),
            Description = Clean(data.Description),
            CounterpartyName = Clean(data.CounterpartyName),
            CounterpartyAccount = Clean(data.CounterpartyAccount),
            IdempotencyKey = Clean(data.IdempotencyKey),
            CreatedByUserId = userId,
            CreatedAtUtc = nowUtc,
        };
    }

    /// <summary>RN-06: solo una pendiente se edita, y solo por quien la creó o por alguien que puede procesar.</summary>
    public void UpdatePending(PendingTransactionChanges changes, int userId, bool canEditOthers, DateTime nowUtc)
    {
        EnsureStatus(TransactionStatus.Pending, "editar");
        if (userId != CreatedByUserId && !canEditOthers)
            throw new DomainException("transaction.edit_forbidden", "Solo quien registró la operación o un supervisor puede editarla.");

        EnsureValidAmount(changes.Amount);
        EnsureValidDate(changes.OperationDate, nowUtc);
        EnsureCounterparty(Type, changes.CounterpartyName, changes.CounterpartyAccount);

        OperationDate = changes.OperationDate;
        Amount = changes.Amount;
        Description = Clean(changes.Description);
        CounterpartyName = Clean(changes.CounterpartyName);
        CounterpartyAccount = Clean(changes.CounterpartyAccount);
        Touch(userId, nowUtc);
    }

    /// <summary>RN-05 y RN-08: Pendiente → Procesada, con doble control desde $10.000.000.</summary>
    public void Process(int userId, DateTime nowUtc)
    {
        EnsureStatus(TransactionStatus.Pending, "procesar");
        if (Amount >= TransactionRules.DualControlThreshold && userId == CreatedByUserId)
            throw new DomainException("transaction.dual_control",
                $"Una operación de {TransactionRules.DualControlThreshold:N0} o más debe procesarla una persona distinta a quien la registró.");

        Status = TransactionStatus.Processed;
        ProcessedAtUtc = nowUtc;
        Touch(userId, nowUtc);
    }

    /// <summary>RN-07: Pendiente → Rechazada, con motivo obligatorio.</summary>
    public void Reject(string reason, int userId, DateTime nowUtc)
    {
        EnsureStatus(TransactionStatus.Pending, "rechazar");
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("transaction.reason_required", "El motivo del rechazo es obligatorio.");

        Status = TransactionStatus.Rejected;
        RejectionReason = reason.Trim();
        Touch(userId, nowUtc);
    }

    /// <summary>
    /// RN-07: revierte una operación Procesada o Conciliada. Crea una operación de tipo Reversión con el mismo valor
    /// y sentido contrario (ya procesada) y deja la original en Revertida. Una reversión no se revierte.
    /// </summary>
    public Transaction Reverse(string reason, int userId, DateTime nowUtc)
    {
        if (Type == TransactionType.Reversal)
            throw new DomainException("transaction.reversal_of_reversal", "Una reversión no se puede revertir.");
        if (Status is not (TransactionStatus.Processed or TransactionStatus.Reconciled))
            throw new DomainException("transaction.invalid_transition",
                $"Solo se revierten operaciones procesadas o conciliadas (estado actual: {Status}).");
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("transaction.reason_required", "El motivo de la reversión es obligatorio.");

        var reversal = new Transaction
        {
            PublicId = Guid.CreateVersion7(),
            CompanyId = CompanyId,
            BranchId = BranchId,
            AccountId = AccountId,
            Type = TransactionType.Reversal,
            Status = TransactionStatus.Processed,
            Reference = ReversalReference(Reference),
            OperationDate = BusinessCalendar.Today(nowUtc),
            Amount = Amount,
            Direction = (short)-Direction,
            Description = Truncate($"Reversión de {Reference}: {reason.Trim()}", 250),
            OriginalTransactionId = Id,
            CreatedByUserId = userId,
            CreatedAtUtc = nowUtc,
            ProcessedAtUtc = nowUtc,
        };

        Status = TransactionStatus.Reversed;
        Touch(userId, nowUtc);
        return reversal;
    }

    /// <summary>Referencia de la reversión: REV- + la original (recortada para no superar 40 caracteres).</summary>
    public static string ReversalReference(string originalReference) =>
        "REV-" + Truncate(originalReference, TransactionRules.ReferenceMaxLength - 4);

    private void EnsureStatus(TransactionStatus expected, string action)
    {
        if (Status != expected)
            throw new DomainException("transaction.invalid_transition",
                $"No se puede {action} una operación en estado {Status}; debe estar {expected}.");
    }

    private static void EnsureValidAmount(decimal amount)
    {
        if (amount <= 0 || amount > TransactionRules.MaxAmount || decimal.Round(amount, 2) != amount)
            throw new DomainException("transaction.invalid_amount", "El valor debe ser positivo, con máximo 2 decimales y hasta 5.000 millones.");
    }

    private static void EnsureValidDate(DateOnly date, DateTime nowUtc)
    {
        var today = BusinessCalendar.Today(nowUtc);
        if (date > today || date < today.AddDays(-TransactionRules.MaxBackdatedDays))
            throw new DomainException("transaction.invalid_date",
                $"La fecha no puede ser futura ni anterior a {TransactionRules.MaxBackdatedDays} días.");
    }

    private static void EnsureCounterparty(TransactionType type, string? name, string? account)
    {
        if (type.RequiresCounterpartyName() && string.IsNullOrWhiteSpace(name))
            throw new DomainException("transaction.counterparty_required", "Transferencias y pagos requieren el nombre de la contraparte.");
        if (type.RequiresCounterpartyAccount() && string.IsNullOrWhiteSpace(account))
            throw new DomainException("transaction.counterparty_account_required", "Una transferencia requiere la cuenta destino.");
    }

    private void Touch(int userId, DateTime nowUtc)
    {
        UpdatedByUserId = userId;
        UpdatedAtUtc = nowUtc;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
