using BasaChai.Application.Common;
using BasaChai.Application.Listings;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace BasaChai.Infrastructure.Services;

public sealed class CloudinaryOptions
{
    public string CloudName { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string ApiSecret { get; set; } = "";
    /// <summary>Signed upload preset with the 1,600 px incoming transformation that strips metadata.</summary>
    public string UploadPreset { get; set; } = "basachai_listing";
}

/// <summary>Uploads through the signed preset; Cloudinary re-encodes the original and drops EXIF, including GPS.</summary>
public sealed class CloudinaryPhotoStore(Cloudinary cloudinary, CloudinaryOptions options) : IPhotoStore
{
    public async Task<StoredPhoto> UploadAsync(Stream content, string folder, CancellationToken ct = default)
    {
        var upload = new ImageUploadParams
        {
            File = new FileDescription("photo", content),
            Folder = folder,
            UploadPreset = options.UploadPreset,
            AllowedFormats = ["jpg", "png", "webp"],
            UseFilename = false,
            UniqueFilename = true,
            Overwrite = false,
        };
        ImageUploadResult result;
        try { result = await cloudinary.UploadAsync(upload, ct); }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException) { throw new PhotoStoreException("Cloudinary unreachable.", e); }

        if (result.Error is { } error)
        {
            // 4xx means Cloudinary looked at the file and refused it; anything else is the service failing.
            if ((int)result.StatusCode is >= 400 and < 500) throw new PhotoRejectedException(error.Message);
            throw new PhotoStoreException(error.Message);
        }
        return new StoredPhoto(result.PublicId, long.Parse(result.Version), result.Width, result.Height, result.Bytes);
    }

    public async Task DeleteAsync(string publicId, CancellationToken ct = default)
    {
        var result = await cloudinary.DestroyAsync(new DeletionParams(publicId) { ResourceType = ResourceType.Image, Invalidate = true });
        if (result.Error is { } e) throw new PhotoStoreException(e.Message);
    }

    public async Task<PhotoUsage?> UsageAsync(CancellationToken ct = default)
    {
        var u = await cloudinary.GetUsageAsync(ct);
        if (u.Error is { } e) throw new PhotoStoreException(e.Message);
        // Credits are fractional; the SDK's typed Used is a whole number, so read them from the JSON.
        var credits = u.JsonObj?["credits"];
        double? Number(string key) => credits?[key]?.Type is Newtonsoft.Json.Linq.JTokenType.Float or Newtonsoft.Json.Linq.JTokenType.Integer
            ? credits[key]!.ToObject<double>() : null;
        // Plans without a credit allowance cannot be compared with it: report "not available" rather than 0%.
        if (Number("limit") is not { } limit || Number("usage") is not { } used) return null;
        return new PhotoUsage(u.Plan, used, limit, limit > 0 ? Math.Round(used / limit * 100, 1) : Number("used_percent") ?? 0,
            u.Storage?.Used ?? 0, u.Bandwidth?.Used ?? 0, u.Transformations?.Used ?? 0,
            new DateTimeOffset(DateTime.SpecifyKind(u.LastUpdated, DateTimeKind.Utc)));
    }
}

/// <summary>Used when Cloudinary is not configured (local development): photos are refused with a clear error.</summary>
public sealed class UnconfiguredPhotoStore : IPhotoStore
{
    public Task<StoredPhoto> UploadAsync(Stream content, string folder, CancellationToken ct = default)
        => throw new PhotoStoreException("Cloudinary is not configured. Set Cloudinary:CloudName, ApiKey and ApiSecret.");
    public Task DeleteAsync(string publicId, CancellationToken ct = default) => Task.CompletedTask;
}
