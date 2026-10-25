using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using UniClaw.Host.Runtime;
using UniClaw.Host.SettingsCoverage;
using Xunit;

namespace UniClaw.Host.Tests;

public sealed class RequiredReportCoverageTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("required-coverage-").FullName;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    public void Dispose() => Directory.Delete(_root, true);
    private static CoverageReportMaterial Material() => new(new("controlled-run", Guid.NewGuid().ToString("N"),
        Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N")),
        JsonSerializer.Serialize(new CoverageReport("BoundedStop", [new("root", "controlled", false, null)],
            0, 0, ["root"], "controlled-stop", 0, 0, 0, 0, 1), Json), "[]");

    [Fact]
    public async Task FixedCoverageInputsAndBinaryProofSurviveLossOfOriginalDirectoryAndGenerator()
    {
        var source = Directory.CreateDirectory(Path.Combine(_root, "run-original")).FullName;
        Directory.CreateDirectory(Path.Combine(source, "evidence"));
        var xml = Encoding.UTF8.GetBytes("<hierarchy><node text=\"original\"/></hierarchy>");
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aX1sAAAAASUVORK5CYII=");
        await File.WriteAllBytesAsync(Path.Combine(source, "evidence/capture.xml"), xml);
        await File.WriteAllBytesAsync(Path.Combine(source, "evidence/capture.png"), png);
        var options = new CoverageReportDeliveryOptions { Storage = new() { StoreRelativePath = "delivery/required" },
            Attachments = [new("evidence/capture.xml", "explicit-replay-input", true, true, Convert.ToHexString(SHA256.HashData(xml))),
                new("evidence/capture.png", "explicit-proof-image", true, true, Convert.ToHexString(SHA256.HashData(png)))] };
        var material = Material(); ReportDeliveryRecord accepted;
        await using (var delivery = new CoverageReportDelivery(_root, options))
        {
            accepted = await delivery.RequireAsync("run-original");
            Assert.Equal(ReportDeliveryStatus.AwaitingInputs, accepted.Status); Assert.Equal(source, accepted.SourceDirectory);
            var ready = await delivery.CaptureAsync(accepted.DeliveryId, material);
            Assert.Equal(ReportDeliveryStatus.Ready, ready.Record.Status); Assert.NotNull(ready.Record.SourceFingerprint);
        }
        Directory.Delete(source, true); File.Delete(accepted.Request.GeneratorPath);
        await using var reopened = new CoverageReportDelivery(_root, options);
        var result = await reopened.RetryAsync(accepted.DeliveryId);
        Assert.Equal(ReportDeliveryStatus.Delivered, result.Delivery.Record.Status); Assert.True(result.Delivery.IsReadable);
        Assert.Equal(material.ReportJson, await File.ReadAllTextAsync(Path.Combine(reopened.OutputDirectory(accepted.DeliveryId), "coverage-report.json")));
        Assert.Equal(material.StepsJson, await File.ReadAllTextAsync(Path.Combine(reopened.OutputDirectory(accepted.DeliveryId), "coverage-steps.json")));
        Assert.False(Directory.Exists(source));
        var protection = await reopened.GetProtectedSourcesAsync(accepted.DeliveryId);
        Assert.Contains(protection, item => item.RelativePath == "evidence/capture.xml" && item.RetainAfterDelivery);
    }

    [Fact]
    public async Task MissingRequiredProofKeepsTheUniqueReportAndCanOnlyUseTheDeclaredRestoredBytes()
    {
        var source = Directory.CreateDirectory(Path.Combine(_root, "run-missing")).FullName;
        var bytes = Encoding.UTF8.GetBytes("<hierarchy original=\"true\"/>");
        var options = new CoverageReportDeliveryOptions { Attachments = [new("proof.xml", "required-proof", true, true,
            Convert.ToHexString(SHA256.HashData(bytes)))] };
        var material = Material();
        await using var delivery = new CoverageReportDelivery(_root, options);
        var accepted = await delivery.RequireAsync("run-missing");
        var failed = await delivery.CaptureAndDeliverAsync(accepted.DeliveryId, material);
        Assert.Equal(ReportDeliveryStatus.Failed, failed.Delivery.Record.Status);
        Assert.Null(failed.Delivery.Record.SourceFingerprint); Assert.Contains("coverage-attachment-missing", failed.Delivery.Record.Reason);
        Assert.True(File.Exists(Path.Combine(source, CoverageReportDelivery.SourceFileName)));
        await File.WriteAllTextAsync(Path.Combine(source, "proof.xml"), "different source");
        var wrong = await delivery.RetryAsync(accepted.DeliveryId);
        Assert.Equal(ReportDeliveryStatus.Failed, wrong.Delivery.Record.Status); Assert.Null(wrong.Delivery.Record.SourceFingerprint);
        await File.WriteAllBytesAsync(Path.Combine(source, "proof.xml"), bytes);
        var restored = await delivery.RetryAsync(accepted.DeliveryId);
        Assert.Equal(ReportDeliveryStatus.Delivered, restored.Delivery.Record.Status);
        Assert.Equal(material.ReportJson, await File.ReadAllTextAsync(Path.Combine(source, "coverage-report.json")));
    }

    [Fact]
    public async Task DamagedFixedProofIsRejectedWithoutReconsultationOrInputReplacement()
    {
        Directory.CreateDirectory(Path.Combine(_root, "run-fixed"));
        await using var delivery = new CoverageReportDelivery(_root);
        var accepted = await delivery.RequireAsync("run-fixed");
        var ready = await delivery.CaptureAsync(accepted.DeliveryId, Material());
        Assert.Equal(ReportDeliveryStatus.Ready, ready.Record.Status);
        var originalFingerprint = ready.Record.SourceFingerprint;
        var inputs = Path.Combine(Path.GetDirectoryName(delivery.OutputDirectory(accepted.DeliveryId))!, "inputs");
        await File.WriteAllTextAsync(Path.Combine(inputs, CoverageReportDelivery.BundleFileName), "corrupt");
        var result = await delivery.RetryAsync(accepted.DeliveryId);
        Assert.Equal(ReportDeliveryStatus.Failed, result.Delivery.Record.Status); Assert.False(result.Delivery.IsReadable);
        Assert.Equal(originalFingerprint, result.Delivery.Record.SourceFingerprint);
        Assert.Contains("report-input-hash-mismatch", result.Delivery.Record.Reason);
    }

    [Fact]
    public async Task AConflictingLegacyProjectionDoesNotOverwriteItOrMislabelTheVerifiedPublication()
    {
        var source = Directory.CreateDirectory(Path.Combine(_root, "run-conflict")).FullName;
        await using var delivery = new CoverageReportDelivery(_root);
        var accepted = await delivery.RequireAsync("run-conflict");
        var material = Material(); await delivery.CaptureAsync(accepted.DeliveryId, material);
        await File.WriteAllTextAsync(Path.Combine(source, "coverage-report.json"), "foreign output");
        var result = await delivery.RetryAsync(accepted.DeliveryId);
        Assert.True(result.Delivery.IsReadable); Assert.Equal(ReportDeliveryStatus.Delivered, result.Delivery.Record.Status);
        Assert.NotNull(result.ProjectionProblem); Assert.Equal("foreign output", await File.ReadAllTextAsync(Path.Combine(source, "coverage-report.json")));
        Assert.Equal(material.ReportJson, await File.ReadAllTextAsync(Path.Combine(delivery.OutputDirectory(accepted.DeliveryId), "coverage-report.json")));
    }
}
