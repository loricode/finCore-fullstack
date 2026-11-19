using FinCore.Domain.Common;

namespace FinCore.Domain.Security;

/// <summary>
/// Usuario de la plataforma. Tabla sec.Users.
/// Contiene las reglas de inicio de sesión (bloqueo tras intentos fallidos) y de administración (activar, desactivar).
/// Roles y sucursales asignadas son relaciones que administra Application (sec.UserRoles, sec.UserBranches).
/// </summary>
public sealed class User
{
    public int Id { get; private set; }
    public int CompanyId { get; private set; }
    public string UserName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string FullName { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public int FailedLoginCount { get; private set; }
    public DateTime? LockoutEndUtc { get; private set; }
    public DateTime? LastLoginAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    /// <summary>Concurrencia optimista (ROWVERSION).</summary>
    public byte[] RowVersion { get; private set; } = [];

    private User() { } // EF Core

    public static User Create(int companyId, string userName, string email, string fullName, string passwordHash, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(userName)) throw new DomainException("user.username_required", "El usuario es obligatorio.");
        if (string.IsNullOrWhiteSpace(passwordHash)) throw new DomainException("user.password_required", "La contraseña es obligatoria.");

        return new User
        {
            CompanyId = companyId,
            UserName = userName.Trim().ToLowerInvariant(),
            Email = email.Trim().ToLowerInvariant(),
            FullName = fullName.Trim(),
            PasswordHash = passwordHash,
            IsActive = true,
            CreatedAtUtc = nowUtc,
        };
    }

    public bool IsLockedOut(DateTime nowUtc) => LockoutEndUtc is { } end && end > nowUtc;

    /// <summary>Puede iniciar sesión o renovar su sesión: está activo y no bloqueado.</summary>
    public bool CanSignIn(DateTime nowUtc) => IsActive && !IsLockedOut(nowUtc);

    /// <summary>
    /// Registra un intento fallido. Al llegar a <see cref="LoginPolicy.MaxFailedAttempts"/> bloquea la cuenta
    /// y reinicia el contador, de modo que al vencer el bloqueo hay de nuevo cinco intentos.
    /// </summary>
    /// <returns><c>true</c> si este intento bloqueó la cuenta.</returns>
    public bool RegisterFailedLogin(DateTime nowUtc)
    {
        UpdatedAtUtc = nowUtc;
        FailedLoginCount++;

        if (FailedLoginCount < LoginPolicy.MaxFailedAttempts)
            return false;

        LockoutEndUtc = nowUtc.Add(LoginPolicy.LockoutDuration);
        FailedLoginCount = 0;
        return true;
    }

    public void RegisterSuccessfulLogin(DateTime nowUtc)
    {
        if (!CanSignIn(nowUtc))
            throw new DomainException("user.cannot_sign_in", "El usuario está inactivo o bloqueado.");

        FailedLoginCount = 0;
        LockoutEndUtc = null;
        LastLoginAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>Reemplaza el hash cuando el algoritmo o sus parámetros se actualizaron.</summary>
    public void UpgradePasswordHash(string newHash, DateTime nowUtc)
    {
        PasswordHash = newHash;
        UpdatedAtUtc = nowUtc;
    }

    public void UpdateProfile(string fullName, string email, DateTime nowUtc)
    {
        FullName = fullName.Trim();
        Email = email.Trim().ToLowerInvariant();
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>Desactiva al usuario. Nadie puede desactivarse a sí mismo (evita dejar la empresa sin administrador).</summary>
    public void Deactivate(int performedByUserId, DateTime nowUtc)
    {
        if (performedByUserId == Id)
            throw new DomainException("user.cannot_deactivate_self", "No puedes desactivar tu propio usuario.");
        if (!IsActive) return;
        IsActive = false;
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>Activa al usuario y levanta un bloqueo vigente.</summary>
    public void Activate(DateTime nowUtc)
    {
        IsActive = true;
        LockoutEndUtc = null;
        FailedLoginCount = 0;
        UpdatedAtUtc = nowUtc;
    }
}
