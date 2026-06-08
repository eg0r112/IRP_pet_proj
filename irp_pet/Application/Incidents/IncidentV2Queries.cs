using MediatR;
using irp_pet.DTOs;
using irp_pet.Models;
using irp_pet.Services;

namespace irp_pet.Application.Incidents;

public record ListIncidentsV2Query(IncidentStatus? Status, int Page = 1, int PageSize = 20)
    : IRequest<IncidentListV2Response>;

public class ListIncidentsV2QueryHandler : IRequestHandler<ListIncidentsV2Query, IncidentListV2Response>
{
    private readonly IncidentService _incidents;

    public ListIncidentsV2QueryHandler(IncidentService incidents) => _incidents = incidents;

    public Task<IncidentListV2Response> Handle(ListIncidentsV2Query request, CancellationToken ct) =>
        _incidents.GetPagedAsync(request.Status, request.Page, request.PageSize, ct);
}
