using FluentValidation;
using MediatR;
using irp_pet.DTOs;
using irp_pet.Services;

namespace irp_pet.Application.Incidents;

public record AcknowledgeIncidentCommand(Guid IncidentId, Guid UserId, long RowVersion) : IRequest<IncidentActionResult>;

public class AcknowledgeIncidentCommandValidator : AbstractValidator<AcknowledgeIncidentCommand>
{
    public AcknowledgeIncidentCommandValidator()
    {
        RuleFor(x => x.IncidentId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.RowVersion).GreaterThanOrEqualTo(0);
    }
}

public class AcknowledgeIncidentCommandHandler : IRequestHandler<AcknowledgeIncidentCommand, IncidentActionResult>
{
    private readonly IncidentService _incidents;
    public AcknowledgeIncidentCommandHandler(IncidentService incidents) => _incidents = incidents;
    public Task<IncidentActionResult> Handle(AcknowledgeIncidentCommand request, CancellationToken ct) =>
        _incidents.AcknowledgeAsync(request.IncidentId, request.UserId, request.RowVersion, ct);
}

public record ResolveIncidentCommand(Guid IncidentId, Guid UserId, long RowVersion) : IRequest<IncidentActionResult>;

public class ResolveIncidentCommandValidator : AbstractValidator<ResolveIncidentCommand>
{
    public ResolveIncidentCommandValidator()
    {
        RuleFor(x => x.IncidentId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.RowVersion).GreaterThanOrEqualTo(0);
    }
}

public class ResolveIncidentCommandHandler : IRequestHandler<ResolveIncidentCommand, IncidentActionResult>
{
    private readonly IncidentService _incidents;
    public ResolveIncidentCommandHandler(IncidentService incidents) => _incidents = incidents;
    public Task<IncidentActionResult> Handle(ResolveIncidentCommand request, CancellationToken ct) =>
        _incidents.ResolveAsync(request.IncidentId, request.UserId, request.RowVersion, ct);
}
