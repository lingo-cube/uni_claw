using UniClaw.Host.Runtime;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;

namespace UniClaw.Host.Tests;

public sealed class RequiredReportSourceProtectionTests
{
    [Fact]
    public async Task BindSourceAsync_ReloadedDeliveryRemainsAwaitingAndRejectsDifferentBinding()
    {
        var root = Path.Combine(Path.GetTempPath(), "required-report-source-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var generator = Path.Combine(root, "generator.py");
        await File.WriteAllTextAsync(generator, "fixed");
        try
        {
            var accepted = await new RequiredReportDelivery(root).AcceptAsync(Request(generator));
            var source = Path.Combine(root, "source");
            var bound = await new RequiredReportDelivery(root).BindSourceAsync(accepted.DeliveryId, source, "coverage-run");
            Assert.Equal(ReportDeliveryStatus.AwaitingInputs, bound.Status);

            var restored = await new RequiredReportDelivery(root).InspectAsync(accepted.DeliveryId);
            Assert.Equal(Path.GetFullPath(source), restored!.Record.SourceDirectory);
            await Assert.ThrowsAsync<InvalidOperationException>(() => new RequiredReportDelivery(root)
                .BindSourceAsync(accepted.DeliveryId, Path.Combine(root, "other"), "coverage-run"));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task GetProtectedSourcesAsync_RespectsDeliveryAndRetention()
    {
        var root = Path.Combine(Path.GetTempPath(), "required-report-protected-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var generator = Path.Combine(root, "generator.py");
        await File.WriteAllTextAsync(generator, "fixed");
        try
        {
            var delivery = new RequiredReportDelivery(root);
            var attachments = new[]
            {
                new ReportAttachmentRequirement("evidence/screen.png", "coverage-evidence", true),
                new ReportAttachmentRequirement("evidence/raw", "capture-tree", RetainAfterDelivery: true, Directory: true)
            };
            var accepted = await delivery.AcceptAsync(Request(generator, new[]
            {
                new ReportProtectedSource("captures/final.png", "coverage-capture"),
                new ReportProtectedSource("archive/run.xml", "audit", true)
            }, attachments));
            Assert.Equal(attachments, accepted.Request.Inputs.Attachments!.ToArray());
            var awaiting = await delivery.GetProtectedSourcesAsync(accepted.DeliveryId);
            Assert.Contains(awaiting, source => source.RelativePath == "coverage.json" && source.Purpose == "required-report-input");
            Assert.Contains(awaiting, source => source.RelativePath == "captures/final.png");
            Assert.Contains(awaiting, source => source.RelativePath == "evidence/screen.png" && source.Purpose == "coverage-evidence");

            var source = Path.Combine(root, "source"); Directory.CreateDirectory(source);
            await File.WriteAllTextAsync(Path.Combine(source, "coverage.json"), "{}");
            await delivery.PrepareAsync(accepted.DeliveryId, source, "coverage-run");
            await delivery.PublishAsync(accepted.DeliveryId, async (context, token) =>
            {
                Directory.CreateDirectory(context.OutputDirectory);
                await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "coverage-report.json"), "{}", token);
                return new ReportGenerationResult(0);
            });
            await delivery.ConfirmAsync(accepted.DeliveryId);
            var delivered = await delivery.GetProtectedSourcesAsync(accepted.DeliveryId);
            Assert.DoesNotContain(delivered, item => item.RelativePath == "coverage.json");
            Assert.DoesNotContain(delivered, item => item.RelativePath == "captures/final.png");
            Assert.DoesNotContain(delivered, item => item.RelativePath == "evidence/screen.png");
            Assert.Contains(delivered, item => item.RelativePath == "archive/run.xml" && item.RetainAfterDelivery);
            Assert.Contains(delivered, item => item.RelativePath == "evidence/raw" && item.RetainAfterDelivery);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task RecordSourceFailureAsync_PersistsOnlyBeforeInputsAreFixed()
    {
        var root = Path.Combine(Path.GetTempPath(), "required-report-source-failure-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var generator = Path.Combine(root, "generator.py");
        await File.WriteAllTextAsync(generator, "fixed");
        try
        {
            var delivery = new RequiredReportDelivery(root);
            var accepted = await delivery.AcceptAsync(Request(generator));
            var failed = await delivery.RecordSourceFailureAsync(accepted.DeliveryId, "coverage-source-not-persisted");
            Assert.Equal(ReportDeliveryStatus.Failed, failed.Status);
            Assert.Equal("coverage-source-not-persisted", failed.Reason);
            Assert.Equal(0, failed.Attempts);

            var source = Path.Combine(root, "source"); Directory.CreateDirectory(source);
            await File.WriteAllTextAsync(Path.Combine(source, "coverage.json"), "{}");
            var ready = await delivery.PrepareAsync(accepted.DeliveryId, source, "coverage-run");
            Assert.Equal(ReportDeliveryStatus.Ready, ready.Status);
            await Assert.ThrowsAsync<InvalidOperationException>(() => delivery
                .RecordSourceFailureAsync(accepted.DeliveryId, "must-not-overwrite-fixed-inputs"));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task AcceptAsync_RejectsInvalidAttachmentRequirements()
    {
        var root = Path.Combine(Path.GetTempPath(), "required-report-attachment-invalid-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var generator = Path.Combine(root, "generator.py");
        await File.WriteAllTextAsync(generator, "fixed");
        try
        {
            var invalidHash = new ReportAttachmentRequirement("evidence/raw", "capture", ExpectedSha256: "not-a-sha");
            await Assert.ThrowsAsync<InvalidDataException>(() => new RequiredReportDelivery(root)
                .AcceptAsync(Request(generator, attachments: new[] { invalidHash })));
            var directoryHash = new ReportAttachmentRequirement("evidence/tree", "capture", ExpectedSha256: new string('a', 64), Directory: true);
            await Assert.ThrowsAsync<InvalidDataException>(() => new RequiredReportDelivery(root)
                .AcceptAsync(Request(generator, attachments: new[] { directoryHash })));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task OldReadyRecordWithoutNewNullFields_RestartsPrepareAndPublish()
    {
        var root = Path.Combine(Path.GetTempPath(), "required-report-old-ready-" + Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "source");
        Directory.CreateDirectory(source);
        await File.WriteAllTextAsync(Path.Combine(source, "run.json"), "{}");
        try
        {
            const string owner = "old-owner";
            const string requestId = "old-request";
            const string sourceLabel = "old-run";
            const string generatorText = "fixed";
            var id = "report-" + Hash($$"""{"ownerRef":"{{owner}}","requestId":"{{requestId}}"}""");
            var job = Path.Combine(root, id);
            var inputs = Path.Combine(job, "inputs");
            Directory.CreateDirectory(Path.Combine(job, "versions"));
            Directory.CreateDirectory(inputs);
            await File.WriteAllTextAsync(Path.Combine(job, "generator.py"), generatorText);
            await File.WriteAllTextAsync(Path.Combine(inputs, "run.json"), "{}");

            var generatorHash = Hash(generatorText);
            var sourceHash = Hash("{}");
            var sources = $$"""[{"name":"run","fileName":"run.json","present":true,"bytes":2,"sha256":"{{sourceHash}}"}]""";
            var inputContract = "{\"schemaVersion\":\"uniclaw.run-report.inputs.v1\",\"generatorName\":\"run-report\",\"generatorVersion\":1,\"files\":[{\"name\":\"run\",\"fileName\":\"run.json\",\"strictRequired\":false,\"requiredForDelivery\":true}],\"outputFiles\":[\"report.json\"]}";
            var fingerprint = Hash($$"""{"generatorSha256":"{{generatorHash}}","inputs":{{inputContract}},"strict":false,"sourceLabel":"{{sourceLabel}}","sources":{{sources}}}""");
            var request = $$"""{"ownerRef":"{{owner}}","requestId":"{{requestId}}","reportKind":"run-report","generatorPath":"{{Escape(Path.Combine(root, "generator.py"))}}","inputs":{{inputContract}},"strict":false,"expectedGeneratorSha256":null}""";
            var payload = $$"""{"deliveryId":"{{id}}","request":{{request}},"generatorSha256":"{{generatorHash}}","status":"ready","revision":1,"attempts":0,"sourceDirectory":"{{Escape(source)}}","sourceLabel":"{{sourceLabel}}","sourceFingerprint":"{{fingerprint}}","sources":{{sources}},"outputs":null,"reason":null}""";
            var frameHash = Hash("\n" + payload);
            var version = "1-" + frameHash + ".json";
            var frame = $$"""{"schema":"uniclaw.report-delivery.v1","payload":{{JsonSerializer.Serialize(payload)}},"previousFile":null,"previousHash":null,"hash":"{{frameHash}}"}""";
            var head = $$"""{"schema":"uniclaw.report-delivery.v1","revision":1,"fileName":"{{version}}","hash":"{{frameHash}}"}""";
            await File.WriteAllTextAsync(Path.Combine(job, "versions", version), frame);
            await File.WriteAllTextAsync(Path.Combine(job, "head.json"), head);

            var prepared = await new RequiredReportDelivery(root).PrepareAsync(id, source, sourceLabel);
            Assert.Equal(ReportDeliveryStatus.Ready, prepared.Status);
            var published = await new RequiredReportDelivery(root).PublishAsync(id, async (context, token) =>
            {
                Directory.CreateDirectory(context.OutputDirectory);
                await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "report.json"), "{}", token);
                return new ReportGenerationResult(0);
            });
            Assert.Equal(ReportDeliveryStatus.Published, published.Status);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData("../escape.json")]
    [InlineData("nested/../escape.json")]
    [InlineData("nested\\escape.json")]
    [InlineData("/absolute.json")]
    public void Paths_RejectInvalidRelativePaths(string path) =>
        Assert.Throws<ArgumentException>(() => RequiredReportDeliveryPaths.ValidateRelativePath(path));

    [Fact]
    public void Paths_ResolveNestedPathAndRejectExistingLink()
    {
        var root = Path.Combine(Path.GetTempPath(), "required-report-path-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            Assert.Equal(Path.Combine(root, "nested", "report.json"),
                RequiredReportDeliveryPaths.ResolveSourcePath(root, "nested/report.json"));
            var link = Path.Combine(root, "linked");
            try { Directory.CreateSymbolicLink(link, Path.GetTempPath()); }
            catch (UnauthorizedAccessException) { return; }
            Assert.Throws<InvalidDataException>(() => RequiredReportDeliveryPaths.ResolveSourcePath(root, "linked/report.json"));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private static ReportDeliveryRequest Request(string generator, IReadOnlyList<ReportProtectedSource>? protectedSources = null,
        IReadOnlyList<ReportAttachmentRequirement>? attachments = null) => new(
        "coverage-owner", "coverage-request", "coverage-report", generator,
        new ReportInputContract("uniclaw.coverage-report.inputs.v1", "coverage-report", 1,
            new[] { new ReportInputFile("coverage", "coverage.json", false, true) }, new[] { "coverage-report.json" }, attachments),
        ProtectedSources: protectedSources);

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
