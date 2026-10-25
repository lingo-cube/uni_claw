using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UniClaw.Host.Runtime;

namespace UniClaw.Host.Dsh;

public static class ReportDeliveryEndpoints
{
    private const string Prefix = "/api/uniclaw-runtime";
    private const int MaxRetryBodyBytes = 4096;
    private const long MaxArtifactBytes = 32L * 1024 * 1024;

    private sealed record ArtifactView(string FileName, string Href);
    private sealed record DeliveryView(
        string DeliveryId,
        string Status,
        bool Readable,
        string? Reason,
        string? SourceFingerprint,
        int Revision,
        int Attempts,
        string OwnerRef,
        string RequestId,
        string ReportKind,
        IReadOnlyList<ArtifactView> Artifacts);

    private sealed record RetryBody(string? RunDir);

    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder routes, RuntimeToolHost tools, string runsRoot)
    {
        ArgumentNullException.ThrowIfNull(routes);
        ArgumentNullException.ThrowIfNull(tools);
        ArgumentException.ThrowIfNullOrWhiteSpace(runsRoot);
        var root = Path.GetFullPath(runsRoot);

        routes.MapGet(Prefix + "/report-deliveries", async (HttpContext context, CancellationToken token) =>
        {
            var ownerRef = context.Request.Query["ownerRef"].ToString();
            if (string.IsNullOrWhiteSpace(ownerRef)) return Results.BadRequest(new { error = "ownerRef-required" });
            try
            {
                var rows = await tools.ListRequiredReportsAsync(root, ownerRef, token);
                return Results.Ok(rows.Select(row => Project(row)).ToArray());
            }
            catch (Exception error) when (IsStorageError(error))
            { return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "report-store-unavailable"); }
        });

        routes.MapGet(Prefix + "/runs/{runId}/report-deliveries", async (string runId, CancellationToken token) =>
        {
            if (string.IsNullOrWhiteSpace(runId)) return Results.BadRequest(new { error = "runId-required" });
            try
            {
                var rows = await tools.ListRequiredReportsAsync(root, "runtime-run:" + runId, token);
                return Results.Ok(rows.Select(row => Project(row)).ToArray());
            }
            catch (Exception error) when (IsStorageError(error))
            { return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "report-store-unavailable"); }
        });

        routes.MapGet(Prefix + "/report-deliveries/{deliveryId}", async (string deliveryId, CancellationToken token) =>
            await Inspect(deliveryId, token));

        routes.MapGet(Prefix + "/report-deliveries/{deliveryId}/sources", async (string deliveryId, CancellationToken token) =>
        {
            if (!IsDeliveryId(deliveryId)) return Results.NotFound(new { error = "report-delivery-not-found" });
            try
            {
                var snapshot = await tools.GetReportDeliveryAsync(root, deliveryId, token);
                if (snapshot is null) return Results.NotFound(new { error = "report-delivery-not-found" });
                return Results.Ok(new { snapshot.Record.DeliveryId, sourceBound = snapshot.Record.SourceDirectory is not null,
                    protectedSources = await tools.GetReportSourceProtectionAsync(root, deliveryId, token) });
            }
            catch (Exception error) when (IsStorageError(error))
            { return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "report-store-unavailable"); }
        });

        routes.MapPost(Prefix + "/report-deliveries/{deliveryId}/retry", async (string deliveryId,
            HttpContext context, CancellationToken token) =>
        {
            if (!IsDeliveryId(deliveryId)) return Results.NotFound(new { error = "report-delivery-not-found" });
            RetryBody? body;
            try { body = await ReadRetryBody(context, token); }
            catch (BadHttpRequestException error)
            {
                return Results.Json(new { error = error.StatusCode == StatusCodes.Status413PayloadTooLarge
                    ? "retry-body-too-large" : "invalid-retry-body" },
                    statusCode: error.StatusCode == StatusCodes.Status413PayloadTooLarge
                        ? StatusCodes.Status413PayloadTooLarge : StatusCodes.Status400BadRequest);
            }
            catch (JsonException) { return Results.BadRequest(new { error = "invalid-retry-body" }); }

            try
            {
                var result = await tools.RetryRequiredReportAsync(root, deliveryId, body?.RunDir, token);
                if (!result.Ok)
                    return Results.Json(Project(result.Delivery), statusCode: StatusCodes.Status409Conflict);
                return Results.Ok(Project(result.Delivery));
            }
            catch (KeyNotFoundException) { return Results.NotFound(new { error = "report-delivery-not-found" }); }
            catch (Exception error) when (IsStorageError(error))
            { return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "report-store-unavailable"); }
            catch (InvalidOperationException error)
            {
                var reason = SafeReason(error.Message);
                return Results.Conflict(new { error = reason });
            }
            catch (IOException)
            { return Results.Problem(statusCode: StatusCodes.Status422UnprocessableEntity, title: "report-generation-failed"); }
        });

        routes.MapGet(Prefix + "/report-deliveries/{deliveryId}/artifacts/{fileName}", async (
            string deliveryId, string fileName, CancellationToken token) =>
        {
            if (!IsDeliveryId(deliveryId) || string.IsNullOrWhiteSpace(fileName) || Path.GetFileName(fileName) != fileName)
                return Results.NotFound(new { error = "report-artifact-not-found" });
            try
            {
                var snapshot = await tools.GetReportDeliveryAsync(root, deliveryId, token);
                var declared = snapshot?.Record.Outputs?.SingleOrDefault(output => output.FileName == fileName);
                if (snapshot is null || !snapshot.IsReadable || snapshot.Record.Status != ReportDeliveryStatus.Delivered
                    || declared is null || !snapshot.Record.Request.Inputs.OutputFiles.Contains(fileName, StringComparer.Ordinal))
                    return Results.NotFound(new { error = "report-artifact-not-found" });

                var path = tools.ResolveRequiredReportArtifact(root, snapshot.Record.DeliveryId, fileName);
                if (declared.Bytes <= 0 || declared.Bytes > MaxArtifactBytes) return Results.NotFound(new { error = "report-artifact-not-found" });
                var info = new FileInfo(path);
                if (!info.Exists || info.Length != declared.Bytes || info.Length > MaxArtifactBytes)
                    return Results.NotFound(new { error = "report-artifact-not-found" });
                var bytes = await ReadBoundedArtifact(path, declared.Bytes, token);
                var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
                if (bytes.LongLength != declared.Bytes || hash != declared.Sha256)
                    return Results.Conflict(new { error = "report-artifact-integrity-failed" });
                return Results.File(bytes, fileName.EndsWith(".json", StringComparison.Ordinal)
                    ? "application/json; charset=utf-8" : "text/markdown; charset=utf-8", fileName);
            }
            catch (KeyNotFoundException) { return Results.NotFound(new { error = "report-artifact-not-found" }); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException)
            { return Results.NotFound(new { error = "report-artifact-not-found" }); }
        });

        return routes;

        async Task<IResult> Inspect(string deliveryId, CancellationToken token)
        {
            if (!IsDeliveryId(deliveryId)) return Results.NotFound(new { error = "report-delivery-not-found" });
            try
            {
                var snapshot = await tools.GetReportDeliveryAsync(root, deliveryId, token);
                return snapshot is null ? Results.NotFound(new { error = "report-delivery-not-found" })
                    : Results.Ok(Project(snapshot));
            }
            catch (Exception error) when (IsStorageError(error))
            { return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "report-store-unavailable"); }
        }

        DeliveryView Project(ReportDeliverySnapshot snapshot)
        {
            var record = snapshot.Record;
            var reason = snapshot.IntegrityProblem ?? record.Reason;
            return new(record.DeliveryId, record.Status.ToString(), snapshot.IsReadable, SafeReason(reason),
                record.SourceFingerprint, record.Revision, record.Attempts, record.Request.OwnerRef,
                record.Request.RequestId, record.Request.ReportKind,
                (record.Outputs ?? Array.Empty<ReportOutputSnapshot>())
                    .Select(output => new ArtifactView(output.FileName,
                        Prefix + "/report-deliveries/" + Uri.EscapeDataString(record.DeliveryId)
                        + "/artifacts/" + Uri.EscapeDataString(output.FileName)))
                    .ToArray());
        }
    }

    private static bool IsStorageError(Exception error) => error is IOException or UnauthorizedAccessException
        or InvalidDataException or System.Text.Json.JsonException;

    private static bool IsDeliveryId(string deliveryId)
    {
        if (deliveryId.Length != 71 || !deliveryId.StartsWith("report-", StringComparison.Ordinal)) return false;
        return deliveryId.AsSpan(7).IndexOfAnyExcept("0123456789abcdef") < 0;
    }

    private static async Task<RetryBody?> ReadRetryBody(HttpContext context, CancellationToken token)
    {
        var request = context.Request;
        if (request.ContentLength is 0) return null;
        if (request.ContentLength > MaxRetryBodyBytes)
            throw new BadHttpRequestException("retry-body-too-large", StatusCodes.Status413PayloadTooLarge);

        using var content = new MemoryStream();
        var buffer = new byte[1024];
        while (true)
        {
            var read = await request.Body.ReadAsync(buffer.AsMemory(), token);
            if (read == 0) break;
            if (content.Length + read > MaxRetryBodyBytes)
                throw new BadHttpRequestException("retry-body-too-large", StatusCodes.Status413PayloadTooLarge);
            await content.WriteAsync(buffer.AsMemory(0, read), token);
        }
        if (content.Length == 0) return null;
        if (!request.HasJsonContentType()) throw new JsonException("retry-body-must-be-json");
        return JsonSerializer.Deserialize<RetryBody>(content.ToArray(), new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    private static async Task<byte[]> ReadBoundedArtifact(string path, long expectedBytes, CancellationToken token)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var buffer = new MemoryStream((int)expectedBytes);
        var chunk = new byte[64 * 1024];
        long total = 0;
        while (true)
        {
            var read = await stream.ReadAsync(chunk.AsMemory(), token);
            if (read == 0) break;
            if (total + read > expectedBytes) throw new InvalidDataException("report-artifact-size-mismatch");
            await buffer.WriteAsync(chunk.AsMemory(0, read), token);
            total += read;
        }
        if (total != expectedBytes) throw new InvalidDataException("report-artifact-size-mismatch");
        return buffer.ToArray();
    }

    private static string? SafeReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) return reason;
        var firstLine = reason.Split('\n', '\r')[0];
        if (firstLine.Length > 160) firstLine = firstLine[..160];
        return firstLine.Contains(Path.DirectorySeparatorChar) || firstLine.Contains(Path.AltDirectorySeparatorChar)
            ? "report-operation-failed" : firstLine;
    }
}
