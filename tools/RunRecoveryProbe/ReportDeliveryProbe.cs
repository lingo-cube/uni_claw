using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using UniClaw.Host.Runtime;

internal static class ReportDeliveryProbe
{
    private const string SourceLabel = "crash-test-business-source";
    private const string InputFileName = "run.json";
    private const string GeneratorFileName = "generator.py";

    internal static async Task Run(string mode, string directory, string phase)
    {
        Directory.CreateDirectory(directory);
        var storageRoot = Path.Combine(directory, "store");
        var sourceDirectory = Path.Combine(directory, "source");
        var originalGeneratorPath = Path.Combine(directory, GeneratorFileName);
        var readyPath = Path.Combine(directory, "writer-ready.json");
        var deliveryIdPath = Path.Combine(directory, "delivery-id.txt");
        var delivery = new RequiredReportDelivery(storageRoot);

        if (mode == "write-report-delivery")
        {
            var request = new ReportDeliveryRequest("crash-test-owner", "crash-test-request", "run-report",
                originalGeneratorPath,
                new ReportInputContract("uniclaw.run-report.inputs.v1", "run-report", 1,
                    new[] { new ReportInputFile("run", InputFileName, false, true) },
                    new[] { "report.json", "report.md" }));
            var accepted = await delivery.AcceptAsync(request);
            File.WriteAllText(deliveryIdPath, accepted.DeliveryId);
            var record = accepted;

            if (phase != "accepted")
                record = await delivery.PrepareAsync(accepted.DeliveryId, sourceDirectory, SourceLabel);

            if (phase is "accepted" or "ready")
            {
                await SignalReadyAsync(readyPath, new
                {
                    ProcessId = Environment.ProcessId,
                    Phase = phase,
                    DeliveryId = accepted.DeliveryId,
                    Status = record.Status.ToString(),
                    record.Attempts,
                    record.SourceFingerprint,
                    record.GeneratorSha256,
                    GeneratorCalls = 0,
                });
                await Task.Delay(Timeout.InfiniteTimeSpan);
                return;
            }

            if (phase == "generating")
            {
                await delivery.PublishAsync(accepted.DeliveryId, async (_, cancellationToken) =>
                {
                    await SignalReadyAsync(readyPath, new
                    {
                        ProcessId = Environment.ProcessId,
                        Phase = phase,
                        DeliveryId = accepted.DeliveryId,
                        Status = ReportDeliveryStatus.Generating.ToString(),
                        Attempts = 1,
                        SourceFingerprint = record.SourceFingerprint,
                        record.GeneratorSha256,
                        GeneratorCalls = 1,
                    });
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                    return new ReportGenerationResult(0);
                });
                return;
            }

            if (phase == "published")
            {
                string? publishedGeneratorText = null;
                record = await delivery.PublishAsync(accepted.DeliveryId,
                    async (context, cancellationToken) =>
                    {
                        publishedGeneratorText = await File.ReadAllTextAsync(context.GeneratorPath, cancellationToken);
                        await WriteOutputsAsync(context, cancellationToken);
                        return new ReportGenerationResult(0);
                    });
                await SignalReadyAsync(readyPath, new
                {
                    ProcessId = Environment.ProcessId,
                    Phase = phase,
                    DeliveryId = accepted.DeliveryId,
                    Status = record.Status.ToString(),
                    record.Attempts,
                    record.SourceFingerprint,
                    record.GeneratorSha256,
                    PrivateGeneratorText = publishedGeneratorText,
                    GeneratorCalls = 1,
                });
                await Task.Delay(Timeout.InfiniteTimeSpan);
                return;
            }

            throw new ArgumentException("unsupported-report-delivery-phase:" + phase);
        }

        if (mode != "read-report-delivery") throw new ArgumentException("report-delivery-mode-required");
        var id = File.ReadAllText(deliveryIdPath).Trim();
        var initial = await delivery.InspectAsync(id) ?? throw new InvalidDataException("report-delivery-not-found");
        var prepared = initial.Record;
        if (prepared.Status == ReportDeliveryStatus.AwaitingInputs)
            prepared = await delivery.PrepareAsync(id, sourceDirectory, SourceLabel);

        var generationCalls = 0;
        string? privateInputText = null;
        string? privateGeneratorText = null;
        var published = await delivery.PublishAsync(id, async (context, cancellationToken) =>
        {
            generationCalls++;
            privateInputText = await File.ReadAllTextAsync(Path.Combine(context.InputDirectory, InputFileName), cancellationToken);
            privateGeneratorText = await File.ReadAllTextAsync(context.GeneratorPath, cancellationToken);
            await WriteOutputsAsync(context, cancellationToken, privateInputText);
            return new ReportGenerationResult(0);
        });
        var confirmed = await new RequiredReportDelivery(storageRoot).ConfirmAsync(id);
        var final = await new RequiredReportDelivery(storageRoot).InspectAsync(id)
            ?? throw new InvalidDataException("report-delivery-disappeared");
        var outputDirectory = delivery.OutputDirectory(id);
        var outputJson = await File.ReadAllTextAsync(Path.Combine(outputDirectory, "report.json"));
        var outputMarkdown = await File.ReadAllTextAsync(Path.Combine(outputDirectory, "report.md"));
        var resultPath = Path.Combine(directory, "reader-" + Environment.ProcessId + ".json");
        await WriteJsonAtomicallyAsync(resultPath, new
        {
            ProcessId = Environment.ProcessId,
            DeliveryId = id,
            InitialStatus = initial.Record.Status.ToString(),
            InitialIsReadable = initial.IsReadable,
            InitialSourceFingerprint = initial.Record.SourceFingerprint,
            PreparedStatus = prepared.Status.ToString(),
            PreparedAttempts = prepared.Attempts,
            PreparedSourceFingerprint = prepared.SourceFingerprint,
            PublishedStatus = published.Status.ToString(),
            PublishedAttempts = published.Attempts,
            PublishedSourceFingerprint = published.SourceFingerprint,
            GenerationCalls = generationCalls,
            PrivateInputText = privateInputText,
            PrivateGeneratorText = privateGeneratorText,
            ConfirmedStatus = confirmed.Status.ToString(),
            FinalStatus = final.Record.Status.ToString(),
            final.IsReadable,
            final.IntegrityProblem,
            final.Record.SourceFingerprint,
            ReportJson = outputJson,
            ReportMarkdown = outputMarkdown,
        });
    }

    private static async Task WriteOutputsAsync(ReportGenerationContext context, CancellationToken cancellationToken,
        string? inputText = null)
    {
        inputText ??= await File.ReadAllTextAsync(Path.Combine(context.InputDirectory, InputFileName), cancellationToken);
        Directory.CreateDirectory(context.OutputDirectory);
        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "report.json"),
            JsonSerializer.Serialize(new { phase = "report-delivery-crash-probe", source = inputText }), cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "report.md"),
            "# Crash probe report\n\n" + inputText, cancellationToken);
    }

    private static async Task SignalReadyAsync<T>(string path, T value) => await WriteJsonAtomicallyAsync(path, value);

    private static async Task WriteJsonAtomicallyAsync<T>(string path, T value)
    {
        var temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
        await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(value));
        File.Move(temporary, path);
    }
}
