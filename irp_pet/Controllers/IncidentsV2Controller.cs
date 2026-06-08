using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using irp_pet.Application.Incidents;
using irp_pet.Models;

namespace irp_pet.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/incidents")]
[ApiVersion("2.0")]
[Authorize]
public class IncidentsV2Controller : ControllerBase
{
    private readonly IMediator _mediator;

    public IncidentsV2Controller(IMediator mediator) => _mediator = mediator;

    /// <summary>v2: список с пагинацией (page, pageSize, totalCount).</summary>
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] IncidentStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new ListIncidentsV2Query(status, page, pageSize), ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var item = await _mediator.Send(new GetIncidentQuery(id), ct);
        if (item is null)
            return NotFound();
        return Ok(new { apiVersion = "2.0", data = item });
    }
}
