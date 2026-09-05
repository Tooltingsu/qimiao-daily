using System.Text;
using QimiaoDaily.Data;

namespace QimiaoDaily.Services;

/// <summary>Caches provider-supplied thumbnails only; original artwork URLs remain the source of truth.</summary>
public sealed class ArtworkThumbnailCacheService(HttpClient client, QimiaoDailyPaths paths, string? sessionCookie = null)
{
    public const int MaxBytes = 5 * 1024 * 1024;

    public async Task<bool> TryCacheAsync(ArtworkEntity artwork, CancellationToken cancellationToken = default)
    {
        if (File.Exists(artwork.ThumbnailUrl) && artwork.ThumbnailUrl.StartsWith(paths.ImagesDirectory, StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(artwork.ThumbnailSha256) && !string.IsNullOrWhiteSpace(artwork.PerceptualHash)) return true;
            var localPayload = await ArtworkImageFingerprint.TryCreateAsync(await File.ReadAllBytesAsync(artwork.ThumbnailUrl, cancellationToken), cancellationToken);
            if (localPayload is null) return false;
            artwork.ThumbnailSha256 = localPayload.ContentSha256;
            artwork.PerceptualHash = localPayload.PerceptualHash;
            if (artwork.Width is null && localPayload.Width > 0) artwork.Width = localPayload.Width;
            if (artwork.Height is null && localPayload.Height > 0) artwork.Height = localPayload.Height;
            return true;
        }
        var payload = await DownloadAsync(artwork.ThumbnailUrl, artwork.SourceUrl, cancellationToken);
        if (payload is null) return false;
        await StoreAsync(artwork, payload, cancellationToken);
        return true;
    }

    public async Task<ArtworkThumbnailPayload?> DownloadAsync(string thumbnailUrl, string sourceUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(thumbnailUrl) || !Uri.TryCreate(thumbnailUrl, UriKind.Absolute, out var uri)) return null;
        if (!uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) return null;
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Referrer = Uri.TryCreate(sourceUrl, UriKind.Absolute, out var source) ? source : new Uri("https://www.pixiv.net/");
        request.Headers.UserAgent.ParseAdd("QimiaoDaily/1.0 thumbnail-cache");
        if (!string.IsNullOrWhiteSpace(sessionCookie)) request.Headers.TryAddWithoutValidation("Cookie", sessionCookie);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength is > MaxBytes) return null;
        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var output = new MemoryStream();
        var buffer = new byte[81920];
        while (true)
        {
            var read = await input.ReadAsync(buffer, cancellationToken);
            if (read == 0) break;
            if (output.Length + read > MaxBytes) return null;
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
        return await ArtworkImageFingerprint.TryCreateAsync(output.ToArray(), cancellationToken);
    }

    public async Task StoreAsync(ArtworkEntity artwork, ArtworkThumbnailPayload payload, CancellationToken cancellationToken = default)
    {
        paths.EnsureDirectories();
        var identity = artwork.Platform + ":" + artwork.ArtworkId;
        var suffix = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(identity))).ToLowerInvariant()[..24];
        var path = Path.Combine(paths.ImagesDirectory, "thumbnail-" + suffix + ".jpg");
        await File.WriteAllBytesAsync(path, payload.Bytes, cancellationToken);
        artwork.ThumbnailUrl = path;
        artwork.ThumbnailSha256 = payload.ContentSha256;
        artwork.PerceptualHash = payload.PerceptualHash;
        if (artwork.Width is null && payload.Width > 0) artwork.Width = payload.Width;
        if (artwork.Height is null && payload.Height > 0) artwork.Height = payload.Height;
    }
}
