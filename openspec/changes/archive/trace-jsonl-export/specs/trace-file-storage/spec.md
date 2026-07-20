## ADDED Requirements

### Requirement: IFileProvider defines 6-method abstraction decoupling Core from System.IO

IFileProvider SHALL define exactly 6 synchronous methods: `EnsureDirectory(string path)` (void, mkdir -p), `AppendLine(string path, string line)` (void, append a line to a text file), `ReadAllText(string path)` (string?, read entire file content, null if not exists), `ReadAllLines(string path)` (IReadOnlyList<string>, read all lines, empty if not exists), `FileExists(string path)` (bool), `DirectoryExists(string path)` (bool). No Delete/Copy/Move methods — YAGNI. All methods are synchronous, consistent with D-22 ITraceStorage sync-first design.

#### Scenario: IFileProvider has exactly 6 members
- **WHEN** IFileProvider is inspected for method declarations
- **THEN** it declares exactly: EnsureDirectory (void), AppendLine (void), ReadAllText (string?), ReadAllLines (IReadOnlyList<string>), FileExists (bool), DirectoryExists (bool)

#### Scenario: No delete/copy/move methods on IFileProvider
- **WHEN** IFileProvider is inspected for Delete, Copy, or Move methods
- **THEN** no such methods are declared — YAGNI for trace storage use case

### Requirement: PhysicalFileProvider implements IFileProvider with System.IO static methods

PhysicalFileProvider SHALL implement IFileProvider by delegating to System.IO static methods: `Directory.CreateDirectory`, `File.AppendAllText` (with newline suffix), `File.Exists` ? `File.ReadAllText` : null, `File.ReadAllLines`, `File.Exists`, `Directory.Exists`. PhysicalFileProvider SHALL be a sealed class with no constructor parameters.

#### Scenario: PhysicalFileProvider delegates EnsureDirectory to Directory.CreateDirectory
- **WHEN** `PhysicalFileProvider.EnsureDirectory("traces/abc")` is called
- **THEN** `Directory.CreateDirectory("traces/abc")` is invoked

#### Scenario: PhysicalFileProvider.AppendLine appends line with newline
- **WHEN** `PhysicalFileProvider.AppendLine("traces/abc/trace.jsonl", jsonLine)` is called
- **THEN** `File.AppendAllText(path, line + "\n")` is invoked

#### Scenario: PhysicalFileProvider.ReadAllText returns null for nonexistent file
- **WHEN** `PhysicalFileProvider.ReadAllText("nonexistent.json")` is called
- **THEN** `File.Exists(path)` returns false, and method returns null

### Requirement: FileTraceStorage implements ITraceStorage with JSONL write and session.json metadata

FileTraceStorage SHALL implement ITraceStorage, accepting `IFileProvider` and `string baseDir` (default "traces") as constructor parameters. Write methods (AddExecution, AddTransition, AddError, AddPageTransition, AddAICall) SHALL serialize each record as a JSON line with `record_type` discriminator and append to `{baseDir}/{traceId}/trace.jsonl`. Read methods SHALL deserialize from the same JSONL file. SetSession SHALL write `{baseDir}/{traceId}/session.json`. EndSession SHALL overwrite `session.json` with updated TraceSession (EndTime populated).

#### Scenario: FileTraceStorage constructor accepts IFileProvider and baseDir
- **WHEN** FileTraceStorage is constructed with `new FileTraceStorage(fileProvider, "traces")`
- **THEN** `_fileProvider` and `_baseDir` are stored; default baseDir = "traces"

#### Scenario: SetSession creates directory and writes session.json
- **WHEN** `SetSession(TraceSession(TraceId="abc", StartTime=now))` is called
- **THEN** `_fileProvider.EnsureDirectory("traces/abc")` is called
- **AND** `_fileProvider.AppendLine("traces/abc/session.json", serializedSession)` is called (or `WriteAllText` equivalent)

#### Scenario: AddExecution appends JSONL line with record_type="execution"
- **WHEN** `AddExecution(ExecutionRecord(...))` is called
- **THEN** the record is serialized as JSON with `record_type: "execution"` as the first field
- **AND** `_fileProvider.AppendLine("traces/{traceId}/trace.jsonl", jsonLine)` is called

#### Scenario: AddTransition appends JSONL line with record_type="state_transition"
- **WHEN** `AddTransition(StateTransitionRecord(...))` is called
- **THEN** the record is serialized with `record_type: "state_transition"`
- **AND** `_fileProvider.AppendLine` is called

#### Scenario: AddError appends JSONL line with record_type="error"
- **WHEN** `AddError(ErrorRecord(...))` is called
- **THEN** the record is serialized with `record_type: "error"`
- **AND** `_fileProvider.AppendLine` is called

#### Scenario: AddPageTransition appends JSONL line with record_type="page_transition"
- **WHEN** `AddPageTransition(PageTransitionRecord(...))` is called
- **THEN** the record is serialized with `record_type: "page_transition"`
- **AND** `_fileProvider.AppendLine` is called

#### Scenario: AddAICall appends JSONL line with record_type="ai_call"
- **WHEN** `AddAICall(AICallRecord(...))` is called
- **THEN** the record is serialized with `record_type: "ai_call"`
- **AND** `_fileProvider.AppendLine` is called

### Requirement: FileTraceStorage read methods deserialize from JSONL with corrupted line tolerance

Read methods (GetExecutions, GetTransitions, GetErrors, GetPageTransitions, GetAICalls) SHALL read `_fileProvider.ReadAllLines(tracePath)` and deserialize each line. Lines with unrecognized `record_type` or invalid JSON SHALL be skipped (not thrown). Each read method SHALL filter by its corresponding `record_type` discriminator. Nonexistent trace directory SHALL return empty collection (no exception).

#### Scenario: GetExecutions filters by record_type="execution"
- **WHEN** `GetExecutions()` is called for a trace with mixed record types
- **THEN** only lines with `record_type: "execution"` are deserialized to ExecutionRecord
- **AND** other record types are skipped

#### Scenario: Corrupted JSONL line is skipped during read
- **WHEN** `GetExecutions()` reads a JSONL file containing one corrupted line (invalid JSON)
- **THEN** the corrupted line is skipped; valid lines are deserialized normally
- **AND** no exception is thrown

#### Scenario: Nonexistent traceId returns empty collection
- **WHEN** `GetExecutions()` is called for a traceId whose directory does not exist
- **THEN** an empty IReadOnlyList<ExecutionRecord> is returned (no exception)

#### Scenario: Missing session.json returns null CurrentSession
- **WHEN** `CurrentSession` is accessed and `session.json` does not exist
- **THEN** null is returned (no exception)

### Requirement: FileTraceStorage index methods build temporary Dictionary from JSONL

GetByNodeId and GetBySpanType SHALL be FileTraceStorage-specific (NOT on ITraceStorage interface, per ISP). They SHALL build temporary Dictionary indexes from ReadAllLines → deserialize → filter → group by Context.NodeId or SpanType. Indexes are not persisted — computed at query time, consistent with InMemoryTraceStorage incremental approach but without persistent storage.

#### Scenario: GetByNodeId builds temporary index from deserialized ExecutionRecords
- **WHEN** `GetByNodeId("wifi_node")` is called
- **THEN** all execution records are deserialized from JSONL
- **AND** records with `Context.NodeId == "wifi_node"` are grouped and returned
- **AND** the temporary Dictionary is discarded after the call

#### Scenario: GetBySpanType builds temporary index from deserialized ExecutionRecords
- **WHEN** `GetBySpanType(SpanType.DfsForward)` is called
- **THEN** execution records with matching SpanType are grouped and returned

### Requirement: FileTraceStorage throws IOException on write failure, not log-and-continue

Write methods SHALL throw IOException when `_fileProvider.AppendLine` or `_fileProvider.EnsureDirectory` fails (e.g., disk full, permission denied). Unlike Python FileStorage (log-and-continue on queue full), C# ITraceStorage is synchronous — callers expect write to succeed or fail. Silent discard creates inconsistency between engine state and trace data.

#### Scenario: EnsureDirectory failure throws IOException
- **WHEN** `_fileProvider.EnsureDirectory` throws (e.g., permission denied)
- **THEN** SetSession propagates IOException to the caller

#### Scenario: AppendLine failure throws IOException
- **WHEN** `_fileProvider.AppendLine` throws (e.g., disk full)
- **THEN** AddExecution propagates IOException to the caller

### Requirement: FileTraceStorage serialization uses DomainJsonOptions.Default

All JSON serialization in FileTraceStorage SHALL use `DomainJsonOptions.Default` (camelCase + enum-as-string + null skip), consistent with InMemoryTraceStorage.Export() format. This ensures JSONL line format compatibility between InMemory export and File export.

#### Scenario: JSONL line uses camelCase property names
- **WHEN** an ExecutionRecord is serialized to JSONL
- **THEN** property names use camelCase (e.g., `record_type`, `spanId`, `childNodeId`)

#### Scenario: Enum values serialized as strings
- **WHEN** SpanType enum is serialized in JSONL
- **THEN** the value is a string (e.g., `"dfs_forward"`), not an integer
