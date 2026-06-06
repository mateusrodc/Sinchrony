using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sinchrony.Application.Auth.Commands.Login;
using Sinchrony.Domain.Enums;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;
using System.Security.Claims;

namespace Sinchrony.Api.Controllers.Erp;

[ApiController]
[Route("api/auth")]
public class ErpAuthController(IMediator mediator, IUserRepository userRepository, ITokenService tokenService) : ControllerBase
{
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] ErpLoginRequest req, CancellationToken ct)
    {
        // Reuse LoginCommand but enforce admin/teacher role
        var result = await mediator.Send(new LoginCommand(req.email, req.password), ct);

        // Verify role
        var user = await userRepository.GetByEmailAsync(req.email, ct);
        if (user?.Role == Role.student)
            throw DomainException.Forbidden("FORBIDDEN");

        return Ok(result);
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await mediator.Send(new Application.Auth.Commands.Logout.LogoutCommand(userId), ct);
        return Ok(new { success = true });
    }

    [Authorize]
    [HttpGet("validate")]
    public async Task<IActionResult> Validate(CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = await userRepository.GetByIdAsync(userId, ct)
            ?? throw DomainException.NotFound("User not found.");

        var newToken = tokenService.GenerateAccessToken(user);
        return Ok(new
        {
            token = newToken,
            user = new { id = user.Id, name = user.Name, email = user.Email, role = user.Role.ToString() }
        });
    }

    [Authorize]
    [HttpPut("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ErpChangePasswordRequest req, CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await mediator.Send(
            new Application.Auth.Commands.ChangePassword.ChangePasswordCommand(userId, req.currentPassword, req.newPassword), ct);
        return Ok(new { success = true });
    }
}

public record ErpLoginRequest(string email, string password);
public record ErpChangePasswordRequest(string currentPassword, string newPassword);