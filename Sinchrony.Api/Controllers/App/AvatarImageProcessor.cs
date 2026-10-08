using Microsoft.AspNetCore.Mvc;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using Sinchrony.Domain.Exceptions;

namespace Sinchrony.Api.Controllers.App;

// Regras de upload de avatar compartilhadas entre a troca da própria foto e a troca pelo admin.
public static class AvatarImageProcessor
{
    private static readonly string[] AllowedTypes = ["image/jpeg", "image/png", "image/webp"];
    private const long MaxBytes = 2 * 1024 * 1024;

    public static void EnsureAllowedType(IFormFile file)
    {
        if (!AllowedTypes.Contains(file.ContentType.ToLower()))
            throw DomainException.Validation("INVALID_FILE_TYPE",
                "Formato não suportado. Use JPEG, PNG ou WebP.");
    }

    public static bool IsTooLarge(IFormFile file) => file.Length > MaxBytes;

    public static ObjectResult TooLargeResult() => new(new
    {
        error = new { code = "FILE_TOO_LARGE", message = "Arquivo maior que 2 MB." }
    })
    { StatusCode = 413 };

    // Redimensiona para 400x400 (crop) e devolve o JPEG pronto para upload.
    public static async Task<MemoryStream> ResizeAsync(IFormFile file, CancellationToken ct)
    {
        var imageStream = new MemoryStream();
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
        return imageStream;
    }
}
