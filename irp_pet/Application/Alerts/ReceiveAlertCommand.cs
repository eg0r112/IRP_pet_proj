using FluentValidation;
using MediatR;
using irp_pet.DTOs;
using irp_pet.Models;
using irp_pet.Services;

namespace irp_pet.Application.Alerts;

public record ReceiveAlertCommand(
    string ServiceKey,
    string Fingerprint,
    Models.Severity Severity,
    string Message,
    string Source,
    string? IdempotencyKey) : IRequest<ReceiveAlertResponse?>;

public class ReceiveAlertCommandValidator : AbstractValidator<ReceiveAlertCommand>
{
    public ReceiveAlertCommandValidator()
    {
        RuleFor(x => x.ServiceKey).NotEmpty();
        RuleFor(x => x.Fingerprint).NotEmpty();
        RuleFor(x => x.Message).NotEmpty();
    }
}

public class ReceiveAlertCommandHandler : IRequestHandler<ReceiveAlertCommand, ReceiveAlertResponse?>
{
    private readonly AlertService _alerts;
    public ReceiveAlertCommandHandler(AlertService alerts) => _alerts = alerts;
    public Task<ReceiveAlertResponse?> Handle(ReceiveAlertCommand request, CancellationToken ct) =>
        _alerts.ReceiveAsync(new ReceiveAlertRequest
        {
            ServiceKey = request.ServiceKey,
            Fingerprint = request.Fingerprint,
            Severity = request.Severity,
            Message = request.Message,
            Source = request.Source,
            IdempotencyKey = request.IdempotencyKey
        }, ct);
}
