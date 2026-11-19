using System.Text.RegularExpressions;
using FinCore.Domain.Security;
using FluentValidation;

namespace FinCore.Application.Users;

public sealed record UserSummaryDto(
    int Id,
    string UserName,
    string FullName,
    string Email,
    bool IsActive,
    bool IsLockedOut,
    DateTime? LastLoginAtUtc,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> BranchCodes,
    string? WindowsAccount);

public sealed record UserDetailDto(
    int Id,
    string UserName,
    string FullName,
    string Email,
    bool IsActive,
    bool IsLockedOut,
    DateTime? LastLoginAtUtc,
    DateTime CreatedAtUtc,
    IReadOnlyList<string> Roles,
    IReadOnlyList<int> BranchIds,
    string RowVersion,
    string? WindowsAccount);

public sealed record RoleDto(string Code, string Name, string? Description, IReadOnlyList<string> Permissions, bool GrantsAllBranches);

public interface IUserQueries
{
    Task<IReadOnlyList<UserSummaryDto>> ListByCompanyAsync(int companyId, DateTime nowUtc, CancellationToken cancellationToken);
    Task<UserDetailDto?> GetAsync(int companyId, int userId, DateTime nowUtc, CancellationToken cancellationToken);
    Task<IReadOnlyList<RoleDto>> ListRolesAsync(CancellationToken cancellationToken);
}

public sealed record CreateUserCommand(
    string UserName,
    string Email,
    string FullName,
    string Password,
    IReadOnlyList<string> Roles,
    IReadOnlyList<int> BranchIds);

public sealed record UpdateUserCommand(
    int UserId,
    string Email,
    string FullName,
    IReadOnlyList<string> Roles,
    IReadOnlyList<int> BranchIds,
    string RowVersion);

public sealed record UpdateUserBranchesCommand(int UserId, IReadOnlyList<int> BranchIds);

/// <summary>Cuenta de Windows con la que el usuario abre los informes de Reporting Services. Null o vacío quita la asociación.</summary>
public sealed record SetUserWindowsAccountCommand(int UserId, string? WindowsAccount);

internal static partial class UserRules
{
    [GeneratedRegex("^[a-z0-9][a-z0-9._-]{2,49}$")]
    public static partial Regex UserName();

    /// <summary>DOMINIO\usuario o EQUIPO\usuario: una sola barra invertida, sin espacios ni caracteres que Windows no admite.</summary>
    [GeneratedRegex(@"^[^\\/:*?""<>|\s]{1,100}\\[^\\/:*?""<>|\s\[\]=;,+]{1,104}$")]
    public static partial Regex WindowsAccount();

    public static IRuleBuilderOptions<T, string> ValidFullName<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithMessage("El nombre es obligatorio.")
            .Length(3, 150).WithMessage("El nombre debe tener entre 3 y 150 caracteres.");

    public static IRuleBuilderOptions<T, string> ValidEmail<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithMessage("El correo es obligatorio.")
            .MaximumLength(150).WithMessage("El correo no puede superar 150 caracteres.")
            .EmailAddress().WithMessage("El correo no es válido.");

    public static IRuleBuilderOptions<T, IReadOnlyList<string>> ValidRoles<T>(this IRuleBuilder<T, IReadOnlyList<string>> rule) =>
        rule.NotNull().Must(r => r is { Count: > 0 }).WithMessage("Asigna al menos un rol.");
}

internal sealed class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.UserName).NotEmpty().WithMessage("El usuario es obligatorio.")
            .Must(u => UserRules.UserName().IsMatch(u?.Trim().ToLowerInvariant() ?? ""))
            .WithMessage("El usuario debe tener de 3 a 50 caracteres: letras minúsculas, números, punto, guion o guion bajo.");
        RuleFor(x => x.Email).ValidEmail();
        RuleFor(x => x.FullName).ValidFullName();
        RuleFor(x => x.Password).Must(PasswordPolicy.IsSatisfiedBy).WithMessage(PasswordPolicy.Description);
        RuleFor(x => x.Roles).ValidRoles();
        RuleFor(x => x.BranchIds).NotNull();
    }
}

internal sealed class UpdateUserCommandValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserCommandValidator()
    {
        RuleFor(x => x.Email).ValidEmail();
        RuleFor(x => x.FullName).ValidFullName();
        RuleFor(x => x.Roles).ValidRoles();
        RuleFor(x => x.BranchIds).NotNull();
        RuleFor(x => x.RowVersion).NotEmpty().WithMessage("Falta la versión del registro (rowVersion).");
    }
}

internal sealed class SetUserWindowsAccountCommandValidator : AbstractValidator<SetUserWindowsAccountCommand>
{
    public SetUserWindowsAccountCommandValidator()
    {
        RuleFor(x => x.WindowsAccount)
            .Must(a => UserRules.WindowsAccount().IsMatch(a!.Trim()))
            .When(x => !string.IsNullOrWhiteSpace(x.WindowsAccount))
            .WithMessage(@"Escribe la cuenta como DOMINIO\usuario (por ejemplo EQUIPO\ana).");
    }
}
