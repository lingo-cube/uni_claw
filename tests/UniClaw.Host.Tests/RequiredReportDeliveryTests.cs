using System.Security.Cryptography;
using UniClaw.Host.Runtime;
using Xunit;

namespace UniClaw.Host.Tests;

public sealed class RequiredReportDeliveryTests
{
    [Fact]
    public async Task AcceptAsync_ReloadedHostCanInspectAwaitingInputsRequest()
    {
        var root = Path.Combine(Path.GetTempPath(), "required-report-delivery-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var generatorPath = Path.Combine(root, "fake-generator.txt");
        await File.WriteAllTextAsync(generatorPath, "fixed inert generator placeholder");

        var request = new ReportDeliveryRequest(
            "owner-run-1",
            "request-1",
            "run-report",
            generatorPath,
            new ReportInputContract(
                "uniclaw.run-report.inputs.v1",
                "run-report",
                1,
                new[] { new ReportInputFile("run", "run.json", true, true) },
                new[] { "report.json", "report.md" }));

        try
        {
            var accepted = await new RequiredReportDelivery(root).AcceptAsync(request);
            var snapshot = await new RequiredReportDelivery(root).InspectAsync(accepted.DeliveryId);

            Assert.NotNull(snapshot);
            Assert.False(snapshot!.IsReadable);
            Assert.Null(snapshot.IntegrityProblem);
            Assert.Equal(accepted.DeliveryId, snapshot.Record.DeliveryId);
            var restoredRequest = snapshot.Record.Request;
            Assert.Equal(request.OwnerRef, restoredRequest.OwnerRef);
            Assert.Equal(request.RequestId, restoredRequest.RequestId);
            Assert.Equal(request.ReportKind, restoredRequest.ReportKind);
            Assert.Equal(request.GeneratorPath, restoredRequest.GeneratorPath);
            Assert.Equal(request.Strict, restoredRequest.Strict);
            Assert.Equal(request.Inputs.Schema, restoredRequest.Inputs.Schema);
            Assert.Equal(request.Inputs.GeneratorName, restoredRequest.Inputs.GeneratorName);
            Assert.Equal(request.Inputs.GeneratorVersion, restoredRequest.Inputs.GeneratorVersion);
            Assert.Equal(request.Inputs.Files.Select(file => (file.Name, file.FileName, file.StrictRequired, file.RequiredForDelivery)),
                restoredRequest.Inputs.Files.Select(file => (file.Name, file.FileName, file.StrictRequired, file.RequiredForDelivery)));
            Assert.Equal(request.Inputs.OutputFiles, restoredRequest.Inputs.OutputFiles);
            Assert.Equal(ReportDeliveryStatus.AwaitingInputs, snapshot.Record.Status);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task PublishAfterPrepare_UsesCapturedInputsAndGeneratorAndConfirmIsIdempotent()
    {
        var root = Path.Combine(Path.GetTempPath(), "required-report-delivery-publish-" + Guid.NewGuid().ToString("N"));
        var storageRoot = Path.Combine(root, "store");
        var sourceDirectory = Path.Combine(root, "source");
        Directory.CreateDirectory(sourceDirectory);
        Directory.CreateDirectory(storageRoot);
        var originalGenerator = Path.Combine(root, "fake-generator.py");
        const string sourceText = "{\"runId\":\"captured-run\"}";
        const string generatorText = "# fixed generator bytes; never execute this file";
        await File.WriteAllTextAsync(Path.Combine(sourceDirectory, "run.json"), sourceText);
        await File.WriteAllTextAsync(originalGenerator, generatorText);

        var request = new ReportDeliveryRequest("owner-publish", "request-publish", "run-report", originalGenerator,
            new ReportInputContract("uniclaw.run-report.inputs.v1", "run-report", 1,
                new[] { new ReportInputFile("run", "run.json", false, true) },
                new[] { "report.json", "report.md" }));

        try
        {
            var accepted = await new RequiredReportDelivery(storageRoot).AcceptAsync(request);
            var prepared = await new RequiredReportDelivery(storageRoot)
                .PrepareAsync(accepted.DeliveryId, sourceDirectory, "captured-run");
            Assert.Equal(ReportDeliveryStatus.Ready, prepared.Status);
            Assert.False(string.IsNullOrWhiteSpace(prepared.SourceFingerprint));
            var sourceFingerprint = prepared.SourceFingerprint;

            File.Delete(Path.Combine(sourceDirectory, "run.json"));
            File.Delete(originalGenerator);

            var generationCalls = 0;
            string? capturedInputText = null;
            string? capturedGeneratorText = null;
            string? capturedInputDirectory = null;
            string? capturedGeneratorPath = null;
            var published = await new RequiredReportDelivery(storageRoot).PublishAsync(accepted.DeliveryId,
                async (context, cancellationToken) =>
                {
                    generationCalls++;
                    capturedInputDirectory = context.InputDirectory;
                    capturedGeneratorPath = context.GeneratorPath;
                    capturedInputText = await File.ReadAllTextAsync(
                        Path.Combine(context.InputDirectory, "run.json"), cancellationToken);
                    capturedGeneratorText = await File.ReadAllTextAsync(context.GeneratorPath, cancellationToken);
                    Directory.CreateDirectory(context.OutputDirectory);
                    await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "report.json"),
                        "{\"delivery\":\"captured-run\"}", cancellationToken);
                    await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "report.md"),
                        "captured report body", cancellationToken);
                    return new ReportGenerationResult(0);
                });

            Assert.Equal(ReportDeliveryStatus.Published, published.Status);
            Assert.Equal(1, generationCalls);
            Assert.Equal(sourceText, capturedInputText);
            Assert.Equal(generatorText, capturedGeneratorText);
            Assert.NotEqual(sourceDirectory, capturedInputDirectory);
            Assert.NotEqual(originalGenerator, capturedGeneratorPath);
            Assert.Equal(sourceFingerprint, published.SourceFingerprint);

            var publishedSnapshot = await new RequiredReportDelivery(storageRoot).InspectAsync(accepted.DeliveryId);
            Assert.NotNull(publishedSnapshot);
            Assert.True(publishedSnapshot!.IsReadable);
            Assert.Null(publishedSnapshot.IntegrityProblem);
            Assert.Equal(sourceFingerprint, publishedSnapshot.Record.SourceFingerprint);
            var outputDirectory = new RequiredReportDelivery(storageRoot).OutputDirectory(accepted.DeliveryId);
            Assert.Equal("{\"delivery\":\"captured-run\"}",
                await File.ReadAllTextAsync(Path.Combine(outputDirectory, "report.json")));
            Assert.Equal("captured report body", await File.ReadAllTextAsync(Path.Combine(outputDirectory, "report.md")));

            var delivered = await new RequiredReportDelivery(storageRoot).ConfirmAsync(accepted.DeliveryId);
            Assert.Equal(ReportDeliveryStatus.Delivered, delivered.Status);
            var repeatedPublish = await new RequiredReportDelivery(storageRoot).PublishAsync(accepted.DeliveryId,
                (_, _) =>
                {
                    generationCalls++;
                    throw new InvalidOperationException("published report must not be generated twice");
                });
            var repeatedConfirm = await new RequiredReportDelivery(storageRoot).ConfirmAsync(accepted.DeliveryId);

            Assert.Equal(ReportDeliveryStatus.Delivered, repeatedPublish.Status);
            Assert.Equal(ReportDeliveryStatus.Delivered, repeatedConfirm.Status);
            Assert.Equal(1, generationCalls);
            var deliveredSnapshot = await new RequiredReportDelivery(storageRoot).InspectAsync(accepted.DeliveryId);
            Assert.NotNull(deliveredSnapshot);
            Assert.True(deliveredSnapshot!.IsReadable);
            Assert.Equal(sourceFingerprint, deliveredSnapshot.Record.SourceFingerprint);
            Assert.Equal("{\"delivery\":\"captured-run\"}",
                await File.ReadAllTextAsync(Path.Combine(outputDirectory, "report.json")));
            Assert.Equal("captured report body", await File.ReadAllTextAsync(Path.Combine(outputDirectory, "report.md")));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Prepare_MissingRequiredInputFailsThenCanRetryAfterSourceIsCompleted()
    {
        var root = Path.Combine(Path.GetTempPath(), "required-report-delivery-missing-" + Guid.NewGuid().ToString("N"));
        var storageRoot = Path.Combine(root, "store");
        var sourceDirectory = Path.Combine(root, "source");
        Directory.CreateDirectory(sourceDirectory);
        Directory.CreateDirectory(storageRoot);
        var generatorPath = Path.Combine(root, "fake-generator.py");
        await File.WriteAllTextAsync(generatorPath, "# fixed generator bytes; never execute this file");
        var request = new ReportDeliveryRequest("owner-missing", "request-missing", "run-report", generatorPath,
            new ReportInputContract("uniclaw.run-report.inputs.v1", "run-report", 1,
                new[] { new ReportInputFile("run", "run.json", false, true) },
                new[] { "report.json", "report.md" }));

        try
        {
            var accepted = await new RequiredReportDelivery(storageRoot).AcceptAsync(request);
            var firstPrepare = await new RequiredReportDelivery(storageRoot)
                .PrepareAsync(accepted.DeliveryId, sourceDirectory, "completed-run");
            Assert.Equal(ReportDeliveryStatus.Failed, firstPrepare.Status);
            Assert.Null(firstPrepare.SourceFingerprint);

            var generationCalls = 0;
            await File.WriteAllTextAsync(Path.Combine(sourceDirectory, "run.json"), "{\"runId\":\"completed-run\"}");
            var secondPrepare = await new RequiredReportDelivery(storageRoot)
                .PrepareAsync(accepted.DeliveryId, sourceDirectory, "completed-run");
            Assert.Equal(ReportDeliveryStatus.Ready, secondPrepare.Status);
            Assert.False(string.IsNullOrWhiteSpace(secondPrepare.SourceFingerprint));

            var published = await new RequiredReportDelivery(storageRoot).PublishAsync(accepted.DeliveryId,
                async (context, cancellationToken) =>
                {
                    generationCalls++;
                    var source = await File.ReadAllTextAsync(Path.Combine(context.InputDirectory, "run.json"), cancellationToken);
                    Assert.Equal("{\"runId\":\"completed-run\"}", source);
                    Directory.CreateDirectory(context.OutputDirectory);
                    await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "report.json"), "{}", cancellationToken);
                    await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "report.md"), "completed report", cancellationToken);
                    return new ReportGenerationResult(0);
                });
            Assert.Equal(ReportDeliveryStatus.Published, published.Status);
            Assert.Equal(1, generationCalls);
            Assert.Equal(secondPrepare.SourceFingerprint, published.SourceFingerprint);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Publish_NonzeroGeneratorResultCanRetryFromSameCapturedInputsAfterReopen()
    {
        var root = Path.Combine(Path.GetTempPath(), "required-report-delivery-retry-" + Guid.NewGuid().ToString("N"));
        var storageRoot = Path.Combine(root, "store");
        var sourceDirectory = Path.Combine(root, "source");
        Directory.CreateDirectory(storageRoot);
        Directory.CreateDirectory(sourceDirectory);
        var generatorPath = Path.Combine(root, "fake-generator.py");
        await File.WriteAllTextAsync(generatorPath, "# fixed generator bytes; never execute this file");
        const string sourceText = "{\"runId\":\"retry-run\"}";
        await File.WriteAllTextAsync(Path.Combine(sourceDirectory, "run.json"), sourceText);
        var request = new ReportDeliveryRequest("owner-retry", "request-retry", "run-report", generatorPath,
            new ReportInputContract("uniclaw.run-report.inputs.v1", "run-report", 1,
                new[] { new ReportInputFile("run", "run.json", false, true) },
                new[] { "report.json", "report.md" }));

        try
        {
            var accepted = await new RequiredReportDelivery(storageRoot).AcceptAsync(request);
            var prepared = await new RequiredReportDelivery(storageRoot)
                .PrepareAsync(accepted.DeliveryId, sourceDirectory, "retry-run");
            Assert.Equal(ReportDeliveryStatus.Ready, prepared.Status);
            var sourceFingerprint = prepared.SourceFingerprint;

            var failed = await new RequiredReportDelivery(storageRoot).PublishAsync(accepted.DeliveryId,
                (_, _) => Task.FromResult(new ReportGenerationResult(17, "fixed generator failure")));
            Assert.Equal(ReportDeliveryStatus.Failed, failed.Status);
            Assert.Equal(1, failed.Attempts);
            Assert.Equal(sourceFingerprint, failed.SourceFingerprint);

            var reopened = new RequiredReportDelivery(storageRoot);
            var failedSnapshot = await reopened.InspectAsync(accepted.DeliveryId);
            Assert.NotNull(failedSnapshot);
            Assert.False(failedSnapshot!.IsReadable);
            Assert.Equal(ReportDeliveryStatus.Failed, failedSnapshot.Record.Status);
            Assert.Equal(sourceFingerprint, failedSnapshot.Record.SourceFingerprint);

            var successful = await new RequiredReportDelivery(storageRoot).PublishAsync(accepted.DeliveryId,
                async (context, cancellationToken) =>
                {
                    Assert.Equal(sourceText, await File.ReadAllTextAsync(
                        Path.Combine(context.InputDirectory, "run.json"), cancellationToken));
                    Directory.CreateDirectory(context.OutputDirectory);
                    await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "report.json"), "{\"attempt\":2}", cancellationToken);
                    await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "report.md"), "retry succeeded", cancellationToken);
                    return new ReportGenerationResult(0);
                });
            Assert.Equal(ReportDeliveryStatus.Published, successful.Status);
            Assert.Equal(2, successful.Attempts);
            Assert.Equal(sourceFingerprint, successful.SourceFingerprint);
            var publishedSnapshot = await new RequiredReportDelivery(storageRoot).InspectAsync(accepted.DeliveryId);
            Assert.NotNull(publishedSnapshot);
            Assert.True(publishedSnapshot!.IsReadable);
            Assert.Equal(sourceFingerprint, publishedSnapshot.Record.SourceFingerprint);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Inspect_TamperedPublishedOutputIsUnreadableAndCannotBeConfirmed()
    {
        var root = Path.Combine(Path.GetTempPath(), "required-report-delivery-tamper-" + Guid.NewGuid().ToString("N"));
        var storageRoot = Path.Combine(root, "store");
        var sourceDirectory = Path.Combine(root, "source");
        Directory.CreateDirectory(storageRoot);
        Directory.CreateDirectory(sourceDirectory);
        var generatorPath = Path.Combine(root, "fake-generator.py");
        await File.WriteAllTextAsync(generatorPath, "# fixed generator bytes; never execute this file");
        await File.WriteAllTextAsync(Path.Combine(sourceDirectory, "run.json"), "{\"runId\":\"tamper-run\"}");
        var request = new ReportDeliveryRequest("owner-tamper", "request-tamper", "run-report", generatorPath,
            new ReportInputContract("uniclaw.run-report.inputs.v1", "run-report", 1,
                new[] { new ReportInputFile("run", "run.json", false, true) },
                new[] { "report.json", "report.md" }));

        try
        {
            var accepted = await new RequiredReportDelivery(storageRoot).AcceptAsync(request);
            await new RequiredReportDelivery(storageRoot).PrepareAsync(accepted.DeliveryId, sourceDirectory, "tamper-run");
            var published = await new RequiredReportDelivery(storageRoot).PublishAsync(accepted.DeliveryId,
                async (context, cancellationToken) =>
                {
                    Directory.CreateDirectory(context.OutputDirectory);
                    await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "report.json"), "{}", cancellationToken);
                    await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "report.md"), "original report", cancellationToken);
                    return new ReportGenerationResult(0);
                });
            Assert.Equal(ReportDeliveryStatus.Published, published.Status);
            var outputPath = Path.Combine(new RequiredReportDelivery(storageRoot).OutputDirectory(accepted.DeliveryId), "report.md");
            await File.WriteAllTextAsync(outputPath, "tampered report");

            var snapshot = await new RequiredReportDelivery(storageRoot).InspectAsync(accepted.DeliveryId);
            Assert.NotNull(snapshot);
            Assert.False(snapshot!.IsReadable);
            Assert.False(string.IsNullOrWhiteSpace(snapshot.IntegrityProblem));
            Assert.Contains("report-output-hash-mismatch", snapshot.IntegrityProblem!);
            await Assert.ThrowsAsync<InvalidDataException>(
                () => new RequiredReportDelivery(storageRoot).ConfirmAsync(accepted.DeliveryId));
            var afterConfirmAttempt = await new RequiredReportDelivery(storageRoot).InspectAsync(accepted.DeliveryId);
            Assert.NotNull(afterConfirmAttempt);
            Assert.Equal(ReportDeliveryStatus.Published, afterConfirmAttempt!.Record.Status);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Publish_ConcurrentStoreCannotAcquireLeaseUntilActiveGenerationSettles()
    {
        var root = Path.Combine(Path.GetTempPath(), "required-report-delivery-lease-" + Guid.NewGuid().ToString("N"));
        var storageRoot = Path.Combine(root, "store");
        var sourceDirectory = Path.Combine(root, "source");
        Directory.CreateDirectory(storageRoot);
        Directory.CreateDirectory(sourceDirectory);
        var generatorPath = Path.Combine(root, "fake-generator.py");
        await File.WriteAllTextAsync(generatorPath, "# fixed generator bytes; never execute this file");
        await File.WriteAllTextAsync(Path.Combine(sourceDirectory, "run.json"), "{\"runId\":\"lease-run\"}");
        var request = new ReportDeliveryRequest("owner-lease", "request-lease", "run-report", generatorPath,
            new ReportInputContract("uniclaw.run-report.inputs.v1", "run-report", 1,
                new[] { new ReportInputFile("run", "run.json", false, true) },
                new[] { "report.json", "report.md" }));
        var generationStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var settleGeneration = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var generationCalls = 0;
        Task<ReportDeliveryRecord>? activePublish = null;

        try
        {
            var accepted = await new RequiredReportDelivery(storageRoot).AcceptAsync(request);
            await new RequiredReportDelivery(storageRoot).PrepareAsync(accepted.DeliveryId, sourceDirectory, "lease-run");
            activePublish = new RequiredReportDelivery(storageRoot).PublishAsync(accepted.DeliveryId,
                async (context, cancellationToken) =>
                {
                    generationCalls++;
                    generationStarted.TrySetResult();
                    await settleGeneration.Task.WaitAsync(cancellationToken);
                    Directory.CreateDirectory(context.OutputDirectory);
                    await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "report.json"), "{}", cancellationToken);
                    await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "report.md"), "lease released", cancellationToken);
                    return new ReportGenerationResult(0);
                });
            await generationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

            await Assert.ThrowsAnyAsync<IOException>(() =>
                new RequiredReportDelivery(storageRoot).PublishAsync(accepted.DeliveryId,
                    (_, _) => Task.FromResult(new ReportGenerationResult(0))));

            settleGeneration.TrySetResult();
            var published = await activePublish;
            Assert.Equal(ReportDeliveryStatus.Published, published.Status);
            var confirmed = await new RequiredReportDelivery(storageRoot).ConfirmAsync(accepted.DeliveryId);
            Assert.Equal(ReportDeliveryStatus.Delivered, confirmed.Status);
            Assert.Equal(1, generationCalls);
        }
        finally
        {
            settleGeneration.TrySetResult();
            if (activePublish is not null)
            {
                try { await activePublish; }
                catch { /* Preserve the original assertion failure while allowing generation to settle. */ }
            }
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DeliveredOutputDamage_RemainsDeliveredButRejectsReadPublishAndConfirm()
    {
        var root = Path.Combine(Path.GetTempPath(), "required-report-delivery-delivered-tamper-" + Guid.NewGuid().ToString("N"));
        var storageRoot = Path.Combine(root, "store");
        var sourceDirectory = Path.Combine(root, "source");
        Directory.CreateDirectory(storageRoot);
        Directory.CreateDirectory(sourceDirectory);
        var generatorPath = Path.Combine(root, "fake-generator.py");
        await File.WriteAllTextAsync(generatorPath, "# fixed inert generator");
        await File.WriteAllTextAsync(Path.Combine(sourceDirectory, "run.json"), "{\"runId\":\"delivered-tamper\"}");
        var request = new ReportDeliveryRequest("owner-delivered-tamper", "request-1", "run-report", generatorPath,
            new ReportInputContract("uniclaw.run-report.inputs.v1", "run-report", 1,
                new[] { new ReportInputFile("run", "run.json", false, true) },
                new[] { "report.json", "report.md" }));

        try
        {
            var host = new RequiredReportDelivery(storageRoot);
            var accepted = await host.AcceptAsync(request);
            await host.PrepareAsync(accepted.DeliveryId, sourceDirectory, "delivered-tamper");
            var generatorCalls = 0;
            var delivered = await host.PublishAsync(accepted.DeliveryId, async (context, cancellationToken) =>
            {
                generatorCalls++;
                Directory.CreateDirectory(context.OutputDirectory);
                await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "report.json"), "{}", cancellationToken);
                await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "report.md"), "original delivered text", cancellationToken);
                return new ReportGenerationResult(0);
            });
            delivered = await host.ConfirmAsync(accepted.DeliveryId);
            Assert.Equal(ReportDeliveryStatus.Delivered, delivered.Status);
            Assert.Equal(1, generatorCalls);
            var revision = delivered.Revision;
            var outputPath = Path.Combine(host.OutputDirectory(accepted.DeliveryId), "report.md");
            await File.WriteAllTextAsync(outputPath, "tampered after delivery");

            var damaged = await new RequiredReportDelivery(storageRoot).InspectAsync(accepted.DeliveryId);
            Assert.NotNull(damaged);
            Assert.Equal(ReportDeliveryStatus.Delivered, damaged!.Record.Status);
            Assert.Equal(revision, damaged.Record.Revision);
            Assert.False(damaged.IsReadable);
            Assert.Contains("report-output-hash-mismatch", damaged.IntegrityProblem!);
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                new RequiredReportDelivery(storageRoot).PublishAsync(accepted.DeliveryId, (_, _) =>
                {
                    generatorCalls++;
                    throw new InvalidOperationException("damaged delivered report must not regenerate");
                }));
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                new RequiredReportDelivery(storageRoot).ConfirmAsync(accepted.DeliveryId));

            var afterRejectedOperations = await new RequiredReportDelivery(storageRoot).InspectAsync(accepted.DeliveryId);
            Assert.NotNull(afterRejectedOperations);
            Assert.Equal(ReportDeliveryStatus.Delivered, afterRejectedOperations!.Record.Status);
            Assert.Equal(revision, afterRejectedOperations.Record.Revision);
            Assert.False(afterRejectedOperations.IsReadable);
            Assert.Equal("tampered after delivery", await File.ReadAllTextAsync(outputPath));
            Assert.Equal(1, generatorCalls);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("source")]
    [InlineData("generator")]
    public async Task Publish_CapturedSourceOrGeneratorHashMismatchRejectsBeforeRetryDelegate(string tamperTarget)
    {
        var root = Path.Combine(Path.GetTempPath(), "required-report-delivery-hash-tamper-" + tamperTarget + "-" + Guid.NewGuid().ToString("N"));
        var storageRoot = Path.Combine(root, "store");
        var sourceDirectory = Path.Combine(root, "source");
        Directory.CreateDirectory(storageRoot);
        Directory.CreateDirectory(sourceDirectory);
        var generatorPath = Path.Combine(root, "fake-generator.py");
        await File.WriteAllTextAsync(generatorPath, "# fixed inert generator");
        await File.WriteAllTextAsync(Path.Combine(sourceDirectory, "run.json"), "{\"runId\":\"hash-tamper\"}");
        var request = new ReportDeliveryRequest("owner-hash-tamper", "request-" + tamperTarget, "run-report", generatorPath,
            new ReportInputContract("uniclaw.run-report.inputs.v1", "run-report", 1,
                new[] { new ReportInputFile("run", "run.json", false, true) },
                new[] { "report.json", "report.md" }));

        try
        {
            var host = new RequiredReportDelivery(storageRoot);
            var accepted = await host.AcceptAsync(request);
            await host.PrepareAsync(accepted.DeliveryId, sourceDirectory, "hash-tamper");
            string? capturedInputDirectory = null;
            string? capturedGeneratorPath = null;
            var firstAttempt = await host.PublishAsync(accepted.DeliveryId, (context, _) =>
            {
                capturedInputDirectory = context.InputDirectory;
                capturedGeneratorPath = context.GeneratorPath;
                return Task.FromResult(new ReportGenerationResult(19, "fixture attempt to obtain public generation context"));
            });
            Assert.Equal(ReportDeliveryStatus.Failed, firstAttempt.Status);
            Assert.Equal(1, firstAttempt.Attempts);
            Assert.NotNull(capturedInputDirectory);
            Assert.NotNull(capturedGeneratorPath);

            if (tamperTarget == "source")
                await File.WriteAllTextAsync(Path.Combine(capturedInputDirectory!, "run.json"), "{\"runId\":\"changed\"}");
            else
                await File.WriteAllTextAsync(capturedGeneratorPath!, "# changed pinned generator bytes");

            var retryDelegateCalls = 0;
            var rejected = await new RequiredReportDelivery(storageRoot).PublishAsync(accepted.DeliveryId, (_, _) =>
            {
                retryDelegateCalls++;
                return Task.FromResult(new ReportGenerationResult(0));
            });
            Assert.Equal(ReportDeliveryStatus.Failed, rejected.Status);
            Assert.Equal(1, rejected.Attempts);
            Assert.Equal(0, retryDelegateCalls);
            Assert.Contains(tamperTarget == "source" ? "report-input-hash-mismatch" : "report-generator-hash-mismatch",
                rejected.Reason!, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Prepare_SourceQuotaFailureIsIndependentFromAnotherDelivery()
    {
        var root = Path.Combine(Path.GetTempPath(), "required-report-delivery-source-quota-" + Guid.NewGuid().ToString("N"));
        var storageRoot = Path.Combine(root, "store");
        var sourceDirectory = Path.Combine(root, "source");
        Directory.CreateDirectory(storageRoot);
        Directory.CreateDirectory(sourceDirectory);
        var generatorPath = Path.Combine(root, "fake-generator.py");
        await File.WriteAllTextAsync(generatorPath, "# inert");
        var limits = new ReportDeliveryLimits
        {
            MaxSourceFileBytes = 16,
            MaxSourceBytes = 16,
            MaxOutputFileBytes = 128,
            MaxGeneratorBytes = 128,
            MaxRecordBytes = 4096,
        };
        var host = new RequiredReportDelivery(storageRoot, limits);
        var oversizedSource = Path.Combine(sourceDirectory, "oversized.json");
        var boundedSource = Path.Combine(sourceDirectory, "bounded.json");
        await File.WriteAllTextAsync(oversizedSource, "{\"payload\":\"too-large-for-source-budget\"}");
        await File.WriteAllTextAsync(boundedSource, "{\"x\":1}");
        var oversizedRequest = new ReportDeliveryRequest("owner-quota", "request-oversized", "run-report", generatorPath,
            new ReportInputContract("uniclaw.run-report.inputs.v1", "run-report", 1,
                new[] { new ReportInputFile("run", "oversized.json", false, true) },
                new[] { "report.json", "report.md" }));
        var boundedRequest = new ReportDeliveryRequest("owner-quota", "request-bounded", "run-report", generatorPath,
            new ReportInputContract("uniclaw.run-report.inputs.v1", "run-report", 1,
                new[] { new ReportInputFile("run", "bounded.json", false, true) },
                new[] { "report.json", "report.md" }));

        try
        {
            var oversized = await host.AcceptAsync(oversizedRequest);
            var bounded = await host.AcceptAsync(boundedRequest);
            var oversizedPrepared = await host.PrepareAsync(oversized.DeliveryId, sourceDirectory, "oversized-source");
            Assert.Equal(ReportDeliveryStatus.Failed, oversizedPrepared.Status);
            Assert.Null(oversizedPrepared.SourceFingerprint);
            Assert.Contains("source-byte-limit", oversizedPrepared.Reason!, StringComparison.Ordinal);

            var generationCalls = 0;
            var boundedPrepared = await new RequiredReportDelivery(storageRoot, limits)
                .PrepareAsync(bounded.DeliveryId, sourceDirectory, "bounded-source");
            Assert.Equal(ReportDeliveryStatus.Ready, boundedPrepared.Status);
            var boundedPublished = await new RequiredReportDelivery(storageRoot, limits).PublishAsync(bounded.DeliveryId,
                async (context, cancellationToken) =>
                {
                    generationCalls++;
                    Directory.CreateDirectory(context.OutputDirectory);
                    await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "report.json"), "{}", cancellationToken);
                    await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "report.md"), "bounded report", cancellationToken);
                    return new ReportGenerationResult(0);
                });
            Assert.Equal(ReportDeliveryStatus.Published, boundedPublished.Status);
            Assert.Equal(1, generationCalls);
            var oversizedAfterSiblingSuccess = await new RequiredReportDelivery(storageRoot, limits).InspectAsync(oversized.DeliveryId);
            Assert.NotNull(oversizedAfterSiblingSuccess);
            Assert.Equal(ReportDeliveryStatus.Failed, oversizedAfterSiblingSuccess!.Record.Status);
            Assert.Contains("source-byte-limit", oversizedAfterSiblingSuccess.Record.Reason!, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Publish_NonCooperativeTimeoutRetainsJobLeaseUntilSettlementThenAllowsRetry()
    {
        var root = Path.Combine(Path.GetTempPath(), "required-report-delivery-noncooperative-" + Guid.NewGuid().ToString("N"));
        var storageRoot = Path.Combine(root, "store");
        var sourceDirectory = Path.Combine(root, "source");
        Directory.CreateDirectory(storageRoot);
        Directory.CreateDirectory(sourceDirectory);
        var generatorPath = Path.Combine(root, "fake-generator.py");
        var sentinelPath = Path.Combine(root, "business-sentinel.txt");
        await File.WriteAllTextAsync(generatorPath, "# inert");
        await File.WriteAllTextAsync(Path.Combine(sourceDirectory, "run.json"), "{\"runId\":\"timeout-run\"}");
        await File.WriteAllTextAsync(sentinelPath, "business outcome remains completed");
        var sentinelHash = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(sentinelPath))).ToLowerInvariant();
        var limits = new ReportDeliveryLimits { GenerationTimeout = TimeSpan.FromMilliseconds(150) };
        var host = new RequiredReportDelivery(storageRoot, limits);
        var request = new ReportDeliveryRequest("owner-timeout", "request-timeout", "run-report", generatorPath,
            new ReportInputContract("uniclaw.run-report.inputs.v1", "run-report", 1,
                new[] { new ReportInputFile("run", "run.json", false, true) },
                new[] { "report.json", "report.md" }));
        var releaseNonCooperativeGenerator = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var generatorStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var generatorFinished = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var generationCalls = 0;

        try
        {
            var accepted = await host.AcceptAsync(request);
            await host.PrepareAsync(accepted.DeliveryId, sourceDirectory, "timeout-run");
            var timedOut = await host.PublishAsync(accepted.DeliveryId, async (context, _) =>
            {
                generationCalls++;
                generatorStarted.TrySetResult(true);
                await releaseNonCooperativeGenerator.Task;
                Directory.CreateDirectory(context.OutputDirectory);
                await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "report.json"), "{\"retry\":true}", CancellationToken.None);
                await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "report.md"), "retry after settlement", CancellationToken.None);
                generatorFinished.TrySetResult(true);
                return new ReportGenerationResult(0);
            });
            await generatorStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Equal(ReportDeliveryStatus.Failed, timedOut.Status);
            Assert.Equal(1, timedOut.Attempts);
            Assert.Contains("CanceledException", timedOut.Reason!, StringComparison.Ordinal);

            var siblingStore = new RequiredReportDelivery(storageRoot, limits);
            await Assert.ThrowsAnyAsync<IOException>(() => host.PublishAsync(accepted.DeliveryId,
                (_, _) => Task.FromResult(new ReportGenerationResult(0))));
            await Assert.ThrowsAnyAsync<IOException>(() => siblingStore.PublishAsync(accepted.DeliveryId,
                (_, _) => Task.FromResult(new ReportGenerationResult(0))));

            var retryDeadline = DateTime.UtcNow.AddSeconds(5);
            ReportDeliveryRecord? retry = null;
            while (retry is null && DateTime.UtcNow < retryDeadline)
            {
                releaseNonCooperativeGenerator.TrySetResult(true);
                try
                {
                    retry = await new RequiredReportDelivery(storageRoot, limits).PublishAsync(accepted.DeliveryId,
                        async (context, cancellationToken) =>
                        {
                            generationCalls++;
                            Directory.CreateDirectory(context.OutputDirectory);
                            await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "report.json"), "{\"retry\":true}", cancellationToken);
                            await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "report.md"), "retry after settlement", cancellationToken);
                            return new ReportGenerationResult(0);
                        });
                }
                catch (IOException)
                {
                    await Task.Delay(20);
                }
            }

            Assert.NotNull(retry);
            Assert.Equal(ReportDeliveryStatus.Published, retry!.Status);
            Assert.Equal(2, retry.Attempts);
            Assert.Equal(2, generationCalls);
            await generatorFinished.Task.WaitAsync(TimeSpan.FromSeconds(3));
            var delivered = await new RequiredReportDelivery(storageRoot, limits).ConfirmAsync(accepted.DeliveryId);
            Assert.Equal(ReportDeliveryStatus.Delivered, delivered.Status);
            Assert.Equal(sentinelHash, Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(sentinelPath))).ToLowerInvariant());
        }
        finally
        {
            releaseNonCooperativeGenerator.TrySetResult(true);
            if (generatorStarted.Task.IsCompleted)
            {
                try { await generatorFinished.Task.WaitAsync(TimeSpan.FromSeconds(5)); }
                catch (TimeoutException) { }
            }
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Accept_DistinctRequestIdsCreateDistinctRequirementsForSameOwner()
    {
        var root = Path.Combine(Path.GetTempPath(), "required-report-delivery-request-ids-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var generatorPath = Path.Combine(root, "fake-generator.py");
        await File.WriteAllTextAsync(generatorPath, "# inert");
        var inputs = new ReportInputContract("uniclaw.run-report.inputs.v1", "run-report", 1,
            new[] { new ReportInputFile("run", "run.json", false, true) },
            new[] { "report.json", "report.md" });

        try
        {
            var host = new RequiredReportDelivery(root);
            var first = await host.AcceptAsync(new("owner-distinct-requests", "request-a", "run-report", generatorPath, inputs));
            var second = await host.AcceptAsync(new("owner-distinct-requests", "request-b", "run-report", generatorPath, inputs));
            Assert.NotEqual(first.DeliveryId, second.DeliveryId);
            Assert.Equal(ReportDeliveryStatus.AwaitingInputs, first.Status);
            Assert.Equal(ReportDeliveryStatus.AwaitingInputs, second.Status);
            Assert.NotNull(await new RequiredReportDelivery(root).InspectAsync(first.DeliveryId));
            Assert.NotNull(await new RequiredReportDelivery(root).InspectAsync(second.DeliveryId));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
