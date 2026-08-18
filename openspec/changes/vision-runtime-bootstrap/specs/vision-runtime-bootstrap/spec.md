# Spec: vision-runtime-bootstrap

> BASELINE spec for the minimum production Vision runtime bootstrap. No code in
> this change. Cross-ref: vision audit + `l1-assistance-real-world-validation.md`
> (blocker B1).

## ADDED Requirements

### Requirement: Production lifecycle owner

The PhysicalHost application / composition root MUST own the Vision lifecycle
(create → start → await readiness → inject endpoint → dispose/shutdown). No other
component (Runtime.Agent, Environment, LocalVisionPerceptionSource, DriverHost,
DSH, AssistanceBridge) MAY start or own the Vision process.

#### Scenario: application root owns managed host

Given PhysicalHost startup,
When Vision is managed,
Then the application root creates, starts, awaits readiness, injects the endpoint,
and disposes the managed host — and no other component launches a Vision process.

### Requirement: Managed startup sequence

The bootstrap MUST follow: resolve Vision runtime config → `CanonicalVisionHostFactory.Create`
→ `host.StartAsync()` → HEALTHY → `host.SocketPath` → `BuildRealEnvironment(host.SocketPath)`
→ runtime execution. Vision startup/readiness failure MUST fail PhysicalHost
initialization truthfully (no silent degraded continuation).

#### Scenario: readiness failure fails initialization

Given a Vision service that cannot reach HEALTHY,
When the bootstrap runs,
Then PhysicalHost initialization fails truthfully and no runtime executes against
an unavailable perception source.

### Requirement: Single endpoint source (managed)

In managed mode the socket path MUST be an OUTPUT of `VisionServiceHost`
(`host.SocketPath`) — PhysicalHost MUST NOT independently guess it. `/tmp/uniclaw-vision.sock`
MUST cease to be the implicit managed-production truth.

#### Scenario: injected endpoint equals host output

Given a HEALTHY managed host,
When `BuildRealEnvironment` is composed,
Then its perception source consumes exactly `host.SocketPath`.

### Requirement: Managed vs External modes are explicit

The existing `--vision-socket` buyer MUST be classified as explicit
EXTERNAL_ATTACH_MODE (consume an externally managed endpoint, own no Vision
process); managed mode is the default. Modes MUST NOT be inferred ambiguously.

#### Scenario: external attach owns no process

Given EXTERNAL_ATTACH_MODE with a supplied endpoint,
When PhysicalHost runs,
Then it consumes the supplied endpoint and owns no Vision process.

### Requirement: Python runtime resolution

The bootstrap MUST resolve the Python executable by precedence: explicit
CLI/config → repository-managed development runtime (`.venv-local-vision`) →
actionable configuration error. Incompatible system python MUST NOT be silently
used while waiting out a health timeout. Early validation (executable exists;
module importable) is preferred.

#### Scenario: missing venv fails early and actionably

Given a missing/incompatible configured Python,
When the bootstrap resolves the runtime,
Then it fails early with an actionable configuration error (naming the expected
repository-managed runtime) instead of a health-timeout wait.

### Requirement: Repo/module resolution

`uniclaw_perception.server` MUST resolve through the existing supported mechanism
(working directory / PYTHONPATH pointing at `platforms/perception`); no second
Python packaging mechanism MAY be invented.

#### Scenario: module resolves through the existing mechanism

Given the managed process launch,
When the Python runtime starts,
Then `uniclaw_perception.server` resolves via working directory / PYTHONPATH =
`platforms/perception` — no new packaging or install step is introduced.

### Requirement: Deployment receipt reuse (classification B)

The bootstrap MUST reuse the existing canonical governance artifact
(`platforms/perception/governance/artifacts/current-active-identity.json`) as the
deployment receipt. Receipt validation MUST NOT be bypassed or weakened; no fake
constant receipt MAY be created.

#### Scenario: invalid receipt fails closed

Given a missing or malformed current-active-identity.json,
When the bootstrap creates the managed host,
Then `CanonicalVisionHostFactory.Create` throws and PhysicalHost reports the
configuration error (receipt validation preserved).

### Requirement: Readiness contract

The bootstrap MUST establish `VisionReady { endpoint, processState, healthVerified,
deploymentIdentityVerified }` as composition/runtime readiness — NOT added to
Runtime WorldBelief, NOT a new Runtime semantic event.

#### Scenario: readiness is composition-level

Given a HEALTHY managed host,
When the bootstrap completes,
Then the readiness record carries endpoint/process/health/identity facts at the
composition level and no Runtime WorldBelief or Runtime event is affected.

### Requirement: Failure and cleanup

No orphan Vision process MAY remain on any failure path; the managed host MUST be
disposed; the session endpoint MUST be cleaned per existing VisionServiceHost
shutdown behavior; existing restart-budget semantics MUST be preserved without
duplicated supervision.

#### Scenario: no orphan on startup failure

Given a Vision startup that fails,
When the bootstrap unwinds,
Then the managed host is disposed and no Vision subprocess remains.

### Requirement: Tests use the production path

The Vision host tests MUST be repaired to exercise the SAME runtime resolution as
production (`.venv-local-vision` + PYTHONPATH/cwd), not per-test hard-coded hacks.

#### Scenario: tests resolve through the production path

Given the repaired Vision host tests,
When a test launches the real service,
Then it uses the same python executable and module resolution as production, and
no per-test hard-coded runtime path exists.

## MODIFIED Requirements

None. This change modifies no existing spec or implementation.
