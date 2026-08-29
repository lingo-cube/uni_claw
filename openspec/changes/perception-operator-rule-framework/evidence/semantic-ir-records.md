# Development Semantic IR Records — perception-operator-rule-framework

## IR-S1A (tasks S1.1–S1.4 — framework core)

- **DesiredReality**: perception-internal operator/rule framework core exists: registry &
  contract types (authority class, typed IO, bounded params with validator-safe
  direction, fail-closed contract, trace), selector model (5 dims + tags, canonical
  values, `default`), specificity-cascade resolver with intersection-scoped conflict
  detection (order-independent, per-value provenance), deterministic rule-set
  serialization + loader/linter — as NEW modules with property tests, with ZERO wiring
  into the current fusion pipeline (wiring/port is S1B; this slice changes no behavior).
- **ClaimUnderTest**: the framework core is addable with zero pipeline behavior change
  and deterministic, explainable resolution semantics.
- **ExistingEvidence**: spec requirements (Operator contract / Authority classes /
  Selector dimensions / Cascade / Governed assets); CSS specificity model; existing
  perception test suite (incl. `tests/test_navigation_row_composition.py`, 27 tests).
- **EvidenceGap**: no framework code exists.
- **GapKind**: `TOOLING_GAP` (perception-internal infra, now Human-authorized:
  `APPROVED_S1_S2_S4`, Gate #2).
- **ObservedReality**: fusion pipeline is monolithic (`fusion/engine|heuristics|
  row_grouping|scoring.py`) with no operator abstraction, no rule binding.
- **FirstDivergencePoint**: greenfield absence — no Runtime/perception behavior diverges.
- **Owner**: perception infra (`platforms/perception`).
- **ExcludedOwners**: Runtime (forbidden this batch), CURRENT-ACTIVE governance state,
  Phase 2.6 lifecycle.
- **AllowedChange**: NEW files under `platforms/perception/uniclaw_perception/operators/`
  + NEW tests under `platforms/perception/tests/`.
- **ForbiddenChange**: editing `fusion/*.py` behavior (S1B scope), `server.py` request
  surface (context header is S1B-wired), Runtime, governance artifacts, CURRENT-ACTIVE.
- **AcceptanceEvidence**: new framework property tests green; FULL perception suite
  green (proving zero behavior change); deterministic serialization round-trip.
- **StopCondition**: core semantics cannot be expressed without pipeline/Runtime edits.
- **SemanticResolution**: RESOLVED.

## IR-S1B (tasks S1.6 + S1.8 — port + verifier + trace; after S1A)

PENDING (depends on S1A types).

## IR-S1C (tasks S1.5 + S1.7 — governance binding + equivalence regression; after S1B)

PENDING. Note: equivalence baseline = fixture corpus from the repair's 27-test file +
v1n false-positive fixtures; archived campaign frames are Runtime observations (no raw
fusion inputs) — cross-UI real-frame validation belongs to S2 acceptance per Gate #2.
