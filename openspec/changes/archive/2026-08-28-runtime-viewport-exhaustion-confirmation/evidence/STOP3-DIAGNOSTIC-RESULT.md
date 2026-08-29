# PROJECT_LEADER_STOP3_TRANSIENT_VIEWPORT_STABILITY_DIAGNOSTIC_RESULT

Gate: `PROJECT_LEADER_STOP3_TRANSIENT_VIEWPORT_STABILITY_DIAGNOSTIC_GATE` (2026-08-28).
Evidence-only; zero production changes (verified: `git status` delta this round = this
file + IR updates only).

## 1. Human-readable Failure Reconstruction

滚到底部前的某一轮滚动后，Runtime 立刻开始"理解世界"。但那一刻列表还在动
（或刚停止重排）：同一行标题的 OCR 文本被融合层贴到了过渡帧的两个框上，
形成同帧双签名。Runtime 的稳定门只检查"页面身份没变"，不检查"画面不再动"
—— 于是把过渡伪影当作稳定世界消费，随后归一化正确地拒绝歧义。人停下来
之后（E0 实测），残影不存在。

## 2. run-6 Temporal Frame Timeline (from archived frames + event log)

| Phase | Obs | Content | sv text_blocks | Consumed? |
|---|---|---|---|---|
| initial | 1 | launch transition (0 nav) | — | no (fails empty-signature if included) |
| scroll1→decide | 2 | top rows (6 menus) | 0 | yes (decision) |
| settle pair | 4/5 | +Sound (7) | 0 | 5 yes (confirmed) |
| scroll2→ | 7/8 | mid rows (8) | 0 | 8 yes |
| scroll3→ | 10/11 | (8) | 0 | 11 yes |
| scroll4→ | **13/14** | [W,A,S,L] | **2 (dup pair)** | **14 yes (confirmed — the failing admission)** |
| scroll5→ | 16 | bottom (8) | 0 | yes |
| scroll6→ | 18 | bottom identical | 0 | yes → completeness → Unresolved (obs-14 dup) |

The dup existed in BOTH frames of the 13/14 settle pair — i.e., across ~2-4s of
post-scroll time on a step≈0.7-0.8 scroll (the grown step by decision 4).

## 3. Scroll Parameters (source-verified, run-6 applicable)

`InitialStepFraction=0.4`, grow `+0.1` per comfortable overlap, ceiling `0.8`, floor
`0.1` (Agent.OpenWorld:1063-1066). Physical: center swipe, distance = `0.4×step×H`
(384-768px on 2400px), duration velocity-capped (DeviceActionTranslator.TranslateScroll —
fling-blur explicitly avoided by design). Run-6 reached ≈0.7-0.8 by the failing scroll.

## 4. Current Settle/Stability Implementation (source evidence, not docs)

- **Open-world scroll loop: NO explicit delay and NO motion gate.** Dispatch →
  `_observeInitial` (next pipeline observation, naturally ~1-2s later) →
  `TryVerifyViewportContinuity` (Container.cs:380) — verifies reconciled SEMANTIC PAGE
  identity + sequence freshness only; appends unconditionally (no drift/motion check).
- `SettlePostActionObservationAsync` (for dispatched actions): candidate→confirmation =
  two consecutive observations with the SAME reconciled page identity; budget 3; **no
  time delay, no element-position comparison, no signature-set stability comparison**.
- `Traversal.SettlePostActionStateEvidenceAsync` (300ms delay, max 3) exists ONLY in the
  tap/control state-evidence path — NOT in the open-world viewport scroll path.
- **Transient frames have no explicit state** — nothing distinguishes a
  still-settling viewport observation from a stable one at admission time.

## 5-6. Sound & vibration A/B Persistence Trace / Transitional-vs-Settled evidence

- **E0 (settled baseline)**: gentle scroll + 3s wait + 4 observations 1.5s apart →
  sv = exactly ONE clean `menu_item`, 4/4; no ghost, no stray caption candidate.
  **人停下来以后，残影不在。**
- **E1 (campaign-profile scroll, rapid fire)**: first post-scroll frame (t+0) contains
  3 transient `text_block`s; by t+1.7s and all later frames: zero text_blocks.
- **E2 (stepwise 0.4→0.6, rapid per-step)**: transient text_block at step-0.6 t+1.0,
  gone by t+1.7 (steps 0.4/0.5 clean).
- **E3 (run-6 exact growth recipe 0.4→0.5→0.6→0.7 + rapid fire on the sv viewport)**:
  the exact sv-dup did NOT re-trigger (sv=1 transiently, then scrolled past).
- Classification: every transient artifact observed vanished within ≤1.7s of scroll
  completion; none ever persisted into a settled sequence.

## 7-10. Experiment matrix results

| Variant | Result |
|---|---|
| Baseline (current step+settle) | transient text_blocks present in first post-scroll frame(s); vanish ≤1.7s (E1/E2); exact dup not re-triggered at will (E3) |
| Smaller step (0.4-0.5) | clean even in immediate frames (E2 steps 1-2) — **ghost-class pressure appears from step ≥0.6** |
| Stronger settle (spacing) | all transients gone by second spaced observation (E0/E1) |
| Combined | clean (E0) |

## 11-13. Rates / inventory correctness / time impact

- Ghost-occurrence rate: exact dup 1 campaign occurrence (run-6) in 6 campaign runs;
  manual re-trigger 0/3 attempts. Transient text_block class: observed at steps ≥0.6
  in immediate frames (2/3 experiments).
- Stable-inventory correctness: settled observations are composition-clean (E0), so a
  quiescence-gated admission would consume only clean frames.
- Traversal-time impact of a quiescence gate: bounded (transients die ≤1.7s; a
  one-extra-observation gate costs ~1-2s per scroll step; ~6-8 steps per page ≈ +10-15s
  per container — acceptable vs. correctness).

## 14. Exhaustion existing-contract verification

**CLOSED**: `CanonicalStop2Shape_TerminalPair_ResolvesOnOldContract_ErratumConfirmed`
(leader-verified green) proves the existing contract already resolves clean-shape
bounded exhaustion (fresh stable viewport + zero new logical source → exhaustion
provable). The viewport-exhaustion-confirmation capability is NOT needed.

## 15-17. FDP / Owner / GapKind

- **First Divergence Point**: the open-world scroll path admits the FIRST post-scroll
  observation into stable inventory without any visual-quiescence check
  (dispatch → observe → page-identity-only continuity → append). The transitional
  frame is consumed as if the world had stopped.
- **Owner**: post-scroll settle policy / stable-observation admission boundary
  (ruling-owner C+D: observation stability detector + stable-observation admission).
  NOT the normalizer (it correctly refused), NOT corroboration (it operated on a
  transient frame that should never have been admitted).
- **GapKind**: `CONTRACT_GAP` (Runtime): no defined post-scroll quiescence boundary —
  the loop's only stability notion is semantic-page identity constancy.

## 18. Development Semantic IR

- **DesiredReality**: scroll → wait until the viewport is actually stable → admit the
  stable observation → compare with accumulated inventory; visually-conflicted frames
  never enter stable admission (bounded wait, else fail-closed).
- **ObservedReality**: first post-scroll observation admitted with page-identity check
  only; transient same-text artifacts live ≤1.7s and were consumed (run-6 obs 13/14).
- **Evidence**: E0-E3 + source (§4) + run-6 timeline + frozen erratum test.
- **Hypotheses**: H1 ✓ (transitional artifact); H2 ✓ (consumed too early);
  H3 ✓ (gate checks page identity, not motion); H4 partial (transients from step ≥0.6;
  not the sole cause); H5 ✗ (never reached — no persistent settled ghost found).
- **FirstDivergencePoint / Owner / GapKind**: §15-17 above.
- **SemanticResolution**: **PARTIALLY_RESOLVED** — mechanism class proven with source +
  temporal evidence; the EXACT sv-dup is not stably re-triggerable at will (CASE E
  caveat on the specific artifact), so a deterministic reproduction of that exact
  frame is not yet available (the transient CLASS is reproducible).
- **CandidateMinimalChange** (NOT implemented): a post-scroll visual-quiescence
  admission boundary (bounded consecutive-observation convergence: navigation
  signature set + element-position drift within tolerance, fail-closed on budget) —
  a small Runtime contract addition; exact mechanism to be designed under a new
  authorized change. NOT "sleep +Nms" (diagnostic waits were observation tools only).
- **ForbiddenChange / StopCondition**: unchanged from the gate.

## 19. Case classification

**CASE B — SETTLE_GATE_TOO_WEAK** (primary; transient class reproduced and source
mechanism proven), with a **CASE E caveat** for the exact sv-dup artifact (not
re-triggerable at will in 3 attempts; its class is).

## 20-21. New OpenSpec needed? / unique-corroboration disposition

- **viewport-exhaustion-confirmation**: withdraw (premise refuted; exhaustion already
  provable on the existing contract).
- **unique-corroboration-admission**: **ABANDON** — its premise (stable duplicate
  admission) is disproved by E0; the dup was transient. Corroboration surgery would
  treat a symptom of premature admission.
- **New OpenSpec actually needed**: YES — `post-scroll observation quiescence/admission
  boundary` (Runtime contract): bounded visual-stability gate before a post-scroll
  observation enters stable inventory. RED basis: the transient class (E1/E2 protocol)
  + run-6 obs-13/14 timeline; a fully deterministic harness-level reproduction of the
  exact dup is desirable in its design stage (scripted-environment replay with
  transient frames).

## 22-23. Phase 2.6 resume recommendation / next Human Gate

Phase 2.6 remains STOPPED. Chain to resume: (1) new OpenSpec (quiescence boundary) →
Human implementation gate → implement + regress + graduate; (2) then reentry campaign
from Stage A per the standing conditions. **Next Human Gate**: adjudicate this
diagnosis — authorize the quiescence-boundary OpenSpec (and the withdrawal/abandonment
dispositions above), or redirect.

---

## ERRATUM #2 (2026-08-28, per Human Gate `..._QUIESCENCE_ADMISSION_OPENSPEC_PROPOSAL`)

§4's statement "Open-world scroll loop: NO explicit delay and NO motion gate … the only
stability notion is semantic-page identity constancy" is **WRONG**: the Runtime ALREADY
has post-scroll visual quiescence confirmation — `ConfirmScrollStabilityAsync`
(Agent.OpenWorld.cs:2200; called at :1112 forward loop and :796 revisit path): bounded
re-observation (`MaxScrollStabilityObservations`), per-row center-Y drift comparison
(`IsViewportStable`, epsilon-bounded), page/foreground sanity, budget exhaustion
fail-closed, latest-confirmed-frame-only admission, full trace. The leader's source
survey missed it (read the loop from :1130, past the :1112 call site).

**The real defect** (as the Gate states): `NavigationRowCenters` (:2290) builds
`Dictionary<string,float>` via `TryAdd` — same-frame duplicate signatures COLLAPSE to
one entry. Stability comparison therefore loses occurrence MULTIPLICITY: a duplicate-
artifact frame pair can compare "stable" (one entry each, same center) and be admitted
as the stable decision basis; the normalizer then correctly fails on the real two
occurrences. Secondary gap: the stability evidence is an unordered signature→center
map, so same-signature count changes and ordering are invisible to the gate
(ruling Scenarios 5/7), and in-frame ambiguity never blocks admission (Scenarios 1/2).

Dispositions recorded by the same Gate:
- `runtime-viewport-exhaustion-confirmation`: **WITHDRAWN** (hypothesis refuted; kept as
  history of the RED-first discipline working).
- `unique-corroboration-admission`: **ABANDONED_AS_PRIMARY_FIX** (diagnosis evidence
  retained; not implemented).
- Successor: `runtime-evidence-based-quiescence-admission` (this proposal round).
