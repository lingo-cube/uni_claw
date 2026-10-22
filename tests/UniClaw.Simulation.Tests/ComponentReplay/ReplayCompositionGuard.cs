using System.Security.Cryptography;
using System.Text.Json;

namespace UniClaw.Simulation.Tests.ComponentReplay;

internal sealed class ReplayCompositionGuard
{
    private static readonly string[] RequiredKinds = ["product-contract", "product-implementation", "simulation-adapter", "replay-input", "oracle"];
    private readonly string _json;

    private ReplayCompositionGuard(string json) => _json = json;

    public string ManifestSha256 { get; private set; } = string.Empty;
    public IReadOnlyList<JsonElement> Pins { get; private set; } = Array.Empty<JsonElement>();

    public static ReplayCompositionGuard FromJson(string json) => new(json);

    public T Create<T>(string repoRoot, Func<T> factory, Func<string, byte[]>? readBytes = null)
    {
        try
        {
            using var doc = JsonDocument.Parse(_json);
            var root = doc.RootElement;
            if (root.GetProperty("schemaVersion").GetString() != "component-replay-composition.v1" ||
                string.IsNullOrWhiteSpace(root.GetProperty("causalChange").GetString()))
                throw new InvalidDataException("manifest-error:unsupported schema/change");
            var pins = root.GetProperty("pins").EnumerateArray().Select(pin => pin.Clone()).ToArray();
            var paths = new HashSet<string>(StringComparer.Ordinal);
            var kinds = new HashSet<string>(StringComparer.Ordinal);
            readBytes ??= File.ReadAllBytes;
            foreach (var pin in pins)
            {
                var kind = pin.GetProperty("kind").GetString()!;
                var relative = pin.GetProperty("path").GetString()!;
                var expected = pin.GetProperty("sha256").GetString()!;
                if (!RequiredKinds.Contains(kind, StringComparer.Ordinal) || !paths.Add(relative))
                    throw new InvalidDataException("manifest-error:invalid kind or duplicate path");
                kinds.Add(kind);
                var actual = Convert.ToHexString(SHA256.HashData(readBytes(Path.GetFullPath(Path.Combine(repoRoot, relative))))).ToLowerInvariant();
                if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(actual), Convert.FromHexString(expected)))
                    throw new InvalidDataException($"hash-mismatch:{relative}");
            }
            if (RequiredKinds.Any(kind => !kinds.Contains(kind))) throw new InvalidDataException("manifest-error:required kind missing");
            Pins = Array.AsReadOnly(pins);
            ManifestSha256 = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(root))).ToLowerInvariant();
        }
        catch (InvalidDataException) { throw; }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or FormatException or InvalidOperationException)
        { throw new InvalidDataException("manifest-error:invalid manifest", ex); }
        return factory();
    }
}
