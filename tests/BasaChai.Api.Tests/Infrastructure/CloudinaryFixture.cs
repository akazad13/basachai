using BasaChai.Infrastructure.Services;
using CloudinaryDotNet;

namespace BasaChai.Api.Tests.Infrastructure;

/// <summary>
/// The real Cloudinary test account, configured like the API: Cloudinary__CloudName, Cloudinary__ApiKey,
/// Cloudinary__ApiSecret and Cloudinary__UploadPreset (CI secrets for the nightly run). Uploads land in
/// basachai/test/... and are destroyed afterwards.
/// </summary>
public sealed class CloudinaryFixture : IAsyncLifetime
{
    public const string Folder = "basachai/test/listings/contract";

    private static string Setting(string key) => Environment.GetEnvironmentVariable($"Cloudinary__{key}") ?? "";

    public static bool Configured => Setting("CloudName").Length > 0 && Setting("ApiKey").Length > 0 && Setting("ApiSecret").Length > 0;

    private readonly List<string> _uploaded = [];
    private readonly CloudinaryPhotoStore? _store;
    private readonly string _cloud = Setting("CloudName");

    public CloudinaryFixture()
    {
        if (!Configured) return;
        var options = new CloudinaryOptions { CloudName = _cloud, ApiKey = Setting("ApiKey"), ApiSecret = Setting("ApiSecret") };
        if (Setting("UploadPreset") is { Length: > 0 } preset) options.UploadPreset = preset;
        _store = new CloudinaryPhotoStore(new Cloudinary(new Account(options.CloudName, options.ApiKey, options.ApiSecret)) { Api = { Secure = true } }, options);
    }

    /// <summary>
    /// Fetches like a browser that takes WebP (so f_auto picks a modern format, as for real visitors), and never
    /// follows a redirect, so a placeholder image cannot stand in for the real response.
    /// </summary>
    public HttpClient Http { get; } = CreateHttp();

    private static HttpClient CreateHttp()
    {
        var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(60) };
        http.DefaultRequestHeaders.Accept.ParseAdd("image/webp,image/png,image/jpeg;q=0.9");
        return http;
    }

    /// <summary>Uploads through the same store and signed preset as the API, and remembers the photo for clean-up.</summary>
    public async Task<StoredPhoto> UploadAsync(string testFile)
    {
        if (_store is null) throw new InvalidOperationException("REQUIRE_CLOUDINARY=1 but the Cloudinary test account is not configured.");
        await using var file = TestFiles.Open(testFile);
        var photo = await _store.UploadAsync(file, Folder);
        lock (_uploaded) _uploaded.Add(photo.PublicId);
        return photo;
    }

    public string OriginalUrl(StoredPhoto p) => $"https://res.cloudinary.com/{_cloud}/image/upload/v{p.Version}/{p.PublicId}";

    /// <summary>A named transformation, as the web app requests it (lib/photos.ts).</summary>
    public string Url(StoredPhoto p, string size) => $"https://res.cloudinary.com/{_cloud}/image/upload/t_{size}/v{p.Version}/{p.PublicId}";

    public string AdHocUrl(StoredPhoto p, string transformation) => $"https://res.cloudinary.com/{_cloud}/image/upload/{transformation}/v{p.Version}/{p.PublicId}";

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (_store is not null)
            foreach (var id in _uploaded) await _store.DeleteAsync(id);
        Http.Dispose();
    }
}

/// <summary>
/// A test against the real Cloudinary test account; skipped (with the reason) when it is not configured, except
/// where REQUIRE_CLOUDINARY=1 (the CI gate), so missing credentials fail there instead of passing as skipped.
/// </summary>
public sealed class CloudinaryFactAttribute : FactAttribute
{
    public CloudinaryFactAttribute()
    {
        if (!CloudinaryFixture.Configured && Environment.GetEnvironmentVariable("REQUIRE_CLOUDINARY") != "1")
            Skip = "Cloudinary test account not configured (set Cloudinary__CloudName, Cloudinary__ApiKey, Cloudinary__ApiSecret).";
    }
}
