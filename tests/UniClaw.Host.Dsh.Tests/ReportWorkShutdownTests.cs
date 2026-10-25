using System.Diagnostics;
using System.Text.Json;
using UniClaw.Host.Dsh;
using UniClaw.Host.Runtime;
using Xunit;

namespace UniClaw.Host.Dsh.Tests;

public sealed class ReportWorkShutdownTests
{
    [Fact]
    public async Task Drain_ReportsGeneratingRunAsPendingThenSettlesAfterReportDelivery()
    {
        var root = Path.Combine(Path.GetTempPath(), "report-work-shutdown-" + Guid.NewGuid().ToString("N"));
        var storageRoot = Path.Combine(root, "store");
        var sourceDirectory = Path.Combine(root, "source");
        Directory.CreateDirectory(storageRoot);
        Directory.CreateDirectory(sourceDirectory);
        var generatorPath = Path.Combine(root, "fake-generator.py");
        const string sourceJson = "{\"runId\":\"shutdown-run-x\",\"outcome\":\"completed\"}";
        await File.WriteAllTextAsync(generatorPath, "# fixed inert generator; never execute");
        await File.WriteAllTextAsync(Path.Combine(sourceDirectory, "run.json"), sourceJson);

        var delivery = new RequiredReportDelivery(storageRoot);
        var request = new ReportDeliveryRequest("shutdown-owner", "shutdown-request", "run-report", generatorPath,
            new ReportInputContract("uniclaw.run-report.inputs.v1", "run-report", 1,
                new[] { new ReportInputFile("run", "run.json", false, true) },
                new[] { "report.json", "report.md" }));
        var accepted = await delivery.AcceptAsync(request);
        await delivery.PrepareAsync(accepted.DeliveryId, sourceDirectory, "shutdown-run-x");

        var generatorStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseGenerator = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var workTracker = new RuntimeBackgroundWork();
        Task? trackedWork = null;

        try
        {
            async Task PublishAndConfirmAsync()
            {
                var published = await delivery.PublishAsync(accepted.DeliveryId, async (context, cancellationToken) =>
                {
                    generatorStarted.TrySetResult(true);
                    await releaseGenerator.Task.WaitAsync(cancellationToken);
                    var capturedSource = await File.ReadAllTextAsync(
                        Path.Combine(context.InputDirectory, "run.json"), cancellationToken);
                    Directory.CreateDirectory(context.OutputDirectory);
                    await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "report.json"),
                        JsonSerializer.Serialize(new { run = capturedSource }), cancellationToken);
                    await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "report.md"),
                        "# Shutdown report\n\n" + capturedSource, cancellationToken);
                    return new ReportGenerationResult(0);
                });
                var confirmed = await delivery.ConfirmAsync(accepted.DeliveryId);
                if (published.Status != ReportDeliveryStatus.Published || confirmed.Status != ReportDeliveryStatus.Delivered)
                    throw new InvalidOperationException("report-delivery-did-not-settle");
            }

            trackedWork = PublishAndConfirmAsync();
            workTracker.Track("run-x", trackedWork);
            await generatorStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));

            var timedDrain = await workTracker.DrainAsync(TimeSpan.FromMilliseconds(50));
            var duringGeneration = await new RequiredReportDelivery(storageRoot).InspectAsync(accepted.DeliveryId);
            Assert.NotNull(duringGeneration);
            Assert.Equal(ReportDeliveryStatus.Generating, duringGeneration!.Record.Status);
            Assert.False(duringGeneration.IsReadable);
            Assert.False(timedDrain.Settled);
            Assert.Contains("run-x", timedDrain.PendingRunIds);

            releaseGenerator.TrySetResult(true);
            var settledDrain = await workTracker.DrainAsync(TimeSpan.FromSeconds(2));
            Assert.True(settledDrain.Settled);
            Assert.Empty(settledDrain.PendingRunIds);
            Assert.Empty(settledDrain.FailedRunIds);
            await trackedWork.WaitAsync(TimeSpan.FromSeconds(2));

            var afterDrain = await new RequiredReportDelivery(storageRoot).InspectAsync(accepted.DeliveryId);
            Assert.NotNull(afterDrain);
            Assert.Equal(ReportDeliveryStatus.Delivered, afterDrain!.Record.Status);
            Assert.True(afterDrain.IsReadable);
            var outputDirectory = new RequiredReportDelivery(storageRoot).OutputDirectory(accepted.DeliveryId);
            Assert.Contains(sourceJson, await File.ReadAllTextAsync(Path.Combine(outputDirectory, "report.md")), StringComparison.Ordinal);
        }
        finally
        {
            releaseGenerator.TrySetResult(true);
            if (trackedWork is not null)
            {
                try { await trackedWork.WaitAsync(TimeSpan.FromSeconds(3)); }
                catch { /* Keep the original failure while bounding fixture cleanup. */ }
            }
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Drain_ExposesFaultedTrackedRunId()
    {
        var workTracker = new RuntimeBackgroundWork();
        workTracker.Track("run-failed", Task.FromException(new InvalidOperationException("fixture failure")));

        var drain = await workTracker.DrainAsync(TimeSpan.FromSeconds(2));

        Assert.True(drain.Settled);
        Assert.Empty(drain.PendingRunIds);
        Assert.Contains("run-failed", drain.FailedRunIds);
    }

    [Fact]
    public async Task ToolHost_CloseCancelsActiveReportProcessAndLeavesFailedDeliveryQueryable()
    {
        var repositoryRoot = RuntimeToolHostTests.RepoRoot();
        var evidenceRoot = Path.Combine(repositoryRoot, "changes", "RUNTIME-STORAGE-002", "evidence", "slice2", "shutdown-tests");
        Directory.CreateDirectory(evidenceRoot);
        var root = Path.Combine(evidenceRoot, "toolhost-close-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + "-" + Guid.NewGuid().ToString("N"));
        var toolsDirectory = Directory.CreateDirectory(Path.Combine(root, "tools"));
        var runsRoot = Directory.CreateDirectory(Path.Combine(root, "runs"));
        const string runId = "run-toolhost-shutdown-001";
        const string runDirectoryName = "run-toolhost-shutdown-001";
        var sourceDirectory = Directory.CreateDirectory(Path.Combine(runsRoot.FullName, runDirectoryName));
        await File.WriteAllTextAsync(Path.Combine(sourceDirectory.FullName, "facts.json"),
            "{\"runId\":\"run-toolhost-shutdown-001\",\"status\":\"completed\"}");

        var startedPath = Path.Combine(root, "started");
        var processIdPath = Path.Combine(root, "child.pid");
        var releasePath = Path.Combine(root, "release");
        var generatorPath = Path.Combine(toolsDirectory.FullName, "report.py");
        var controlRootLiteral = JsonSerializer.Serialize(root);
        var contract = "{'schemaVersion':'uniclaw.run-report.inputs.v1','generatorName':'run-report','generatorVersion':1,'files':[{'name':'facts','fileName':'facts.json','strictRequired':False,'requiredForDelivery':True}],'outputFiles':['report.json','report.md']}";
        var script = "import json,os,sys,time\n"
            + "if '--describe-inputs' in sys.argv:\n print(json.dumps(" + contract + "))\n sys.exit(0)\n"
            + "control_root = " + controlRootLiteral + "\n"
            + "open(os.path.join(control_root,'child.pid'),'w').write(str(os.getpid()))\n"
            + "open(os.path.join(control_root,'started'),'w').write('1')\n"
            + "while not os.path.exists(os.path.join(control_root,'release')): time.sleep(0.02)\n"
            + "out = sys.argv[sys.argv.index('--out-dir')+1]\n"
            + "os.makedirs(out,exist_ok=True)\n"
            + "open(os.path.join(out,'report.json'),'w').write('{}')\n"
            + "open(os.path.join(out,'report.md'),'w').write('# controlled report')\n";
        await File.WriteAllTextAsync(generatorPath, script);
        var registryPath = Path.Combine(root, "tool-registry.yaml");
        await File.WriteAllTextAsync(registryPath, "version: 1\ntools:\n  run-report:\n    summary: shutdown fixture\n    backing: harness-native\n    invocation: deterministic-script\n    entry: tools/report.py\n    requiredCapability: tool_only\n    posture: local-write\n    surfaces: [cli, workbench]\n    status: implemented\n");

        var host = RuntimeToolHost.Load(registryPath);
        Task<RuntimeToolHost.RequiredReportResult>? retryTask = null;
        var hostClosed = false;
        var requiredReport = default(ReportDeliveryRecord);
        try
        {
            requiredReport = await host.RequireReportAsync(runsRoot.FullName, "runtime-run:" + runId, "shutdown-primary");
            Assert.Equal(ReportDeliveryStatus.AwaitingInputs, requiredReport.Status);
            retryTask = host.RetryRequiredReportAsync(runsRoot.FullName, requiredReport.DeliveryId, runDirectoryName);
            await WaitForFileAsync(startedPath, retryTask, TimeSpan.FromSeconds(5));

            var generatorProcessId = int.Parse(await File.ReadAllTextAsync(processIdPath));
            var close = await host.CloseReportProcessesAsync(TimeSpan.FromSeconds(2));
            hostClosed = true;
            Assert.True(close.Settled);
            Assert.Empty(close.UnconfirmedOperations);

            var failedResult = await retryTask.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(failedResult.Ok);
            Assert.Equal(ReportDeliveryStatus.Failed, failedResult.Delivery.Record.Status);
            Assert.False(failedResult.Delivery.IsReadable);
            Assert.Contains("CanceledException", failedResult.Delivery.Record.Reason!, StringComparison.Ordinal);

            var freshInspection = await RuntimeToolHost.Load(registryPath)
                .GetReportDeliveryAsync(runsRoot.FullName, requiredReport.DeliveryId);
            Assert.NotNull(freshInspection);
            Assert.Equal(ReportDeliveryStatus.Failed, freshInspection!.Record.Status);
            Assert.False(freshInspection.IsReadable);
            Assert.Equal("shutdown-primary", freshInspection.Record.Request.RequestId);
            Assert.Equal("runtime-run:" + runId, freshInspection.Record.Request.OwnerRef);
            Assert.Equal(Path.GetFullPath(sourceDirectory.FullName), freshInspection.Record.SourceDirectory);
            Assert.NotNull(freshInspection.Record.SourceFingerprint);
            Assert.Contains("facts", freshInspection.Record.Sources!.Select(source => source.Name));

            var exited = await WaitForProcessExitAsync(generatorProcessId, TimeSpan.FromSeconds(3));
            Assert.True(exited, "report generator process did not exit after Host close");
            var newRequirement = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                host.RequireReportAsync(runsRoot.FullName, "runtime-run:" + runId, "shutdown-after-close"));
            Assert.Contains("report-host-closing", newRequirement.Message, StringComparison.Ordinal);

            await File.WriteAllTextAsync(Path.Combine(root, "close-summary.json"), JsonSerializer.Serialize(new
            {
                DeliveryId = requiredReport.DeliveryId,
                GeneratorProcessId = generatorProcessId,
                GeneratorExited = exited,
                CloseDrainSettled = close.Settled,
                close.UnconfirmedOperations,
                FailedStatus = failedResult.Delivery.Record.Status.ToString(),
                FailedReason = failedResult.Delivery.Record.Reason,
                SourceDirectory = freshInspection.Record.SourceDirectory,
                SourceFingerprint = freshInspection.Record.SourceFingerprint,
                RequestId = freshInspection.Record.Request.RequestId,
                NewRequirementRejected = newRequirement.Message,
            }));
        }
        finally
        {
            await File.WriteAllTextAsync(releasePath, "1");
            if (!hostClosed)
            {
                try { await host.CloseReportProcessesAsync(TimeSpan.FromSeconds(2)); }
                catch { /* Preserve the test failure; subprocess cleanup remains bounded below. */ }
            }
            if (File.Exists(processIdPath))
            {
                var pid = int.Parse(await File.ReadAllTextAsync(processIdPath));
                if (!await WaitForProcessExitAsync(pid, TimeSpan.FromSeconds(3)))
                {
                    try
                    {
                        using var child = Process.GetProcessById(pid);
                        if (!child.HasExited) child.Kill(entireProcessTree: true);
                    }
                    catch (ArgumentException) { }
                }
            }
            if (retryTask is not null)
            {
                try { await retryTask.WaitAsync(TimeSpan.FromSeconds(3)); }
                catch { /* Bounded cleanup after a failed assertion. */ }
            }
        }
    }

    private static async Task WaitForFileAsync(string path, Task pendingTask, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!File.Exists(path) && !pendingTask.IsCompleted && DateTime.UtcNow < deadline)
            await Task.Delay(20);
        Assert.True(File.Exists(path), pendingTask.IsCompleted
            ? "report retry ended before generator start"
            : "report generator did not signal start before timeout");
    }

    private static async Task<bool> WaitForProcessExitAsync(int processId, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                using var process = Process.GetProcessById(processId);
                if (process.HasExited) return true;
            }
            catch (ArgumentException) { return true; }
            await Task.Delay(20);
        }
        return false;
    }
}
