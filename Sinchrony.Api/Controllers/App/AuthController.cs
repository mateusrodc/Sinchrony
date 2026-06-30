using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sinchrony.Api.SwaggerExamples.Auth;
using Sinchrony.Application.Auth.Commands.ChangePassword;
using Sinchrony.Application.Auth.Commands.GoogleLogin;
using Sinchrony.Application.Auth.Commands.Login;
using Sinchrony.Application.Auth.Commands.Logout;
using Sinchrony.Application.Auth.Commands.RefreshToken;
using Sinchrony.Application.Auth.Commands.Register;
using Sinchrony.Application.Auth.Queries.GetMe;
using Swashbuckle.AspNetCore.Filters;
using System.Security.Claims;

namespace Sinchrony.Api.Controllers.App;

[ApiController]
[Route("auth")]
[Produces("application/json")]
public class AuthController(IMediator mediator) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")!);

    [HttpPost("login")]
    [ProducesResponseType(typeof(object), 200)]
    [SwaggerResponseExample(200, typeof(LoginResponseExample))]
    public async Task<IActionResult> Login([FromBody] LoginRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new LoginCommand(req.email, req.password), ct);
        return Ok(result);
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new RegisterCommand(req.name, req.email, req.phone, req.password, req.cpf), ct);
        return StatusCode(201, result);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new RefreshTokenCommand(req.refresh_token), ct);
        return Ok(result);
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await mediator.Send(new LogoutCommand(UserId), ct);
        return Ok(new { success = true });
    }

    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(object), 200)]
    [SwaggerResponseExample(200, typeof(MeResponseExample))]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var result = await mediator.Send(new GetMeQuery(UserId), ct);
        return Ok(result);
    }

    [Authorize]
    [HttpGet("me/permissions")]
    [ProducesResponseType(typeof(object), 200)]
    [SwaggerResponseExample(200, typeof(PermissionsResponseExample))]
    public IActionResult Permissions()
    {
        var role = User.FindFirstValue(ClaimTypes.Role);
        return Ok(new { permissions = GetPermissionsByRole(role) });
    }

    [Authorize]
    [HttpPut("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req, CancellationToken ct)
    {
        await mediator.Send(new ChangePasswordCommand(UserId, req.currentPassword, req.newPassword), ct);
        return Ok(new { success = true });
    }
    [HttpPost("google")]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new GoogleLoginCommand(req.idToken), ct);
        return Ok(result);
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(
    [FromBody] ForgotPasswordRequest req, CancellationToken ct)
    {
        await mediator.Send(
            new Application.Auth.Commands.ForgotPassword.ForgotPasswordCommand(req.email), ct);
        return Ok(new { success = true, message = "Se o email existir, você receberá o link de recuperação." });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordRequest req, CancellationToken ct)
    {
        await mediator.Send(
            new Application.Auth.Commands.ResetPassword.ResetPasswordCommand(req.token, req.newPassword), ct);
        return Ok(new { message = "Senha redefinida com sucesso." });
    }

    public record GoogleLoginRequest(string idToken);

    private static string[] GetPermissionsByRole(string? role) => role switch
    {
        "admin" => ["class:read", "class:book", "class:cancel", "purchase:create", "purchase:read",
                    "card:manage", "profile:manage", "booking:read", "booking:create",
                    "admin:students", "admin:teachers", "admin:studios", "admin:packages",
                    "admin:reports", "admin:settings"],
        "teacher" => ["class:read", "profile:manage", "class:session", "class:attendance", "teacher:metrics"],
        _ => ["class:read", "class:book", "class:cancel", "purchase:create", "purchase:read",
              "card:manage", "profile:manage", "booking:read", "booking:create"]
    };
}

public record LoginRequest(string email, string password);
public record RegisterRequest(string name, string email, string? phone, string password, string? cpf);
public record RefreshRequest(string refresh_token);
public record ForgotPasswordRequest(string email);
public record ResetPasswordRequest(string token, string newPassword);
public record ChangePasswordRequest(string currentPassword, string newPassword);