namespace BasaChai.Api.Tests;

/// <summary>
/// Contract tests against the real Cloudinary test account (photos spec). They run nightly and before releases in CI,
/// where the test account's credentials are set; elsewhere they are skipped.
/// </summary>
[Trait("Category", "Cloudinary")]
public class CloudinaryContractTests(CloudinaryFixture c) : IClassFixture<CloudinaryFixture>
{
    [CloudinaryFact] // PHOTO-API01: PHOTO-1
    public async Task PHOTO_API01_Stored_and_delivered_images_have_no_gps()
    {
        // A phone-sized photo with GPS, so the 1,600 px incoming limit really re-encodes it.
        var uploaded = await c.UploadAsync("phone-gps.jpg");
        Assert.Equal(1600, uploaded.Width);

        (string Url, int MaxWidth)[] deliveries =
            [(c.OriginalUrl(uploaded), 1600), (c.Url(uploaded, "card"), 320), (c.Url(uploaded, "detail"), 768), (c.Url(uploaded, "full"), 1280)];
        foreach (var (url, maxWidth) in deliveries)
        {
            using var res = await c.Http.GetAsync(url);
            Assert.True(res.StatusCode == HttpStatusCode.OK, $"{url}: {(int)res.StatusCode}");
            Assert.Matches("^image/(jpeg|png|webp)$", res.Content.Headers.ContentType?.MediaType ?? "");
            var image = ExifReader.Read(await res.Content.ReadAsByteArrayAsync());   // throws if it is not a readable image
            Assert.False(image.HasGps, url);
            Assert.InRange(image.Width, 1, maxWidth);
        }
    }

    [CloudinaryFact] // PHOTO-API02: PHOTO-2
    public async Task PHOTO_API02_Only_named_transformations_are_served()
    {
        var uploaded = await c.UploadAsync("room-1.jpg");
        using var named = await c.Http.GetAsync(c.Url(uploaded, "card"));
        Assert.Equal(HttpStatusCode.OK, named.StatusCode);
        // Strict transformations refuse the ad-hoc size itself (not a missing photo, a redirect or an outage).
        using var adHoc = await c.Http.GetAsync(c.AdHocUrl(uploaded, "w_500"));
        Assert.Contains(adHoc.StatusCode, new[] { HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden });
    }
}

/// <summary>Checks the contract tests' own tools, so a passing PHOTO-API01 means something. Runs in every build.</summary>
public class ExifReaderTests
{
    [Fact]
    public void The_gps_test_photos_really_carry_gps_and_the_plain_one_does_not()
    {
        Assert.True(ExifReader.HasGps(File.ReadAllBytes(TestFiles.PathOf("room-gps.jpg"))));
        var phone = ExifReader.Read(File.ReadAllBytes(TestFiles.PathOf("phone-gps.jpg")));
        Assert.True(phone.HasGps);
        Assert.True(phone.Width > 1600, "phone-gps.jpg must be wider than the 1,600 px incoming limit");
        Assert.False(ExifReader.HasGps(File.ReadAllBytes(TestFiles.PathOf("room-1.jpg"))));
    }

    [Fact]
    public void Bytes_that_are_not_an_image_are_an_error_not_a_pass()
        => Assert.ThrowsAny<Exception>(() => ExifReader.Read("not an image"u8.ToArray()));
}
