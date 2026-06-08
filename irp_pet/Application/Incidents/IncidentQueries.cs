using MediatR;
using irp_pet.DTOs;
using irp_pet.Models;
using irp_pet.Services;

namespace irp_pet.Application.Incidents;

public record ListIncidentsQuery(IncidentStatus? Status) : IRequest<List<IncidentListItemDto>>;

public class ListIncidentsQueryHandler : IRequestHandler<ListIncidentsQuery, List<IncidentListItemDto>>
{
    private readonly IncidentService _incidents;
    public ListIncidentsQueryHandler(IncidentService incidents) => _incidents = incidents;
    public Task<List<IncidentListItemDto>> Handle(ListIncidentsQuery request, CancellationToken ct) =>
        _incidents.GetAllAsync(request.Status, ct);
}

public record GetIncidentQuery(Guid Id) : IRequest<IncidentDetailDto?>;

public class GetIncidentQueryHandler : IRequestHandler<GetIncidentQuery, IncidentDetailDto?>
{
    private readonly IncidentService _incidents;
    public GetIncidentQueryHandler(IncidentService incidents) => _incidents = incidents;
    public Task<IncidentDetailDto?> Handle(GetIncidentQuery request, CancellationToken ct) =>
        _incidents.GetByIdAsync(request.Id, ct);
}
