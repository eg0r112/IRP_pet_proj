using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using irp_pet.Application.Alerts;
using irp_pet.Auth;

namespace irp_pet.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/alerts")]
[ApiVersion("1.0")]
[Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName)]
public class AlertsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AlertsController(IMediator mediator) => _mediator = mediator;

    [HttpPost]
    public async Task<IActionResult> Receive([FromBody] ReceiveAlertCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        if (result is null)
            return BadRequest(new { message = "Unknown service key." });
        return result.IsNewIncident ? Created(string.Empty, result) : Ok(result);
    }
}
