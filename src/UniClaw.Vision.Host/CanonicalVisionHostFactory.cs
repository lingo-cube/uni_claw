using System.Collections.Immutable;
using System.Text.Json;

namespace UniClaw.Vision.Host;

/// <summary>
/// Canonical production composition seam for Vision Host identity checking.
/// It materializes an already-authoritative CURRENT ACTIVE receipt; it does
/// not select, promote, rewrite, or otherwise exercise deployment authority.
/// </summary>
public static class CanonicalVisionHostFactory
{
    public const string RequiredEvidenceSchema = "uniclaw.localVisionEvidence.v1";

    public static VisionServiceHost Create(
        string currentActiveReceiptPath,
        string pythonExecutable = "python3",
        string serviceEntryPoint = "platforms/perception/uniclaw_perception/server.py",
        string repoRoot = ".",
        string modelPath = "platforms/perception/models/yolo/android_ui_detection_yolov8/best.pt",
        string configPath = "platforms/perception/config/label-mapping.json")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentActiveReceiptPath);

        // Read the supplied authoritative receipt exactly once. The receipt is
        // composition input, not a Host-managed mutable operational artifact.
        var receiptBytes = File.ReadAllBytes(currentActiveReceiptPath);
        using var document = JsonDocument.Parse(receiptBytes);
        var active = document.RootElement.TryGetProperty("active", out var value)
            && value.ValueKind == JsonValueKind.Object
            ? value
            : throw new InvalidDataException("CURRENT ACTIVE receipt lacks active identity.");
        var schema = RequiredAxis(active, "schemaVersion");
        if (!string.Equals(schema, RequiredEvidenceSchema, StringComparison.Ordinal))
            throw new InvalidDataException(
                $"CURRENT ACTIVE receipt schema {schema} is unsupported.");

        var expected = new ExpectedDeploymentIdentity
        {
            ModelId = RequiredAxis(active, "modelId"),
            ConfigId = RequiredAxis(active, "configId"),
            PipelineRevision = RequiredAxis(active, "pipelineRevision"),
            DeploymentId = RequiredAxis(active, "deploymentId"),
            RequiredSchemas = ImmutableArray.Create(RequiredEvidenceSchema),
        };

        var config = VisionHostConfig.ForCanonicalProduction(
            expected, pythonExecutable, serviceEntryPoint, repoRoot, modelPath, configPath);
        return new VisionServiceHost(config);
    }

    private static string RequiredAxis(JsonElement active, string name)
    {
        if (!active.TryGetProperty(name, out var value)
            || value.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(value.GetString()))
            throw new InvalidDataException($"CURRENT ACTIVE receipt lacks required {name}.");
        return value.GetString()!;
    }
}
