# Tasks — runtime-debugging-toolchain (umbrella)

## 1. Contracts (this change — no implementation)

^- [x] 1.1 Freeze the unified Data Model + Ref family + correlation keys + identity discipline (runtime-debug-data-model spec; AssetRef first-class)
^- [x] 1.2 Freeze the Query Core contract (six families; execution vs causal trees; prune-only; closed statuses)
^- [x] 1.3 Freeze the CLI/TUI single-core contract (shared Query/Analysis core; JSON canonical; no local reimplementation)
^- [x] 1.4 Freeze the Analysis + Skill-routing contract (structural facts first; packet machine-generable; FACT/INFERENCE/MISSING; implementation evidence gate)

## 2. Governance

^- [x] 2.1 Keep the umbrella at Human Gate (no apply); route slices (P1a/P2/…) to their own gates referencing these specs
^- [x] 2.2 Record missing-trace-field needs as TRACE_GAP for separate buyer gating (no toolchain-driven trace refactor)

## 3. Slice handoff (post-gate)

^- [x] 3.1 P1a `runtime-debug-p1a-summarize-occurrence` proceeds under its own gate (summarize + occurrence; P0 fixtures)
^- [x] 3.2 P1/P2 slices consume this umbrella's specs for run/trace/time/evidence/log/asset queries + packet generator + AssetRef indexing
^- [x] 3.3 P3 TUI consumes the same core (contract already fixed here)

## Design Docs

> Auto-generated from proposal Impact section.
> Implementation agents: read these before starting.

| Module | Design Doc |
|--------|------------|
| `docs/analysis/runtime-debugging-capability-p0-contract.md` | canonical P0 input (frozen, referenced) |
| `.ai/skills/evidence-driven-debugging/references/runtime/*` | canonical Debug IR / Evidence Packet / tooling contracts (frozen, referenced) |
| `tools/runtime_debug/` | `openspec/changes/runtime-debugging-toolchain/design.md` + P1a change |