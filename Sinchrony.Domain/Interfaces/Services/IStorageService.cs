namespace Sinchrony.Domain.Interfaces.Services;

public interface IStorageService
{
    Task<string> UploadAvatarAsync(Stream imageStream, string fileName, CancellationToken ct = default);
    Task DeleteAsync(string fileUrl, CancellationToken ct = default);
}