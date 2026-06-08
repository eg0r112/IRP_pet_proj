using Asp.Versioning;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using irp_pet.Application.Incidents;
using irp_pet.DTOs;
using irp_pet.Models;

namespace irp_pet.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/incidents")]
[ApiVersion("1.0")]
[Authorize]
public class IncidentsController : ControllerBase
{
    private readonly IMediator _mediator;

    public IncidentsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] IncidentStatus? status, CancellationToken ct)
    {
        var items = await _mediator.Send(new ListIncidentsQuery(status), ct);
        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var item = await _mediator.Send(new GetIncidentQuery(id), ct);
        if (item is null)
            return NotFound();
        return Ok(item);
    }

    [HttpPost("{id:guid}/ack")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.OnCall}")]
    public async Task<IActionResult> Ack(Guid id, [FromBody] IncidentActionRequest body, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        var result = await _mediator.Send(new AcknowledgeIncidentCommand(id, userId.Value, body.RowVersion), ct);
        return MapActionResult(result, "Acknowledged.");
    }

    [HttpPost("{id:guid}/resolve")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.OnCall}")]
    public async Task<IActionResult> Resolve(Guid id, [FromBody] IncidentActionRequest body, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        var result = await _mediator.Send(new ResolveIncidentCommand(id, userId.Value, body.RowVersion), ct);
        return MapActionResult(result, "Resolved.");
    }

    private IActionResult MapActionResult(IncidentActionResult result, string successMessage) =>
        result.Status switch
        {
            IncidentActionStatus.Success => Ok(new { message = successMessage }),
            IncidentActionStatus.NotFound => NotFound(new { message = "Incident not found." }),
            IncidentActionStatus.InvalidStatus => BadRequest(new { message = "Invalid incident status for this action." }),
            IncidentActionStatus.ConcurrencyConflict => Conflict(new { message = "Incident was modified by another user. Refresh and retry." }),
            _ => BadRequest()
        };

    private Guid? GetUserId()
    {
        var sub = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}
