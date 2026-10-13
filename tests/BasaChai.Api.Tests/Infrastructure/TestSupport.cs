using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using BasaChai.Application.Common;
using BasaChai.Application.Listings;
using BasaChai.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BasaChai.Api.Tests.Infrastructure;

public sealed record ProblemDto(string Code, string? Title, int Status, int? RetryAfter);
public sealed record TestUser(string Key, Guid Id, string Phone);

/// <summary>Seeded users, read from deploy/seed/pilot.json. The password is E2E_PASSWORD, or a random one per run.</summary>
public static class TestUsers
{
    public static readonly string Password = Environment.GetEnvironmentVariable("E2E_PASSWORD") is { Length: >= 8 } p
        ? p : "t-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(12));

    public static TestUser Get(string key)
    {
        var u = TestData.Pilot.RootElement.GetProperty("users").EnumerateArray().First(x => x.GetProperty("key").GetString() == key);
        return new TestUser(key, TestSeeder.DeterministicId(key), u.GetProperty("phone").GetString()!);
    }
}

/// <summary>Facts from the pilot dataset that tests rely on.</summary>
public static class TestData
{
    public static readonly JsonDocument Pilot = JsonDocument.Parse(File.ReadAllText(FindPilot()));

    public static IEnumerable<JsonElement> Listings => Pilot.RootElement.GetProperty("listings").EnumerateArray();

    /// <summary>A1 to I3: Live and within 30 days.</summary>
    public static readonly string[] LiveIds = ["A1", "A2", "A3", "B1", "B2", "B3", "D1", "D2", "D3", "Z1", "Z2", "Z3", "I1", "I2", "I3"];

    public static IEnumerable<string> OwnerPhonesAllFormats => Pilot.RootElement.GetProperty("users").EnumerateArray()
        .Where(u => u.GetProperty("roles").EnumerateArray().Any(r => r.GetString() == "owner"))
        .SelectMany(u => Phone.AllFormats(Phone.ToE164(u.GetProperty("phone").GetString())!));

    public static IEnumerable<string> FullAddresses => Listings
        .Where(l => l.TryGetProperty("fullAddress", out _)).Select(l => l.GetProperty("fullAddress").GetString()!);

    private static string FindPilot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var p = Path.Combine(dir.FullName, "deploy", "seed", "pilot.json");
            if (File.Exists(p)) return p;
        }
        throw new FileNotFoundException("deploy/seed/pilot.json");
    }
}

public static class TestFiles
{
    /// <summary>A tiny byte sequence with a JPEG signature; the fake store does not decode images.</summary>
    public static readonly byte[] SmallJpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01, 0xFF, 0xD9];

    /// <summary>Real photos in Files/ (copied next to the test assembly).</summary>
    public static string PathOf(string name) => Path.Combine(AppContext.BaseDirectory, "Files", name);

    public static Stream Open(string name) => File.OpenRead(PathOf(name));
}

public static class TestClient
{
    /// <summary>An HttpClient with cookies, signed in as the seeded user, sending the antiforgery header.</summary>
    public static async Task<HttpClient> AsAsync(this ApiFactory f, string role)
    {
        var client = f.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        if (role == "anonymous") return client;
        var user = TestUsers.Get(role);
        var res = await client.PostAsJsonAsync("/v1/auth/login", new { user.Phone, Password = TestUsers.Password });
        res.EnsureSuccessStatusCode();
        await client.AddAntiforgeryHeaderAsync();
        return client;
    }

    public static async Task AddAntiforgeryHeaderAsync(this HttpClient client)
    {
        var token = (await client.GetFromJsonAsync<JsonElement>("/v1/auth/csrf")).GetProperty("token").GetString();
        client.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-XSRF-TOKEN", token);
    }

    /// <summary>Creates a complete draft (everything but photos) and uploads <paramref name="photos"/> photos. Returns its id.</summary>
    public static async Task<string> CreateDraftAsync(this HttpClient client, string title = "Two bedroom flat near the bazar", int photos = 0)
    {
        var res = await client.PostAsJsonAsync("/v1/owner/listings", new ListingInput(
            Title: title, Type: "flat", Area: "bazar", Bedrooms: 2, Bathrooms: 1, Furnishing: "unfurnished", Occupancy: "any",
            AvailableNow: true, MonthlyRent: 8_000, ServiceCharge: 0, Advance: new AdvanceInput(16_000, false)));
        res.EnsureSuccessStatusCode();
        var id = (await res.Content.ReadFromJsonAsync<OwnerListingDto>())!.Id.ToString();
        for (var i = 0; i < photos; i++) (await client.UploadPhotoAsync(id, TestFiles.SmallJpeg)).EnsureSuccessStatusCode();
        return id;
    }

    public static Task<HttpResponseMessage> UploadPhotoAsync(this HttpClient client, string id, byte[] bytes,
        string fileName = "room.jpg", string contentType = "image/jpeg")
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        var form = new MultipartFormDataContent { { file, "file", fileName } };
        return client.PostAsync($"/v1/owner/listings/{id}/photos", form);
    }

    public static async Task<ProblemDto> ProblemAsync(this HttpResponseMessage res)
        => (await res.Content.ReadFromJsonAsync<ProblemDto>())!;
}

/// <summary>Records calls instead of talking to Cloudinary.</summary>
public sealed class FakePhotoStore : IPhotoStore
{
    private int _n;
    public List<string> Uploaded { get; } = [];
    public List<string> Deleted { get; } = [];
    public PhotoUsage? Usage { get; set; }
    public bool UsageFails { get; set; }

    public Task<StoredPhoto> UploadAsync(Stream content, string folder, CancellationToken ct = default)
    {
        lock (Uploaded)
        {
            var id = $"{folder}/fake-{++_n}";
            Uploaded.Add(id);
            return Task.FromResult(new StoredPhoto(id, 1, 1280, 960, content.Length));
        }
    }

    public Task DeleteAsync(string publicId, CancellationToken ct = default)
    {
        lock (Deleted) Deleted.Add(publicId);
        return Task.CompletedTask;
    }

    public Task<PhotoUsage?> UsageAsync(CancellationToken ct = default)
        => UsageFails ? throw new PhotoStoreException("Cloudinary unreachable.") : Task.FromResult(Usage);

    public void Reset() { lock (Uploaded) Uploaded.Clear(); lock (Deleted) Deleted.Clear(); Usage = null; UsageFails = false; }
}
