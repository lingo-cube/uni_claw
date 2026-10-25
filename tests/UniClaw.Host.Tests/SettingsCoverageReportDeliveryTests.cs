using UniClaw.Host.Runtime;
using UniClaw.Host.SettingsCoverage;
using UniClaw.Kernel.Runtime;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;

namespace UniClaw.Host.Tests;

public sealed partial class SettingsCoverageScenarioTests
{
    [Fact]
    public async Task CoverageRunnerReturnsItsOriginalOutcomeAndHasAnIndependentDurableDelivery()
    {
        var root = Directory.CreateTempSubdirectory("coverage-report-").FullName;
        try
        {
            var world = new SimulatedSettingsWorld(); var consultations = 0;
            var result = await SettingsCoverageRunner.RunAsync(root, new("sim-device",
                new LivePerception.LiveAssets("sim-device", "fixture", "/nonexistent", "/nonexistent", "/nonexistent"),
                ConfigWith(), context => { consultations++; return new AgentDecision.Blocked(context.DecisionId, "controlled stop"); },
                FeedNext: world.Next, CurrentCaptureId: () => world.CurrentCaptureId, EffectDriver: new SimulatedEffectDriver(world)));
            Assert.True(consultations > 0);
            var rows = await new RequiredReportDelivery(Path.Combine(root, ".required-reports"))
                .ListAsync("coverage-run:" + Path.GetFileName(result.RunDir));
            var delivery = Assert.Single(rows);
            Assert.Equal("coverage-report", delivery.Record.Request.ReportKind);
            Assert.Equal(ReportDeliveryStatus.Delivered, delivery.Record.Status);
            Assert.True(delivery.IsReadable);
            Assert.Equal("agent-blocked:controlled stop", result.Reason);
            Assert.True(File.Exists(Path.Combine(result.RunDir, "coverage-report.json")));
            Assert.True(File.Exists(Path.Combine(result.RunDir, "coverage-steps.json")));
            var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
            Assert.Equal(JsonSerializer.Serialize(result.Report, json), await File.ReadAllTextAsync(Path.Combine(result.RunDir, "coverage-report.json")));
            Assert.Equal(JsonSerializer.Serialize(result.Steps, json), await File.ReadAllTextAsync(Path.Combine(result.RunDir, "coverage-steps.json")));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task MissingProofDoesNotChangeTheRunnerResultAndRetryNeverCallsItsAgentOrEffectAgain()
    {
        var root = Directory.CreateTempSubdirectory("coverage-missing-proof-").FullName;
        try
        {
            var world = new SimulatedSettingsWorld(); var consultations = 0;
            var proof = Encoding.UTF8.GetBytes("<hierarchy original=\"coverage-proof\"/>");
            var reportOptions = new CoverageReportDeliveryOptions { Attachments = [new("proof.xml", "required-replay-source", true, true,
                Convert.ToHexString(SHA256.HashData(proof)))] };
            var result = await SettingsCoverageRunner.RunAsync(root, new("sim-device",
                new LivePerception.LiveAssets("sim-device", "fixture", "/nonexistent", "/nonexistent", "/nonexistent"),
                ConfigWith(), context => { consultations++; return new AgentDecision.Blocked(context.DecisionId, "controlled stop"); },
                FeedNext: world.Next, CurrentCaptureId: () => world.CurrentCaptureId, EffectDriver: new SimulatedEffectDriver(world), ReportDelivery: reportOptions));
            Assert.Equal("agent-blocked:controlled stop", result.Reason); Assert.True(consultations > 0);
            Assert.NotNull(result.ReportDelivery); Assert.Equal(ReportDeliveryStatus.Failed, result.ReportDelivery!.Delivery.Record.Status);
            var before = consultations;
            var facts = await File.ReadAllBytesAsync(Path.Combine(result.RunDir, "facts.json"));
            var execution = await File.ReadAllBytesAsync(Path.Combine(result.RunDir, "exec.journal"));
            await File.WriteAllBytesAsync(Path.Combine(result.RunDir, "proof.xml"), proof);
            await using var reopened = new CoverageReportDelivery(root);
            var retried = await reopened.RetryAsync(result.ReportDelivery.Delivery.Record.DeliveryId);
            Assert.True(retried.Delivery.IsReadable); Assert.Equal(ReportDeliveryStatus.Delivered, retried.Delivery.Record.Status);
            Assert.Equal(before, consultations);
            Assert.Equal(facts, await File.ReadAllBytesAsync(Path.Combine(result.RunDir, "facts.json")));
            Assert.Equal(execution, await File.ReadAllBytesAsync(Path.Combine(result.RunDir, "exec.journal")));
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
