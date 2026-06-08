using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using irp_pet.Application.Admin;
using irp_pet.DTOs;
using irp_pet.Models;

namespace irp_pet.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/admin")]
[ApiVersion("1.0")]
[Authorize(Roles = Roles.Admin)]
public class AdminController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminController(IMediator mediator) => _mediator = mediator;

    [HttpGet("ping")]
    public IActionResult Ping() =>
        Ok(new { message = "Admin controller is alive", utcNow = DateTime.UtcNow });

    [HttpGet("users")]
    public async Task<IActionResult> ListUsers(CancellationToken ct)
    {
        var users = await _mediator.Send(new ListUsersQuery(), ct);
        return Ok(users);
    }

    [HttpGet("oncall/current")]
    public async Task<IActionResult> GetCurrentOnCall(CancellationToken ct)
    {
        var current = await _mediator.Send(new GetCurrentOnCallQuery(), ct);
        if (current.Count == 0)
            return NotFound(new { message = "No active on-call shifts." });
        return Ok(current);
    }

    [HttpPost("oncall/shifts")]
    public async Task<IActionResult> CreateOnCallShift([FromBody] CreateOnCallShiftRequest request, CancellationToken ct)
    {
        var shift = await _mediator.Send(new CreateOnCallShiftCommand(
            request.UserId, request.StartsAtUtc, request.EndsAtUtc, request.Note), ct);
        if (shift is null)
            return BadRequest(new { message = "Invalid shift or user (must be admin/oncall)." });
        return Created(string.Empty, new { shift.Id, shift.UserId, shift.StartsAtUtc, shift.EndsAtUtc });
    }

    [HttpPost("users")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateUserCommand(
            request.Email,
            request.Password,
            request.DisplayName,
            request.Role,
            request.TelegramChatId), ct);

        if (!result.Success)
            return Conflict(new { message = "User with this email already exists." });

        var user = result.User!;
        return Created(string.Empty, new
        {
            user.Id,
            user.Email,
            user.DisplayName,
            user.Role
        });
    }
}
