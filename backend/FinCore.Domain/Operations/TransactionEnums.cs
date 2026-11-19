namespace FinCore.Domain.Operations;

/// <summary>Tipos de operación. Los valores son los Id de fin.TransactionTypes: no se renumeran.</summary>
public enum TransactionType : byte
{
    Income = 1,     // ING · entra dinero
    Expense = 2,    // EGR · sale
    Transfer = 3,   // TRF · sale hacia otra cuenta
    Payment = 4,    // PAG · sale hacia un tercero
    Reversal = 5,   // REV · sentido contrario a la original; solo la crea Transaction.Reverse
}

/// <summary>Estados de una operación. Los valores son los Id de fin.TransactionStatus.</summary>
public enum TransactionStatus : byte
{
    Pending = 1,
    Processed = 2,
    Reconciled = 3,
    Rejected = 4,
    Reversed = 5,
}

public static class TransactionTypeExtensions
{
    /// <summary>Sentido del dinero respecto a la cuenta: 1 entra, -1 sale.</summary>
    public static short DefaultDirection(this TransactionType type) => type switch
    {
        TransactionType.Income => 1,
        TransactionType.Expense or TransactionType.Transfer or TransactionType.Payment => -1,
        _ => throw new ArgumentOutOfRangeException(nameof(type), "La reversión toma el sentido contrario de la original."),
    };

    public static bool RequiresCounterpartyName(this TransactionType type) =>
        type is TransactionType.Transfer or TransactionType.Payment;

    public static bool RequiresCounterpartyAccount(this TransactionType type) => type is TransactionType.Transfer;

    public static string Code(this TransactionType type) => type switch
    {
        TransactionType.Income => "ING",
        TransactionType.Expense => "EGR",
        TransactionType.Transfer => "TRF",
        TransactionType.Payment => "PAG",
        TransactionType.Reversal => "REV",
        _ => type.ToString(),
    };
}
