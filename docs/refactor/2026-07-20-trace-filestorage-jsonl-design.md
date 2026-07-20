# C-7: Trace FileStorage JSONL Export Bridge — Design Spec

> Date: 2026-07-20
> Priority: P3 (C-bucket backlog, toolchain interop)
> Branch: feature/refactor

## 1. Summary

Build a FileTraceStorage that implements ITraceStorage and writes C# trace records to JSONL files, enabling Python dashboards to consume C# trace data. Uses IFileProvider abstraction to keep Core classlib decoupled from direct System.IO dependency.

## 2. Current State

- Observability layer is entirely in-memory: InMemoryTraceStorage.Export() returns a single JSON string, no file output
- ITraceStorage has 14 synchronous methods (3 lifecycle + 5 write + 6 read)
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
      InMemoryTraceStorage.cs       ← existing, moved from root
      InMemoryTraceService.cs       ← existing, moved from root
    File/                           ← file implementations
      IFileProvider.cs              ← new: abstract interface (~6 methods)
      PhysicalFileProvider.cs       ← new: System.IO implementation
      FileTraceStorage.cs           ← new: JSONL write implementation
    ITraceStorage.cs                ← existing interface, stays at root
    ITraceService.cs                ← existing interface, stays at root
    ITraceRecorder.cs               ← existing interface, stays at root
    TraceContext.cs                 ← existing, stays at root
    TraceSession.cs                 ← existing, stays at root
    (5 record types)                ← existing, stays at root
```

Interfaces and record types at Observability root. Implementations organized by storage strategy (InMemory/ vs File/). Namespace stays `UniClaw.Core.Observability` for all — file organization only.

### Migration to Approach B (Independent Storage Project)

If later a Storage project becomes warranted (multiple backends: File + DB + S3), migration from A→B is trivial:
- Move 3 files (IFileProvider, PhysicalFileProvider, FileTraceStorage) to UniClaw.Storage project
- ITraceStorage interface stays in Core
- Engine code uses ITraceStorage regardless of which project provides the implementation

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
}
```

- 6 methods covering all I/O needs of FileTraceStorage
- No Delete/Copy/Move — YAGNI
- Sync methods — consistent with D-22 ITraceStorage sync-first

### 4.2 PhysicalFileProvider

```csharp
public sealed class PhysicalFileProvider : IFileProvider
{
    public void EnsureDirectory(string path)   => Directory.CreateDirectory(path);
    public void AppendLine(string path, string line) => File.AppendAllText(path, line + "\n");
    public string? ReadAllText(string path)    => File.Exists(path) ? File.ReadAllText(path) : null;
    public IReadOnlyList<string> ReadAllLines(string path) => File.ReadAllLines(path);
    public bool FileExists(string path)        => File.Exists(path);
    public bool DirectoryExists(string path)   => Directory.Exists(path);
}
```

### 4.3 JSONL Line Format

Each line is a standalone JSON object with `record_type` discriminator:

```jsonl
{"record_type":"execution","context":{"traceId":"...","nodeId":"...","stepNumber":1},"fsmType":"Traversal","spanId":"...","childNodeId":"...","parentNodeId":"...","pageId":"...","depth":0,"durationMs":42}
{"record_type":"state_transition","context":{"traceId":"...","nodeId":"...","stepNumber":1},"fromState":"Executing","toState":"Branching","reason":"..."}
{"record_type":"error","context":{"traceId":"...","nodeId":"...","stepNumber":1},"errorType":"Timeout","message":"..."}
{"record_type":"page_transition","context":{"traceId":"...","nodeId":"...","stepNumber":1},"pageId":"...","targetType":"App","targetValue":"Settings"}
{"record_type":"ai_call","context":{"traceId":"...","nodeId":"...","stepNumber":1},"provider":"...","tokensIn":0,"tokensOut":0,"durationMs":0}
```

- `record_type`: string discriminator — `"execution"`, `"state_transition"`, `"error"`, `"page_transition"`, `"ai_call"`
- `context`: TraceContext 4-field sub-object
- Remaining fields per each record type definition
- Python dashboards: look at `record_type` then dispatch

### 4.4 Directory Structure

```
{baseDir}/{traceId}/
  ├── session.json    ← TraceSession metadata (TraceId, StartTime, EndTime, Metadata)
  └── trace.jsonl     ← line-by-line appended trace records
```

- `{baseDir}`: constructor parameter, default `"traces"` (relative path)
- `{traceId}`: from `StartSession(TraceSession session)` → session.TraceId
- `session.json`: written at StartSession, **rewritten at EndSession** (TraceSession is immutable record — EndSession creates new instance with EndTime populated, overwrites the entire file)

### 4.5 FileTraceStorage Write Methods

```csharp
// StartSession: EnsureDirectory + write session.json
// WriteExecution/WriteStateTransition/...: AppendLine(trace.jsonl, jsonLine)
// Index methods (GetByNodeId, GetBySpanType): ReadAllLines → filter by type + build temp Dictionary
```

- All write operations are synchronous (D-22)
- No internal buffered queue (unlike Python) — ITraceStorage is sync, ITraceRecorder async layer handles async
- AppendLine via IFileProvider.AppendLine — PhysicalFileProvider uses File.AppendAllText
- Serialization via DomainJsonOptions.Default (same as InMemoryTraceStorage.Export)

## 5. Data Flow & Error Handling

### Write Flow (Engine Runtime)

```
TraversalEngine step
  → ITraceRecorder.WriteExecutionAsync(record)  (async layer, D-19)
    → ITraceStorage.WriteExecution(record)       (sync layer, D-22)
      → FileTraceStorage:
        → JsonSerializer.Serialize(record, DomainJsonOptions.Default)
        → IFileProvider.AppendLine(tracePath, jsonLine)
```

### Read Flow (Dashboards/Debugging)

```
Python dashboard or debug tool
  → Read {baseDir}/{traceId}/trace.jsonl
  → Parse each line as JSON
  → Dispatch by record_type
```

### Export Flow (ITraceService.ExportTrace)

```
ExportTrace()
  → ReadAllLines(trace.jsonl)
  → Return JSON string (bulk format, compatible with InMemoryTraceStorage.Export())
```

### Error Handling Matrix

| Scenario | Behavior | Reason |
|----------|----------|--------|
| Directory creation fails | Throw IOException | Filesystem unavailable = serious error, must not silently continue |
| AppendLine fails (disk full) | Throw IOException | Unlike InMemory (can't fail), I/O failure is real and must propagate |
| Read nonexistent file | Return empty collection/null | Trace file may not exist yet (StartSession not called) |
| Corrupted JSONL line | Skip line + log warning | Single corrupted line should not block entire trace read |
| Missing session.json | TraceSession returns null | Don't block trace data reading |
| Nonexistent traceId directory | Read methods return empty collection | Equivalent to "no data", don't throw |

**Key Decision: Throw on write failure vs Log-and-Continue**

Python FileStorage uses log-and-continue (queue full = drop + warning). C# ITraceStorage is synchronous — callers expect write to succeed or fail. Silent discard creates inconsistency between engine state and trace data, breaking ExpectedBehavior verification. **C# throws** — upper layer decides how to handle (retry, degrade to InMemory, terminate traversal).

## 6. Testing Strategy

### New Test File

`tests/UniClaw.Core.Tests/Observability/File/FileTraceStorageTests.cs`

### Test Matrix

| Test Group | Coverage | Est. Count |
|------------|----------|------------|
| StartSession/EndSession | Create directory + write session.json + update EndTime | 2-3 |
| Write methods | WriteExecution/WriteStateTransition/WriteError/WritePageTransition/WriteAICall → JSONL line append | 5 |
| Read methods | GetExecutions/GetTransitions/GetErrors/GetPageTransitions/GetAICalls → deserialize from JSONL | 5 |
| Index methods | GetByNodeId/GetBySpanType → temporary index build | 2 |
| JSONL format validation | Each line has record_type + correct payload | 2-3 |
| Empty trace | StartSession with no writes → read returns empty collection | 1 |
| Corrupted line tolerance | One corrupted line in JSONL → skip + rest normal | 1-2 |
| Nonexistent traceId | Read nonexistent directory → empty/null | 1 |
| ExportTrace compatibility | FileTraceStorage.ExportTrace() format ≈ InMemoryTraceStorage.ExportTrace() | 1 |
| PhysicalFileProvider | 6 methods basic validation with temp directory | 2-3 |

**Total: ~20-25 new tests**. All existing 721 tests unchanged.

### Test Method

- **MockFileProvider** (in-memory Dictionary<string, string> simulating path→content) injected for all FileTraceStorage tests — no real filesystem needed
- Only PhysicalFileProvider tests use temp directory (TempPath + cleanup)
- InMemory/ file relocation: InMemoryTraceStorage.cs and InMemoryTraceService.cs moved to Observability/InMemory/ — pure file move, no code changes, namespace unchanged

## 7. Out of Scope

- Python TraceNode hierarchy compatibility (A-7 removed it, C-7 outputs flat records)
- DB/S3 storage backends
- IAsyncTraceStorage (Phase 3 roadmap)
- TraceContext 4→6 field expansion (VisitSpanId + ParentSpanId)
- Dashboard/visualization in Core classlib (A-10)
