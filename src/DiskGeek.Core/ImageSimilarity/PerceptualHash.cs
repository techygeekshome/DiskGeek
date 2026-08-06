using System.Numerics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace DiskGeek.Core.ImageSimilarity;

/// <summary>
/// Computes a "difference hash" (dHash) for an image: a 64-bit fingerprint that's robust to small
/// changes like re-compression, resizing, or minor edits, unlike a byte-for-byte content hash which
/// changes completely if a single pixel or a JPEG quality setting differs. Two images that "look the
/// same" to a person end up with hashes that differ in only a handful of bits, measured by
/// <see cref="HammingDistance"/> - which is what makes this useful for finding near-duplicate
/// photos (the same shot exported twice, a photo and its slightly-cropped copy, etc.) that exact
/// SHA-256 duplicate detection would never catch.
///
/// The algorithm: shrink the image to a fixed tiny 9x8 grayscale grid (destroying all the detail
/// that makes two near-identical photos hash differently, while keeping the coarse light/dark
/// pattern that makes them hash the same), then record one bit per pixel for whether it's lighter
/// or darker than the pixel immediately to its right. 8 rows x 8 comparisons per row = 64 bits.
/// </summary>
public static class PerceptualHash
{
    private const int HashWidth = 9; // one extra column so there are 8 left-vs-right comparisons per row
    private const int HashHeight = 8;

    /// <summary>Image file extensions this can attempt to decode (matches <see cref="Models.FileSystemNode.Extension"/>'s leading-dot format).</summary>
    public static readonly IReadOnlySet<string> SupportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp", ".tiff", ".tif"
    };

    /// <summary>
    /// Computes the dHash for the image at <paramref name="path"/>, or null if the file can't be
    /// read or decoded as an image (corrupt file, unsupported/unrecognized format, permissions,
    /// vanished mid-scan, etc.) — callers should skip files that return null rather than fail the
    /// whole scan over one bad file, same convention as <c>DuplicateFinder</c>'s hashing.
    /// </summary>
    public static ulong? Compute(string path)
    {
        try
        {
            using var image = Image.Load<L8>(path);
            image.Mutate(ctx => ctx.Resize(new ResizeOptions
            {
                Size = new Size(HashWidth, HashHeight),
                Mode = ResizeMode.Stretch,
                Sampler = KnownResamplers.Bicubic
            }));

            ulong hash = 0;
            var bitIndex = 0;
            for (var y = 0; y < HashHeight; y++)
            {
                for (var x = 0; x < HashWidth - 1; x++)
                {
                    if (image[x, y].PackedValue > image[x + 1, y].PackedValue)
                        hash |= 1UL << bitIndex;
                    bitIndex++;
                }
            }

            return hash;
        }
        catch (OutOfMemoryException)
        {
            throw; // a real resource problem, not "this file isn't a valid/supported image" - don't swallow it
        }
        catch
        {
            // ImageSharp throws several different exception types for "not a decodable image"
            // (unknown format, corrupt content, unsupported variant, plus plain I/O/permission
            // errors) - all of them mean the same thing here: skip this file and move on.
            return null;
        }
    }

    /// <summary>Number of differing bits between two hashes — 0 means identical-looking thumbnails, 64 means completely different.</summary>
    public static int HammingDistance(ulong a, ulong b) => BitOperations.PopCount(a ^ b);
}
