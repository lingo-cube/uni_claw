using System.Text.Json;
using UniClaw.Host.Runtime;
using UniClaw.Host.SettingsCoverage;
using Xunit;

namespace UniClaw.Host.Dsh.Tests;

public sealed class CoverageReportToolTests
{
    [Fact]
    public async Task NewHost_RetriesPinnedCoverageInputsAfterOriginalSourceAndGeneratorAreRemoved()
    {
        var root = Directory.CreateTempSubdirectory("storage002-coverage-tool-");
        var storage = new ReportDeliveryStorageOptions { StoreRelativePath = "products/alpha/reports/required" };
        try
        {
            const string runName = "coverage-run-tool-001";
            var source = Directory.CreateDirectory(Path.Combine(root.FullName, runName)).FullName;
            var material = Material();
            ReportDeliveryRecord accepted;
            await using (var delivery = new CoverageReportDelivery(root.FullName, new() { Storage = storage }))
            {
                accepted = await delivery.RequireAsync(runName);
                var ready = await delivery.CaptureAsync(accepted.DeliveryId, material);
                Assert.Equal(ReportDeliveryStatus.Ready, ready.Record.Status);
                Assert.NotNull(ready.Record.SourceFingerprint);
            }

            File.Delete(accepted.Request.GeneratorPath);
            Directory.Delete(source, recursive: true);

            var host = RuntimeToolHost.LoadWithReportStorage(
                Path.Combine(RuntimeToolHostTests.RepoRoot(), "tool-registry.yaml"), storage);
            var retried = await host.RetryRequiredReportAsync(root.FullName, accepted.DeliveryId);

            Assert.True(retried.Ok, retried.Result.StderrTail);
            Assert.Equal("coverage-report", retried.Delivery.Record.Request.ReportKind);
            Assert.True(retried.Delivery.IsReadable);
            Assert.False(Directory.Exists(source));
            Assert.NotNull(retried.Result.ReportJson);
            Assert.Equal(material.ReportJson,
                await File.ReadAllTextAsync(Path.Combine(root.FullName, retried.Result.ReportJson!)));
            Assert.False(Directory.Exists(Path.Combine(root.FullName, ".required-reports")));
            Assert.True(Directory.Exists(Path.Combine(root.FullName, "products", "alpha", "reports", "required")));

            var protection = await host.GetReportSourceProtectionAsync(root.FullName, accepted.DeliveryId);
            Assert.Contains(protection, item => item.RelativePath == CoverageReportDelivery.SourceFileName);
            Assert.All(protection, item => Assert.False(Path.IsPathRooted(item.RelativePath)));
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    internal static CoverageReportMaterial Material()
    {
        var report = new CoverageReport("BoundedStop", [new("settings/root", "controlled", false, null)],
            0, 0, ["settings/root"], "controlled-stop", 1, 0, 0, 0, 0);
        var steps = new[] { new CoverageStepRecord(1, "decision-001", null, "root", "settings/root", "none",
            null, true, null, null, null, null, null, null, false, "controlled-stop", "observe") };
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        return new(new(Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N"),
            Guid.NewGuid().ToString("N")), JsonSerializer.Serialize(report, json), JsonSerializer.Serialize(steps, json));
    }
}
