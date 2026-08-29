## Context

Authority: Runtime Architecture Contract I-1..I-14; frozen
`SourceEquivalenceNormalizer`/`Container` viewport acceptance; the discovery-epoch
freeze philosophy (`DiscoveryEpochState`: post-proof same-Container evidence is
consistency-only, never appended to discovery inputs); Human Gate
`PROJECT_LEADER_RUNTIME_VIEWPORT_EXHAUSTION_CONFIRMATION_CONTRACT_GATE` (2026-08-28);
STOP-2 + reentry-runs 1–6 evidence in
`../runtime-iterative-full-traversal-acceptance/evidence/` (STOP-2 file + `G-stage-a/reentry/`).

## 1. Evidence → FDP → Owner (rebuilt, per ruling §1)

- **Evidence**: run-6 (archived `reentry/run6.json` + `run6-frames.json`): clean root
  composition, 7 viewport decisions (obs 2,5,8,11,14,16,18), 6 scrolls, final windows
  16/18 byte-identical menu lists; honest `Source normalization is unresolved` at the
  exhaustion proof. Run-1 passed the same shape only via frame-to-frame PerceptionType
  flips (signatures differed by luck).
- **FDP**: the first zero-new-source post-scroll confirmation window enters
  `Container.ViewportExplorationObservations` (via `TryVerifyViewportContinuity`,
  Container.cs:380 / `TryAcceptVerifiedContinuity`, :241 — unconditional append) and
  violates `Normalize`'s extension assumption: each subsequent window must overlap
  suffix(union-so-far) ↔ prefix(window) uniquely (`FindUniqueSuffixPrefixOverlap`
  returns null on zero OR multiple match lengths — SourceEquivalenceNormalizer.cs).
- **Owner**: Runtime / World normalization + the Agent completeness consumer.
  Harness levers exhausted (STOP-2 §"harness-side levers"): the binding evaluator
  cannot honestly predict exhaustion; "keep scrolling" appends more identical windows;
  altering observations is truth injection (forbidden).
- **Not perception instability / not a harness bug**: the perception chain is locked
  green (247+93 tests, byte-equivalence gate, real-geometry regressions); the failure is
  deterministic on STABLE observations — exactly the condition the contract must support.

## 2. Minimal closed semantics (ruling §2)

Per window i>0 over the accepted sequence, classify:

- **EXTENDING_WINDOW**: existing unique-overlap rule holds (unchanged, including its
  ambiguity/absence = non-extending outcomes).
- **CONSISTENT_CONFIRMATION_WINDOW** iff ALL minimal sufficient conditions (§3) hold.
- **UNRESOLVED_WINDOW**: otherwise — the whole normalization stays fail-closed exactly
  as today.

`zero new source` alone is NEVER equivalent to exhausted — it is necessary, never
sufficient.

## 3. CONSISTENT_CONFIRMATION — minimal sufficient conditions (source-derived)

All must hold; any miss → UNRESOLVED_WINDOW:

1. **Fresh observation**: sequence strictly greater than the previous accepted window
   (already enforced by the container continuity gate that admitted it).
2. **Stable viewport**: the window's ordered navigation-signature sequence is
   ELEMENT-WISE IDENTICAL to the immediately preceding accepted window's sequence
   (zero scroll motion; byte-stable `Text|PerceptionType` signatures).
3. **Provable tail correspondence**: the window's signature sequence is a contiguous
   SUFFIX of the accumulated canonical union order (the visible window shows the
   accumulated list's END, with the same ordering).
4. **Zero genuinely new logical sources**: every window source already exists in the
   union (follows from 2+3; asserted explicitly for the evidence record).
5. **No identity/type conflict, no ambiguous alignment**: signature equality is exact
   (identity key unchanged); no duplicate-signature within the window (existing
   in-frame rule already enforces); the tail alignment of 3 is unique by construction
   (suffix of the same accumulated order).
6. **Bounded repetition**: at most `MaxConsecutiveConfirmationWindows = 2` consecutive
   confirmation windows (derived bound: the loop's settle structure produces the
   candidate+confirmed pair; a third consecutive zero-motion window adds no information
   and stays resolvable only under the same classification — the bound keeps the
   evidence finite and matches the existing two-observation confirmation discipline).
   A further consecutive confirmation → UNRESOLVED (fail-closed).

## 4. Invariants preserved (ruling §4 — normative, restated in spec)

- DISCOVERED != GROUNDED != CURRENTLY_VISIBLE != AUTHORIZED != VISITED != COMPLETED —
  confirmation windows touch NONE of these sets (no source added, no grounding, no
  authorization, nothing visited/completed).
- Exhaustion confirmation is NOT GoalEvidence; NOT subtree completion; produces NO
  dispatch authority (historical evidence never authorizes).
- SourceIdentity / occurrence identity unchanged; no dedup-by-string/hashset that could
  mask conflicts (identity keys stay exact; conflicts → unresolved).
- No fixed UI text, coordinates, or Settings-specific special-casing anywhere in the
  Runtime change; no ADB/XML as Runtime truth; fail-closed preserved everywhere.

## 5. Implementation-location Owner analysis (ruling §5)

- **Option A — `SourceEquivalenceNormalizer` gains the explicit
  consistency-confirmation semantic** (chosen): the exhaustion PROOF is a
  normalization-contract truth; the normalizer already owns window-to-union
  relationships and already speaks evidence (`SourceEquivalenceEvidence` kinds);
  the classification is a pure, deterministic extension of its existing closed
  vocabulary, consistent with the discovery-epoch philosophy (post-proof evidence is
  consistency-only — here: pre-proof consistency evidence classified distinctly, never
  merged into discovery sources).
- **Option B — upstream (binding evaluator / Agent loop) does not submit the
  confirmation window as a discovery-extension window** (rejected): the evaluator is a
  COMPOSITION-provided criterion; letting it decide "this window is an exhaustion
  confirmation" would require it to pre-know a truth (that the list is genuinely
  exhausted) that only the Runtime's normalization+completeness proof establishes —
  exactly the truthfulness violation the ruling names ("若 B 会让 evaluator 预知 Runtime
  尚未证明的 truth，则拒绝 B"). It would also move contract semantics into a
  replaceable binding.

## 6. Consumer integration (completeness seam, evidence-only)

`TryBuildContainerInventoryCompleteness` consumes the extended result: a resolved
normalization whose trailing windows include confirmations is complete-compatible;
the completeness evidence records the confirmation windows (sequences + classification)
as `ExplorationExhausted` backing. Union/sources/grounding inputs unchanged; pending
branch work unchanged; no new dispatch path. GoalEvidence evaluation untouched.

## 7. Risks / Trade-offs

- [Confirmation masquerades as extension] → conditions 2+3 are strict (identical
  sequence AND contiguous-union-suffix); partial-overlap/shifted/reordered windows stay
  unresolved (spec counter-examples pin each).
- [Type-flip luck re-enters] → condition 2 requires byte-identical signatures; a
  type-flipped window is NOT a confirmation (counter-example pinned) — it stays
  unresolved unless it extends.
- [Silent authority growth] → spec forbids confirmation→any authority; test asserts.

## 8. Stop conditions

Any pressure to: weaken signature identity, add text/coordinate special-cases, let the
binding classify confirmations, or count zero-new-source alone as exhaustion → STOP,
return to Human Gate.

## Spec → owner symbol → test mapping

| Spec requirement | Owner symbol (intended) | Test class |
|---|---|---|
| Three-way window classification | `SourceEquivalenceNormalizer.Normalize` + `SourceNormalizationResult` (additive `WindowClassification` per window) | `SourceEquivalenceNormalizerConfirmationTests` (synthetic sequences) |
| Confirmation conditions §3 | same (internal predicates) | per-condition negative tests (each miss → unresolved) |
| Bounded repetition | `MaxConsecutiveConfirmationWindows` const | 2-pass/3-fail test |
| Completeness integration | `Agent.OpenWorld.TryBuildContainerInventoryCompleteness` (+ evidence record) | traversal/exhaustion capability tests |
| No authority from confirmation | completeness + ledger accounting | authority-invariant test |
| STOP-2 reproduction | end-to-end normalization over the run-6 window sequence | `Stop2ReproductionTests` (deterministic synthetic windows; old contract red → new green) |
| Old-contract regressions | existing normalization/traversal suites | full deterministic Phase 2/2.5 regression |

## AuthorityDelta

`RuntimeBehaviorDelta: PRESENT` (normalization classification + completeness evidence
recording), `AuthorityDelta: NONE` (no new authority anywhere — confirmations are
inert evidence), `ArchitectureDelta: ADDITIVE_INTERNAL` (no surface/wire/API change).

## STOP-2 coverage & Phase 2.6 resume

STOP-2's exact failing sequence (windows: extend×5 → identical zero-motion terminal
pair) becomes the deterministic reproduction the new contract must pass. Phase 2.6
resumes ONLY after this change's implementation + regression + independent graduation:
fresh reentry campaign from the STOP-2 layer (Stage A restart; never mid-stage).
