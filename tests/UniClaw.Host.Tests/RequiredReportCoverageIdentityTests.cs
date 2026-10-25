using System.Text.Json;
using UniClaw.Host.Runtime;
using UniClaw.Host.SettingsCoverage;
using Xunit;

namespace UniClaw.Host.Tests;

public sealed class RequiredReportCoverageIdentityTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("required-coverage-identity-").FullName;
    public void Dispose() => Directory.Delete(_root, true);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" run-1 ")]
    public async Task CaptureAsync_RejectsInvalidKernelRunId(string? runId)
    {
        const string runDirectory = "invalid-run-id";
        Directory.CreateDirectory(Path.Combine(_root, runDirectory));
        await using var delivery = new CoverageReportDelivery(_root);
        var accepted = await delivery.RequireAsync(runDirectory);

        var result = await delivery.CaptureAsync(accepted.DeliveryId, Material(runId!));

        Assert.Equal(ReportDeliveryStatus.Failed, result.Record.Status);
        Assert.Contains("coverage-source-run-id-invalid", result.Record.Reason);
        Assert.Null(result.Record.SourceFingerprint);
    }

    [Fact]
    public async Task CaptureAsync_RejectsReplacingUnfixedMaterialWithDifferentKernelRunId()
    {
        const string runDirectory = "material-conflict";
        Directory.CreateDirectory(Path.Combine(_root, runDirectory));
        var options = new CoverageReportDeliveryOptions { Attachments = [new("proof.xml", "required-proof", true)] };
        await using var delivery = new CoverageReportDelivery(_root, options);
        var accepted = await delivery.RequireAsync(runDirectory);

        var first = await delivery.CaptureAsync(accepted.DeliveryId, Material("kernel-run-a"));
        Assert.Equal(ReportDeliveryStatus.Failed, first.Record.Status);
        Assert.True(File.Exists(Path.Combine(_root, runDirectory, CoverageReportDelivery.SourceFileName)));

        var replaced = await delivery.CaptureAsync(accepted.DeliveryId, Material("kernel-run-b"));
        Assert.Equal(ReportDeliveryStatus.Failed, replaced.Record.Status);
        Assert.Contains("coverage-source-material-conflict", replaced.Record.Reason);
        Assert.Null(replaced.Record.SourceFingerprint);
    }

    [Fact]
    public async Task RetryAsync_RejectsMissingRunIdInOriginalSourceBeforeInputsAreFixed()
    {
        const string runDirectory = "source-corrupt";
        Directory.CreateDirectory(Path.Combine(_root, runDirectory));
        var options = new CoverageReportDeliveryOptions { Attachments = [new("proof.xml", "required-proof", true)] };
        await using var delivery = new CoverageReportDelivery(_root, options);
        var accepted = await delivery.RequireAsync(runDirectory);
        await delivery.CaptureAsync(accepted.DeliveryId, Material("kernel-run"));
        var path = Path.Combine(_root, runDirectory, CoverageReportDelivery.SourceFileName);
        var source = System.Text.Json.Nodes.JsonNode.Parse(await File.ReadAllTextAsync(path))!;
        source["material"]!["identity"]!["runId"] = "";
        await File.WriteAllTextAsync(path, source.ToJsonString());

        var retried = await delivery.RetryAsync(accepted.DeliveryId);

        Assert.Equal(ReportDeliveryStatus.Failed, retried.Delivery.Record.Status);
        Assert.Null(retried.Delivery.Record.SourceFingerprint);
        Assert.Contains("coverage-source-run-id-invalid", retried.Delivery.Record.Reason);
    }

    private static CoverageReportMaterial Material(string runId) => new(
        new(runId, Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N")),
        JsonSerializer.Serialize(new CoverageReport("BoundedStop", [new("root", "controlled", false, null)],
            0, 0, ["root"], "controlled-stop", 0, 0, 0, 0, 1)), "[]");
}
