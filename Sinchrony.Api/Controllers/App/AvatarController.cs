using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;
using System.Security.Claims;

namespace Sinchrony.Api.Controllers.App;

[Authorize]
[ApiController]
[Route("api/upload")]
[Produces("application/json")]
public class AvatarController(
    IStorageService storageService,
    IUserRepository userRepository) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")!);

    [HttpPost("avatar")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(object), 200)]
    public async Task<IActionResult> UploadAvatar(
        IFormFile file, CancellationToken ct)
    {
        // Valida formato e tamanho (2 MB)
        AvatarImageProcessor.EnsureAllowedType(file);
        if (AvatarImageProcessor.IsTooLarge(file))
            return AvatarImageProcessor.TooLargeResult();

        var user = await userRepository.GetByIdAsync(UserId, ct)
            ?? throw DomainException.NotFound("User not found.");

        // Remove avatar anterior
        if (!string.IsNullOrEmpty(user.Avatar))
            await storageService.DeleteAsync(user.Avatar, ct);

        // Redimensiona para 400x400
        using var imageStream = await AvatarImageProcessor.ResizeAsync(file, ct);

        // Nome único do arquivo
        var fileName = $"avatars/{UserId}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.jpg";

        var url = await storageService.UploadAvatarAsync(imageStream, fileName, ct);

        // Salva URL no usuário
        user.UpdateProfile(user.Name, user.Email, user.Phone, url);
        await userRepository.SaveAsync(ct);

        return Ok(new { url });
    }
}