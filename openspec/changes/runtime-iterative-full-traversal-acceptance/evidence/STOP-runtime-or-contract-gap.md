# STOPPED_AT_RUNTIME_OR_CONTRACT_GAP — Stage G (Phase 2.6A real-tree traversal)

> Status: **FINAL** (2026-08-27). The collection-fidelity and adaptation increments landed
> and were independently verified;
> the full 4-round adaptive campaign executed honestly on the real emulator
> (`G-stage-a/stageAB-adaptive-campaign.json`) — every round fail-closed at the same
> composition gap, confirming the stop determination below.

## Expected Reality

The Phase 2.6 hypothesis (per `docs/decisions/runtime-full-traversal-acceptance-analysis.md`):
the graduated recursive open-world machine (strategy wire → recursive descent → scroll
exhaustion → identity-exact ledger) can autonomously exhaust a real bounded Android
Settings UI tree on the Real Emulator, with validation-only tooling around it. The first
conservative run was expected to resolve the Settings root page, inventory its rows,
scroll-exhaust the viewport, and descend one level (depth 1).

## Observed Reality

Every real-Settings run on the strategy wire fails closed at the FIRST completeness proof:

```
RunFailed: "Source normalization is unresolved; completeness cannot be proven."
```

- Composition chain itself works: admission ✓, autonomy ✓ (exactly one accepted
  `run.strategy.start`, zero post-admission driver calls), invariants ✓, real launch ✓,
  real vision perception ✓, real scroll dispatched ✓, real observations consumed (13 frames
  on the pixel-profile AVD), truthful terminal + events collected (after the G1
  collection-fidelity fix).
- The Runtime then correctly refuses to normalize the root viewport inventory.

## Reality Gap

The production perception pipeline emits MULTIPLE fused `menu_item` candidates for the SAME
visual row (icon box + text box both fused with the same OCR text), producing identical
in-frame navigation signatures (`Text|PerceptionType`). The Runtime's frozen normalization
contract (SourceEquivalenceNormalizer: duplicate in-frame signatures → Unresolved →
completeness cannot be proven) fail-closes. Neither side misbehaves in isolation; the
COMPOSITION has never been exercised before (the proposal itself recorded that the
recursive machine "has never executed on the strategy wire against a real unknown tree").

## Evidence Reference

| # | Evidence | Location |
|---|---|---|
| E1 | r1c run (scroll-test AVD): 4 events + truthful `Failed: Source normalization is unresolved` | `/tmp/p26-stageA-r1c.json` (archived to `evidence/G-stage-a/`) |
| E2 | r1e run (p26_pixel AVD, real-device geometry 1080x2400@420): 13 observed frames, same fail-closed terminal | `/tmp/p26-stageA-r1e.json` + `/tmp/p26-frames.json` |
| E3 | Frames: duplicate same-text menu_item rows on BOTH AVDs (scroll-test: `Passwords…` ×2, `About…` ×3; pixel: `Network & internet` ×3, `Notifications` ×4) — refutes the AVD-rendering hypothesis | `/tmp/p26-frames.json` |
| E4 | WIRELESS_SETTINGS subpage probe: duplicates persist on a small page (`Internet` ×3, `SIMs` ×2, `Hotspot` ×2) — refutes the small-scope hypothesis | probe transcript in `G-stage-a/probe-subpage.txt` |
| E5 | Raw YOLO output for the same screenshot: NO duplicate menu labels (7 icon + 17 text_block + 1 switch; one overlapping text_block pair IoU 0.41 conf 0.80/0.43) — the duplicate `menu_item` candidates are produced by the FUSION layer assigning the same OCR text to each per-row box | probe transcript in `G-stage-a/probe-raw-vs-fused.txt` |
| E6 | Fusion dedup exists ONLY for switch/toggle and raw-pixel regions, not menu_item rows | `platforms/perception/uniclaw_perception/fusion/heuristics.py:359,542-548` |
| E7 | Signature construction: vision occurrence identity = `Text|PerceptionType`; per-occurrence evidence emission (`GroupBy(OccurrenceId)`) means each fused duplicate box is an independent NavigationCandidate occurrence | `src/UniClaw.Runtime/World/SourceEquivalenceNormalizer.cs` (BuildSignature), `src/UniClaw.Semantic.Settings/SettingsSemanticCapability.cs` (InterpretAsync) |
| E8 | Frozen normalization precondition: duplicate in-frame signatures → Unresolved (graduated fail-closed contract) | `src/UniClaw.Runtime/World/SourceEquivalenceNormalizer.cs` (Normalize) |
| E9 | Graduated precedents never exercised this composition: real-device Phase-2 walked a FIXED 31-step plan with its own first-seen inventory (no open-world normalization); fixture capstone used a purpose-built single-detection app; TREE capstone used a synthetic perfect world | `tests/UniClaw.Runtime.Tests/Scenario/SettingsSingleRecursiveChildTests.cs:700-760`, TierBProgram, SettingsTreeCapstoneTests |
| E10 | No alternative governance config exists (single config manifest, confidence 0.35); deployments share it | `platforms/perception/governance/artifacts/config-manifests/` |

## First Divergence Point

The first observation of the first run: the in-frame navigation-signature set already
contains duplicates (production perception fusion output), colliding with the Runtime's
frozen one-signature-per-source-per-frame normalization precondition. Divergence is
pre-execution: no dispatch beyond the initial scroll ever became provable.

## Owner

- **FDP spans two production layers, neither owned by this change:**
  - Production perception pipeline (`platforms/perception` fusion heuristics) — emits
    duplicate per-row candidates;
  - Runtime frozen normalization contract (`src/UniClaw.Runtime` SourceEquivalenceNormalizer
    precondition) — correctly refuses ambiguity.
- Harness-side levers (scope root, launch intent, binding, AVD, deployment selection) were
  EXHAUSTED (E3, E4, E10) — none can avoid the collision.

## Missing Capability

One of (Human decision required):
1. **Perception-side row-level deduplication** (production `platforms/perception` change:
   extend fusion dedup to navigation-row candidates via INTRA-FRAME LAYOUT CLUSTERING).
   **FEASIBILITY VALIDATED OFFLINE on the captured real frames**
   (`G-stage-a/probe-layout-clustering-feasibility.md`): 30/30 duplicate navigation
   occurrences clustered away, 0 distinct rows mis-merged, >5x separation margin
   (same-row duplicate gaps ≤ 0.010 vs distinct-row gaps ≥ 0.053). Intra-frame row
   clustering does NOT touch the frozen cross-frame identity contract (bounds stay
   excluded from identity; the two concerns are orthogonal). Estimated change size:
   one fusion pass + tests.
2. **Runtime-side normalization tolerance** (contract change: allow N identical in-frame
   signatures as one logical source — weakens the PROV ambiguity fail-closed; requires
   OpenSpec + Human Gate; risks false-merging genuinely duplicate controls);
3. **A different perception deployment/model** proven (with evidence) to emit
   single-detection rows on real Settings (none exists today — E10);
4. **Accept the boundary**: real-tree full traversal on the strategy wire is BLOCKED at the
   current graduation composition; Phase 2.6B is not achievable without one of 1–3.

## Authority Impact

- No change was made to any production layer (Runtime byte-identity verified against the
  baseline manifest; perception pipeline untouched; frozen SHA guards green).
- All Phase 2.6 work products so far are validation-side and remain valid: campaign runner,
  knowledge fixture + persistence, PlanDelta validator, SettingsStrategyBinding, the real
  campaign composition (with G1's collection-fidelity fix), and the G2 adaptation planner.
- The four frozen invariants and single-run autonomy were asserted and PASSED on every real
  run executed — the stop is not an autonomy or authority failure; it is a capability
  boundary of the frozen composition on real trees.

## What this means for the Phase 2.6 goals

- Goal 1 (upper-agent iterative planning): achievable in reduced form — fail-closed
  Results are real Results; knowledge (KnownUnresolved root-inventory) and PlanDeltas
  (scope/root changes) are expressible — but "behaviorally visible improvement" cannot be
  demonstrated when every scope fails identically; ≥3 GENUINE adaptations with visible
  strategy-behavior differences are not achievable on this composition.
- Goal 2 (persisted knowledge reuse): the fixture/persistence machinery is built and
  tested; reuse can be demonstrated only for "start elsewhere/record unresolved" knowledge,
  which is thin without a traversable scope.
- Goal 3 (full traversal, 2.6B): BLOCKED outright — no reachable real Settings scope
  normalizes (E4), and recursive descent requires normalization at EVERY level.
- Goal 4 (RestartRequiredAdvisoryCase evidence): partially collectable — the
  normalization-unresolved terminal is itself a candidate advisory case ("is this a
  duplicate detection or two identical controls?" — a question Runtime cannot decide alone
  but a UniAgent advisory checkpoint could).

## Stop decision

Per §18 of the implementing instruction (and the spec's stop-condition requirement):
continuing would require modifying an unauthorized production layer. Stop. Await Human Gate.
