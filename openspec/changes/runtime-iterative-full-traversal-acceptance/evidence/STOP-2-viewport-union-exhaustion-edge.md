# STOPPED_AT_RUNTIME_OR_CONTRACT_GAP #2 — IR-G1: viewport-union exhaustion edge

2026-08-28, after the perception chain (S1–S4 + 4 defect repairs) drove the real-emulator
campaign to CLEAN root exhaustion.

## Expected Reality

With perception composition stable and correct, a bounded real Settings list scrolled to
true exhaustion should normalize its viewport union and prove completeness, then descend.

## Observed Reality (run 6, evidence `/tmp/p26-reentry-r6.json` + frames)

- Root fully composed and CLEAN at every viewport (no caption inversion — S2fix4 verified
  on real data; no duplicate-line pollution — S2fix3; full 8-title menu composition).
- 7 viewport decisions (obs 2,5,8,11,14,16,18), 6 scroll dispatches, zero-intervention,
  autonomy/invariants/gates green. At obs 18 (bottom; final scroll yields zero new rows —
  the list IS exhausted) → honest fail-closed: `Source normalization is unresolved`.

## Reality Gap — the frozen contract edge (mechanism, source-proven)

`SourceEquivalenceNormalizer.Normalize` builds each adjacent overlap as
**suffix(accumulated-union) vs prefix(next window)** — every accepted window must EXTEND
the list. The exhaustion-confirmation window (zero new rows, revisiting the union's own
tail) has NO suffix-prefix alignment → `FindUniqueSuffixPrefixOverlap` = absent →
Unresolved. (If instead the raw-adjacent framing is considered, identical windows give
MULTIPLE match lengths → the same function returns null as "ambiguous" by design.)
Either reading: **a true bounded-list exhaustion confirmation window cannot normalize.**

- `Container.TryAcceptVerifiedContinuity` (Container.cs:241/380) appends every verified
  observation raw — no zero-new-source consolidation.
- Run 1 passed root ONLY through perception instability luck (frame-to-frame
  PerceptionType flips made signatures differ); the now-STABLE perception (the whole
  point of S1–S4) makes the edge deterministic.

## Evidence Reference

| # | Evidence |
|---|---|
| E1 | Run-6 decision sequence + identical final windows (frames 16/18 byte-identical menu lists; `/tmp/p26-frames.json`, archived below) |
| E2 | `FindUniqueSuffixPrefixOverlap` (SourceEquivalenceNormalizer.cs): multi-match → null; zero-match → null |
| E3 | `Normalize`: overlap = suffix(union-so-far) vs prefix(next) — extension assumption |
| E4 | `Container.TryAcceptVerifiedContinuity`: unconditional append |
| E5 | Run-1 comparison: identical final pair existed but type-flip variance made it pass ( archived run-1 frames) |
| E6 | The whole repaired chain is green: 247+93 tests, byte-equivalence gate, cross-UI, real-geometry regressions (caption inversion, dup lines, title column) |

## First Divergence Point

The FIRST exhaustion-confirmation window enters the container's accepted viewport set
(after the final scroll of any genuinely bounded list) and violates the normalizer's
extension assumption.

## Owner

**Runtime / World normalization (frozen)** — `SourceEquivalenceNormalizer` +
`Container` viewport acceptance. Harness-side levers exhausted: the binding's evaluator
cannot honestly predict exhaustion (guessing violates fail-closed); "keep scrolling"
only appends more identical windows; the binding cannot alter observations (truth
injection forbidden).

## Missing Capability (Human Gate options)

1. **Runtime contract change (OpenSpec + Human Gate)**: consolidate zero-new-source
   confirmation windows — e.g., a window whose navigation-signature set adds nothing to
   the accumulated union AND is consistent with its tail is a CONFIRMATION (consistent
   with the existing discovery-epoch freeze philosophy), not an extension requirement;
   or equivalently, skip appending windows the evaluator marks exhausted-with-no-new-
   sources.
2. **Accept the boundary**: on the frozen stack, TRUE bounded-list exhaustion cannot
   complete (Phase 2.6B permanently blocked by this edge even with perfect perception).
3. (Rejected without discussion: deliberately unstable perception to dodge the edge —
   dishonest.)

## Authority Impact

NONE by this session (Runtime byte-untouched throughout the perception chain; all fixes
were perception/harness-side, each locked with regressions). The stop is a
read-only finding about frozen semantics.

## What IS proven up to this edge

Real-emulator: launch → root inventory resolution → full-list clean composition
(titles/captions correctly roled) → exhaustive scrolling with stable signatures →
honest fail-closed exactly at the exhaustion-proof step. The upper-agent/campaign
machinery (knowledge, PlanDelta, autonomy, invariants) all held on every run.

---

## ERRATUM (2026-08-28, empirical reconstruction — supersedes the mechanism section above)

The mechanism recorded above ("suffix(union)↔prefix(window) overlap absent/ambiguous on
the terminal confirmation pair") is **REFUTED by evidence**: the canonical STOP-2 shape
resolves on the current code (the identical terminal pair IS the unique union-tail
suffix — frozen test `CanonicalStop2Shape_TerminalPair_ResolvesOnOldContract_ErratumConfirmed`).
Run-6's actual Unresolved is an **in-frame duplicate signature**: one structured row
corroborating TWO stacked same-text vision occurrences (`Sound & vibration|text_block`
×2) → old-contract fail-closed, correctly. The real gap is unique-corroboration
admission (perception × capability), NOT an exhaustion-confirmation contract gap.
See `../runtime-viewport-exhaustion-confirmation/evidence/STOP-3-erratum-and-real-gap.md`.
