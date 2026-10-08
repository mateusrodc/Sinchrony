using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sinchrony.Api.Controllers.App;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;
using System.Security.Claims;

namespace Sinchrony.Api.Controllers.Erp;

// Admin troca a foto de outro usuário (aluno, professor ou outro admin). A troca da própria foto
// continua em POST /api/upload/avatar.
[Authorize(Roles = "admin")]
[ApiController]
[Route("api/users")]
[Produces("application/json")]
public class ErpUserAvatarController(
    IStorageService storageService,
    IUserRepository userRepository,
    IUnitContext unitContext,
    IAuditService auditService) : ControllerBase
{
    private Guid AdminId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")!);

    [HttpPost("{id:guid}/avatar")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(object), 200)]
    public async Task<IActionResult> UploadAvatar(Guid id, IFormFile file, CancellationToken ct)
    {
        AvatarImageProcessor.EnsureAllowedType(file);
        if (AvatarImageProcessor.IsTooLarge(file))
            return AvatarImageProcessor.TooLargeResult();

        var target = await userRepository.GetByIdAsync(id, ct)
            ?? throw DomainException.NotFound("User not found.");

        // Admin que não é global só altera usuário da própria unidade (mesma regra das outras rotas).
        if (!unitContext.IsGlobalAdmin && unitContext.UnitId.HasValue
            && target.UnitId != unitContext.UnitId)
            return Forbid();

        // Remove a foto anterior do usuário alvo (não a de quem está logado).
        if (!string.IsNullOrEmpty(target.Avatar))
            await storageService.DeleteAsync(target.Avatar, ct);

        using var imageStream = await AvatarImageProcessor.ResizeAsync(file, ct);

        var fileName = $"avatars/{target.Id}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.jpg";
        var url = await storageService.UploadAvatarAsync(imageStream, fileName, ct);

        target.UpdateProfile(target.Name, target.Email, target.Phone, url);
        await userRepository.SaveAsync(ct);

        await auditService.LogAsync(
            "user.avatar_changed_by_admin", "User", target.Id, AdminId,
            $"Name: {target.Name}, Email: {target.Email}", ct: ct);

        return Ok(new { url });
    }
}
