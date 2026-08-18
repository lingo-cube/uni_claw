# Change: vision-runtime-bootstrap

> **BASELINE** for the minimum production Vision runtime bootstrap: make the
> EXISTING Vision implementation reproducibly available to PhysicalHost. No Vision
> redesign, no perception-semantics change, no DSH/Assistance coupling.

## What it defines

The production lifecycle owner (PhysicalHost application/composition root), the
managed startup sequence (`CanonicalVisionHostFactory` → `VisionServiceHost` →
HEALTHY → `host.SocketPath` → `BuildRealEnvironment`), single endpoint source
(host output, never guessed), explicit managed vs EXTERNAL_ATTACH modes, Python
runtime resolution (repository-managed venv, fail early), repo/module resolution
(existing PYTHONPATH/cwd), deployment receipt reuse (classification B —
`governance/artifacts/current-active-identity.json`, validation preserved), the
`VisionReady` readiness contract (composition-level, not Runtime semantics), and
failure/cleanup semantics (no orphan, host disposed, restart budget preserved).

## Scope guardrails

- **BASELINE only**: no code; implementation is the APPLY gate.
- Lifecycle owner frozen: PhysicalHost application root (F1).
- Managed endpoint = host.SocketPath, never guessed (F2/F8).
- No system-python silent fallback (F3); no receipt bypass/fake receipt (F4);
  no degraded continuation on readiness failure (F5); no orphan (F6); readiness
  stays composition-level (F7); tests use the production path (F9); no DSH
  coupling (F10).
- `--vision-socket` compatibility preserved (deprecation is a separate change).

## Documents

- `proposal.md` — buyer/gap/scope/falsifiers
- `design.md` — lifecycle owner, startup sequence, endpoint modes, python/repo/
  receipt resolution, readiness, failure/cleanup, test plan, boundaries
- `specs/vision-runtime-bootstrap/spec.md` — requirements + scenarios
- `tasks.md` — baseline slices + APPLY implementation plan

## Next gate (planning context)

`PROJECT_LEADER_APPLY_VISION_RUNTIME_BOOTSTRAP` (implementation A1–A4), then the
L1 real-world validation environment blockers B2/B3 (DSH application + model
credentials) are addressed separately.
