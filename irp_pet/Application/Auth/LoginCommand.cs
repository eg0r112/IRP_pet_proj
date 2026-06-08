using FluentValidation;
using MediatR;
using irp_pet.DTOs;
using irp_pet.Services;

namespace irp_pet.Application.Auth;

public record LoginCommand(string Email, string Password) : IRequest<AuthResponse?>;

public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(6);
    }
}

public class LoginCommandHandler : IRequestHandler<LoginCommand, AuthResponse?>
{
    private readonly AuthService _auth;
    public LoginCommandHandler(AuthService auth) => _auth = auth;
    public Task<AuthResponse?> Handle(LoginCommand request, CancellationToken ct) =>
        _auth.LoginAsync(new LoginRequest { Email = request.Email, Password = request.Password }, ct);
}
