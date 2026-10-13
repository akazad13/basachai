using BasaChai.Application.Common;
using BasaChai.Application.Listings;

namespace BasaChai.Infrastructure.Services;

/// <summary>
/// Development and Test only, when Cloudinary is not configured: keeps uploads in a local folder and serves them
/// from /v1/dev-photos. It does not strip metadata or resize, so it must never run in Staging or Production.
/// </summary>
public sealed class LocalPhotoStore(string root) : IPhotoStore
{
    public const string Prefix = "local/";

    public async Task<StoredPhoto> UploadAsync(Stream content, string folder, CancellationToken ct = default)
    {
        Directory.CreateDirectory(root);
        var name = Guid.NewGuid().ToString("N");
        var path = Path.Combine(root, name);
        await using (var file = File.Create(path)) await content.CopyToAsync(file, ct);
        var bytes = new FileInfo(path).Length;
        if (bytes == 0) { File.Delete(path); throw new PhotoRejectedException("Empty file."); }
        return new StoredPhoto(Prefix + name, 1, 1280, 960, bytes);
    }

    public Task DeleteAsync(string publicId, CancellationToken ct = default)
    {
        if (TryResolve(publicId) is { } path && File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    /// <summary>The file for a public id, or null when the id is not one of ours (no path tricks).</summary>
    public string? TryResolve(string publicId)
    {
        if (!publicId.StartsWith(Prefix, StringComparison.Ordinal)) return null;
        var name = publicId[Prefix.Length..];
        return name.Length == 32 && name.All(Uri.IsHexDigit) ? Path.Combine(root, name) : null;
    }
}
