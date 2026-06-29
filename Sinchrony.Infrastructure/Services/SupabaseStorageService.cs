using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Sinchrony.Domain.Interfaces.Services;
using System.Net.Http.Headers;

namespace Sinchrony.Infrastructure.Services;

public class SupabaseStorageService(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<SupabaseStorageService> logger) : IStorageService
{
    private readonly string _bucket = configuration["Storage:Bucket"] ?? "sinchrony-avatars";
    private readonly string _storageUrl = configuration["Storage:Url"]!;

    public async Task<string> UploadAvatarAsync(
        Stream imageStream, string fileName, CancellationToken ct = default)
    {
        var url = $"{_storageUrl}/object/{_bucket}/{fileName}";

        using var content = new StreamContent(imageStream);
        content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");

        var response = await httpClient.PostAsync(url, content, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Supabase upload failed: {Status} {Body}", response.StatusCode, body);
            throw new InvalidOperationException($"Storage upload failed: {body}");
        }

        return $"{_storageUrl}/object/public/{_bucket}/{fileName}";
    }

    public async Task DeleteAsync(string fileUrl, CancellationToken ct = default)
    {
        try
        {
            // Extrai o path do arquivo da URL
            var path = fileUrl.Replace($"{_storageUrl}/object/public/{_bucket}/", "");
            var url = $"{_storageUrl}/object/{_bucket}/{path}";
            await httpClient.DeleteAsync(url, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to delete file: {Url}", fileUrl);
        }
    }
}