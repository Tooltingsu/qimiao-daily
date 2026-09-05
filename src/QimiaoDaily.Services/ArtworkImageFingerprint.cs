using System.Security.Cryptography;
using System.Numerics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace QimiaoDaily.Services;

public sealed record ArtworkThumbnailPayload(byte[] Bytes, string ContentSha256, string? PerceptualHash, int Width, int Height);

/// <summary>Computes an image-backed content hash and 64-bit DCT perceptual hash.</summary>
public static class ArtworkImageFingerprint
{
    public static async Task<ArtworkThumbnailPayload?> TryCreateAsync(byte[] bytes, CancellationToken cancellationToken = default)
    {
        if (bytes.Length == 0) return null;
        try
        {
            await using var input = new MemoryStream(bytes, writable: false);
            using var image = await Image.LoadAsync<Rgba32>(input, cancellationToken);
            var width = image.Width;
            var height = image.Height;
            image.Mutate(context => context.Resize(new ResizeOptions { Size = new Size(32, 32), Mode = ResizeMode.Stretch }).Grayscale());
            var values = new double[32, 32];
            for (var y = 0; y < 32; y++)
                for (var x = 0; x < 32; x++)
                    values[x, y] = image[x, y].R;

            var coefficients = new List<double>(63);
            for (var u = 0; u < 8; u++)
                for (var v = 0; v < 8; v++)
                {
                    if (u == 0 && v == 0) continue;
                    var sum = 0d;
                    for (var x = 0; x < 32; x++)
                        for (var y = 0; y < 32; y++)
                            sum += values[x, y] * Math.Cos((2 * x + 1) * u * Math.PI / 64d) * Math.Cos((2 * y + 1) * v * Math.PI / 64d);
                    coefficients.Add(sum);
                }

            var median = coefficients.OrderBy(x => x).ElementAt(coefficients.Count / 2);
            ulong bits = 0;
            for (var i = 0; i < coefficients.Count; i++)
                if (coefficients[i] > median) bits |= 1UL << i;
            return new ArtworkThumbnailPayload(bytes, Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), bits.ToString("X16"), width, height);
        }
        catch (UnknownImageFormatException)
        {
            return new ArtworkThumbnailPayload(bytes, Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), null, 0, 0);
        }
        catch (InvalidImageContentException)
        {
            return new ArtworkThumbnailPayload(bytes, Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), null, 0, 0);
        }
    }

    public static bool IsNearDuplicate(string? first, string? second, int maxDistance = 4)
    {
        if (string.IsNullOrWhiteSpace(first) || string.IsNullOrWhiteSpace(second)) return false;
        if (!ulong.TryParse(first, System.Globalization.NumberStyles.HexNumber, null, out var left) || !ulong.TryParse(second, System.Globalization.NumberStyles.HexNumber, null, out var right)) return false;
        return BitOperations.PopCount(left ^ right) <= maxDistance;
    }
}
