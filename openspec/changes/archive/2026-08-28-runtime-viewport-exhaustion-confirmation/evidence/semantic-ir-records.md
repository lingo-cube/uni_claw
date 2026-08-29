# Development Semantic IR — runtime-viewport-exhaustion-confirmation (locked at gate open)

Per Human Gate `..._IMPLEMENTATION_GATE` §1 (verbatim fields):

- **DesiredReality**: Runtime 能诚实区分 discovery progress、stable zero-new-source
  confirmation、以及 unresolved alignment。
- **ClaimUnderTest**: `VIEWPORT_EXHAUSTION_CONFIRMATION`
- **ObservedReality**: STOP-2 中真实有界 Settings 列表在最后稳定确认窗口确定性进入
  Unresolved。
- **FirstDivergencePoint**: `SourceEquivalenceNormalizer.Normalize` 当前 extension-only
  alignment contract。
- **Owner**: Runtime / World normalization
- **GapKind**: `CONTRACT_GAP`
- **AllowedChange**: OpenSpec 明确授权的 internal normalization/exhaustion-confirmation
  semantics。
- **ForbiddenChange**: harness truth injection；evaluator 预判 exhaustion；Settings
  special case；identity relaxation；Runtime API/wire change；GoalEvidence/FSM authority
  change；planner/recovery/memory/advisory/dynamic depth。
- **SemanticResolution**: RESOLVED

Erratum recorded at gate open (honesty over convenience): the leader's STOP-2 mechanism
reconstruction (union-overlap absent on the terminal pair) does NOT reproduce on a
menu-text-only simulation of the archived run-6 window sequence (it resolves). The TRUE
failing input must be re-derived from evidence before any implementation: signature
extraction includes ALL admitted NavigationCandidate occurrences (e.g., corroborated
text_block rows), and the exact accepted-window subset is not the decision-frame list
by necessity. WI-VEC-0 therefore derives the deterministic RED from the archived frames
via the PUBLIC Normalize surface before any production edit. If no deterministic RED
can be stabilized → the ruling's §12 stop condition applies
(`STOP-2 RED 无法稳定复现`).

## IR-VEC-0 (RED derivation)

- DesiredReality: a deterministic, mechanism-expressing reproduction of STOP-2's
  Unresolved on synthetic-signature observations derived from the archived real frames.
- ClaimUnderTest: the old extension-only contract fails on the real accepted sequence.
- EvidenceGap: the exact failing accepted subset + true signature sequence.
- GapKind: `TEST_HARNESS_GAP` (diagnostic derivation, no production change).
- Owner: validation-side test authoring (this change's test area).
- AllowedChange: new test files only. ForbiddenChange: production code.
- AcceptanceEvidence: a stable RED with recorded mechanism; or a BLOCKED report
  triggering §12.
- SemanticResolution: RESOLVED (derivable via public Normalize over archived frames).

## IR-VEC-1 (I.1+I.2 core classification) — pending RED
## IR-VEC-2 (I.3 consumer) — pending

---

## IR-STOP3 (Transient Viewport Stability Diagnostic — appended per Gate)

See `STOP3-DIAGNOSTIC-RESULT.md` §18 for the full IR (DesiredReality…SemanticResolution:
PARTIALLY_RESOLVED; CASE B primary + CASE E caveat; CandidateMinimalChange = post-scroll
quiescence admission boundary, NOT implemented). Dispositions: exhaustion-confirmation
capability WITHDRAWN (premise refuted, §14); unique-corroboration ABANDONED (E0
disproves stable duplication); new OpenSpec needed = post-scroll observation
quiescence boundary.
