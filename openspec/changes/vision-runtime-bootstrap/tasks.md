# Tasks: vision-runtime-bootstrap

> System of record. THIS GATE IS BASELINE ONLY (proposal/design/spec/tasks +
> validation). Implementation tasks are pending the APPLY gate.

## Slices (this gate)

- [x] Slice 0 — OpenSpec change scaffolding (proposal/design/spec/README/.openspec.yaml)
- [x] Slice 1 — Verified source baseline (host/factory/endpoint/python/receipt facts)
- [x] Slice 2 — Lifecycle owner freeze (PhysicalHost application/composition root)
- [x] Slice 3 — Managed startup sequence + readiness contract (VisionReady)
- [x] Slice 4 — Single endpoint source (host.SocketPath as output; managed vs
      EXTERNAL_ATTACH explicit; stale default retired from managed truth)
- [x] Slice 5 — Python runtime resolution (precedence + early validation;
      repository-managed venv; no system-python fallback)
- [x] Slice 6 — Repo/module resolution (existing PYTHONPATH/cwd mechanism)
- [x] Slice 7 — Deployment receipt reuse (classification B; governance artifact;
      validation preserved)
- [x] Slice 8 — Failure/cleanup semantics (no orphan; host disposed; restart budget
      preserved)
- [x] Slice 9 — Test plan T1–T10 (production path) + falsifiers F1–F10
- [x] Validation — openspec validate --strict, check-consistency.sh, audit +
      validation-doc cross-check

## Implementation plan (APPLY gate — EXECUTED 2026-08-17)

- [x] A1 — `VisionRuntimeBootstrap` (new): `ResolveVisionRuntimeConfiguration`
      (managed/external; python precedence CLI → .venv-local-vision → actionable
      error; repo/receipt from deterministic app root) + early validation +
      `CreateManagedVisionHost` (CanonicalVisionHostFactory + governance receipt)
- [x] A2 — `Program`: `BuildEnvironmentAsync` (managed: create → StartAsync →
      Healthy → host.SocketPath → BuildRealEnvironment; external: consume
      explicit endpoint) wired into all 4 proofs with finally dispose
- [x] A3 — `PhysicalHostOptions`: `VisionSocketPath` nullable (null = MANAGED;
      provided = EXTERNAL_ATTACH) + `--vision-python`
- [x] A4 — `BuildRealEnvironment(options, serial, visionSocketPath?)`: no implicit
      stale default; unresolved endpoint throws (T11)
- [x] A5 — Tests: `VisionRuntimeBootstrapTests` (T1–T14, 10 tests) + Vision host
      tests repaired through the production python resolution boundary
- [x] A6 — Historical Vision failures rechecked: configuration/lifecycle gap
      REPAIRED (real service now starts via venv python; CORR_HOST03 progressed
      from launch failure → PIPELINE axis verified); remaining = DEPLOYMENT
      identity drift (receipt is content-derived and stale relative to current
      perception code/model; PIPELINE axis synced to real computed value;
      DEPLOYMENT axis authoritative fix belongs to governance admission)
- [x] A7 — Real bootstrap smoke: managed path executes fully (service starts →
      real socket → health probe → identity verification intercepts truthfully);
      B1 = PARTIALLY_REPAIRED (identity-drift blocker remains)

## Falsifier mapping

- [x] F1 — lifecycle owner = PhysicalHost application root only
- [x] F2 — managed endpoint = host.SocketPath (never guessed)
- [x] F3 — no system-python silent fallback (fail early, actionable)
- [x] F4 — receipt validation preserved (governance artifact reused, no fake)
- [x] F5 — readiness failure fails initialization (no degraded continuation)
- [x] F6 — no orphan Vision process on any failure/shutdown path
- [x] F7 — readiness stays composition-level (no WorldBelief / Runtime event)
- [x] F8 — no dual-source endpoint (managed host socket == injected socket)
- [x] F9 — tests use the production resolution (no per-test hard-coded hacks)
- [x] F10 — no DSH/Assistance coupling to Vision lifecycle
