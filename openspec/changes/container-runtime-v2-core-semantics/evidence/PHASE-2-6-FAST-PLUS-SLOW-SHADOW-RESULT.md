# PHASE_2_6_FAST_PLUS_SLOW_SHADOW_RESULT

STATUS: `SLOW_SEMANTIC_SHADOW_EXPERIMENT_COMPLETE_NO_PURCHASE` (bounded,
Shadow-only; Slow action/recovery authority NOT purchased — per the
experiment's stop condition)

Date: 2026-09-02

Authorization: `SLOW_SEMANTIC_SHADOW_EXPERIMENT_APPROVED_BOUNDED` (Human
direction, this session). NOT Slow production graduation.

## Experiment arms

- **Fast production arm (unchanged)**: `FAST_BASELINE_FROZEN` after the two
  deterministic residuals were closed (see
  `P26-V2-RESIDUAL-REPAIRS-RESULT.md`): perception pytest 384 + 95 subtests /
  unittest 337 / S1 frozen baseline byte-stable; no special-case rules added
  per new variance during the campaign.
- **Slow shadow arm (instrument)**: harness-side `QwenVlSlowAdvisor` +
  `SlowShadowEvaluator` (`P26-V2-SLOW-SHADOW-WIRING-RESULT.md`), local
  Qwen2.5-VL-3B-UI-R1 (Q4_K_M) via llama-server; Shadow-mode acquisition
  through the frozen R5 seam; zero production Runtime effect
  (`HasRuntimeEffect=false`, Agent stays Disabled; all runs' boundary
  prohibitions and gates G1–G4 passed).

## Fresh campaign sample (n=3 rounds, fresh emulator boot each, no
observe→patch→rerun; zero mid-run intervention)

| Run | Path | Terminal | Blocker (evidence) |
|---|---|---|---|
| 7 | Settings → Display | RunFailed: "Unknown interaction affordances remain; completeness cannot be proven." | Affordance-level Unknown at Display (same class as runs 1/6). First divergence: per-element interaction affordance classification stays Unknown for specific visible rows; Runtime fail-closed correctly. Owner: affordance-level semantic interpretation (perception→admission chain) — same owner class as run 1's adjudication. Evidence: `p26-v2-run7/` |
| 8 | Settings → Display → Brightness | RunFailed: "Open-world viewport exploration did not prove positive exhaustion (outcome=Unresolved…)" | Viewport-exhaustion Unresolved at Brightness — a NEW blocker instance (migration continued as predicted under the frozen baseline). First divergence: exploration cannot prove positive exhaustion of the Brightness scope. Owner: exploration/exhaustion semantics — recorded, NOT patched (frozen baseline honored). Evidence: `p26-v2-run8/` |
| 9 | Settings → subpage | RunFailed: "Unknown interaction affordances remain…" | Affordance-level Unknown (run-7 class again). Evidence: `p26-v2-run9/` |

**0/3 Completed** — consistent with the campaign's NotCompleted history
(19 prior fresh runs, 0 Completed). Page identity held in every run
(no wrong-branch, no false identity, no false trust, no recovery
fabrication; fail-closed discipline held).

### Frozen-baseline effectiveness check (live)

- 'Color' phantom navigation row (run-6 residual 1): **0 occurrences** in
  19 Display frames across runs 7–9; 'Color' appears ONLY as NonInteractive
  `section_label`.
- Raw text_block twin (run-6 residual 2): **0 floating occurrences** —
  `duplicate_section_label_supporting` absorbed 8/3/8 twins (runs 7/8/9).
- Sticky demotion (residual 1's cross-frame guard): armed (X-Known-Rows
  carries the additive `type` field; C#→python path verified by tests) but
  **not exercised live** — no height-jitter flip frame sampled in these 3
  runs (recorded honestly).
- Blocker migration continued (run 8's new instance) — recorded with
  evidence, first divergence, and owner; no special-case rule added.

## Slow shadow measurements (58 invocations across 3 runs)

### Kind channel (container identity)

| Run | Invocations | Confirm | Challenge | Correct | Insufficient | ParseFail | Identity-concurring "Correct" | Genuine corrections |
|---|---|---|---|---|---|---|---|---|
| 7 | 21 | 0 | 0 | 21 | 0 | 0 | 21/21 | 0 |
| 8 | 16 | 1 | 0 | 11 | 4 | 4 | 11/11 | 0 |
| 9 | 21 | 0 | 0 | 21 | 0 | 0 | 21/21 | 0 |
| **Σ** | **58** | 1 | 0 | 53 | 4 | 4 | **53/53 (100%)** | **0** |

**Finding (SLOW_RISK, decisive):** at 3B scale the model cannot use the
"correct" channel correctly — it answers "correct" with
`corrected_identity` ECHOING the fast identity (it reads "correct" as "the
correct identity is X"). Every one of the 53 "Correct" assessments is a
false correction under the contract semantics; naive kind-based consumption
would record 53 phantom ConflictsWithFast and 53 phantom corrections. No
identity blocker even occurred (Fast identity held throughout) — so the
identity channel had no rescue opportunity, and it adds no usable
independent confirmation signal.

### Candidate-role channel (the blocker-relevant channel; instrument v2)

Ground truth: the structured Android UI tier's `clickable` flag per text
(an independent, non-vision source), joined by text per frame.

| Run | Matched judgments | TP (interactive right) | FP (false interactive) | TN | FN (false non-interactive) | Accuracy |
|---|---|---|---|---|---|---|
| 8 | 70 | 30 | 0 | 0 | 40 | 42.9% |
| 9 | 117 | 30 | 0 | 0 | 87 | 25.6% |
| **Σ** | **187** | **60** | **0** | **0** | **127** | **32.1%** |

**Finding (decisive):** the role channel is noise-dominated and
false-demotion-biased: 127/187 (67.9%) of genuine tappable rows were judged
`non_interactive` — often with self-contradictory reasons ("a menu item")
or hallucinated layout patterns ("short line directly below a row" for
plain root-page rows). Precision(interactive) is 100% (it never falsely
grants interactivity — fail-closed direction) but recall(interactive) is
32%: consumed, it would demote two-thirds of real rows → MORE Unknown
affordances → deeper failure, not less.

### Infrastructure / cost / latency

- Bounded-output discipline: **0 parse failures in 54 provider-stable
  invocations** (runs 7/9 + run 8's 12 good calls); the model reliably
  emits the constrained JSON vocabulary.
- Run 8's 4 Insufficient: 1 provider timeout (90s) + 3 connection-refused
  after the llama-server process died mid-run (fail-closed recorded, never
  escaped; server restarted as a managed job for run 9 — provider
  fragility is itself a consumption cost).
- Latency: avg 12.6s (run 7, v1 instrument) / 20.4s / 21.2s (v2); max 90s
  (timeout). Sequential queue drained post-run (~4–7 min) — no run-path
  impact (async, Shadow).
- Tokens (58 calls): 230,568 prompt + 11,076 completion (~3,975
  prompt/call, mostly image tokens). Local inference — no external cost.
- Traversal depth impact: **zero by construction** (Shadow arm never feeds
  the Runtime; all runs identical Stage-A posture, depth 1).
- Harness-side input gaps (recorded): TriggerOccurrence /
  TransitionOccurrence / Graph candidates are Agent-internal — not on any
  read surface the taps see; requests carried them null. A real
  ASYNC_ADVISORY consumption point must acquire them Runtime-side (R5
  deferred scope).

## Per-blocker classification (per the authorized A/B/C scheme)

| Blocker | Slow can correctly resolve? | Classification |
|---|---|---|
| run 7 — affordance Unknown @ Display | No: on the blocker frames Slow's role channel outputs false demotions of real rows (would deepen Unknown); identity channel concurs (no signal) | **C (SLOW_RISK)** — not A |
| run 8 — viewport exhaustion @ Brightness | No: neither channel addresses exhaustion semantics; role noise would worsen | **C (SLOW_RISK)** |
| run 9 — affordance Unknown @ subpage | Same as run 7 | **C (SLOW_RISK)** |

- **SemanticRescueRate: 0/3 (0%)**
- **WouldRescueRun: 0/3**
- FalseCorrection exposure: 53/53 "Correct" kinds are false corrections
  under contract semantics (100% of the channel); role channel 67.9% false
  demotion on true rows. **Not acceptable.**

## Answers to the four questions

1. **Slow 是否能稳定解决当前漂移型 semantic blocker？** — **否（本
   provider 规模下）**。当前 blocker 类（affordance 级 Unknown、viewport
   exhaustion）恰恰需要 per-candidate 语义判断，而 3B VLM 的 role 通道
   是噪声主导（32.1% 准确率、67.9% 假降级）；identity 通道零真实纠正
   （53/53 为 echo 型假纠正），且 3 轮中根本没有 identity blocker 需要救。
2. **false correction 是否可接受？** — **不可接受**：按契约语义 100% 的
   "Correct" 是假纠正（naive 消费 = 53 次 phantom conflict）；role 通道会
   把 2/3 的真实行降级为 non_interactive，加深 Unknown 而非减少。唯一安全
   用法是忽略 kind 通道除非 corrected_identity ≠ fast identity，并把
   non_interactive 判断视为不可用——那等于没有可消费的信号。
3. **若消费 Slow，是否预计减少 repeated repair / wrong branch / deep
   Unknown？** — **预计不会**：3 轮中 0 次 wrong-branch、0 次 identity
   事故、0 次 repeated repair（需要救的事件没有发生）；对 deep Unknown 的
   预期效果为负（假降级）。fail-closed 方向的 100% interactive precision
   是唯一正面性质，但它不足以构成 advisory 价值。
4. **Slow 无法解决的剩余 blocker 是否明确集中于
   capture/detection/frame stability？** — **否**。3 轮中 capture 稳定
   （每帧 PNG 都成功采集并归档，含子页面帧）、detection 正常（每帧
   fusion trace 齐全，label 规则 live 生效）、frame stability 正常
   （identity 保持，无 flapping）。剩余 blocker 集中于 **affordance 级语义
   解释质量**（per-element interaction 分类的 admission/解释链）与
   **exploration/exhaustion 语义** — 是解释质量问题，不是
   capture/detection/frame-stability 通道问题。

## Conclusion / proposal

**两项购买条件均未触发：**

- `ASYNC_ADVISORY consumption purchase`（要求明显 rescue value）— **不提
  出**：SemanticRescueRate 0%，false-correction 风险实测不可接受。
- `perception-channel Architecture Gate`（Structured Corroboration vs
  Detector/Frame-Stability，要求 Slow 大量 INSUFFICIENT 且源于
  visual-evidence 缺失/不稳）— **不提出**：4 次 Insufficient 全部是
  provider 基础设施问题（timeout/连接拒绝），不是证据缺失；且实测显示
  capture/detection/frame-stability 并非当前约束。

**实际指向（记录，不购买）：**

1. 当前 blocker 类的可行买家仍是 **确定性的 affordance 解释链**：
   本实验同场验证了 label-height/twin-dedup 确定性修复在 3 轮 fresh run
   中 100% 消除了既有病灶类（phantom 行、twin 双表示）——明确病灶走
   deterministic repair 的边际收益远高于注入一个噪声 VLM。
2. affordance-Unknown 的语义质量缺口若未来要用 LLM 补，需要 **更强的
   provider（更大/更适配的模型）重新做一次有界 Shadow 实验**——本实验的
   shadow harness（ledger + ground-truth join 脚本 + PNG 归档）是可复用
   的测量基础设施，换 provider 即可重跑；3B Q4 的结论不应外推到更大模型。
3. `FAST_BASELINE_FROZEN` 维持；新 blocker（run 8 的 viewport-exhaustion
   实例）已按"evidence + first divergence + owner"记录，未追加
   special-case 规则。

## Limitations (honest)

- n=3 rounds / 58 invocations / 187 ground-truth role joins — the
  kind-channel bias (53/53) and role-channel noise are systematic enough
  that more rounds of the SAME provider would not change the conclusion
  qualitatively, but n is small for blocker-migration statistics.
- One provider only (Qwen2.5-VL-3B-UI-R1, Q4_K_M, local): the conclusion
  is about THIS provider scale, not about Slow semantics in general.
- Instrument iteration: v1 (identity-only) for run 7, v2 (adds the
  candidate-role question) for runs 8–9; both ledgers retain raw model
  outputs for re-analysis.
- Shadow requests lacked Runtime-internal correlation (trigger/transition/
  graph candidates) — a real consumption design would need them; this gap
  is recorded, not solved.

## Artifacts

- Run evidence: `evidence/p26-v2-run{7,8,9}/` (logs, frames, timestamps,
  fusion traces, slow-shadow ledgers; runs 8/9 include per-frame PNG
  archives for FalseCorrection review).
- Wiring: `P26-V2-SLOW-SHADOW-WIRING-RESULT.md`; residual repairs:
  `P26-V2-RESIDUAL-REPAIRS-RESULT.md`.
- Slow shadow harness: `src/UniClaw.Runtime.ValidationHarness/
  SettingsCampaign/SlowShadow/` + `tests/UniClaw.Runtime.Tests/
  ValidationHarness/SlowShadowAdvisorTests.cs` (7/7).
