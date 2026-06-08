using FluentValidation;
using MediatR;
using irp_pet.DTOs;
using irp_pet.Models;
using irp_pet.Services;

namespace irp_pet.Application.Admin;

public record CreateUserCommand(string Email, string Password, string DisplayName, string Role, string? TelegramChatId)
    : IRequest<CreateUserResult>;

public record CreateUserResult(bool Success, bool EmailTaken, User? User);

public class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(6);
        RuleFor(x => x.DisplayName).NotEmpty();
        RuleFor(x => x.Role).Must(r => r is Roles.Admin or Roles.OnCall or Roles.User);
    }
}

public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, CreateUserResult>
{
    private readonly AuthService _auth;
    public CreateUserCommandHandler(AuthService auth) => _auth = auth;

    public async Task<CreateUserResult> Handle(CreateUserCommand request, CancellationToken ct)
    {
        var user = await _auth.CreateUserAsync(new CreateUserRequest
        {
            Email = request.Email,
            Password = request.Password,
            DisplayName = request.DisplayName,
            Role = request.Role,
            TelegramChatId = request.TelegramChatId
        }, ct);

        return user is null
            ? new CreateUserResult(false, true, null)
            : new CreateUserResult(true, false, user);
    }
}
