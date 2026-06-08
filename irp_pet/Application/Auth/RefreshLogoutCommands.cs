using FluentValidation;
using MediatR;
using irp_pet.DTOs;
using irp_pet.Services;

namespace irp_pet.Application.Auth;

public record RefreshCommand(string RefreshToken) : IRequest<AuthResponse?>;

public class RefreshCommandValidator : AbstractValidator<RefreshCommand>
{
    public RefreshCommandValidator() => RuleFor(x => x.RefreshToken).NotEmpty();
}

public class RefreshCommandHandler : IRequestHandler<RefreshCommand, AuthResponse?>
{
    private readonly AuthService _auth;
    public RefreshCommandHandler(AuthService auth) => _auth = auth;
    public Task<AuthResponse?> Handle(RefreshCommand request, CancellationToken ct) =>
        _auth.RefreshAsync(request.RefreshToken, ct);
}

public record LogoutCommand(string RefreshToken) : IRequest<bool>;

public class LogoutCommandValidator : AbstractValidator<LogoutCommand>
{
    public LogoutCommandValidator() => RuleFor(x => x.RefreshToken).NotEmpty();
}

public class LogoutCommandHandler : IRequestHandler<LogoutCommand, bool>
{
    private readonly AuthService _auth;
    public LogoutCommandHandler(AuthService auth) => _auth = auth;
    public Task<bool> Handle(LogoutCommand request, CancellationToken ct) =>
        _auth.LogoutAsync(request.RefreshToken, ct);
}
