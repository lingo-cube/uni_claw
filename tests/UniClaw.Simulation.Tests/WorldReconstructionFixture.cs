using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using UniClaw.Kernel.Perception;
using UniClaw.Kernel.World;

namespace UniClaw.Simulation.Tests;

internal sealed record WorldReconstructionFixtureData(WorldReconstructionRequest Request);

/// <summary>
/// 同帧四资产 fixture。实际 Slice occurrence 只从 UI XML 派生；Human GT 是独立
/// expected source，不能参与 actual 构造。
/// </summary>
internal static partial class WorldReconstructionFixture
{
    private const string FrameId = "7e41f85e06e4";
    private const int ExpectedWidth = 1080;
    private const int ExpectedHeight = 2400;
    private const string RootContainerId = "container-settings-content-7e41f85e06e4";
    private const string SourceRevisionId = "world-revision-settings-7e41f85e06e4";
    private const string RecyclerResourceId = "com.android.settings:id/recycler_view";

    private const string ScreenshotPath =
        "platforms/perception/evaluation/validation/fastscreen-v1/frames/7e41f85e06e4.png";
    private const string XmlPath =
        "platforms/perception/evaluation/validation/fastscreen-v1/uia/7e41f85e06e4.xml";
    private const string ResponsePath =
        "platforms/perception/evaluation/reports/fsv001-v2/frames/7e41f85e06e4.json";
    private const string GroundTruthPath =
        "platforms/perception/evaluation/validation/fastscreen-v1/gt/7e41f85e06e4.json";

    public static WorldReconstructionFixtureData LoadSettingsFrame()
    {
        var screenshotBytes = ReadAsset(ScreenshotPath);
        var xmlBytes = ReadAsset(XmlPath);
        var responseBytes = ReadAsset(ResponsePath);
        var groundTruthBytes = ReadAsset(GroundTruthPath);

        var screenshot = PngImage.Decode(screenshotBytes);
        RequireDimensions(screenshot.Width, screenshot.Height, "screenshot");
        ValidateResponse(responseBytes);

        var spatialFrameId = $"device-viewport:{FrameId}:{ExpectedWidth}x{ExpectedHeight}";
        var document = XDocument.Parse(Encoding.UTF8.GetString(xmlBytes));
        var recycler = document.Descendants("node").Single(node =>
            Attribute(node, "resource-id") == RecyclerResourceId);
        var contentBounds = ParsePixelBounds(Attribute(recycler, "bounds"));
        var occurrences = recycler.Elements("node")
            .Select((row, index) => CreateOccurrence(row, index, spatialFrameId))
            .Where(occurrence => occurrence is not null)
            .Cast<OccurrenceFact>()
            .ToArray();
        if (occurrences.Length != 7)
            throw new InvalidDataException($"expected 7 ContentOnly XML rows, got {occurrences.Length}");

        var groundTruthElements = LoadGroundTruth(groundTruthBytes);
        var sourceSlice = new Slice(
            SourceRevisionId,
            RootContainerId,
            new FreshnessBasis(new DateTimeOffset(2026, 9, 13, 0, 0, 0, TimeSpan.Zero)),
            new[] { RootContainerId },
            occurrences,
            new Dictionary<string, string>());
        var assets = new[]
        {
            Asset("screenshot", ScreenshotPath, screenshotBytes, "recorded-input", spatialFrameId),
            Asset("uia-xml", XmlPath, xmlBytes, "recorded-input", spatialFrameId),
            Asset("response-json", ResponsePath, responseBytes, "perception-output", spatialFrameId),
            Asset("human-gt", GroundTruthPath, groundTruthBytes, "test-oracle-only", spatialFrameId),
        };
        var request = new WorldReconstructionRequest(
            FrameId,
            sourceSlice,
            spatialFrameId,
            Normalize(contentBounds),
            new HumanGroundTruthSet(FrameId, SourceRevisionId, spatialFrameId, groundTruthElements),
            assets);
        return new WorldReconstructionFixtureData(request);
    }

    private static OccurrenceFact? CreateOccurrence(XElement row, int index, string spatialFrameId)
    {
        if (!TryParsePixelBounds(Attribute(row, "bounds"), out var rowBounds)
            || !rowBounds.IsValid
            || Attribute(row, "clickable") != "true")
            return null;

        var text = row.Descendants("node")
            .Where(node => TryParsePixelBounds(Attribute(node, "bounds"), out var bounds)
                && bounds.IsValid
                && rowBounds.Contains(bounds))
            .Select(node => Attribute(node, "text"))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
        if (text.Length == 0)
            return null;

        return new OccurrenceFact(
            $"occ-settings-content-{index + 1}",
            RootContainerId,
            "list_item",
            string.Join(' ', text),
            State: null,
            Locator: new SpatialLocator(
                (double)rowBounds.X1 / ExpectedWidth,
                (double)rowBounds.Y1 / ExpectedHeight,
                (double)rowBounds.X2 / ExpectedWidth,
                (double)rowBounds.Y2 / ExpectedHeight,
                spatialFrameId));
    }

    private static IReadOnlyList<HumanGroundTruthElement> LoadGroundTruth(byte[] bytes)
    {
        using var document = JsonDocument.Parse(bytes);
        var root = document.RootElement;
        if (root.GetProperty("frameId").GetString() != FrameId
            || root.GetProperty("reviewStatus").GetString() != "calibrated")
            throw new InvalidDataException("GT is not calibrated for the selected frame");

        return root.GetProperty("elements").EnumerateArray()
            .Select(element =>
            {
                var bounds = element.GetProperty("bounds");
                return new HumanGroundTruthElement(
                    element.GetProperty("gtId").GetString()!,
                    element.GetProperty("gtClass").GetString()!,
                    element.GetProperty("text").GetString()!,
                    new NormalizedRegion(
                        bounds.GetProperty("x1").GetDouble(),
                        bounds.GetProperty("y1").GetDouble(),
                        bounds.GetProperty("x2").GetDouble(),
                        bounds.GetProperty("y2").GetDouble()));
            })
            .ToArray();
    }

    private static void ValidateResponse(byte[] bytes)
    {
        using var document = JsonDocument.Parse(bytes);
        var root = document.RootElement;
        if (root.GetProperty("frameId").GetString() != FrameId
            || !root.GetProperty("hasPng").GetBoolean())
            throw new InvalidDataException("response JSON is not correlated to the selected screenshot");
        var image = root.GetProperty("arms").GetProperty("baseline")
            .GetProperty("qualityResponse").GetProperty("image");
        RequireDimensions(
            image.GetProperty("width").GetInt32(),
            image.GetProperty("height").GetInt32(),
            "response JSON");
    }

    private static void RequireDimensions(int width, int height, string source)
    {
        if (width != ExpectedWidth || height != ExpectedHeight)
            throw new InvalidDataException(
                $"{source} dimensions {width}x{height} do not match {ExpectedWidth}x{ExpectedHeight}");
    }

    private static ReconstructionAssetInput Asset(
        string kind,
        string relativePath,
        byte[] bytes,
        string role,
        string spatialFrameId) =>
        new(
            new ReconstructionAssetRef(
                kind,
                relativePath,
                Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
                role,
                FrameId,
                SourceRevisionId,
                spatialFrameId),
            bytes);

    private static byte[] ReadAsset(string relativePath)
    {
        var path = Path.Combine(GoldenPaths.RepoRoot(), relativePath);
        if (!File.Exists(path))
            throw new FileNotFoundException("WRC-001 asset is missing", path);
        return File.ReadAllBytes(path);
    }

    private static string Attribute(XElement element, string name) =>
        element.Attribute(name)?.Value ?? string.Empty;

    private static PixelBounds ParsePixelBounds(string value)
    {
        if (!TryParsePixelBounds(value, out var bounds))
            throw new InvalidDataException($"invalid Android bounds: {value}");
        return bounds;
    }

    private static bool TryParsePixelBounds(string value, out PixelBounds bounds)
    {
        var match = AndroidBoundsPattern().Match(value);
        if (!match.Success)
        {
            bounds = default;
            return false;
        }

        bounds = new PixelBounds(
            int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture),
            int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture),
            int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture),
            int.Parse(match.Groups[4].Value, CultureInfo.InvariantCulture));
        return true;
    }

    private static NormalizedRegion Normalize(PixelBounds bounds) =>
        new(
            (double)bounds.X1 / ExpectedWidth,
            (double)bounds.Y1 / ExpectedHeight,
            (double)bounds.X2 / ExpectedWidth,
            (double)bounds.Y2 / ExpectedHeight);

    [GeneratedRegex(@"^\[(\d+),(\d+)\]\[(\d+),(\d+)\]$", RegexOptions.CultureInvariant)]
    private static partial Regex AndroidBoundsPattern();

    private readonly record struct PixelBounds(int X1, int Y1, int X2, int Y2)
    {
        public bool IsValid => X1 >= 0 && Y1 >= 0 && X2 > X1 && Y2 > Y1;

        public bool Contains(PixelBounds candidate) =>
            candidate.X1 >= X1 && candidate.Y1 >= Y1
            && candidate.X2 <= X2 && candidate.Y2 <= Y2;
    }
}
