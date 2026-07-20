## MODIFIED Requirements

### Requirement: ITraceStorage defines Export method for full trace export

ITraceStorage SHALL define an `Export()` method returning a `string` containing the complete trace data serialized as JSON. InMemoryTraceStorage SHALL return its existing in-memory JSON format. FileTraceStorage SHALL read all lines from `trace.jsonl` and return the aggregated JSON (compatible format with InMemoryTraceStorage.Export). ExportTrace on ITraceService SHALL delegate to `_storage.Export()`.

#### Scenario: Export method is on ITraceStorage interface
- **WHEN** ITraceStorage is inspected for method declarations
- **THEN** `Export()` returning `string` is declared on the interface

#### Scenario: FileTraceStorage.Export returns JSONL content as aggregated JSON string
- **WHEN** `FileTraceStorage.Export()` is called
- **THEN** all lines from `trace.jsonl` are read and returned as a single JSON string (array format wrapping all records)

#### Scenario: ExportTrace on ITraceService delegates to storage.Export
- **WHEN** `InMemoryTraceService.ExportTrace()` is called
- **THEN** it delegates to `_storage.Export()` and returns the same string
