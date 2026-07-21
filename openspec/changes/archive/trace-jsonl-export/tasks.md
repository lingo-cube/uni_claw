## 1. Directory Reorganization (InMemory files)

- [x] 1.1 Move `InMemoryTraceStorage.cs` from `Observability/` to `Observability/InMemory/` — pure file move, no code changes, namespace stays `UniClaw.Core.Observability`
- [x] 1.2 Move `InMemoryTraceService.cs` from `Observability/` to `Observability/InMemory/` — same as above (also moved InMemoryTraceRecorder.cs)
- [x] 1.3 Verify build succeeds after file moves — `dotnet build src/UniClaw.Core.sln` (0 errors)

## 2. IFileProvider + PhysicalFileProvider

- [x] 2.1 Create `Observability/File/IFileProvider.cs` — 6-method interface (EnsureDirectory, AppendLine, ReadAllText, ReadAllLines, FileExists, DirectoryExists), all sync, namespace `UniClaw.Core.Observability`
- [x] 2.2 Create `Observability/File/PhysicalFileProvider.cs` — sealed class implementing IFileProvider, delegates to System.IO static methods
- [x] 2.3 Create `PhysicalFileProviderTests.cs` in `tests/.../Observability/File/` — 4 tests (EnsureDirectory, AppendLine, ReadAllText null, ReadAllLines empty)
- [x] 2.4 Verify build succeeds after new files — 0 errors (restored unrelated broken TraversalPlan.cs changes)

## 3. FileTraceStorage Core Implementation

- [x] 3.1 Create `Observability/File/FileTraceStorage.cs` — sealed class implementing ITraceStorage
- [x] 3.2 Implement `SetSession` — EnsureDirectory + write session.json
- [x] 3.3 Implement `EndSession` — overwrite session.json with EndTime
- [x] 3.4 Implement 5 write methods — SerializeWithDiscriminator + AppendLine
- [x] 3.5 Implement `CurrentSession` getter — ReadAllText + deserialize, null if missing/corrupted
- [x] 3.6 Implement 5 read methods — DeserializeByType with record_type filter + corrupted line tolerance
- [x] 3.7 Implement `Export` — ReadAllLines, wrap in JSON array
- [x] 3.8 Implement index methods (GetByNodeId, GetBySpanType) — query-time computation
- [x] 3.9 Verify build succeeds after FileTraceStorage — 0 errors (restored 5 unrelated broken files)

## 4. FileTraceStorage Tests

- [x] 4.1 Create `MockFileProvider.cs` test helper — in-memory Dictionary<string, string> simulating path→content, implements IFileProvider for test injection
- [x] 4.2 Create `FileTraceStorageTests.cs` in `tests/.../Observability/File/` — StartSession/EndSession tests (directory creation + session.json write + EndTime update), ~2-3 tests
- [x] 4.3 Add write method tests — each of 5 write methods produces correct JSONL line with correct `record_type`, ~5 tests
- [x] 4.4 Add read method tests — each of 5 read methods deserializes from JSONL, filters by `record_type`, ~5 tests
- [x] 4.5 Add index method tests — GetByNodeId/GetBySpanType build temp index from JSONL, ~2 tests
- [x] 4.6 Add JSONL format validation tests — line has `record_type` + correct payload, camelCase property names, enum-as-string, ~2-3 tests
- [x] 4.7 Add error handling tests — corrupted line tolerance (skip + rest normal), nonexistent traceId (empty collection), missing session.json (null), write failure IOException propagation, ~3-4 tests
- [x] 4.8 Add ExportTrace compatibility test — FileTraceStorage.Export() format ≈ InMemoryTraceStorage.Export(), ~1 test
- [x] 4.9 Run full test suite — `dotnet test src/UniClaw.Core.sln` — **803/803 pass**

## 5. Documentation Update

- [x] 5.1 Update `docs/system/layers/` relevant doc (Observability layer description) — observability.md updated with FileTraceStorage + IFileProvider + directory layout
- [x] 5.2 Add D-95 decision to `docs/system/decisions/log.md` (D-91–D-94 were from prior change)
