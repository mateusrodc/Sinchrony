using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
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
    public async Task<IActionResult> UploadAvatar(
        IFormFile file, CancellationToken ct)
    {
        // Valida formato
        var allowedTypes = new[] { "image/jpeg", "image/png", "image/webp" };
        if (!allowedTypes.Contains(file.ContentType.ToLower()))
            throw DomainException.Validation("INVALID_FILE_TYPE",
                "Formato não suportado. Use JPEG, PNG ou WebP.");

        // Valida tamanho (2 MB)
        if (file.Length > 2 * 1024 * 1024)
            return StatusCode(413, new
            {
                error = new { code = "FILE_TOO_LARGE", message = "Arquivo maior que 2 MB." }
            });

        var user = await userRepository.GetByIdAsync(UserId, ct)
            ?? throw DomainException.NotFound("User not found.");

        // Remove avatar anterior
        if (!string.IsNullOrEmpty(user.Avatar))
            await storageService.DeleteAsync(user.Avatar, ct);

        // Redimensiona para 400x400
        using var imageStream = new MemoryStream();
        using (var image = await Image.LoadAsync(file.OpenReadStream(), ct))
        {
            image.Mutate(x => x
                .Resize(new ResizeOptions
                {
                    Size = new Size(400, 400),
                    Mode = ResizeMode.Crop
                }));
            await image.SaveAsJpegAsync(imageStream, ct);
        }
        imageStream.Position = 0;

        // Nome único do arquivo
        var fileName = $"avatars/{UserId}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.jpg";

        var url = await storageService.UploadAvatarAsync(imageStream, fileName, ct);

        // Salva URL no usuário
        user.UpdateProfile(user.Name, user.Email, user.Phone, url);
        await userRepository.SaveAsync(ct);

        return Ok(new { url });
    }
}