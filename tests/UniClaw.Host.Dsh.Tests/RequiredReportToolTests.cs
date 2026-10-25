using System.Text.Json;
using Xunit;

namespace UniClaw.Host.Dsh.Tests;

public sealed class RequiredReportToolTests
{
    private sealed record LocalReportRegistry(string Root, string RegistryPath, string GeneratorPath);

    private static LocalReportRegistry CreateLocalReportRegistry()
    {
        var root = Directory.CreateTempSubdirectory("storage002-local-registry-");
        var toolsDir = Directory.CreateDirectory(Path.Combine(root.FullName, "tools"));
        var generatorPath = Path.Combine(toolsDir.FullName, "gen-run-report.py");
        File.Copy(Path.Combine(RuntimeToolHostTests.RepoRoot(), "tools", "gen-run-report.py"), generatorPath);
        var registryPath = Path.Combine(root.FullName, "tool-registry.yaml");
        File.WriteAllText(registryPath, """
            version: 1
            tools:
              run-report:
                summary: deterministic run report
                backing: harness-native
                invocation: deterministic-script
                entry: tools/gen-run-report.py
                requiredCapability: tool_only
                posture: local-write
                surfaces: [cli, workbench]
                status: implemented
            """);
        return new(root.FullName, registryPath, generatorPath);
    }

    [Fact]
    public async Task RequiredReport_IsDeliveredAndReadableThroughNewHost()
    {
        var repoRoot = RuntimeToolHostTests.RepoRoot();
        var registryPath = Path.Combine(repoRoot, "tool-registry.yaml");
        var runsRoot = Directory.CreateTempSubdirectory("storage002-required-report-");
        try
        {
            const string runDirName = "run-required-report-001";
            var runDir = Path.Combine(runsRoot.FullName, runDirName);
            Directory.CreateDirectory(runDir);
            File.WriteAllText(Path.Combine(runDir, "facts.json"),
                """{"runId":"run-required-report-001","status":"completed","terminal":"completed"}""");

            var invokingHost = RuntimeToolHost.Load(registryPath);
            var invoked = await invokingHost.InvokeRequiredReportAsync(runsRoot.FullName, runDirName);

            Assert.True(invoked.Ok, invoked.Result.StderrTail);
            Assert.Equal(UniClaw.Host.Runtime.ReportDeliveryStatus.Delivered, invoked.Delivery.Record.Status);
            Assert.True(invoked.Delivery.IsReadable);
            Assert.NotEmpty(invoked.Delivery.Record.DeliveryId);
            Assert.NotNull(invoked.Result.ReportJson);
            Assert.NotNull(invoked.Result.ReportMd);
            Assert.True(File.Exists(Path.Combine(runsRoot.FullName, invoked.Result.ReportJson!)));
            Assert.True(File.Exists(Path.Combine(runsRoot.FullName, invoked.Result.ReportMd!)));
            Assert.Equal(new[] { "report.json", "report.md" },
                invoked.Delivery.Record.Outputs!.Select(output => output.FileName).OrderBy(name => name, StringComparer.Ordinal));

            var inspectingHost = RuntimeToolHost.Load(registryPath);
            var inspected = await inspectingHost.GetReportDeliveryAsync(
                runsRoot.FullName, invoked.Delivery.Record.DeliveryId);

            Assert.NotNull(inspected);
            Assert.Equal(UniClaw.Host.Runtime.ReportDeliveryStatus.Delivered, inspected!.Record.Status);
            Assert.True(inspected.IsReadable);
            Assert.Equal(new[] { "report.json", "report.md" },
                inspected.Record.Outputs!.Select(output => output.FileName).OrderBy(name => name, StringComparer.Ordinal));
        }
        finally
        {
            runsRoot.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task RequiredReport_UsesPinnedGeneratorAcrossHostsAfterOriginalScriptIsRemoved()
    {
        var registry = CreateLocalReportRegistry();
        var runsRoot = Directory.CreateTempSubdirectory("storage002-pinned-generator-");
        try
        {
            const string owner = "runtime-run:run-pinned-generator-001";
            const string requestId = "pinned-generator-test";
            const string runDirName = "run-pinned-generator-001";
            var runDir = Path.Combine(runsRoot.FullName, runDirName);
            Directory.CreateDirectory(runDir);
            File.WriteAllText(Path.Combine(runDir, "facts.json"),
                """{"runId":"run-pinned-generator-001","status":"completed","terminal":"finalized"}""");

            var firstHost = RuntimeToolHost.Load(registry.RegistryPath);
            var requirement = await firstHost.RequireReportAsync(runsRoot.FullName, owner, requestId);
            Assert.Equal(UniClaw.Host.Runtime.ReportDeliveryStatus.AwaitingInputs, requirement.Status);
            File.Delete(registry.GeneratorPath);

            var newHost = RuntimeToolHost.Load(registry.RegistryPath);
            var generated = await newHost.RetryRequiredReportAsync(
                runsRoot.FullName, requirement.DeliveryId, runDirName);
            Assert.True(generated.Ok, generated.Result.StderrTail);
            Assert.Equal(UniClaw.Host.Runtime.ReportDeliveryStatus.Delivered, generated.Delivery.Record.Status);
            Assert.True(generated.Delivery.IsReadable);
            Assert.True(File.Exists(Path.Combine(runsRoot.FullName, generated.Result.ReportJson!)));

            var repeated = await newHost.RequireReportAsync(runsRoot.FullName, owner, requestId);
            Assert.Equal(requirement.DeliveryId, repeated.DeliveryId);
            Assert.Equal(UniClaw.Host.Runtime.ReportDeliveryStatus.Delivered, repeated.Status);
        }
        finally
        {
            runsRoot.Delete(recursive: true);
            Directory.Delete(registry.Root, recursive: true);
        }
    }

    [Fact]
    public async Task StrictRequiredReport_CanRetryAfterMissingFactsAreAdded()
    {
        var registry = CreateLocalReportRegistry();
        var runsRoot = Directory.CreateTempSubdirectory("storage002-strict-retry-");
        try
        {
            const string owner = "runtime-run:run-strict-retry-001";
            const string requestId = "strict-retry-test";
            const string runDirName = "run-strict-retry-001";
            var runDir = Path.Combine(runsRoot.FullName, runDirName);
            Directory.CreateDirectory(runDir);
            File.WriteAllText(Path.Combine(runDir, "metadata.json"), "{}");
            File.WriteAllText(Path.Combine(runDir, "trace.json"), "{}");
            File.WriteAllBytes(Path.Combine(runDir, "exec.journal"), Array.Empty<byte>());

            var host = RuntimeToolHost.Load(registry.RegistryPath);
            var requirement = await host.RequireReportAsync(runsRoot.FullName, owner, requestId, strict: true);
            var missing = await host.RetryRequiredReportAsync(runsRoot.FullName, requirement.DeliveryId, runDirName);
            Assert.False(missing.Ok);
            Assert.Equal(UniClaw.Host.Runtime.ReportDeliveryStatus.Failed, missing.Delivery.Record.Status);
            Assert.Contains("report-source-missing:facts", missing.Delivery.Record.Reason, StringComparison.Ordinal);

            File.WriteAllText(Path.Combine(runDir, "facts.json"),
                """{"runId":"run-strict-retry-001","status":"completed","terminal":"finalized"}""");
            var retried = await host.RetryRequiredReportAsync(runsRoot.FullName, requirement.DeliveryId, runDirName);

            Assert.True(retried.Ok, retried.Result.StderrTail);
            Assert.Equal(UniClaw.Host.Runtime.ReportDeliveryStatus.Delivered, retried.Delivery.Record.Status);
            Assert.True(retried.Delivery.IsReadable);
        }
        finally
        {
            runsRoot.Delete(recursive: true);
            Directory.Delete(registry.Root, recursive: true);
        }
    }

    [Fact]
    public async Task RuntimeRunOwner_RejectsMismatchedFactsWithoutBindingWrongSource()
    {
        var registry = CreateLocalReportRegistry();
        var runsRoot = Directory.CreateTempSubdirectory("storage002-owner-binding-");
        try
        {
            const string ownerRunId = "run-owner-a-001";
            const string requestId = "owner-binding-test";
            const string wrongRunDir = "run-owner-b-001";
            const string correctRunDir = "run-owner-a-001";
            WriteFacts(runsRoot.FullName, wrongRunDir, "run-owner-b-001");
            WriteFacts(runsRoot.FullName, correctRunDir, ownerRunId);

            var host = RuntimeToolHost.Load(registry.RegistryPath);
            var required = await host.RequireReportAsync(runsRoot.FullName, "runtime-run:" + ownerRunId, requestId);

            var mismatch = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                host.RetryRequiredReportAsync(runsRoot.FullName, required.DeliveryId, wrongRunDir));
            Assert.Contains("report-source-owner-mismatch", mismatch.Message, StringComparison.Ordinal);
            var afterMismatch = await host.GetReportDeliveryAsync(runsRoot.FullName, required.DeliveryId);
            Assert.NotNull(afterMismatch);
            Assert.Equal(UniClaw.Host.Runtime.ReportDeliveryStatus.AwaitingInputs, afterMismatch!.Record.Status);
            Assert.Equal("runtime-run:" + ownerRunId, afterMismatch.Record.Request.OwnerRef);

            var correct = await host.RetryRequiredReportAsync(runsRoot.FullName, required.DeliveryId, correctRunDir);
            Assert.True(correct.Ok, correct.Result.StderrTail);
            Assert.Equal(ownerRunId, JsonDocument.Parse(File.ReadAllText(
                Path.Combine(runsRoot.FullName, correct.Result.ReportJson!)))
                .RootElement.GetProperty("identities").GetProperty("runId").GetProperty("value").GetString());
            Assert.Equal("runtime-run:" + ownerRunId, correct.Delivery.Record.Request.OwnerRef);
        }
        finally
        {
            runsRoot.Delete(recursive: true);
            Directory.Delete(registry.Root, recursive: true);
        }
    }

    [Fact]
    public async Task ArtifactSetOwner_RejectsDifferentRunDirectoryBeforeDelivery()
    {
        var registry = CreateLocalReportRegistry();
        var runsRoot = Directory.CreateTempSubdirectory("storage002-artifact-owner-");
        try
        {
            const string ownerArtifactSet = "artifact-set:run-artifact-a-001";
            const string requestId = "artifact-owner-test";
            const string wrongRunDir = "run-artifact-b-001";
            WriteFacts(runsRoot.FullName, wrongRunDir, "run-artifact-b-001");

            var host = RuntimeToolHost.Load(registry.RegistryPath);
            var required = await host.RequireReportAsync(runsRoot.FullName, ownerArtifactSet, requestId);

            var mismatch = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                host.RetryRequiredReportAsync(runsRoot.FullName, required.DeliveryId, wrongRunDir));
            Assert.Contains("report-source-owner-mismatch", mismatch.Message, StringComparison.Ordinal);
            var afterMismatch = await host.GetReportDeliveryAsync(runsRoot.FullName, required.DeliveryId);
            Assert.NotNull(afterMismatch);
            Assert.Equal(UniClaw.Host.Runtime.ReportDeliveryStatus.AwaitingInputs, afterMismatch!.Record.Status);
            Assert.Equal(ownerArtifactSet, afterMismatch.Record.Request.OwnerRef);
        }
        finally
        {
            runsRoot.Delete(recursive: true);
            Directory.Delete(registry.Root, recursive: true);
        }
    }

    private static void WriteFacts(string runsRoot, string runDirName, string runId)
    {
        var runDir = Path.Combine(runsRoot, runDirName);
        Directory.CreateDirectory(runDir);
        File.WriteAllText(Path.Combine(runDir, "facts.json"),
            JsonSerializer.Serialize(new { runId, status = "completed", terminal = "finalized" }));
    }
}
