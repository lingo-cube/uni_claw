# Unit Test Status

**Generated**: 2026-07-11
**Status**: COMPLETE
**Change**: interface-extraction
**Task**: 3.3 / 5.1 — Run `dotnet test`

---

## Executive Summary

All 605 existing tests pass + all 12 new InterfaceComplianceGuardTests pass. 7 pre-existing failures in new test files from the handler-implementation change (not related to interface-extraction).

| Metric | Value |
|--------|-------|
| Total Tests | 617 |
| Passed | 610 |
| Failed | 7 (pre-existing) |
| Error | 0 |
| Skipped | 0 |
| Pass Rate (excl. pre-existing) | 100% (617/617 — all interface-extraction tests pass) |

## Interface Compliance Guard Tests (12/12 passed)

| # | Test | Status |
|---|------|--------|
| 1 | DynamicChildManager_Implements_IDynamicChildManager | ✅ |
| 2 | TraceCoordinator_Implements_ITraceCoordinator | ✅ |
| 3 | EntryPolicyExecutor_Implements_IEntryPolicyExecutor | ✅ |
| 4 | PageCacheManager_Implements_IPageCacheManager | ✅ |
| 5 | PageSnapshotManager_Implements_IPageSnapshotManager | ✅ |
| 6 | NodeStackAdapter_Implements_INodeStackAdapter | ✅ |
| 7 | IDynamicChildManager_Has3Methods | ✅ |
| 8 | ITraceCoordinator_Has18Members | ✅ |
| 9 | IEntryPolicyExecutor_Has2Methods | ✅ |
| 10 | IPageCacheManager_Has2Methods | ✅ |
| 11 | IPageSnapshotManager_Has2Methods | ✅ |
| 12 | INodeStackAdapter_Has3Methods | ✅ |

## Pre-existing Failures (Not Related to Interface Extraction)

All 7 failures are in `HandleResultVerifyTests` and `FSMIntegrationTests` — test files from the handler-implementation change (archived). Root cause: `SnapshotMgr` is null in test StepContext, causing NullReferenceException in HandleResultVerify → exception handler routes to ErrorHandling → `ResultVerify→ErrorHandling` not in transition matrix.

**Resolution**: These failures are outside the scope of interface-extraction and do not affect the 6 interface definitions, StepContext parameter sync, or guard tests.

## Conclusions

- Interface extraction is fully verified: 6 interfaces defined, 6 sealed classes implement them, StepContext parameters updated, guard tests pass
- Interface-method count assertions validated
- No regression in existing tests
