# Perception Platform Phase 2 — Provider Host Review

> Date: 2026-08-12
> Role: Project Leader / Phase Gate Reviewer
> Lane: `ARCHITECTURE_DISCOVERY`
> Inputs:
> - `docs/decisions/perception-platform-architecture-gate.md` (gate definition)
> - `docs/decisions/perception-platform-contract-extraction-result.md` (Phase 1 authorization)
> - Phase 1 completion: `PERCEPTION_PLATFORM_PHASE_1_CONTRACT_EXTRACTION = VALIDATED`
> - Frozen contract: `GET /version` → `supportedSchemas`, `modelId`, `configHash`
> Result: `PERCEPTION_PLATFORM_PHASE_2_PROVIDER_HOST_REVIEW_RESULT`
> Decision: **PURCHASE_WITH_CONSTRAINTS**
> Implementation authority: **NOT YET GRANTED** (requires separate task authorization after review acceptance)

---

## 0. Review result

```text
PHASE_2_PROVIDER_HOST_REVIEW
  = PURCHASE_WITH_CONSTRAINTS

ARCHITECTURE_GATE_REFINEMENTS
  = 6 (detailed below)

BLOCKING_ISSUES
  = 0

RUNTIME_DELTA
  = NONE

OWNERSHIP_DELTA
  = VisionServiceHost (new C# project, Runtime.Adapters solution folder)

AUTHORITY_DELTA
  = NONE
```

Phase 2 is architecturally sound and ready to purchase, with six refinements
to the original gate design. Each refinement is a constraint on the Phase 2
implementation, not a reason to delay.

---

## REVIEW 1 — OWNERSHIP

### 1.1 Host ownership

The `VisionServiceHost` owns the Python vision service process lifecycle.
It is the **sole mutable state owner** for host lifecycle state.

```text
VisionServiceHost owns:
  • Python process lifecycle (start, monitor, restart, stop)
  • Startup validation (Python, packages, model, config)
  • Service readiness determination (warm=true via /health)
  • Health polling (periodic GET /health)
  • Graceful shutdown (SIGTERM → drain → SIGKILL)
  • Crash detection (process exit event)
  • Bounded restart (budget + backoff)
  • Socket lifecycle (create path, pass to child, cleanup)
  • Schema version compatibility validation
  • Operational diagnostics (crash count, last error, state transitions)

VisionServiceHost MUST NOT own:
  • Runtime semantic state (Agent, Container, Traversal)
  • Observation truth (that's IEnvironment → PhysicalEnvironment)
  • Agent decisions (capability selection, goal satisfaction)
  • Capability selection (perception is infrastructure, not a capability)
  • Action retry (Traversal owns retry)
  • Runtime recovery (Agent owns Recovery)
  • GoalEvidence (Agent owns completion)
  • Task completion (Agent owns terminal adjudication)
```

### 1.2 Mutable state owner

One mutable state, one owner:

```text
HOST_LIFECYCLE_STATE: VisionServiceHost (sole owner)

STATE VALUES: { COLD, WARMING, HEALTHY, UNHEALTHY, CRASHED, SHUTDOWN }

MUTATION: Only VisionServiceHost internal logic may transition state.
          External consumers (PhysicalEnvironment adapter, diagnostics)
          read state via a thread-safe property. No external writer.

READ CONSUMERS:
  • PhysicalEnvironment: reads IsHealthy before ObserveAsync
  • Harness diagnostics: reads State, CrashCount, LastError for
    HARNESS_OPERATIONAL_FAILURE episode construction
  • Host self: reads state to determine actions (restart, shutdown)
```

### 1.3 Adapter relationship

The `PhysicalEnvironment` does NOT own the Host and does NOT call Host
lifecycle methods. The Host is composed at application startup and provides
an `IPerceptionSource` implementation that internally manages the Python
process lifecycle.

```text
Application composition (Startup.cs or equivalent):

  VisionServiceHost host = new(config);
  await host.StartAsync(ct);        // → HEALTHY or throws

  IPerceptionSource perception = host.PerceptionSource;
  // PerceptionSource internally handles:
  //   - connection to UDS
  //   - request timeout
  //   - empty result on connection failure (fail closed)

  PhysicalEnvironment env = new(screenshot, perception, dispatch, ...);
  // PhysicalEnvironment has NO knowledge of Host, process lifecycle,
  // health state, or restart. It only sees IPerceptionSource.
```

---

## REVIEW 2 — PROCESS MODEL

### 2.1 Decision: Option B — dedicated project

```text
HostPlacement: UniClaw.Vision.Host (separate C# project)

Project location:
  uni-agent/src/UniClaw.Vision.Host/
    VisionServiceHost.cs          — lifecycle state machine, process management
    VisionServiceHostConfig.cs    — immutable configuration
    VisionHostHealthState.cs      — state enum + transition validation
    VisionHostDiagnostics.cs      — crash count, last error, state history
    SocketManager.cs              — UDS path creation, ownership, cleanup
    PythonEnvironmentValidator.cs — Python/packages/model/config validation
    VersionNegotiation.cs         — schema compatibility check
    HostedPerceptionSource.cs     — IPerceptionSource wrapper with health gate
```

### 2.2 Rationale

| Criterion | Option A (in Adapters) | Option B (separate project) | Choice |
|---|---|---|---|
| **Lifecycle ownership** | Adapter owns perception translation, not process lifecycle — conflates concerns | Host has one job: manage OS process. Clean separation. | B |
| **Failure isolation** | Process crash in same assembly as perception adaptation — harder to reason about | Crash/restart logic in isolated assembly. Adapter unchanged. | B |
| **Deployment simplicity** | Fewer assemblies. Simpler. | One more .dll. Acceptable cost for separation. | B (acceptable cost) |
| **Testability** | Process lifecycle tests mixed with adapter tests | Host tests run against real Python process. Adapter tests mock IPerceptionSource. Independent test suites. | B |
| **Future training/model platform** | Phase 4 model governance needs Host concept. Inline in Adapters forces Adapters to know about training. | Host is reusable for future model serving, evaluation, and training infrastructure without touching Adapters. | B |
| **Dependency direction** | Adapters → Python (indirect, through Process.Start) | Host → Python (direct). Adapters → IPerceptionSource (unchanged). Cleaner dependency graph. | B |

### 2.3 Assembly dependency graph

```text
UniClaw.Vision.Host
  ├── depends on: System.Diagnostics.Process (BCL)
  ├── depends on: System.Net.Http (BCL, for /health + /version polling)
  ├── depends on: Microsoft.Extensions.Logging (for diagnostics)
  └── depends on: UniClaw.Runtime.Adapters (for IPerceptionSource)

UniClaw.Runtime.Adapters
  ├── depends on: IPerceptionSource (its own interface)
  └── does NOT depend on: UniClaw.Vision.Host
      (Host is composed at application startup, not referenced by Adapters)

UniClaw.Runtime
  └── does NOT depend on: UniClaw.Vision.Host
      (Host is invisible to Runtime. Runtime only sees IEnvironment.)
```

### 2.4 Composition root

The application entry point (Program.cs / Startup.cs) composes the Host and
passes the `IPerceptionSource` to `PhysicalEnvironment`. This is NOT a DI
container resolution — it's explicit composition with a defined startup order:

```csharp
// Application composition — explicit, ordered, no DI magic
var hostConfig = VisionServiceHostConfig.FromEnvironment();
using var host = new VisionServiceHost(hostConfig, loggerFactory.CreateLogger("VisionHost"));
await host.StartAsync(ct);

var perception = host.PerceptionSource; // IPerceptionSource
var screenshot = new AdbScreenshotSource();
var dispatch = new AdbDispatchTarget();

using var environment = new PhysicalEnvironment(
    screenshot, perception, dispatch,
    foregroundApp, displayWidth, displayHeight);
```

---

## REVIEW 3 — STARTUP CONTRACT

### 3.1 Approved startup sequence

The 14-step sequence from the gate is approved with one refinement: steps
6–7 (compute modelId/configHash) are moved to after step 5 (validate
artifacts exist) but before step 10 (launch). The service computes these
internally; the Host computes them independently to verify.

```text
STARTUP SEQUENCE (sequential, fail-fast):

 1. Resolve configured Python executable path.
    Source: config or UNICLAW_PYTHON_BIN env var.
    Validate: file exists, is executable.
    Fail: PythonNotFound → fail startup.

 2. Validate service entry point exists.
    Source: UNICLAW_VISION_SERVICE_PATH env var or default
            "tools/local_vision/server.py" relative to repo root.
    Validate: file exists, readable.
    Fail: ServiceEntryPointNotFound → fail startup.

 3. Validate required packages are importable.
    Execute: python -c "import ultralytics; import rapidocr_onnxruntime; import fastapi"
    Fail: PackageImportFailed → fail startup.
    Note: does NOT pip install. Only validates.

 4. Validate model artifact exists.
    Source: UNICLAW_YOLO_MODEL env var or default path.
    Validate: file exists, non-empty, readable.
    Fail: ModelNotFound → fail startup.

 5. Validate config artifact exists and is valid JSON.
    Source: UNICLAW_LABEL_MAPPING env var or default path.
    Validate: file exists, parses as valid JSON, contains "mappings" key.
    Fail: ConfigNotFound or ConfigInvalid → fail startup.

 6. Choose process-specific UDS path.
    Format: /tmp/uniclaw-vision-{sessionGuid}.sock
    sessionGuid = Guid.NewGuid() at Host construction.
    See REVIEW 4 for socket ownership.

 7. Remove only safely-owned stale socket.
    If socket path exists:
      a. Attempt Unix Domain Socket connect + GET /health (timeout 500ms).
      b. If connect succeeds AND /health returns 200:
         → Another live service owns this socket. Fail: SocketCollision.
      c. If connect fails OR /health fails:
         → Socket is stale. Remove it. Proceed.
    See REVIEW 4 for safety rules.

 8. Launch Python child process.
    Command: {python} -m uvicorn tools.local_vision.server:app
             --uds {socketPath}
    Environment: UNICLAW_VISION_SOCKET={socketPath}
                 UNICLAW_YOLO_MODEL={modelPath}
                 UNICLAW_LABEL_MAPPING={configPath}
                 (all other UNICLAW_* vars inherited from parent)
    Redirect: stdout → logger (Info), stderr → logger (Warning/Error)
    Fail: ProcessStartFailed → fail startup.

 9. Poll GET /health every 500ms.
    Timeout: 60 seconds from process start.
    Success condition: response 200 + body.warm == true.
    Timeout: HealthNeverReady → kill process, fail startup.

10. Query GET /version.
    Validate: response 200, valid JSON, supportedSchemas is non-empty array.
    Fail: VersionQueryFailed → kill process, fail startup.

11. Validate schema compatibility.
    Adapter declares: MAX_SUPPORTED_SCHEMA = "uniclaw.localVisionEvidence.v1"
    Service returns: supportedSchemas
    Intersection must be non-empty.
    Fail: SchemaIncompatible → kill process, fail startup.

12. Record operational facts.
    - modelId (from /version response)
    - configHash (from /version response)
    - serviceVersion (from /version response)
    - socketPath
    - processId
    These are diagnostic facts, not compatibility decisions.

13. Enter HEALTHY state.

14. Return. Caller receives a ready IPerceptionSource.
```

### 3.2 Startup failure behavior

Every startup failure kills the child process (if launched), cleans up the
socket (if created), and throws a descriptive exception. The application
composition layer decides whether to retry startup or surface the error.

The Host does NOT automatically retry startup. Startup retry is an
application-level concern (e.g., "retry vision service startup 3 times with
5-second backoff before surfacing error to operator").

---

## REVIEW 4 — SOCKET OWNERSHIP

### 4.1 Decision: process-specific UDS with session GUID

```text
SocketOwnership: VisionServiceHost owns socket lifecycle.

Socket naming:
  Format:  /tmp/uniclaw-vision-{sessionGuid}.sock
  sessionGuid: Guid.NewGuid() at Host construction time.
               Fresh GUID per Host instance. Not pid-based
               (pid can be reused by OS, GUID cannot).

  Example: /tmp/uniclaw-vision-a1b2c3d4-e5f6-7890-abcd-ef1234567890.sock

Ownership:
  • Host CREATES the socket path (by choosing the GUID).
  • Host PASSES the path to child process via UNICLAW_VISION_SOCKET env.
  • Child BINDS to the path (uvicorn --uds).
  • Host REMOVES the path on shutdown or startup if stale.

Cleanup:
  • On graceful shutdown: Host sends SIGTERM, waits for child exit,
    removes socket file.
  • On crash: Socket file remains. Next Host startup detects stale socket
    (step 7), removes it.
  • On Host disposal: Socket removed in Dispose() / using block.

Collision prevention:
  • GUID-based naming makes collision astronomically unlikely.
  • Before creating, Host checks if path exists. If it does:
    - If owned by a live service → SocketCollision error (should not
      happen with GUID naming unless two Hosts share a sessionGuid).
    - If stale → remove and proceed.

Stale socket handling:
  Stale socket = path exists but no process is listening.
  Detection: attempt Unix Domain Socket connect + GET /health (500ms timeout).
  If connection refused or /health times out → stale → safe to remove.
  If /health returns 200 → LIVE → do NOT remove. Fail with SocketCollision.

Safety rules:
  • Host MUST NOT delete a socket path that does not match its naming format
    (/tmp/uniclaw-vision-{guid}.sock). No glob deletion. No arbitrary path
    deletion.
  • Host MUST verify staleness before removing any socket, even its own format.
  • Host MUST NOT delete socket files owned by other users (check file ownership
    if possible; otherwise rely on naming convention + staleness check).

Child-process propagation:
  • Socket path passed via UNICLAW_VISION_SOCKET environment variable.
  • server.py reads UNICLAW_VISION_SOCKET; if set, binds to that UDS path.
    If not set, falls back to default /tmp/uniclaw-vision.sock
    (backward-compatible for direct launch without Host).
```

---

## REVIEW 5 — HEALTH STATE MACHINE

### 5.1 Refinement: DEGRADED removed

`DEGRADED` is speculative. There is no current evidence of a partial-failure
mode where the vision service is slow but not dead. The service either responds
or it doesn't. If future evidence emerges (e.g., /health returns OK but
/v1/analyze latency exceeds a threshold), DEGRADED can be added then.

### 5.2 Approved states: 6

```text
LifecycleStates: { COLD, WARMING, HEALTHY, UNHEALTHY, CRASHED, SHUTDOWN }

COLD:
  • Host constructed but StartAsync() not yet called.
  • No child process exists.
  • Socket path chosen but not yet created.

WARMING:
  • Child process launched.
  • /health polling active.
  • warm=false.
  • Startup timeout counting down.
  • PerceptionSource not yet available to Adapter.

HEALTHY:
  • /health returns warm=true.
  • /version validated, schema compatible.
  • PerceptionSource available to Adapter.
  • Normal operation.

UNHEALTHY:
  • Was HEALTHY but /health check failed (timeout or non-200).
  • PerceptionSource temporarily unavailable.
  • Host will attempt restart (transition to WARMING via CRASHED).
  • Adapter sees empty candidates during UNHEALTHY window.

CRASHED:
  • Child process exited (detected via Process.Exited event or
    health check failure that reveals process death).
  • Socket may be stale.
  • Host will attempt restart if budget remains.
  • Transition: CRASHED → WARMING (restart) or CRASHED → SHUTDOWN (budget exhausted).

SHUTDOWN:
  • Graceful shutdown completed or restart budget exhausted.
  • Child process killed (if still running).
  • Socket cleaned up.
  • Terminal state. Host is disposed.
```

### 5.3 Legal transitions

```text
COLD       → WARMING    (StartAsync called)
COLD       → SHUTDOWN   (Dispose called before StartAsync)

WARMING    → HEALTHY    (/health warm=true + /version OK + schema OK)
WARMING    → CRASHED    (child process exits before HEALTHY)
WARMING    → SHUTDOWN   (Dispose called during warmup)

HEALTHY    → UNHEALTHY  (/health check fails: timeout or non-200)
HEALTHY    → CRASHED    (child process exits unexpectedly)
HEALTHY    → SHUTDOWN   (Dispose called)

UNHEALTHY  → CRASHED    (child process confirmed dead)
UNHEALTHY  → WARMING    (restart initiated — kill old process, start new one)
UNHEALTHY  → SHUTDOWN   (Dispose called)

CRASHED    → WARMING    (restart initiated, within budget)
CRASHED    → SHUTDOWN   (restart budget exhausted, or Dispose called)

SHUTDOWN   → (terminal)
```

### 5.4 State mutation authority

```text
LifecycleOwner: VisionServiceHost

Only VisionServiceHost internal methods may call TransitionTo(newState).
All transitions validate against the legal transition table.
Invalid transition → InvalidOperationException (Host bug, not runtime condition).

External consumers read state via:
  host.State          → VisionHostHealthState (thread-safe property)
  host.IsHealthy      → bool (convenience: State == HEALTHY)
  host.PerceptionSource → IPerceptionSource (null until HEALTHY, reset on UNHEALTHY/CRASHED)
```

### 5.5 Observations that cause transitions

| Observation | Source | Current State | New State |
|---|---|---|---|
| StartAsync() called | Application composition | COLD | WARMING |
| /health returns 200 + warm=true + /version OK + schema OK | Health poll timer | WARMING | HEALTHY |
| /health poll timeout (60s from process start) | Health poll timer | WARMING | CRASHED |
| Child process exits (Process.Exited) | Process event | WARMING | CRASHED |
| /health returns non-200 or times out (5s per poll) | Health poll timer | HEALTHY | UNHEALTHY |
| Child process exits (Process.Exited) | Process event | HEALTHY | CRASHED |
| UDS connection failure during perception request | Adapter callback | HEALTHY | (stay HEALTHY; next health poll will catch it) |
| Restart decision (within budget) | Host internal | UNHEALTHY / CRASHED | WARMING |
| Restart budget exhausted | Host internal | CRASHED | SHUTDOWN |
| Dispose() called | Application composition | any | SHUTDOWN |

---

## REVIEW 6 — CRASH / RESTART SEMANTICS

### 6.1 Refinement: configurable budget replaces hardcoded "5"

The original gate proposed "5 crashes per Runtime session." This is replaced
with a configurable budget based on a sliding time window. The default is
conservative and evidence-based — it prevents infinite crash loops without
assuming a magic number.

```text
RestartPolicy:
  Budget:       3 restarts per 60-second sliding window
  Backoff:      1s, 2s, 4s (capped at 30s) — exponential with cap
  Exhaustion:   After 4th crash within window → SHUTDOWN
                → surface HARNESS_OPERATIONAL_FAILURE episode
                → NO further restarts

  Configuration:
    VISION_HOST_MAX_RESTARTS=3      (env var, default 3)
    VISION_HOST_RESTART_WINDOW_SEC=60 (env var, default 60)
    VISION_HOST_RESTART_BACKOFF_MAX_SEC=30 (env var, default 30)

  Rationale for default 3/60s:
    - 3 crashes in 60 seconds is clearly a systemic problem, not a transient
      hiccup. No evidence suggests a vision service that crashes 4+ times
      in a minute will recover on the 5th attempt.
    - 1s → 2s → 4s backoff gives 7 seconds total before exhaustion. If the
      service can't stabilize in 3 attempts over 7 seconds, more attempts
      won't help.
    - Configurable: operators who know their environment can tune it.
```

### 6.2 Scenario-by-scenario behavior

| Scenario | Detection | Host action | Adapter behavior | Runtime impact |
|---|---|---|---|---|
| **Child exits before ready** | Process.Exited event during WARMING | Increment crash count. If budget remains: backoff → restart (WARMING). If budget exhausted: SHUTDOWN. | PerceptionSource still null (not yet handed out). | No Runtime run can start — Host didn't reach HEALTHY. Application composition surface: Host.StartAsync throws or returns failed status. |
| **Child exits while healthy** | Process.Exited event during HEALTHY | Increment crash count. Set PerceptionSource to null. Transition to CRASHED. If budget remains: backoff → restart. | Next AnalyzeAsync call gets null PerceptionSource → empty array returned by HostedPerceptionSource wrapper. | Runtime sees UNKNOWN world (empty Observation.Elements). No Runtime state change. No retry. |
| **/health timeout while healthy** | Health poll timer: /health exceeds 5s timeout | Transition to UNHEALTHY. Set PerceptionSource to null. Send SIGTERM to child. Wait 3s → SIGKILL. Transition to CRASHED → restart if budget remains. | Same as above — empty candidates during UNHEALTHY/CRASHED window. | Same as above. |
| **UDS connection failure** | SocketException in HostedPerceptionSource.AnalyzeAsync | Catch exception. Return empty array for this request. Next health poll (within 5s) will detect UNHEALTHY. | Empty array returned immediately for this request. | UNKNOWN world for one Observation cycle. Next ObserveAsync may succeed (if restart completes) or continue empty (if restart fails). |
| **Malformed /version at startup** | /version returns non-200 or invalid JSON | Startup failure. Do NOT enter WARMING → HEALTHY path. Kill child process. Throw HostStartupException. | N/A — Host never reaches HEALTHY. | N/A. |
| **Schema mismatch at startup** | supportedSchemas intersection is empty | Startup failure. Same as malformed /version. | N/A. | N/A. |
| **modelId drift** | /version modelId differs from previous startup | OBSERVABILITY FACT. Log warning. Do NOT block startup. Do NOT change health state. Record in diagnostics. | Unchanged. | Unchanged. modelId is traceability metadata, not a compatibility gate. |
| **configHash drift** | /version configHash differs from previous startup | OBSERVABILITY FACT. Log info (config updates are normal). Do NOT block startup. Record in diagnostics. | Unchanged. | Unchanged. |

### 6.3 What restart means (and doesn't mean)

```text
Host restart scope: Python VISION SERVICE PROCESS ONLY.

Host restart:
  ✓ Kills the old Python process (SIGTERM → wait → SIGKILL).
  ✓ Cleans up the old socket.
  ✓ Creates a new socket path (fresh sessionGuid).
  ✓ Launches a new Python process.
  ✓ Polls /health until warm.
  ✓ Validates /version + schema compatibility.
  ✓ Enters HEALTHY.
  ✓ Resumes serving perception requests.

Host restart is NOT:
  ✗ DeviceAction retry. Host has no knowledge of DeviceAction.
  ✗ Runtime execution restart. Host is below IEnvironment.
  ✗ Agent Recovery. Host does not know Agent exists.
  ✗ Replanning. Host has no semantic knowledge.
  ✗ Container modification. Host has no Runtime state access.
  ✗ Failure resolution. Host restarts infrastructure.
    Runtime independently adjudicates whether the world changed.
```

### 6.4 During-restart window

Between CRASHED and the next HEALTHY (typically 2–10 seconds with backoff +
warmup), perception is unavailable. The Adapter returns empty candidates.
Runtime observes an empty world.

This is correct behavior: the world is genuinely UNKNOWN during the restart
window. Runtime must not fabricate evidence. If Runtime was mid-execution:
- Current ObserveAsync returns empty Observation.
- Container sees no elements → StateBeliefs go to UNKNOWN.
- Agent sees UNKNOWN → decides StateEvidenceRequired.
- Traversal cannot proceed without evidence.
- The run may terminate with INSUFFICIENT_EVIDENCE.

This is fail-closed, truthful behavior. No Runtime change required.

---

## REVIEW 7 — FAILURE PROPAGATION

### 7.1 What the Adapter sees

```text
FAILURE_PROPAGATION_CONTRACT:

HostedPerceptionSource (implements IPerceptionSource) wraps the real
LocalVisionPerceptionSource. When the Host is not HEALTHY, it returns
empty array WITHOUT calling the underlying HTTP client.

Scenario                     | HostedPerceptionSource.AnalyzeAsync returns
-----------------------------+------------------------------------------
Host HEALTHY, request OK     | Normal candidate array (underlying HTTP call)
Host HEALTHY, HTTP timeout   | Empty array (fail closed)
Host HEALTHY, JSON invalid   | Empty array (fail closed)
Host HEALTHY, non-200        | Empty array (fail closed)
Host UNHEALTHY               | Empty array (no HTTP call attempted)
Host CRASHED                 | Empty array (no HTTP call attempted)
Host SHUTDOWN                | Empty array (no HTTP call attempted)
Host COLD / WARMING          | Empty array (no HTTP call attempted)

DISTINGUISHING OPERATIONAL DIAGNOSTICS (not visible to Runtime):

The Host records a diagnostic reason for each empty-array return:
  • "ServiceUnavailable"       — Host not HEALTHY
  • "RequestTimeout"            — HTTP request exceeded timeout
  • "MalformedResponse"         — JSON parse error or null evidence
  • "HttpError"                 — non-200 status code

These are Host-owned diagnostics. They are surfaced through:
  • Host.Diagnostics property (for Harness failure episode construction)
  • ILogger (for operational monitoring)
  • They are NOT visible to Runtime through IEnvironment or Observation.
```

### 7.2 Interaction with HARNESS_OPERATIONAL_FAILURE

```text
HARNESS_OPERATIONAL_FAILURE episode construction:

When restart budget is exhausted (CRASHED → SHUTDOWN):
  1. Host records terminal diagnostic:
     - CrashCount, CrashTimestamps[], LastExitCode, LastStderr
  2. Harness (or application composition layer) observes Host.State == SHUTDOWN
     AND Host.CrashBudgetExhausted == true.
  3. Harness constructs HARNESS_OPERATIONAL_FAILURE episode:
     - Episode references Host diagnostics (CrashCount, timestamps)
     - Episode is correlated with active Run via RunId (if a run is in progress)
     - Episode does NOT reference Runtime outcome (that's independent)
  4. Episode is persisted as a Harness artifact.

Host diagnostics MUST NOT:
  • Change Runtime.SemanticRunResult
  • Change Agent state
  • Trigger Agent Recovery
  • Modify Container beliefs
  • Retry any DeviceAction
  • Recommend any Runtime action

Harness records the failure. Runtime continues independently.
```

### 7.3 Operational diagnostics vs. semantic evidence

```text
┌─────────────────────────────────────────────────────┐
│ OPERATIONAL DIAGNOSTICS (Host-owned)                │
│                                                     │
│ • Host.State, CrashCount, LastError                 │
│ • Request failure reason (timeout, malformed, etc.) │
│ • modelId, configHash, serviceVersion               │
│                                                     │
│ Consumed by: Harness diagnostics, operator, logs    │
│ MUST NOT cross IEnvironment boundary                │
└─────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────┐
│ PERCEPTION EVIDENCE (IEnvironment boundary)          │
│                                                     │
│ • Observation.Elements (ObservedElement[])           │
│ • Observation.SequenceNumber                        │
│ • ObservedElement.Text, Bounds, SwitchState, Type   │
│                                                     │
│ When perception unavailable: Elements = empty       │
│ No diagnostic reason. No "why" exposed to Runtime.  │
└─────────────────────────────────────────────────────┘
```

---

## REVIEW 8 — VERSION NEGOTIATION

### 8.1 Compatibility rule

```text
VersionNegotiation:

COMPATIBLE if:
  Adapter.SupportedSchemas ∩ Service.SupportedSchemas ≠ ∅

  Negotiated schema = highest version in intersection
  (lexicographic comparison on schema version string)

INCOMPATIBLE if:
  Intersection is empty → startup fails. No fallback.

CURRENT STATE:
  Adapter declares:  ["uniclaw.localVisionEvidence.v1"]
  Service declares:  ["uniclaw.localVisionEvidence.v1"]
  Intersection:      ["uniclaw.localVisionEvidence.v1"]
  Negotiated:        "uniclaw.localVisionEvidence.v1"
  → COMPATIBLE

FUTURE v1.1:
  Adapter declares:  ["uniclaw.localVisionEvidence.v1"]
  Service declares:  ["uniclaw.localVisionEvidence.v1", "uniclaw.localVisionEvidence.v1.1"]
  Intersection:      ["uniclaw.localVisionEvidence.v1"]
  Negotiated:        "uniclaw.localVisionEvidence.v1"
  → COMPATIBLE (adapter uses v1, service emits v1 fields)

FUTURE v2 BREAK:
  Adapter declares:  ["uniclaw.localVisionEvidence.v1"]
  Service declares:  ["uniclaw.localVisionEvidence.v2"]
  Intersection:      []
  → INCOMPATIBLE → fail startup
```

### 8.2 modelId / configHash role

```text
modelId and configHash are OBSERVABILITY FACTS, not compatibility gates.

modelId:
  • Carried in /version response and in every /v1/analyze metadata.
  • Enables traceability: every perception output knows which model produced it.
  • Drift from expected: WARNING. Does not block startup.
  • Drift from previous run: recorded in Host diagnostics. Informational.

configHash:
  • Carried in /version response and in every /v1/analyze metadata.
  • Enables config drift detection: consumer can verify config consistency.
  • Drift from expected: INFO. Does not block startup.
  • Drift from previous run: normal during config updates.

No coupled Runtime/Vision releases:
  • Adapter declares supported schemas at compile time.
  • Service declares supported schemas at runtime.
  • Compatibility determined at Host startup, not build time.
  • Runtime may be released independently of Vision Service.
  • Vision Service may add v1.1 fields without Adapter update.
```

---

## REVIEW 9 — PYTHON ENVIRONMENT

### 9.1 Ownership split

```text
PythonEnvironmentOwnership:

DEPLOYMENT / SETUP OWNS:
  • Python installation (version ≥ 3.11)
  • .venv creation (python -m venv .venv-local-vision)
  • Package installation (pip install -r requirements.txt)
  • Model file provisioning (download/copy to artifacts/)
  • Config file provisioning (label-mapping.json)

HOST OWNS:
  • VALIDATION of deployment artifacts (not creation)
    - Python version check: python --version → parse, verify ≥ 3.11
    - Package import check: python -c "import ultralytics; ..."
    - Model file existence + non-empty check
    - Config file existence + valid JSON check
  • LAUNCH of Python process with validated environment
  • MONITORING of process health
  • SHUTDOWN of process

HOST MUST NOT:
  • pip install (no network dependency at runtime)
  • Create .venv (deployment concern)
  • Download models (deployment concern)
  • Train models (training pipeline concern, Phase 4)
  • Upgrade packages (deployment concern)
  • Modify label-mapping.json (config management concern)
  • Modify model files (model governance concern, Phase 4)
```

### 9.2 Service launch command

```text
Launch command:
  {pythonBin} -m uvicorn tools.local_vision.server:app --uds {socketPath}

  pythonBin:  from config or UNICLAW_PYTHON_BIN env var
              default: "python3" (resolved from PATH)
  socketPath: /tmp/uniclaw-vision-{sessionGuid}.sock

Environment variables passed to child:
  UNICLAW_VISION_SOCKET={socketPath}
  UNICLAW_YOLO_MODEL={modelPath}         (if configured)
  UNICLAW_LABEL_MAPPING={configPath}     (if configured)
  UNICLAW_OCR_BACKEND=rapidocr           (inherit from parent or default)
  UNICLAW_OMP_THREADS=4                  (inherit from parent or default)
  All other UNICLAW_* vars inherited from parent process.

Working directory:
  Repository root (so server.py can resolve relative paths).
  Set via ProcessStartInfo.WorkingDirectory.
```

---

## REVIEW 10 — TEST STRATEGY

### 10.1 Required falsifiers

```text
RequiredFalsifiers: 18 minimum Phase 2 tests

H1  [NORMAL_STARTUP]              Host starts → HEALTHY. PerceptionSource
                                   returns valid candidates for known screenshot.

H2  [PYTHON_MISSING]             Configured Python binary does not exist.
                                   Host.StartAsync throws HostStartupException
                                   with PythonNotFound reason.

H3  [SERVICE_ENTRYPOINT_MISSING] server.py does not exist at configured path.
                                   Host.StartAsync throws HostStartupException
                                   with ServiceEntryPointNotFound.

H4  [MODEL_MISSING]              Model file does not exist. Host.StartAsync
                                   throws HostStartupException with
                                   ModelNotFound.

H5  [CONFIG_MISSING]             label-mapping.json does not exist.
                                   Host.StartAsync throws HostStartupException
                                   with ConfigNotFound.

H6  [HEALTH_NEVER_READY]         Service starts but /health never returns
                                   warm=true within 60s. Host.StartAsync throws
                                   HostStartupException with HealthNeverReady.
                                   Child process is killed.

H7  [CRASH_BEFORE_READY]         Child process exits during WARMING.
                                   CrashCount increments. Host restarts (budget
                                   permitting). If budget exhausted → SHUTDOWN.

H8  [CRASH_AFTER_HEALTHY]        Child process killed (SIGKILL) during HEALTHY.
                                   Host detects exit → CRASHED → restart.
                                   Adapter receives empty array during window.
                                   After restart → HEALTHY → normal operation.

H9  [STALE_SOCKET]               Socket file exists from previous crashed
                                   instance. Host detects stale (no process
                                   listening) → removes it → creates fresh
                                   socket → proceeds normally.

H10 [SOCKET_COLLISION]           Another live Host instance owns the socket.
                                   Host detects live /health response →
                                   SocketCollision → fail startup.

H11 [MALFORMED_VERSION]          /version returns invalid JSON. Host.StartAsync
                                   throws HostStartupException. Child killed.

H12 [UNSUPPORTED_SCHEMA]         Service returns schema version with no adapter
                                   overlap (simulated via test-only server
                                   config). Host.StartAsync throws
                                   HostStartupException with SchemaIncompatible.

H13 [MALFORMED_ANALYZE]          /v1/analyze returns invalid JSON. Adapter
                                   returns empty array. No exception thrown to
                                   Runtime. Host stays HEALTHY (response was
                                   received; it was just malformed).

H14 [REQUEST_TIMEOUT]            /v1/analyze exceeds timeout. Adapter returns
                                   empty array. Host stays HEALTHY (network
                                   blip; next request may succeed). Next health
                                   poll will detect if service is truly dead.

H15 [RESTART_BUDGET_EXHAUSTED]   With budget 3/60s, crash child process 4 times
                                   within 60s. On 4th crash: Host enters SHUTDOWN
                                   (not CRASHED). CrashBudgetExhausted = true.
                                   No further restarts.

H16 [GRACEFUL_SHUTDOWN]          Host HEALTHY. Dispose(). Child process receives
                                   SIGTERM → exits → socket removed. Host state
                                   = SHUTDOWN. No zombie processes.

H17 [HOST_FAILURE_CANNOT_CHANGE_
      RUNTIME_SEMANTIC_OUTCOME]  Runtime run in progress. Host crashes
                                   (CRASHED). Adapter returns empty array.
                                   Runtime run completes with its own outcome
                                   (failure due to UNKNOWN world).
                                   Host diagnostic (CrashCount, LastError) does
                                   NOT appear in SemanticRunResult.

H18 [GOLDEN_RUN_COMPATIBLE]      Existing live semantic golden run replays
                                   successfully with Host-managed vision service.
                                   Runtime regression 819/819 passes.
                                   Architecture Guards 16/16 pass.
```

### 10.2 Test infrastructure requirements

```text
H1-H16: Unit/integration tests in UniClaw.Vision.Host.Tests project.
        • Real Python process for H1, H6-H16.
        • Simulated missing files for H2-H5.
        • Test-only server config for H12.
        • Process.Kill() for H7, H8, H15.
        • Test socket paths in /tmp/uniclaw-vision-test-{guid}.sock.

H17:    Runtime integration test.
        • Mock IPerceptionSource that throws on Nth call.
        • Verify SemanticRunResult is independent of Host diagnostic.

H18:    Existing Runtime regression. No new test — existing suite must pass.
```

---

## REVIEW 11 — PHASE BOUNDARY

### 11.1 Authorized Phase 2 scope

```text
Phase 2 CREATES:
  • UniClaw.Vision.Host C# project
    - VisionServiceHost.cs
    - VisionServiceHostConfig.cs
    - VisionHostHealthState.cs (enum)
    - VisionHostDiagnostics.cs
    - SocketManager.cs
    - PythonEnvironmentValidator.cs
    - VersionNegotiation.cs
    - HostedPerceptionSource.cs (IPerceptionSource wrapper)
  • UniClaw.Vision.Host.Tests C# test project
    - H1–H16 test cases
  • Application composition update (Program.cs / Startup.cs)
    - Compose VisionServiceHost at startup
    - Pass Host.PerceptionSource to PhysicalEnvironment

Phase 2 MAY modify:
  • server.py: ONLY if required for Host compatibility.
    Specific allowed changes:
    - Read UNICLAW_VISION_SOCKET env var for UDS path (if not already supported)
    - Any bug fix that prevents correct /health or /version behavior
    Must remain backward-compatible: direct launch without Host must still work.

Phase 2 MUST NOT:
  • Move Python service files from tools/local_vision/.
  • Rewrite server.py beyond the strictly required Host compatibility fixes.
  • Change YOLO model.
  • Change OCR backend or configuration.
  • Change fusion algorithm.
  • Change label mapping.
  • Activate training pipeline.
  • Activate model registry.
  • Activate dataset governance.
  • Modify Runtime semantics.
  • Modify IEnvironment, Observation, ObservedElement, or any Runtime type.
  • Modify PhysicalEnvironment (except to accept IPerceptionSource from Host).
  • Modify LocalVisionPerceptionSource (except bug fixes for Host compatibility).
  • Add new Runtime ports or interfaces.
```

### 11.2 P4 closure strategy

```text
P4ClosureStrategy: Temp-file copy approach.

P4 (modelId changes when model file changes) was deferred from Phase 1
because mutating the production model was unsafe.

Strategy:
  1. Copy production model file to temp location.
     Path: /tmp/uniclaw-vision-test-{guid}/best.pt
  2. Launch vision service with UNICLAW_YOLO_MODEL={tempPath}.
  3. Query GET /version, record modelId_1.
  4. Shut down service.
  5. Modify one byte in temp model file (seek to offset 0, write different byte).
     This invalidates the model for inference but preserves SHA-256 change.
  6. Launch vision service with same temp path.
  7. Query GET /version, record modelId_2.
  8. Assert: modelId_1 ≠ modelId_2.
  9. Clean up temp directory.

  This test DOES NOT:
    • Mutate the production model.
    • Require the modified model to work for inference (/version is
      pre-inference; the SHA-256 is computed at server startup before
      YOLO model loading attempts inference).

  Integration into Phase 2: H4 extension or new H19 test case.
  Production model remains untouched.
```

---

## 12. Aggregate decision

```text
PERCEPTION_PLATFORM_PHASE_2_PROVIDER_HOST_REVIEW_RESULT
  = PURCHASE_WITH_CONSTRAINTS

HostPlacement:
  UniClaw.Vision.Host (separate C# project, Runtime.Adapters solution folder)

LifecycleOwner:
  VisionServiceHost (sole mutable state owner)

LifecycleStates:
  { COLD, WARMING, HEALTHY, UNHEALTHY, CRASHED, SHUTDOWN }
  DEGRADED removed — no evidence for partial-failure detection.

StartupContract:
  12-step sequence (refined from 14: compute modelId/configHash
  moved to post-launch observability recording, not pre-launch gating).

SocketOwnership:
  Process-specific UDS: /tmp/uniclaw-vision-{sessionGuid}.sock
  Host owns: create, pass to child, staleness detection, cleanup.
  Safety: never delete non-matching paths. Always verify staleness first.

RestartPolicy:
  Configurable sliding window budget: default 3 restarts / 60s.
  Backoff: 1s, 2s, 4s (capped at 30s).
  Hardcoded "5" replaced — no evidence for magic number.

FailurePropagation:
  HostedPerceptionSource returns empty array on any failure.
  Four operational diagnostic reasons (ServiceUnavailable, RequestTimeout,
  MalformedResponse, HttpError) recorded by Host, NOT visible to Runtime.
  HARNESS_OPERATIONAL_FAILURE episode on budget exhaustion, independent
  of Runtime outcome.

VersionNegotiation:
  Intersection-based: Adapter.SupportedSchemas ∩ Service.SupportedSchemas.
  modelId/configHash are observability facts, not compatibility gates.
  No coupled Runtime/Vision releases.

PythonEnvironmentOwnership:
  Deployment/setup owns provisioning (install, .venv, packages, models).
  Host owns validation + launch + monitor + shutdown.
  Host MUST NOT pip install, create .venv, download models, or train.

RequiredFalsifiers:
  18 tests: H1 (normal startup) through H18 (golden run compatibility).

P4ClosureStrategy:
  Temp-file copy approach. Modify one byte of copied model, verify modelId
  changes. Production model untouched.

RuntimeDelta:
  NONE

OwnershipDelta:
  UniClaw.Vision.Host (new C# project, not in Runtime)
  PhysicalEnvironment constructor: accepts IPerceptionSource from Host
  (existing parameter, no interface change)

AuthorityDelta:
  NONE

AuthorizedImplementationScope:
  Phase 2 Provider Host as specified in §11.1 of this review.
  Must satisfy all 6 constraints (refinements) from this review.
  Must pass all 18 falsifiers (H1–H18).
  Must close P4 via temp-file strategy.
  Full Runtime regression: 819/819 + Architecture Guards: 16/16.

ForbiddenScope:
  As specified in §11.1 (Phase Boundary) of this review.

NextTask:
  PROJECT_LEADER_PERCEPTION_PLATFORM_PHASE_2_IMPLEMENTATION
  (requires separate task authorization — implementation authority
   is NOT granted by this review)
```

`PERCEPTION_PLATFORM_PHASE_2_PROVIDER_HOST_REVIEW_RESULT`

STOP.
