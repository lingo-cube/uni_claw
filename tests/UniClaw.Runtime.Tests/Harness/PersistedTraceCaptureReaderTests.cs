using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using UniClaw.Runtime.Harness;
using UniClaw.Runtime.Harness.Capture;
using Xunit;

namespace UniClaw.Runtime.Tests.Harness;

public sealed class PersistedTraceCaptureReaderTests
{
    [Fact]
    public async Task ReadsPublishedTraceAndReconstructsArtifactBytes()
    {
        var root = Directory.CreateTempSubdirectory("trace-reader-").FullName;
        try
        {
            var trace = new TraceRun { TraceRunId = "tr-1", Spans = [new TraceSpan { SpanId = "s-1", Name = "observe", Layer = "Runtime", Component = "Test" }] };
            var bytes = "hello"u8.ToArray().ToImmutableArray();
            var bundle = new TraceCaptureBundle
            {
                CaptureSessionId = "cap-1",
                FinalState = CaptureState.Persisted,
                ObservabilityTrace = trace,
                Artifacts = [new CaptureArtifact { ArtifactId = "a-1", Content = bytes, ByteCount = bytes.Length, ContentHash = TraceCaptureSession.ComputeHash(bytes.AsSpan()) }]
            };
            Assert.True((await new FileTraceCaptureStore(root).SaveAsync(bundle)).Success);

            var result = await new FileTraceCaptureReader(root).ReadAsync("cap-1", "tr-1");
            Assert.Equal(TraceCaptureReadStatus.Found, result.Status);
            Assert.Equal(bytes, result.Bundle!.Artifacts[0].Content);
            Assert.Equal("tr-1", result.Bundle.ObservabilityTrace!.TraceRunId);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task DistinguishesTraceAbsenceAndUnknownCapture()
    {
        var root = Directory.CreateTempSubdirectory("trace-reader-").FullName;
        try
        {
            var store = new FileTraceCaptureStore(root);
            Assert.True((await store.SaveAsync(new TraceCaptureBundle { CaptureSessionId = "no-trace", FinalState = CaptureState.Persisted })).Success);
            var reader = new FileTraceCaptureReader(root);
            Assert.Equal(TraceCaptureReadStatus.TraceAbsent, (await reader.ReadAsync("no-trace")).Status);
            Assert.Equal(TraceCaptureReadStatus.CaptureNotFound, (await reader.ReadAsync("missing")).Status);
            Assert.Equal(TraceCaptureReadStatus.TraceAbsent, (await reader.ReadAsync("no-trace", "required")).Status);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task RejectsUnsafeAndMismatchedIdentityWithoutBundle()
    {
        var root = Directory.CreateTempSubdirectory("trace-reader-").FullName;
        try
        {
            var store = new FileTraceCaptureStore(root);
            Assert.True((await store.SaveAsync(new TraceCaptureBundle { CaptureSessionId = "cap", FinalState = CaptureState.Persisted })).Success);
            var reader = new FileTraceCaptureReader(root);
            var unsafeResult = await reader.ReadAsync("../cap");
            Assert.Equal(TraceCaptureReadStatus.ValidationFailed, unsafeResult.Status);
            Assert.Null(unsafeResult.Bundle);
            Assert.Equal(TraceCaptureReadStatus.TraceAbsent, (await reader.ReadAsync("cap", "different")).Status);
        }
        finally { Directory.Delete(root, true); }
    }


    [Fact]
    public async Task PreservesPublishedFilesAndReadsNonEmptyRecord()
    {
        var root = await CreateCaptureAsync("stable", includeTrace: true, includeArtifact: true, includeRecord: true);
        try
        {
            var files = Directory.EnumerateFiles(Path.Combine(root, "stable"), "*", SearchOption.AllDirectories)
                .ToDictionary(x => x, x => (Hash: Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(x))), Time: File.GetLastWriteTimeUtc(x)));
            var result = await new FileTraceCaptureReader(root).ReadAsync("stable", "tr-1");
            Assert.Equal(TraceCaptureReadStatus.Found, result.Status);
            Assert.NotEmpty(result.Bundle!.Records);
            Assert.All(files, pair => Assert.Equal(pair.Value, (Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(pair.Key))), File.GetLastWriteTimeUtc(pair.Key))));
        }
        finally { Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData("", TraceCaptureReadIssueCode.UnsafeCaptureSessionId)]
    [InlineData(".", TraceCaptureReadIssueCode.UnsafeCaptureSessionId)]
    [InlineData("..", TraceCaptureReadIssueCode.UnsafeCaptureSessionId)]
    [InlineData("a/b", TraceCaptureReadIssueCode.UnsafeCaptureSessionId)]
    [InlineData(".staging-x", TraceCaptureReadIssueCode.UnsafeCaptureSessionId)]
    [InlineData(".STAGING-X", TraceCaptureReadIssueCode.UnsafeCaptureSessionId)]
    public async Task RejectsUnsafeCaptureIds(string id, TraceCaptureReadIssueCode code)
    {
        var root = Directory.CreateTempSubdirectory("trace-reader-").FullName;
        try { var result = await new FileTraceCaptureReader(root).ReadAsync(id); Assert.Equal(TraceCaptureReadStatus.ValidationFailed, result.Status); Assert.Null(result.Bundle); Assert.Contains(result.Issues, x => x.Code == code); }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task RejectsBlankRequiredTraceId()
    {
        var root = await CreateCaptureAsync("blank");
        try { var result = await new FileTraceCaptureReader(root).ReadAsync("blank", " "); Assert.Equal(TraceCaptureReadStatus.ValidationFailed, result.Status); Assert.Contains(result.Issues, x => x.Code == TraceCaptureReadIssueCode.TraceIdentityMismatch); }
        finally { Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData("capture-manifest.json")]
    [InlineData("records.json")]
    [InlineData("checksums.sha256")]
    public async Task MissingPublicationFileFailsClosed(string file)
    {
        var root = await CreateCaptureAsync("missing", includeArtifact: true);
        try { File.Delete(Path.Combine(root, "missing", file)); var result = await new FileTraceCaptureReader(root).ReadAsync("missing"); Assert.Equal(TraceCaptureReadStatus.ValidationFailed, result.Status); Assert.Null(result.Bundle); }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task MalformedManifestFailsClosed()
    {
        var root = await CreateCaptureAsync("malformed");
        try { File.WriteAllText(Path.Combine(root, "malformed", "capture-manifest.json"), "{"); var result = await new FileTraceCaptureReader(root).ReadAsync("malformed"); Assert.Equal(TraceCaptureReadStatus.ValidationFailed, result.Status); Assert.Contains(result.Issues, x => x.Code == TraceCaptureReadIssueCode.MalformedJson); }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task RejectsUnsupportedCaptureSchemaAndIdentityMismatch()
    {
        var root = await CreateCaptureAsync("schema");
        try
        {
            var path = Path.Combine(root, "schema", "capture-manifest.json"); var json = JsonNode.Parse(File.ReadAllText(path))!.AsObject(); json["schemaVersion"] = 2; File.WriteAllText(path, json.ToJsonString());
            var result = await new FileTraceCaptureReader(root).ReadAsync("schema"); Assert.Equal(TraceCaptureReadStatus.UnsupportedSchema, result.Status); Assert.Contains(result.Issues, x => x.Code == TraceCaptureReadIssueCode.UnsupportedCaptureSchema);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task RejectsNotPublishedAndRecordOrderMismatch()
    {
        var root = await CreateCaptureAsync("records", includeRecord: true);
        try
        {
            var path = Path.Combine(root, "records", "capture-manifest.json"); var json = JsonNode.Parse(File.ReadAllText(path))!.AsObject(); json["finalState"] = 1; File.WriteAllText(path, json.ToJsonString());
            var result = await new FileTraceCaptureReader(root).ReadAsync("records"); Assert.Equal(TraceCaptureReadStatus.ValidationFailed, result.Status); Assert.Contains(result.Issues, x => x.Code == TraceCaptureReadIssueCode.NotPublished);
            File.WriteAllText(Path.Combine(root, "records", "records.json"), "[]"); result = await new FileTraceCaptureReader(root).ReadAsync("records"); Assert.Contains(result.Issues, x => x.Code == TraceCaptureReadIssueCode.RecordOrderInvalid);
        }
        finally { Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData("artifact-missing", "artifacts/a-1.bin", TraceCaptureReadIssueCode.ArtifactMissing)]
    [InlineData("artifact-bytes", "artifacts/a-1.bin", TraceCaptureReadIssueCode.ArtifactHashMismatch)]
    [InlineData("checksum", "checksums.sha256", TraceCaptureReadIssueCode.ChecksumManifestInvalid)]
    public async Task RejectsArtifactAndChecksumTampering(string id, string relative, TraceCaptureReadIssueCode code)
    {
        var root = await CreateCaptureAsync(id, includeArtifact: true);
        try { var path = Path.Combine(root, id, relative); if (code == TraceCaptureReadIssueCode.ArtifactMissing) File.Delete(path); else File.WriteAllText(path, "tampered"); var result = await new FileTraceCaptureReader(root).ReadAsync(id); Assert.Equal(TraceCaptureReadStatus.ValidationFailed, result.Status); Assert.Contains(result.Issues, x => x.Code == code); }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task RejectsUnsupportedTraceAndTraceIdentityMismatch()
    {
        var root = await CreateCaptureAsync("trace", includeTrace: true);
        try
        {
            var path = Path.Combine(root, "trace", "observability-trace.json"); var json = JsonNode.Parse(File.ReadAllText(path))!.AsObject(); json["schemaVersion"] = 2; File.WriteAllText(path, json.ToJsonString()); var result = await new FileTraceCaptureReader(root).ReadAsync("trace"); Assert.Equal(TraceCaptureReadStatus.UnsupportedSchema, result.Status); Assert.Contains(result.Issues, x => x.Code == TraceCaptureReadIssueCode.UnsupportedTraceSchema);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task CancellationIsPropagatedBeforeAnyRead()
    {
        using var cts = new CancellationTokenSource(); cts.Cancel(); var root = Directory.CreateTempSubdirectory("trace-reader-").FullName;
        try { await Assert.ThrowsAsync<OperationCanceledException>(() => new FileTraceCaptureReader(root).ReadAsync("x", cancellationToken: cts.Token).AsTask()); }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task RequiredTraceIdentityAndTraceAbsentHaveContractualBundles()
    {
        var root = await CreateCaptureAsync("identity", includeTrace: true);
        try
        {
            var reader = new FileTraceCaptureReader(root);
            var found = await reader.ReadAsync("identity", "tr-1"); Assert.Equal(TraceCaptureReadStatus.Found, found.Status); Assert.NotNull(found.Bundle);
            var mismatch = await reader.ReadAsync("identity", "other"); Assert.Equal(TraceCaptureReadStatus.IdentityMismatch, mismatch.Status); Assert.Null(mismatch.Bundle);
            var noTraceRoot = await CreateCaptureAsync("absent"); try { var absent = await new FileTraceCaptureReader(noTraceRoot).ReadAsync("absent", "tr-1"); Assert.Equal(TraceCaptureReadStatus.TraceAbsent, absent.Status); Assert.NotNull(absent.Bundle); } finally { Directory.Delete(noTraceRoot, true); }
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task RejectsManifestCaptureIdentityMismatch()
    {
        var root = await CreateCaptureAsync("manifest-id"); try { MutateManifest(root, "manifest-id", m => m["captureSessionId"] = "other"); var r = await new FileTraceCaptureReader(root).ReadAsync("manifest-id"); AssertRejected(r, TraceCaptureReadIssueCode.CaptureSessionIdMismatch); } finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task RejectsCaptureDirectorySymbolicLink()
    {
        var root = Directory.CreateTempSubdirectory("trace-reader-root-").FullName;
        var outside = Directory.CreateTempSubdirectory("trace-reader-outside-").FullName;
        try
        {
            await CreateCaptureAtRootAsync(outside, "linked-capture");
            Directory.CreateSymbolicLink(Path.Combine(root, "linked-capture"), Path.Combine(outside, "linked-capture"));
            var result = await new FileTraceCaptureReader(root).ReadAsync("linked-capture");
            AssertRejected(result, TraceCaptureReadIssueCode.UnsafePath);
        }
        catch (PlatformNotSupportedException) { /* Symbolic links unavailable in this test environment. */ }
        catch (UnauthorizedAccessException) { /* Symbolic links unavailable in this test environment. */ }
        finally { SafeDeleteLink(Path.Combine(root, "linked-capture")); Directory.Delete(root, true); Directory.Delete(outside, true); }
    }

    [Fact]
    public async Task RejectsArtifactsDirectorySymbolicLink()
    {
        var root = await CreateCaptureAsync("linked-artifacts", includeArtifact: true);
        var outside = Directory.CreateTempSubdirectory("trace-reader-artifacts-").FullName;
        var artifacts = Path.Combine(root, "linked-artifacts", "artifacts");
        try
        {
            Directory.Move(artifacts, Path.Combine(outside, "artifacts"));
            Directory.CreateSymbolicLink(artifacts, Path.Combine(outside, "artifacts"));
            var result = await new FileTraceCaptureReader(root).ReadAsync("linked-artifacts");
            AssertRejected(result, TraceCaptureReadIssueCode.UnsafePath);
        }
        catch (PlatformNotSupportedException) { /* Symbolic links unavailable in this test environment. */ }
        catch (UnauthorizedAccessException) { /* Symbolic links unavailable in this test environment. */ }
        finally { SafeDeleteLink(artifacts); Directory.Delete(root, true); Directory.Delete(outside, true); }
    }

    [Fact]
    public async Task RejectsDeclaredArtifactFileSymbolicLink()
    {
        var root = await CreateCaptureAsync("linked-artifact", includeArtifact: true);
        var outside = Directory.CreateTempSubdirectory("trace-reader-file-").FullName;
        var artifact = Path.Combine(root, "linked-artifact", "artifacts", "a-1.bin");
        var external = Path.Combine(outside, "a-1.bin");
        try
        {
            File.Move(artifact, external);
            File.CreateSymbolicLink(artifact, external);
            var result = await new FileTraceCaptureReader(root).ReadAsync("linked-artifact");
            AssertRejected(result, TraceCaptureReadIssueCode.UnsafePath);
        }
        catch (PlatformNotSupportedException) { /* Symbolic links unavailable in this test environment. */ }
        catch (UnauthorizedAccessException) { /* Symbolic links unavailable in this test environment. */ }
        finally { SafeDeleteLink(artifact); Directory.Delete(root, true); Directory.Delete(outside, true); }
    }

    [Theory]
    [InlineData("unsafe", "../unsafe")]
    public async Task RejectsDuplicateOrUnsafeArtifactIds(string id, string artifactId)
    {
        var root = await CreateCaptureAsync(id, includeArtifact: true); try { MutateManifest(root, id, m => { var a = m["artifacts"]!.AsArray(); a[0]!["artifactId"] = artifactId; }); var r = await new FileTraceCaptureReader(root).ReadAsync(id); AssertRejected(r, TraceCaptureReadIssueCode.ArtifactIdInvalid); } finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task RejectsDuplicateArtifactIds()
    {
        var root = await CreateCaptureAsync("duplicate", includeArtifact: true); try
        {
            MutateManifest(root, "duplicate", m => { var a = m["artifacts"]!.AsArray(); a.Add(a[0]!.DeepClone()); });
            var r = await new FileTraceCaptureReader(root).ReadAsync("duplicate"); AssertRejected(r, TraceCaptureReadIssueCode.ArtifactIdInvalid);
        }
        finally { Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData("byte-count", "byteCount", 99, TraceCaptureReadIssueCode.ArtifactByteCountMismatch)]
    [InlineData("derived", "derivedFromArtifactId", "missing", TraceCaptureReadIssueCode.ArtifactIdInvalid)]
    [InlineData("derived-self", "derivedFromArtifactId", "a-1", TraceCaptureReadIssueCode.ArtifactIdInvalid)]
    public async Task RejectsArtifactMetadataTampering(string id, string field, object value, TraceCaptureReadIssueCode expected)
    {
        var root = await CreateCaptureAsync(id, includeArtifact: true); try { MutateManifest(root, id, m => m["artifacts"]!.AsArray()[0]![field] = JsonValue.Create(value)); var r = await new FileTraceCaptureReader(root).ReadAsync(id); AssertRejected(r, expected); } finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task RejectsUnknownArtifactFile()
    {
        var root = await CreateCaptureAsync("unknown-file"); try { Directory.CreateDirectory(Path.Combine(root, "unknown-file", "artifacts")); File.WriteAllBytes(Path.Combine(root, "unknown-file", "artifacts", "rogue.bin"), [1]); var r = await new FileTraceCaptureReader(root).ReadAsync("unknown-file"); AssertRejected(r, TraceCaptureReadIssueCode.ChecksumManifestInvalid); } finally { Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData("missing-entry", "")]
    [InlineData("duplicate-entry", "duplicate")]
    [InlineData("unknown-entry", "deadbeef  artifacts/rogue.bin\n")]
    [InlineData("traversal-entry", "deadbeef  ../rogue.bin\n")]
    [InlineData("wrong-hash", "deadbeef  artifacts/a-1.bin\n")]
    public async Task RejectsEachChecksumCoverageViolation(string id, string mode)
    {
        var root = await CreateCaptureAsync(id, includeArtifact: true); try { var p = Path.Combine(root, id, "checksums.sha256"); var valid = File.ReadAllText(p); File.WriteAllText(p, mode switch { "duplicate" => valid + valid, "" => "", _ => mode }); var r = await new FileTraceCaptureReader(root).ReadAsync(id); AssertRejected(r, TraceCaptureReadIssueCode.ChecksumManifestInvalid); } finally { Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData("trace-missing", false, true, TraceCaptureReadIssueCode.TraceFileUnexpected)]
    [InlineData("trace-unexpected", true, false, TraceCaptureReadIssueCode.TraceFileMissing)]
    [InlineData("trace-mismatch", true, true, TraceCaptureReadIssueCode.TraceIdentityMismatch)]
    public async Task RejectsTraceAttachmentPublicationMismatches(string id, bool manifestTrace, bool fileTrace, TraceCaptureReadIssueCode expected)
    {
        var root = await CreateCaptureAsync(id, includeTrace: true); try { var dir = Path.Combine(root, id); if (!fileTrace) File.Delete(Path.Combine(dir, "observability-trace.json")); if (!manifestTrace) MutateManifest(root, id, m => m["observabilityTrace"] = null); if (id == "trace-mismatch") { var p = Path.Combine(dir, "observability-trace.json"); var t = JsonNode.Parse(File.ReadAllText(p))!.AsObject(); t["traceRunId"] = "other"; File.WriteAllText(p, t.ToJsonString()); } var r = await new FileTraceCaptureReader(root).ReadAsync(id); AssertRejected(r, expected); } finally { Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData("blank-trace-id", "traceRunId", "", TraceCaptureReadIssueCode.TraceStructureInvalid)]
    [InlineData("duplicate-span", "duplicateSpan", "", TraceCaptureReadIssueCode.TraceStructureInvalid)]
    [InlineData("blank-parent", "parent", " ", TraceCaptureReadIssueCode.TraceStructureInvalid)]
    [InlineData("self-parent", "self", "", TraceCaptureReadIssueCode.TraceStructureInvalid)]
    [InlineData("cycle", "cycle", "", TraceCaptureReadIssueCode.TraceStructureInvalid)]
    [InlineData("negative", "negative", "", TraceCaptureReadIssueCode.TraceStructureInvalid)]
    [InlineData("overflow", "overflow", "", TraceCaptureReadIssueCode.TraceStructureInvalid)]
    [InlineData("outside", "outside", "", TraceCaptureReadIssueCode.TraceStructureInvalid)]
    [InlineData("outcome", "outcome", "BOGUS", TraceCaptureReadIssueCode.TraceStructureInvalid)]
    [InlineData("event-id", "eventId", "", TraceCaptureReadIssueCode.TraceStructureInvalid)]
    [InlineData("event-span", "eventSpan", "other", TraceCaptureReadIssueCode.TraceStructureInvalid)]
    [InlineData("event-time", "eventTime", "", TraceCaptureReadIssueCode.TraceStructureInvalid)]
    [InlineData("span-attr", "spanAttr", "", TraceCaptureReadIssueCode.TraceStructureInvalid)]
    [InlineData("event-attr", "eventAttr", "", TraceCaptureReadIssueCode.TraceStructureInvalid)]
    public async Task RejectsInvalidTraceStructure(string id, string kind, string value, TraceCaptureReadIssueCode expected)
    {
        var root = await CreateCaptureAsync(id, includeTrace: true); try { MutateTrace(root, id, kind, value); var r = await new FileTraceCaptureReader(root).ReadAsync(id); AssertRejected(r, expected); } finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task AllowsMissingExternalParent()
    {
        var root = await CreateCaptureAsync("external", includeTrace: true); try { MutateTrace(root, "external", "external", "missing"); var r = await new FileTraceCaptureReader(root).ReadAsync("external"); Assert.Equal(TraceCaptureReadStatus.Found, r.Status); Assert.NotNull(r.Bundle); } finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task RejectsManifestRecordContentMismatchAndNonContiguousOrder()
    {
        var root = await CreateCaptureAsync("record-shape", includeRecord: true); try { var p = Path.Combine(root, "record-shape", "records.json"); var n = JsonNode.Parse(File.ReadAllText(p))!.AsArray(); n[0]!["info"] = "different"; File.WriteAllText(p, n.ToJsonString()); var r = await new FileTraceCaptureReader(root).ReadAsync("record-shape"); AssertRejected(r, TraceCaptureReadIssueCode.RecordOrderInvalid); } finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task RejectsNonContiguousRecordOrderEvenWhenManifestAndRecordsMatch()
    {
        var root = await CreateCaptureAsync("record-order", includeRecord: true);
        try
        {
            var recordsPath = Path.Combine(root, "record-order", "records.json");
            var records = JsonNode.Parse(File.ReadAllText(recordsPath))!.AsArray();
            records[0]!["order"] = 2;
            File.WriteAllText(recordsPath, records.ToJsonString());
            MutateManifest(root, "record-order", manifest => manifest["records"] = records.DeepClone());
            var result = await new FileTraceCaptureReader(root).ReadAsync("record-order");
            AssertRejected(result, TraceCaptureReadIssueCode.RecordOrderInvalid);
        }
        finally { Directory.Delete(root, true); }
    }

    private static void AssertRejected(TraceCaptureReadResult result, TraceCaptureReadIssueCode code) { Assert.Equal(TraceCaptureReadStatus.ValidationFailed, result.Status); Assert.Null(result.Bundle); Assert.Contains(result.Issues, x => x.Code == code); }

    private static void MutateManifest(string root, string id, Action<JsonObject> mutate)
    { var p = Path.Combine(root, id, "capture-manifest.json"); var n = JsonNode.Parse(File.ReadAllText(p))!.AsObject(); mutate(n); File.WriteAllText(p, n.ToJsonString()); }

    private static void MutateTrace(string root, string id, string kind, string value)
    {
        var dir = Path.Combine(root, id); var tp = Path.Combine(dir, "observability-trace.json"); var t = JsonNode.Parse(File.ReadAllText(tp))!.AsObject(); var spans = t["spans"]!.AsArray(); var s = spans[0]!.AsObject();
        switch (kind)
        {
            case "traceRunId": t["traceRunId"] = value; break;
            case "duplicateSpan": spans.Add(spans[0]!.DeepClone()); break;
            case "parent": s["parentSpanId"] = value; break;
            case "self": s["parentSpanId"] = "s-1"; break;
            case "cycle": s["parentSpanId"] = "s-2"; spans.Add(new JsonObject { ["spanId"] = "s-2", ["parentSpanId"] = "s-1", ["name"] = "child", ["layer"] = "Runtime", ["component"] = "Test", ["outcome"] = "SUCCEEDED" }); break;
            case "external": s["parentSpanId"] = value; break;
            case "negative": s["startOffsetNs"] = -1; break;
            case "overflow": s["durationNs"] = long.MaxValue; s["startOffsetNs"] = 1; break;
            case "outside": spans.Add(new JsonObject { ["spanId"] = "s-2", ["parentSpanId"] = "s-1", ["name"] = "child", ["layer"] = "Runtime", ["component"] = "Test", ["startOffsetNs"] = 1, ["durationNs"] = 2, ["outcome"] = "SUCCEEDED" }); s["durationNs"] = 1; break;
            case "outcome": s["outcome"] = value; break;
            case "eventId": s["events"] = new JsonArray(new JsonObject { ["eventId"] = "", ["spanId"] = "s-1", ["timestampOffsetNs"] = 0 }); break;
            case "eventSpan": s["events"] = new JsonArray(new JsonObject { ["eventId"] = "e", ["spanId"] = value, ["timestampOffsetNs"] = 0 }); break;
            case "eventTime": s["events"] = new JsonArray(new JsonObject { ["eventId"] = "e", ["spanId"] = "s-1", ["timestampOffsetNs"] = 99 }); break;
            case "spanAttr": s["attributes"] = new JsonArray(new JsonObject { ["key"] = "" }); break;
            case "eventAttr": s["events"] = new JsonArray(new JsonObject { ["eventId"] = "e", ["spanId"] = "s-1", ["timestampOffsetNs"] = 0, ["attributes"] = new JsonArray(new JsonObject { ["key"] = "" }) }); break;
        }
        File.WriteAllText(tp, t.ToJsonString()); MutateManifest(root, id, m => m["observabilityTrace"] = t.DeepClone());
    }

    private static async Task<string> CreateCaptureAsync(string id, bool includeTrace = false, bool includeArtifact = false, bool includeRecord = false)
    {
        var root = Directory.CreateTempSubdirectory("trace-reader-").FullName;
        var trace = includeTrace ? new TraceRun { TraceRunId = "tr-1", Spans = [new TraceSpan { SpanId = "s-1", Name = "observe", Layer = "Runtime", Component = "Test", Outcome = "SUCCEEDED" }] } : null;
        var bytes = "hello"u8.ToArray().ToImmutableArray();
        var bundle = new TraceCaptureBundle
        {
            CaptureSessionId = id,
            FinalState = CaptureState.Persisted,
            ObservabilityTrace = trace,
            Records = includeRecord ? [new CaptureRecord { Order = 1, Kind = CaptureRecordKind.Observation, SequenceNumber = 1 }] : [],
            Artifacts = includeArtifact ? [new CaptureArtifact { ArtifactId = "a-1", Content = bytes, ByteCount = bytes.Length, ContentHash = TraceCaptureSession.ComputeHash(bytes.AsSpan()) }] : []
        };
        Assert.True((await new FileTraceCaptureStore(root).SaveAsync(bundle)).Success); return root;
    }

    private static async Task CreateCaptureAtRootAsync(string root, string id)
    {
        var bundle = new TraceCaptureBundle { CaptureSessionId = id, FinalState = CaptureState.Persisted };
        Assert.True((await new FileTraceCaptureStore(root).SaveAsync(bundle)).Success);
    }

    private static void SafeDeleteLink(string path)
    {
        try
        {
            if (File.Exists(path) && new FileInfo(path).LinkTarget is not null) File.Delete(path);
            else if (Directory.Exists(path) && new DirectoryInfo(path).LinkTarget is not null) Directory.Delete(path);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
