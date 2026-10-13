using System.Text;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.FileType;
using MetadataExtractor.Formats.Jpeg;
using MetadataExtractor.Formats.Png;
using MetadataExtractor.Formats.WebP;

namespace BasaChai.Api.Tests.Infrastructure;

public sealed record ImageFacts(string Format, int Width, int Height, bool HasGps);

/// <summary>
/// Reads a JPEG, PNG or WebP to check that no location data survives an upload. Anything it cannot read as one of
/// those is an error, never "no GPS": an error page or a format we cannot inspect must not pass the privacy test.
/// </summary>
public static class ExifReader
{
    public static ImageFacts Read(byte[] image)
    {
        using var stream = new MemoryStream(image);
        var dirs = ImageMetadataReader.ReadMetadata(stream);   // throws ImageProcessingException for non-images
        var format = dirs.OfType<FileTypeDirectory>().FirstOrDefault()?.GetDescription(FileTypeDirectory.TagDetectedFileTypeName) ?? "";
        var (width, height) = format switch
        {
            "JPEG" => Size(dirs.OfType<JpegDirectory>(), JpegDirectory.TagImageWidth, JpegDirectory.TagImageHeight),
            "PNG" => Size(dirs.OfType<PngDirectory>(), PngDirectory.TagImageWidth, PngDirectory.TagImageHeight),
            "WebP" => Size(dirs.OfType<WebPDirectory>(), WebPDirectory.TagImageWidth, WebPDirectory.TagImageHeight),
            _ => throw new InvalidDataException($"Not a JPEG, PNG or WebP image (detected: '{format}')."),
        };
        // EXIF GPS tags, or GPS written as XMP (plain text inside the file).
        var gps = dirs.OfType<GpsDirectory>().Any(d => d.Tags.Count > 0)
                  || Encoding.Latin1.GetString(image).Contains("exif:GPS", StringComparison.Ordinal);
        return new ImageFacts(format, width, height, gps);
    }

    public static bool HasGps(byte[] image) => Read(image).HasGps;

    private static (int, int) Size<T>(IEnumerable<T> dirs, int widthTag, int heightTag) where T : MetadataExtractor.Directory
    {
        var d = dirs.FirstOrDefault() ?? throw new InvalidDataException($"No {typeof(T).Name} in the image.");
        return (d.GetInt32(widthTag), d.GetInt32(heightTag));
    }
}
