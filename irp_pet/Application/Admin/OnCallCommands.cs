using FluentValidation;
using MediatR;
using irp_pet.DTOs;
using irp_pet.Models;
using irp_pet.Services;

namespace irp_pet.Application.Admin;

public record GetCurrentOnCallQuery : IRequest<List<OnCallInfoDto>>;

public class GetCurrentOnCallQueryHandler : IRequestHandler<GetCurrentOnCallQuery, List<OnCallInfoDto>>
{
    private readonly OnCallService _onCall;
    public GetCurrentOnCallQueryHandler(OnCallService onCall) => _onCall = onCall;
    public Task<List<OnCallInfoDto>> Handle(GetCurrentOnCallQuery request, CancellationToken ct) =>
        _onCall.GetCurrentAllDtoAsync(ct);
}

public record CreateOnCallShiftCommand(Guid UserId, DateTime StartsAtUtc, DateTime EndsAtUtc, string? Note)
    : IRequest<OnCallShift?>;

public class CreateOnCallShiftCommandValidator : AbstractValidator<CreateOnCallShiftCommand>
{
    public CreateOnCallShiftCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.EndsAtUtc).GreaterThan(x => x.StartsAtUtc);
    }
}

public class CreateOnCallShiftCommandHandler : IRequestHandler<CreateOnCallShiftCommand, OnCallShift?>
{
    private readonly OnCallService _onCall;
    public CreateOnCallShiftCommandHandler(OnCallService onCall) => _onCall = onCall;

    public Task<OnCallShift?> Handle(CreateOnCallShiftCommand request, CancellationToken ct) =>
        _onCall.CreateShiftAsync(new CreateOnCallShiftRequest
        {
            UserId = request.UserId,
            StartsAtUtc = request.StartsAtUtc,
            EndsAtUtc = request.EndsAtUtc,
            Note = request.Note
        }, ct);
}
