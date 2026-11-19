namespace FinCore.Domain.Operations;

/// <summary>Cuenta financiera de una sucursal. Tabla fin.Accounts.</summary>
public sealed class Account
{
    public int Id { get; private set; }
    public int CompanyId { get; private set; }
    public int BranchId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string BankName { get; private set; } = string.Empty;
    public string BankAccountNumber { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }

    private Account() { } // EF Core
}
