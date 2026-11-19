using FluentValidation;

namespace FinCore.Application.Auth.Login;

public sealed record LoginCommand(string UserName, string Password);

internal sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.UserName).NotEmpty().WithMessage("El usuario es obligatorio.")
                                .MaximumLength(50).WithMessage("El usuario no puede superar 50 caracteres.");
        RuleFor(x => x.Password).NotEmpty().WithMessage("La contraseña es obligatoria.")
                                .MaximumLength(128).WithMessage("La contraseña no puede superar 128 caracteres.");
    }
}
