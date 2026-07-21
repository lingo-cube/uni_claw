## ADDED Requirements

### Requirement: FileTraceStorage implements ITraceStorage with JSONL file backend via IFileProvider
FileTraceStorage SHALL implement ITraceStorage with per-trace JSONL file storage. Each trace session gets its own directory: `{baseDir}/{traceId}/trace.jsonl` (JSONL records) + `{baseDir}/{traceId}/session.json` (session metadata). BaseDir defaults to `"traces"` but is configurable via constructor. FileTraceStorage consumes IFileProvider (interface, not System.IO directly), enabling MockFileProvider test injection. All write methods use `SerializeWithDiscriminator` to produce JSONL lines with `record_type` discriminator. All read methods deserialize from JSONL, filtering by `record_type`, with corrupted-line tolerance (skip malformed lines, continue with valid ones).

#### Scenario: SetSession creates directory and writes session.json
- **WHEN** FileTraceStorage.SetSession is called with TraceSession(TraceId="abc", StartTime=now)
- **THEN** IFileProvider.CreateDirectory is called for `{baseDir}/abc`; IFileProvider.AppendLine writes session.json

#### Scenario: Write methods produce correct JSONL lines with record_type discriminator
- **WHEN** AddExecution is called with ExecutionRecord(Action="click", SpanType=DfsForward)
- **THEN** trace.jsonl line contains `"record_type":"execution"` and `"action":"click"` in camelCase

#### Scenario: Read methods deserialize JSONL with record_type filter
- **WHEN** GetExecutions is called after AddExecution + AddTransition
- **THEN** only ExecutionRecord entries are returned (filtered by record_type=="execution")

#### Scenario: Corrupted JSONL lines are skipped, valid lines still returned
- **WHEN** trace.jsonl contains a valid execution line, a corrupted line, then another valid execution line
- **THEN** GetExecutions returns 2 records (corrupted line silently skipped)

#### Scenario: Nonexistent traceId returns empty collections
- **WHEN** GetExecutions is called without prior SetSession (no trace directory)
- **THEN** empty IReadOnlyList is returned (no exception)

### Requirement: IFileProvider provides 6-method filesystem abstraction
IFileProvider SHALL declare 6 sync methods: EnsureDirectory, AppendLine, ReadAllText, ReadAllLines, FileExists, DirectoryExists. PhysicalFileProvider SHALL delegate to System.IO static methods. No async methods — sync-only, consistent with ITraceStorage sync-first design. Core classlib stays filesystem-neutral for unit testability.

#### Scenario: PhysicalFileProvider delegates to System.IO
- **WHEN** PhysicalFileProvider.WriteAllText is called (not in interface, but can be added)
- **THEN** System.IO.File.WriteAllText is called with the same arguments

#### Scenario: MockFileProvider enables in-memory testing
- **WHEN** MockFileProvider is injected into FileTraceStorage
- **THEN** all operations happen in memory; no real filesystem access required

### Requirement: Export wraps all JSONL lines in JSON array
FileTraceStorage.Export SHALL read all JSONL lines and wrap them in a JSON array string. Format compatible with InMemoryTraceStorage.Export() output structure.

#### Scenario: Export returns JSON array with all records
- **WHEN** Export is called after SetSession + AddExecution + AddTransition
- **THEN** result is a JSON array containing all trace records with record_type discriminator

### Requirement: IOException propagation on write failure
FileTraceStorage write methods SHALL NOT catch IOExceptions from IFileProvider.AppendLine. IO failures propagate to caller. InMemoryTraceStorage (which never throws on write) is the in-memory baseline; FileTraceStorage can fail on IO.

#### Scenario: IOException from IFileProvider.AppendLine propagates to caller
- **WHEN** IFileProvider.AppendLine throws IOException (disk full, permission denied)
- **THEN** IOException propagates from FileTraceStorage.AddExecution to caller
