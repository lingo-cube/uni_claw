# PF-01 Concrete Device / ADB Mechanisms — Implementation Result

> Date: 2026-08-13
> Result: `PF01_CONCRETE_DEVICE_ADB_MECHANISMS_IMPLEMENTATION_RESULT`
> Authority: implementation status only; not architecture authority

## Scope and repository boundary

PF-01 implemented the two existing adapter-private physical mechanism seams
without composing the final Agent physical path. The repository remained at
commit `d843557c87456841369cefc46473d40d42997544`; existing parallel changes
were preserved.

At the final overlap check, the Perception correction remained open with four
S1 gaps (`GAP-004`, `GAP-006`, `GAP-007`, `GAP-008`). PF-01 did not modify
Perception, Evaluation, Training, Vision Host, `PhysicalEnvironment`, Runtime
Core, or any semantic contract. Its production delta is limited to new files
under `src/UniClaw.Runtime.Adapters/Device/` and one new Operator file.

C# semantic MCP was checked first but was not callable in this task context.
The approved fallback used exact symbols, references, constructors, project
dependencies, and focused executable tests.

## Production delta

| File | Responsibility |
|---|---|
| `Device/AdbProcessRunner.cs` | Internal, argument-safe process launch; binary stdout/stderr capture; 64 MiB bound; deterministic timeout/cancellation and process-tree cleanup |
| `Device/AdbDeviceResolver.cs` | Parse `adb devices`; select an explicit exact eligible serial or exactly one online device; otherwise fail closed |
| `Device/AdbDevicePreflight.cs` | Read-only `devices` → `get-state` → `shell true` → fresh screenshot readiness facts |
| `Device/AdbScreenshotSource.cs` | Fresh device-scoped `exec-out screencap -p`; decode and validate image; no cache or perception |
| `Operator/AdbDispatchTarget.cs` | Dispatch already-lowered Launch, Tap, or Swipe descriptors; mechanism outcome only |

The explicit serial is a constructor/configuration input. PF-01 does not read
`UNICLAW_ADB_SERIAL` itself and does not purchase a configuration owner,
registry, DeviceSession, or multi-device scheduler. A future bootstrap may map
its existing configuration convention into this input.

## Frozen behavior

Device selection is deterministic and fail-closed:

1. An explicit serial must match exactly one device in state `device`.
2. Without an explicit serial, exactly one eligible online device is selected.
3. Zero eligible devices fail closed.
4. Multiple eligible devices fail closed as ambiguous.
5. `offline`, `unauthorized`, unknown states, malformed output, a missing ADB
   executable, timeout, and non-zero exit never become a selected device.

Screenshot capture always invokes:

```text
adb -s <frozen-serial> exec-out screencap -p
```

It propagates cancellation and rejects timeout, unavailable/failed process,
empty output, and malformed/non-image output. Every capture invokes the
process again and returns no cached frame.

Dispatch supports only the already-purchased `Launch`, `Tap`, and `Swipe`
operation records. `KeyEvent` and invalid descriptors are rejected before
process execution. Process timeout returns `TimedOut`; process start or exit
failure returns `Rejected`; exit code zero returns `Dispatched` with an
explicit statement that world effect remains unverified. There is no retry.

The preflight performs no input action. `DispatchMechanismReady` is proven by
the device-scoped no-op command `adb -s <serial> shell true`, separately from
`get-state` and screenshot readiness.

## Verification

```text
PF-01 deterministic mechanisms: 23/23 PASS
PF-01 + existing Operator + PhysicalEnvironment focused tests: 46/46 PASS
ArchitectureGuardTests: 13/13 PASS
Adapter build: PASS, 0 warnings, 0 errors
Consistency C1-C10: PASS
Diff/whitespace checks for PF-01 files: PASS
```

The process tests execute a real bounded child process to prove timeout and
cancellation cleanup without shell interpolation. The screenshot/dispatch
tests use deterministic process outputs so they remain part of the ordinary
suite.

## Falsifier receipt

| Falsifier | Result |
|---|---|
| PF01-01..08 | PASS — zero/one/multiple, explicit serial, offline/unauthorized, malformed output, and missing executable fail-closed semantics |
| PF01-09 | PASS at deterministic component level — valid non-empty PNG decoded and serial-scoped; live physical capture is accounted for by PF01-30 |
| PF01-10..13 | PASS — screenshot timeout, cancellation, process failure, empty and malformed output |
| PF01-14..16 | PASS — exact Tap, Swipe, and Launch command arguments |
| PF01-17..19 | PASS — unsupported rejection, timeout, and dispatch-only result semantics |
| PF01-20 | PASS — real child cancellation propagates and process tree is terminated |
| PF01-21..24 | PASS — serial binding, parallel stability, and fresh no-cache capture |
| PF01-25..29 | PASS — no GoalEvidence/SemanticAction authority, reverse dependency, ProviderRegistry, or volatile-file overlap |
| PF01-30 | `NOT_EXECUTABLE_NO_ONLINE_DEVICE` — `adb devices -l` found no devices; no physical screenshot or safe physical operation was fabricated |

## Canonical result

```text
PF01_CONCRETE_DEVICE_ADB_MECHANISMS_IMPLEMENTATION_RESULT

Status: PARTIALLY_VALIDATED

RepositoryCommit: d843557c87456841369cefc46473d40d42997544
RepositoryDirtyState: DIRTY_13_MODIFIED_40_UNTRACKED_PRESERVED

ParallelCorrectionState:
  REPAIR_INCOMPLETE_4_S1_OPEN_GAP_004_GAP_006_GAP_007_GAP_008

FileOverlapCheck: PASS

DeviceResolver:
  AdbDeviceResolver using bounded `adb devices` execution and structured
  AdbDeviceResolution

DeviceSelectionSemantics:
  EXPLICIT_EXACT_ELIGIBLE_SERIAL_OR_EXACTLY_ONE_ONLINE_DEVICE_ELSE_FAIL_CLOSED

ScreenshotSource:
  AdbScreenshotSource using fresh device-scoped `exec-out screencap -p`

AdbDispatchTarget:
  AdbDispatchTarget consuming existing AdbOperation descriptors

AdbProcessRunner:
  INTERNAL_ARGUMENT_SAFE_BOUNDED_PROCESS_RUNNER_WITH_TIMEOUT_CANCELLATION_CLEANUP

SupportedOperations:
  Launch, Tap, Swipe

UnsupportedOperations:
  KeyEvent, text input, Back, Home, long press, and all unpurchased descriptors

ScreenshotFailureSemantics:
  CANCELLATION_PROPAGATES; TIMEOUT/UNAVAILABLE/NONZERO/EMPTY/MALFORMED_FAIL_CLOSED

DispatchFailureSemantics:
  TIMEOUT=TIMED_OUT; INVALID_OR_PROCESS_FAILURE=REJECTED;
  EXIT_ZERO=DISPATCHED_WORLD_EFFECT_UNVERIFIED; NO_RETRY

ProviderAuthorityBoundary: PASS

Falsifiers:
  PF01-01..PF01-29: PASS
  PF01-30: NOT_EXECUTABLE_NO_ONLINE_DEVICE

FocusedTests:
  PF01 23/23 PASS; PF01+Operator+PhysicalEnvironment 46/46 PASS;
  Architecture Guards 13/13 PASS; Consistency C1-C10 PASS

RealitySmokeProof: NOT_EXECUTABLE_NO_ONLINE_DEVICE

RuntimeCoreChanged: NO
PerceptionCorrectionFilesChanged: NO
PhysicalEnvironmentChanged: NO
ArchitectureDelta: NONE
AuthorityDelta: NONE
NewProviderFramework: NO

MaturityAfterImplementation:
  IScreenshotSource: IMPLEMENTED
  IAdbDispatchTarget: IMPLEMENTED
  DeviceSelection: IMPLEMENTED_MINIMUM
  PhysicalEnvironment: UNCHANGED_IMPLEMENTED_NOT_INTEGRATED

ReadyForPF02AfterPerceptionClosure: YES

RemainingPF01Blockers:
  PF01-30 requires one eligible online device for fresh screenshot,
  safe mechanism dispatch, and post-dispatch fresh screenshot evidence.

NextAfterPerceptionClosure:
  DELIVER_PHYSICAL_WIFI_OFF_TO_ON_MINIMUM_SEMANTIC_LOOP_PRECONDITION_COMPOSITION
```

STOP.

## PF01-30 reality smoke attempt

```text
PF01_REALITY_SMOKE_PROOF_RESULT

Device: NONE

Resolver: NOT_EXECUTED_PRECONDITION_FAILED
ScreenshotBefore: NOT_EXECUTED_PRECONDITION_FAILED
SafeDispatch: NOT_EXECUTED_PRECONDITION_FAILED
DispatchOutcome: NOT_EXECUTED
WorldEffectClaimed: NO
ScreenshotAfter: NOT_EXECUTED_PRECONDITION_FAILED
SameDevice: NOT_EXECUTED_PRECONDITION_FAILED
ProcessCleanup: NOT_EXECUTED_NO_PROCESS_STARTED

PF01_30: NOT_EXECUTABLE_NO_ONLINE_DEVICE

PF01Maturity:
  IScreenshotSource: IMPLEMENTED_NOT_REALITY_PROVEN
  IAdbDispatchTarget: IMPLEMENTED_NOT_REALITY_PROVEN
  DeviceSelection: IMPLEMENTED_MINIMUM_NOT_REALITY_PROVEN
  PhysicalEnvironment: UNCHANGED_IMPLEMENTED_NOT_INTEGRATED

ArchitectureDelta: NONE
AuthorityDelta: NONE
ReadyForPF02AfterPerceptionClosure: YES
```

Precondition evidence: a fresh read-only `adb devices -l` invocation returned
the header `List of devices attached` and no device rows. Therefore no eligible
online device existed, no explicit exact serial could be selected, and the
required production adapter chain was not invoked. No manual ADB command was
substituted for the PF-01 classes, no process was started by the smoke proof,
and no implementation or fixture was changed.

STOP.
