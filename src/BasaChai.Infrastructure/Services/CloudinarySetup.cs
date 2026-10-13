using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace BasaChai.Infrastructure.Services;

/// <summary>
/// Configures a Cloudinary account as the photos spec describes, so environments do not drift:
/// the signed upload preset (1,600 px incoming limit, which re-encodes and drops metadata) and the
/// three named delivery sizes, each allowed under strict transformations. Safe to run again.
/// Strict transformations itself is a console setting (Settings, Security) with no API.
/// </summary>
public static class CloudinarySetup
{
    public static readonly (string Name, int Width)[] Sizes = [("card", 320), ("detail", 768), ("full", 1280)];

    public static async Task<IReadOnlyList<string>> RunAsync(Cloudinary cloudinary, CloudinaryOptions options)
    {
        var done = new List<string>();

        var preset = new UploadPresetParams
        {
            Name = options.UploadPreset,
            Unsigned = false,
            AllowedFormats = ["jpg", "png", "webp"],
            Transformation = new Transformation().Width(1600).Crop("limit"),
            UniqueFilename = true,
            Overwrite = false,
            Invalidate = true,
        };
        var existing = await cloudinary.GetUploadPresetAsync(options.UploadPreset);
        var presetResult = existing.StatusCode == System.Net.HttpStatusCode.OK
            ? (BaseResult)await cloudinary.UpdateUploadPresetAsync(preset)
            : await cloudinary.CreateUploadPresetAsync(preset);
        Check(presetResult, $"upload preset {options.UploadPreset}");
        done.Add($"Upload preset {options.UploadPreset}: signed, jpg/png/webp, incoming c_limit,w_1600");

        foreach (var (name, width) in Sizes)
        {
            var transform = new Transformation().Width(width).Crop("limit").FetchFormat("auto").Quality("auto");
            var current = await cloudinary.GetTransformAsync(name);
            if (current.StatusCode != System.Net.HttpStatusCode.OK)
                Check(await cloudinary.CreateTransformAsync(new CreateTransformParams { Name = name, Transform = transform }), $"t_{name}");
            // One call: the SDK always sends allowed_for_strict, so a separate update would briefly disallow the size.
            // unsafe_update keeps images already derived; after a width change, invalidate them in the console.
            Check(await cloudinary.UpdateTransformAsync(new UpdateTransformParams { Transformation = name, UnsafeUpdate = transform, AllowedForStrict = true }), $"t_{name}");
            done.Add($"Named transformation t_{name}: c_limit,w_{width},f_auto,q_auto, allowed under strict transformations");
        }
        done.Add("Now turn on Strict transformations in the Cloudinary console (Settings, Security), and two-factor sign-in.");
        return done;
    }

    private static void Check(BaseResult result, string what)
    {
        if (result.Error is { } e) throw new InvalidOperationException($"Cloudinary {what}: {e.Message}");
    }
}
