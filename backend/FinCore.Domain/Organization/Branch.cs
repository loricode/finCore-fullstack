namespace FinCore.Domain.Organization;

/// <summary>Sucursal de una empresa. Tabla sec.Branches.</summary>
public sealed class Branch
{
    public int Id { get; private set; }
    public int CompanyId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string City { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private Branch() { } // EF Core
}
