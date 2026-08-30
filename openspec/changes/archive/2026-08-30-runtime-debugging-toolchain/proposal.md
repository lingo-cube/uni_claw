## Why

Runtime failures keep needing Leader hand-crafting of prompts ("读哪个 JSON、按哪个 seq、找哪个 StableKey、怎么看 trace、截图在哪里"). The P0 contract froze the data-level pieces (Debug IR v0, Evidence Packet v0, occurrence correlation, differential, tooling contract, five-case mapping, skill routing) as non-authoritative work contracts. This change is the approved umbrella for building the Runtime Debugging Toolchain as a reusable Developer Tooling capability: a single deterministic Query/Analysis core shared by CLI, TUI, and Agent skill, with AssetRef as a first-class reference into the evidence chain. It is a long-lived Large capability; implementation proceeds only through sliced changes (P1a etc.), each under its own gate.

## What Changes

- Freeze the unified Debug Data Model and Ref model (RunRef … StateRef) with the correlation keys and identity discipline (`StableKey != SameOccurrence proof`, `RowId != SameSource proof`, `Bounds != Identity`, `Text != Identity`) — query/candidate correlation only, never Runtime authority.
- Freeze **AssetRef** as a first-class citizen: screenshots / frames / crops / stage images / overlays / logs / replay fixtures enter Evidence / Trace / Debug IR query chains by reference (never copied into the IR), with the required projections EvidenceRef→AssetRef, Observation→frame/screenshot, Occurrence→crop/overlay.
- Define the read-only deterministic **Debug Query Core** contract: Run / Trace (execution vs causal trees) / Time / Evidence / Log / Asset query families; query-time pruning only, hidden ≠ deleted.
- Define the **CLI contract** (`runtime-debug` command surface, closed statuses, JSON canonical) and the **TUI architecture** (P3 — consumes the same core, never reimplements analysis; no framework pre-selected).
- Define the **Analysis** direction (structural facts first → Debug IR → Agent diagnosis; FACT / INFERENCE / MISSING_EVIDENCE separation), the **Skill routing** trigger (E2/E3/E4 failures auto-load runtime debugging workflow), and the **P0–P5 roadmap** with the first vertical slice.
- Everything stays READ_ONLY / DETERMINISTIC / NO_RUNTIME_AUTHORITY / NO_TRACE_MUTATION; no Runtime authority, execution-semantic, Trace-model, wire, or Phase-2.6 traversal change; missing trace fields are recorded as TRACE_GAP for a separate gate.

## Capabilities

### New Capabilities

- `runtime-debug-data-model`: unified Ref model, correlation keys, identity discipline, and AssetRef schema — the query/evidence vocabulary (non-authoritative; explicitly non-authority).
- `runtime-debug-query-core`: read-only deterministic Query Core contract (six query families, prune-only, fail-closed).
- `runtime-debug-tooling-surface`: CLI contract + TUI architecture that share one Query/Analysis core; closed statuses; JSON-canonical output.
- `runtime-debug-analysis-contract`: structural-facts-first analysis direction, Evidence Packet machine-generation, differential rules, and Skill routing trigger.

### Modified Capabilities

None.

## Impact

- New umbrella spec under `openspec/specs/runtime-debug-*`; the frozen P0 work-contract files (`docs/analysis/runtime-debugging-capability-*.md`, `.ai/skills/evidence-driven-debugging/references/runtime/*`) remain canonical input, referenced not copied.
- Implementation slices live under `tools/runtime_debug/` (Python stdlib-only) with their own changes (P1a `runtime-debug-p1a-summarize-occurrence` already gated) — this change freezes contracts only.
- No change to Runtime, Harness, DriverHost, PhysicalHost, Trace model, wire contract, production dependency graph, or authority.

## Timeline

N/A (long-lived capability; sliced implementation gate by gate).