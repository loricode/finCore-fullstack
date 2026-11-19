namespace FinCore.Domain.Operations;

/// <summary>Reglas numéricas de operaciones (PRD: RN-01, RN-02, RN-08).</summary>
public static class TransactionRules
{
    /// <summary>RN-01: valor máximo por operación.</summary>
    public const decimal MaxAmount = 5_000_000_000m;

    /// <summary>RN-02: días hacia atrás permitidos para la fecha de operación.</summary>
    public const int MaxBackdatedDays = 30;

    /// <summary>RN-08: desde este valor, quien registra no puede procesar.</summary>
    public const decimal DualControlThreshold = 10_000_000m;

    public const int ReferenceMinLength = 4;
    public const int ReferenceMaxLength = 40;

    /// <summary>RN-03: la referencia se guarda en mayúsculas y sin espacios en los extremos.</summary>
    public static string NormalizeReference(string reference) => reference.Trim().ToUpperInvariant();
}
