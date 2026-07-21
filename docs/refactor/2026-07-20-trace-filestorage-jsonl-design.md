# C-7: Trace FileStorage JSONL Export Bridge — Design Spec

> Date: 2026-07-20
> Priority: P3 (C-bucket backlog, toolchain interop)
> Branch: feature/refactor

## 1. Summary

Build a FileTraceStorage that implements ITraceStorage and writes C# trace records to JSONL files, enabling Python dashboards to consume C# trace data. Uses IFileProvider abstraction to keep Core classlib decoupled from direct System.IO dependency.

## 2. Current State

- Observability layer is entirely in-memory: InMemoryTraceStorage.Export() returns a single JSON string, no file output
- ITraceStorage has 14 members (13 methods + 1 property: 3 lifecycle + 5 write + 6 read)
- D-22: ITraceStorage is sync-first, async layer is ITraceRecorder
- D-19: ExportTrace belongs on ITraceService/ITraceStorage, not ITraceRecorder
- Python has full FileStorage with background writer thread + `traces/{trace_id}/trace.jsonl + session.json`
- C# trace model is flat 5 record types + TraceContext (A-7: Python SessionNode/StepNode/SpanNode hierarchy removed)

## 3. Architecture & Layering

### Directory Layout (After Change)

```
UniClaw.Core/
  Observability/
    InMemory/                       ← memory implementations
      InMemoryTraceRecorder.cs     ← existing, moved from root
      InMemoryTraceService.cs      ← existing, moved from root
      InMemoryTraceStorage.cs      ← existing, moved from root
    File/                           ← file implementations
      IFileProvider.cs              ← existing: 7-method abstraction (6 original + WriteAllText D-102)
      PhysicalFileProvider.cs       ← existing: System.IO implementation
      FileTraceStorage.cs           ← existing: JSONL write implementation
    ITraceStorage.cs                ← existing interface, stays at root
    ITraceService.cs                ← existing interface, stays at root
    ITraceRecorder.cs               ← existing interface, stays at root
    TraceContext.cs                 ← existing, stays at root (TraceContext, TraceSession, 5 record types, SpanType enum, ErrorSeverity enum)
    TraceQueryResults.cs            ← existing, stays at root (6 query result types: TraversalTree, NodeSpans, etc.)
```

Interfaces and record types at Observability root. Implementations organized by storage strategy (InMemory/ vs File/). Namespace stays `UniClaw.Core.Observability` for all — file organization only.

### Migration to Approach B (Independent Storage Project)

If later a Storage project becomes warranted (multiple backends: File + DB + S3), migration from A→B is trivial:
- Move 3 files (IFileProvider, PhysicalFileProvider, FileTraceStorage) to UniClaw.Storage project
- ITraceStorage interface stays in Core
- Engine code uses ITraceStorage regardless of which project provides the implementation
- MockFileProvider stays in test project

## 4. Component Details

### 4.1 IFileProvider

```csharp
public interface IFileProvider
{
    void EnsureDirectory(string path);          // mkdir -p
    void AppendLine(string path, string line);  // JSONL line append
    string? ReadAllText(string path);           // read session.json
    IReadOnlyList<string> ReadAllLines(string path); // read trace.jsonl
    bool FileExists(string path);
    bool DirectoryExists(string path);
    void WriteAllText(string path, string content); // overwrite file (D-102: for EndSession session.json rewrite)
}
```

- 7 methods covering all I/O needs of FileTraceStorage (6 original + WriteAllText added for session.json overwrite, D-102)
- No Delete/Copy/Move — YAGNI
- Sync methods — consistent with D-22 ITraceStorage sync-first

### 4.2 PhysicalFileProvider

```csharp
public sealed class PhysicalFileProvider : IFileProvider
{
    public void EnsureDirectory(string path)   => Directory.CreateDirectory(path);
    public void AppendLine(string path, string line) => File.AppendAllText(path, line + "\n");
    public string? ReadAllText(string path)    => File.Exists(path) ? File.ReadAllText(path) : null;
    public IReadOnlyList<string> ReadAllLines(string path) => File.Exists(path) ? File.ReadAllLines(path) : Array.Empty<string>();
    public bool FileExists(string path)        => File.Exists(path);
    public bool DirectoryExists(string path)   => Directory.Exists(path);
    public void WriteAllText(string path, string content) => File.WriteAllText(path, content);  // D-102
}
```

### 4.3 JSONL Line Format

Each line is a standalone JSON object with `record_type` discriminator, followed by the full C# record payload serialized via DomainJsonOptions.Default (camelCase + enum-as-string + null skip):

```jsonl
{"record_type":"execution","action":"click","status":"success","spanType":"dfsForward","context":{"nodeId":"wifi_node","stepSpanId":"step-1","stepNumber":1,"traceId":"trace-001"},"spanId":"s-1","childNodeId":"btn-1","parentNodeId":"root","pageId":"home","targetType":"App","targetValue":"Settings","depth":0,"durationMs":42,"timestamp":"2026-07-20T10:30:00+08:00","metadata":{"retry":false}}
{"record_type":"state_transition","fromState":"Idle","toState":"Traversing","context":{"nodeId":"wifi_node","stepSpanId":"step-1","stepNumber":1,"traceId":"trace-001"},"fsmType":"Traversal","timestamp":"2026-07-20T10:30:01+08:00","reason":"node_enter","metadata":{}}
{"record_type":"error","errorType":"Timeout","errorMessage":"Step exceeded limit","severity":"Error","context":{"nodeId":"wifi_node","stepSpanId":"step-1","stepNumber":1,"traceId":"trace-001"},"timestamp":"2026-07-20T10:30:02+08:00","metadata":{}}
{"record_type":"page_transition","fromPage":"home","toPage":"settings","transitionType":"forward","context":{"nodeId":"wifi_node","stepSpanId":"step-1","stepNumber":1,"traceId":"trace-001"},"durationMs":42,"timestamp":"2026-07-20T10:30:03+08:00","metadata":{}}
{"record_type":"ai_call","capability":"vision","providerId":"mock","success":true,"latencyMs":10.5,"context":{"nodeId":"wifi_node","stepSpanId":"step-1","stepNumber":1,"traceId":"trace-001"},"tokens":0,"timestamp":"2026-07-20T10:30:04+08:00"}
```

Field mapping to C# record types (DomainJsonOptions.Default: camelCase keys, enum-as-string, null-skip):

| JSONL record_type | C# record class | Required fields (camelCase) | Optional fields (camelCase, omitted if null) |
|---|---|---|---|
| `execution` | ExecutionRecord | `action`, `status` | `spanType`, `context{nodeId,stepSpanId,stepNumber,traceId}`, `spanId`, `childNodeId`, `parentNodeId`, `pageId`, `targetType`, `targetValue`, `depth`, `durationMs` (default 0), `timestamp`, `metadata` |
| `state_transition` | StateTransition | `fromState`, `toState` | `context{...}`, `fsmType`, `timestamp`, `reason`, `metadata` |
| `error` | ErrorRecord | `errorType`, `errorMessage`, `severity` | `context{...}`, `timestamp`, `metadata` |
| `page_transition` | PageTransition | `fromPage`, `toPage`, `transitionType` | `context{...}`, `durationMs`, `timestamp`, `metadata` |
| `ai_call` | AICallRecord | `capability`, `providerId`, `success`, `latencyMs` | `context{...}`, `tokens`, `timestamp` |

- `record_type`: snake_case string discriminator — not a C# record field; injected by SerializeWithDiscriminator and stripped by RemoveDiscriminator before deserialization (D-99)
- `context`: TraceContext 4-field sub-object (`nodeId`, `stepSpanId`, `stepNumber`, `traceId`), all optional (D-18)
- `severity`: ErrorSeverity enum — 5 values serialized as camelCase strings (D-E8: locked)
- `spanType`: SpanType enum — 11 values serialized as camelCase strings (D-E8: locked)
- Null-skip: optional fields with null value are omitted from JSON output (DomainJsonOptions.WhenWritingNull)
- Python dashboards: look at `record_type` then dispatch by C# record field names

### 4.4 Directory Structure

```
{baseDir}/{traceId}/
  ├── session.json    ← TraceSession metadata (TraceId, StartTime, EndTime, Metadata)
  └── trace.jsonl     ← line-by-line appended trace records
```

- `{baseDir}`: constructor parameter, default `"traces"` (relative path)
- `{traceId}`: from `SetSession(TraceSession session)` → session.TraceId
- `session.json`: written at SetSession (via AppendLine), **overwritten at EndSession** (via WriteAllText, D-102) — TraceSession is immutable record; EndSession creates new instance with EndTime populated, overwrites the entire file

### 4.5 FileTraceStorage Write Methods

```csharp
// SetSession: EnsureDirectory + AppendLine(session.json)
// EndSession: WriteAllText(session.json, endedSession)  // D-102: overwrite with EndTime populated
// AddExecution/AddTransition/AddError/AddPageTransition/AddAICall: SerializeWithDiscriminator + AppendLine(trace.jsonl, jsonLine)
// Index methods (GetByNodeId, GetBySpanType): GetExecutions() → LINQ .Where() filter (query-time, no pre-built Dictionary)
```

- All write operations are synchronous (D-22)
- No internal buffered queue (unlike Python) — ITraceStorage is sync, ITraceRecorder async layer handles async
- AppendLine via IFileProvider.AppendLine — PhysicalFileProvider uses File.AppendAllText
- Serialization via DomainJsonOptions.Default (same as InMemoryTraceStorage.Export)

## 5. Data Flow & Error Handling

### Write Flow (Engine Runtime)

```
TraversalEngine step
  → ITraceRecorder.RecordExecutionAsync(record)  (async layer, D-19)
    → ITraceStorage.AddExecution(record)          (sync layer, D-22)
      → FileTraceStorage:
        → JsonSerializer.Serialize(record, DomainJsonOptions.Default)
        → SerializeWithDiscriminator injects record_type as first field
        → IFileProvider.AppendLine(tracePath, jsonLine)
```

### Read Flow (Dashboards/Debugging)

```
Python dashboard or debug tool
  → Read {baseDir}/{traceId}/trace.jsonl
  → Parse each line as JSON
  → Dispatch by record_type
```

### Export Flow (ITraceStorage.Export)

```
Export()
  → ReadAllLines(trace.jsonl)
  → Return JSON array string "[line1,line2,...]"
```

**Note: Export format differs from InMemoryTraceStorage.Export()**
- InMemoryTraceStorage.Export() returns a structured JSON object: `{"Session":...,"Executions":[...],"Transitions":[...],...}`
- FileTraceStorage.Export() returns a flat JSON array of JSONL lines: `[{"record_type":"execution",...},{"record_type":"state_transition",...},...]`
- These formats are NOT interchangeable. Consumers must handle each format according to the storage backend used.
- ITraceService.ExportTrace() (on the service layer) could normalize format in the future — currently each ITraceStorage.Export() returns its own format.

### Error Handling Matrix

| Scenario | Behavior | Reason |
|----------|----------|--------|
| Directory creation fails | Throw IOException | Filesystem unavailable = serious error, must not silently continue |
| AppendLine fails (disk full) | Throw IOException | Unlike InMemory (can't fail), I/O failure is real and must propagate (D-101) |
| WriteAllText fails (EndSession overwrite) | Throw IOException | Session metadata must be updated; failure means session.json is stale (D-102) |
| Read nonexistent file | Return empty collection/null | Trace file may not exist yet (StartSession not called) |
| Corrupted JSONL line | Skip line (no log) | Single corrupted line should not block entire trace read; no Observability logging infrastructure available in Core classlib |
| Missing session.json | TraceSession returns null | Don't block trace data reading |
| Nonexistent traceId directory | Read methods return empty collection | Equivalent to "no data", don't throw |

**Key Decision: Throw on write failure vs Log-and-Continue**

Python FileStorage uses log-and-continue (queue full = drop + warning). C# ITraceStorage is synchronous — callers expect write to succeed or fail. Silent discard creates inconsistency between engine state and trace data, breaking ExpectedBehavior verification. **C# throws** — upper layer decides how to handle (retry, degrade to InMemory, terminate traversal).

## 6. Testing Strategy

### Test Files

- `tests/UniClaw.Core.Tests/Observability/File/FileTraceStorageTests.cs` — existing, 21 tests via MockFileProvider
- `tests/UniClaw.Core.Tests/Observability/File/PhysicalFileProviderTests.cs` — existing, 4 tests with temp directory
- `tests/UniClaw.Core.Tests/Observability/File/MockFileProvider.cs` — existing, in-memory IFileProvider for test injection

### Test Matrix

| Test Group | Coverage | Actual Count |
|------------|----------|------------|
| SetSession/EndSession | Create directory + write session.json + overwrite with EndTime (D-102) | 3 |
| Write methods | AddExecution/AddTransition/AddError/AddPageTransition/AddAICall → JSONL line append | 5 |
| Read methods | GetExecutions/GetTransitions/GetErrors/GetPageTransitions/GetAICalls → deserialize from JSONL | 5 |
| Index methods | GetByNodeId/GetBySpanType → LINQ .Where() filter | 2 |
| JSONL format validation | Each line has record_type + camelCase property names + enum-as-string | 2 |
| Empty trace | No SetSession → read returns empty collection | 1 |
| Corrupted line tolerance | One corrupted line in JSONL → skip + rest normal | 1 |
| Missing session.json | CurrentSession returns null | 1 |
| Export format | Export() returns JSON array `[line1,line2,...]` — NOT compatible with InMemoryTraceStorage.Export() structured format | 1 |

**FileTraceStorage total: 21 tests. PhysicalFileProvider total: 4 tests. All existing 814 tests unchanged.**

### Test Method

- **MockFileProvider** (in-memory Dictionary<string, string> simulating path→content) injected for all FileTraceStorage tests — no real filesystem needed. Implements WriteAllText as dictionary key overwrite.
- Only PhysicalFileProvider tests use temp directory (TempPath + cleanup)
- InMemory/ file relocation: InMemoryTraceRecorder.cs, InMemoryTraceService.cs, and InMemoryTraceStorage.cs moved to Observability/InMemory/ — pure file move, no code changes, namespace unchanged
- EndSession overwrite test verifies session.json contains `endTime` after EndSession (D-102)

## 7. Out of Scope

- Python TraceNode hierarchy compatibility (A-7 removed it, C-7 outputs flat records)
- DB/S3 storage backends
- IAsyncTraceStorage (Phase 3 roadmap)
- TraceContext 4→6 field expansion (VisitSpanId + ParentSpanId)
- Dashboard/visualization in Core classlib (A-10)
