# Proposal: vision-runtime-bootstrap

## Buyer

The real runtime has NO production Vision service: `PhysicalHost` connects a fixed
default endpoint (`/tmp/uniclaw-vision.sock`) that nothing launches, while the
complete Vision implementation (`VisionServiceHost` + `CanonicalVisionHostFactory`
+ Python perception service) exists and is exercised only by tests. The
VISION_RUNTIME_CONFIGURATION_AUDIT classified this as
`VISION_CONFIGURATION_AND_LIFECYCLE_GAP` with earliest missing system link
`PRODUCTION_VISION_SERVICE_LAUNCH_OWNER + CANONICAL_VISION_RUNTIME_CONFIGURATION`.

This change purchases ONLY `VISION_RUNTIME_BOOTSTRAP`: the minimum production
composition that makes the EXISTING Vision implementation reproducibly available
to `PhysicalHost`. No Vision redesign, no perception-semantics change, no DSH /
Assistance coupling.

## Gap (verified repository truth)

- `VisionServiceHost` (self-managed subprocess host: `python -m uvicorn
  uniclaw_perception.server:app --uds {SocketDir}/uniclaw-vision-{sessionId}.sock`,
  health/restart-budget/identity-verification/shutdown) has ZERO production callers
  — only tests.
- `PhysicalHostComposition.BuildRealEnvironment` consumes
  `options.VisionSocketPath` (default `/tmp/uniclaw-vision.sock`, a stale
  hard-coded value) with no owner guaranteeing anything listens there.
- Socket paths diverge: PhysicalHost fixed default ≠ VisionServiceHost sessionized
  path → managed host would create socket A while the client connects socket B.
- System `python3` cannot import `uniclaw_perception` (verified:
  `ModuleNotFoundError`), while the repository-managed runtime
  `.venv-local-vision` exists with the required environment.
- `CanonicalVisionHostFactory` requires a deployment identity receipt; the
  authoritative production artifact ALREADY exists:
  `platforms/perception/governance/artifacts/current-active-identity.json`
  (mi.py `ACTIVE_IDENTITY`; carries schemaVersion `uniclaw.localVisionEvidence.v1`
  + modelId/configId/pipelineRevision/deploymentId — exactly the axes the factory
  validates; the Vision tests already consume this same file).

**Earliest missing system link**: `PRODUCTION_VISION_SERVICE_LAUNCH_OWNER` +
`CANONICAL_VISION_RUNTIME_CONFIGURATION`.

## What this change does (BASELINE — design/spec only, APPLY later)

1. Freezes the production lifecycle owner: the **PhysicalHost application /
   composition root** — create → start → await readiness → inject endpoint →
   dispose. No hidden long-lived global ownership; Runtime.Agent / Environment /
   LocalVisionPerceptionSource / DriverHost / DSH / AssistanceBridge own nothing
   of the Vision lifecycle.
2. Defines the **managed startup sequence**: resolve Vision runtime config →
   `CanonicalVisionHostFactory.Create(...)` → `host.StartAsync()` → HEALTHY →
   `host.SocketPath` → `BuildRealEnvironment(host.SocketPath)` → runtime execution.
   Vision startup/readiness failure ⇒ PhysicalHost initialization fails
   truthfully (no silent degraded continuation).
3. **Single configuration source**: in managed mode the socket path is an OUTPUT
   of `VisionServiceHost` (`host.SocketPath`) — never guessed by PhysicalHost.
   `/tmp/uniclaw-vision.sock` ceases to be the implicit managed-production truth.
   The existing `--vision-socket` becomes an explicit
   **EXTERNAL_ATTACH_MODE** (consume an externally managed endpoint, own no Vision
   process); the two modes are explicit, never inferred.
4. **Python runtime resolution** with precedence: explicit CLI/config →
   repository-managed development runtime (`.venv-local-vision`) → actionable
   configuration error (never silent fallback to incompatible system python and a
   health timeout). Early validation: executable exists + module importable.
5. **Repo/module resolution**: `uniclaw_perception.server` resolves through the
   existing mechanism (working directory / PYTHONPATH = `platforms/perception`);
   no second packaging mechanism.
6. **Deployment receipt**: reuse the existing canonical governance artifact
   (`current-active-identity.json`) — classification **B** (deterministic reuse of
   existing deployment config). Receipt validation is NOT bypassed or weakened; no
   fake constant receipt.
7. **Readiness contract**: `VisionReady { endpoint, processState, healthVerified,
   deploymentIdentityVerified }` as composition/runtime readiness — NOT added to
   Runtime WorldBelief, NOT a new Runtime semantic event.
8. **Failure and cleanup**: fail early where possible; no orphan Vision process;
   managed host disposed; session endpoint cleaned per existing VisionServiceHost
   shutdown behavior; existing restart-budget semantics preserved (no duplicated
   supervision).
9. **Tests use the production path**: the existing Vision host tests are repaired
   to exercise the SAME runtime resolution (`.venv-local-vision` + PYTHONPATH),
   not per-test hacks.

## Non-goals

- Vision redesign / perception-semantics changes.
- DSH or Assistance coupling to Vision lifecycle.
- New RuntimeEvent kinds; Runtime semantic changes.
- Removing `--vision-socket` compatibility in this change (deprecation is separate).
- Receipt generation pipeline changes (the artifact exists and is authoritative).

## Required output

`PROJECT_LEADER_VISION_RUNTIME_BOOTSTRAP_BASELINE_RESULT` with Decision
`VISION_RUNTIME_BOOTSTRAP_READY_FOR_APPLY`, the OpenSpec change
(proposal/design/spec/tasks) created and validated, and `NEXT_GATE =
PROJECT_LEADER_APPLY_VISION_RUNTIME_BOOTSTRAP`.

## Falsifiers

| # | Falsifier | Fails if |
|---|---|---|
| F1 | wrong lifecycle owner | any component other than the PhysicalHost application root starts the Vision process |
| F2 | guessed managed endpoint | managed mode connects anything other than `host.SocketPath` |
| F3 | system-python fallback | incompatible python is silently used and the health timeout is waited out |
| F4 | receipt bypass | receipt validation is weakened or a fake constant receipt is invented |
| F5 | degraded continuation | Vision startup/readiness failure is silently ignored (perception continues unavailable) |
| F6 | orphan process | a failed startup or PhysicalHost shutdown leaves a Vision subprocess alive |
| F7 | readiness in Runtime semantics | Vision readiness enters WorldBelief or a new Runtime event |
| F8 | dual-source endpoint | managed host creates socket A while the client connects socket B |
| F9 | per-test hacks | Vision tests are fixed with isolated hard-coded paths instead of the production resolution |
| F10 | DSH/Assistance coupling | Vision lifecycle depends on DSH or the Assistance chain |

## Validation

- `openspec validate vision-runtime-bootstrap --strict --no-interactive`
- `scripts/check-consistency.sh`
- Cross-check against `docs/decisions/l1-assistance-real-world-validation.md` (§1
  blockers B1) and the vision audit record.
