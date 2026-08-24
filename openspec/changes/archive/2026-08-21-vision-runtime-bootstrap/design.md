# Design: vision-runtime-bootstrap

> BASELINE design (no code). Source-verified repository baseline: 2026-08-17.
> Cross-references: `docs/decisions/l1-assistance-real-world-validation.md` (B1),
> VISION_RUNTIME_CONFIGURATION_AUDIT result.

---

## 1. Verified source baseline

| Fact | Source |
|---|---|
| `VisionServiceHost` owns subprocess, socket, restart budget, health, identity verification, shutdown; socket = `{SocketDir}/uniclaw-vision-{sessionId}.sock`; launches `python -m uvicorn uniclaw_perception.server:app --uds {socket}`; env `UNICLAW_VISION_SOCKET` | `src/UniClaw.Vision.Host/VisionServiceHost.cs` |
| `CanonicalVisionHostFactory.Create(receiptPath, pythonExecutable, serviceEntryPoint, repoRoot, modelPath, configPath)` — canonical production composition only; requires receipt with `uniclaw.localVisionEvidence.v1` + modelId/configId/pipelineRevision/deploymentId | `src/UniClaw.Vision.Host/CanonicalVisionHostFactory.cs` |
| Production callers of the host/factory: NONE (only tests) | grep across `src/` |
| PhysicalHost consumes fixed `VisionSocketPath` default `/tmp/uniclaw-vision.sock` (CLI `--vision-socket`) with no launch owner | `src/UniClaw.Runtime.PhysicalHost/PhysicalHostOptions.cs:35`; `PhysicalHostComposition.BuildRealEnvironment` |
| System `python3` cannot import `uniclaw_perception` (ModuleNotFoundError); repository-managed `.venv-local-vision` exists with the required environment | probed 2026-08-17; `.venv-local-vision/bin/python` |
| Authoritative deployment receipt exists: `platforms/perception/governance/artifacts/current-active-identity.json` (mi.py `ACTIVE_IDENTITY`; schema `uniclaw.localVisionEvidence.v1`; modelId/configId/pipelineRevision/deploymentId) — the same file the Vision tests consume as their receipt | `platforms/perception/tools/model_intelligence/mi.py:27,83-86`; `tests/.../Vision/VisionHostFactoryCompositionTests.cs:14-16` |
| Perception server entry: `platforms/perception/uniclaw_perception/server.py` (FastAPI; lazy YOLO/OCR imports; `_model_id()` from health snapshot) | `platforms/perception/uniclaw_perception/server.py` |
| Vision host health: connect to socket + `/health` identity axes (`configId/pipelineRevision/deploymentId`) | `VisionServiceHost.cs`; `uniclaw_perception/health.py:88-91` |

---

## 2. Lifecycle owner (frozen)

**PhysicalHost application / composition root** owns the Vision lifecycle:

```
create → start → await readiness → inject endpoint → dispose/shutdown
```

`PhysicalHostComposition` MAY expose factory/composition helpers
(`ResolveVisionRuntimeConfiguration`, `CreateManagedVisionHost`) but MUST NOT
introduce hidden long-lived global process ownership. No other component
(Runtime.Agent, Environment, LocalVisionPerceptionSource, DriverHost, DSH,
AssistanceBridge) starts or owns the Vision process (F1).

---

## 3. Managed startup sequence (frozen)

```
PhysicalHost (Program.Main)
  ├─ ResolveDeviceAsync (existing preflight)
  ├─ ResolveVisionRuntimeConfiguration        (NEW: python + repo + receipt, fail early)
  │     · python executable (precedence §5)
  │     · repo/module root (PYTHONPATH/cwd = platforms/perception)
  │     · receipt = governance current-active-identity.json (§7)
  │     · early validation: executable exists; import resolves
  ├─ CanonicalVisionHostFactory.Create(receipt, pythonExecutable, repoRoot, ...)
  ├─ host.StartAsync() → VisionHostState.Healthy      (readiness §8)
  │     · failure ⇒ PhysicalHost initialization FAILS truthfully (F5)
  ├─ BuildRealEnvironment(..., visionSocketPath: host.SocketPath)   (endpoint = host OUTPUT, F2/F8)
  ├─ Runtime execution (Agent over real perception)
  └─ finally: host.Shutdown() / dispose               (no orphan, F6; session socket cleaned)
```

---

## 4. Managed vs External endpoint semantics

| Mode | Selection | PhysicalHost owns Vision process? | Endpoint source |
|---|---|---|---|
| **MANAGED** (default) | explicit default / `--vision-managed` | YES — VisionServiceHost lifecycle | `host.SocketPath` (host output, never guessed) |
| **EXTERNAL_ATTACH** | explicit `--vision-external <path>` (the existing `--vision-socket` buyer) | NO — consumes an externally managed endpoint | supplied path (validated reachable at readiness time) |

Modes are explicit and never inferred (F8). `/tmp/uniclaw-vision.sock` is no
longer the implicit managed truth; it remains a valid value ONLY as an explicit
EXTERNAL endpoint. No dual-source behavior.

---

## 5. Python runtime resolution (precedence, fail early)

1. explicit CLI/config (`--vision-python <path>`);
2. repository-managed development runtime (`.venv-local-vision/bin/python` —
   repository truth: the environment exists and is the one capable of importing
   `uniclaw_perception`);
3. else → actionable configuration error naming the expected runtime.

No silent fallback to system `python3` and a health-timeout wait (F3). Early
validation before launch where practical: executable exists; module import
resolves under the selected PYTHONPATH/cwd.

---

## 6. Repo / module resolution

`uniclaw_perception.server` resolves through the EXISTING mechanism: the managed
process is launched with working directory / PYTHONPATH pointing at
`platforms/perception` (repository truth: the package lives there; `-m uvicorn
uniclaw_perception.server:app` imports it). No second Python packaging mechanism is
invented (F10-adjacent).

---

## 7. Deployment receipt source — classification B

**B. Receipt deterministically reused from the existing deployment config.**

`CanonicalVisionHostFactory.Create` consumes
`platforms/perception/governance/artifacts/current-active-identity.json` — the
authoritative artifact already produced by the perception governance tooling
(mi.py `ACTIVE_IDENTITY`) and already consumed by the Vision tests as their
receipt. It carries `schemaVersion: uniclaw.localVisionEvidence.v1` and all four
identity axes. Receipt validation is preserved verbatim (F4); no fake constant
receipt is introduced. If the artifact is ever absent/malformed, `Create` throws
(fail closed) and PhysicalHost reports the configuration error.

**Not** classification D (RECEIPT_PRODUCTION_SOURCE_GAP): the source exists and is
authoritative.

---

## 8. Readiness contract

Reuse `VisionServiceHost` readiness: `StartAsync` → `Healthy` (socket reachable +
identity axes verified via the health endpoint). The bootstrap establishes:

```
VisionReady {
  endpoint                    = host.SocketPath
  processState                = host.State (Healthy)
  healthVerified              = host health probe passed
  deploymentIdentityVerified  = host.Facts (modelId/configId/pipelineRevision/deploymentId)
}
```

This is composition/runtime readiness state — NOT added to Runtime WorldBelief and
NOT a new Runtime semantic event (F7).

---

## 9. Failure and cleanup semantics

| Case | Behavior |
|---|---|
| python executable missing | early validation error (before launch) |
| module/import unavailable | early validation error (actionable: name the venv/PYTHONPATH) |
| receipt invalid/missing | `CanonicalVisionHostFactory.Create` throws → PhysicalHost init fails |
| process exits during startup | existing restart-budget semantics; exhausted → init fails |
| health timeout | existing HealthTimeout; init fails truthfully |
| socket never appears | readiness fails; host disposed |
| startup cancellation | host disposed; no orphan |
| PhysicalHost init failure after Vision started | managed host disposed (finally) |
| normal PhysicalHost shutdown | `host.Shutdown()` cleans session socket; process reaped |

Restart-budget supervision stays in `VisionServiceHost` — no duplicated
supervision (F6).

---

## 10. Test plan (APPLY gate — production path)

| # | Proof |
|---|---|
| T1 | configured real Vision Python (`.venv-local-vision`) launches the service (same resolution as production) |
| T2 | generated session socket is the EXACT endpoint injected into `LocalVisionPerceptionSource` (no guess, F2/F8) |
| T3 | health succeeds through the real process path (identity axes verified) |
| T4 | incompatible/missing Python fails deterministically (early, actionable) |
| T5 | module-resolution failure is actionable and bounded |
| T6 | invalid deployment receipt fails closed |
| T7 | managed host disposed on PhysicalHost shutdown |
| T8 | startup failure leaves no orphan process |
| T9 | existing Vision identity verification tests remain intact |
| T10 | `BuildRealEnvironment` does not independently guess the managed socket path |

The existing Vision host tests are repaired to use this SAME resolution (not
per-test hard-coded hacks — F9).

---

## 11. Architecture boundaries (frozen)

- Runtime.Agent — knows nothing about Vision process lifecycle.
- LocalVisionPerceptionSource — consumes an endpoint; starts no processes.
- VisionServiceHost — owns Vision subprocess supervision.
- PhysicalHost application root — owns composition lifecycle.
- DSH / Assistance — owns none of this (F10).

---

## 12. Deferred

- `--vision-socket` deprecation (separate change; compatibility preserved here).
- Receipt generation pipeline changes (artifact is authoritative).
- Any DSH/Assistance work (unchanged).
