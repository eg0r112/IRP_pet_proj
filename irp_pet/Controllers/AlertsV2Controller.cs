using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using irp_pet.Application.Alerts;
using irp_pet.Auth;
using irp_pet.DTOs;

namespace irp_pet.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/alerts")]
[ApiVersion("2.0")]
[Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName)]
public class AlertsV2Controller : ControllerBase
{
    private readonly IMediator _mediator;

    public AlertsV2Controller(IMediator mediator) => _mediator = mediator;

    /// <summary>v2: тот же alert + receivedAtUtc и apiVersion в ответе.</summary>
    [HttpPost]
    public async Task<IActionResult> Receive([FromBody] ReceiveAlertCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        if (result is null)
            return BadRequest(new { message = "Unknown service key." });

        var response = new ReceiveAlertV2Response
        {
            AlertId = result.AlertId,
            IncidentId = result.IncidentId,
            IsNewIncident = result.IsNewIncident,
            ReceivedAtUtc = DateTime.UtcNow
        };

        return result.IsNewIncident ? Created(string.Empty, response) : Ok(response);
    }
}
