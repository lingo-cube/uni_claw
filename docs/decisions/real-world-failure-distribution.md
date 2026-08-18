# Real-World Failure Distribution — Gate Record

> REAL_WORLD_FAILURE_DISTRIBUTION_GATE (evidence-collection). 2026-08-18.
> Prerequisites: POST_ACTION_STATE_SETTLE = GRADUATED; L0_REAL_DEVICE_CLOSED_LOOP = PROVEN;
> REAL_L0_WIFI_CLOSED_LOOP = VALIDATED. This gate collects natural failure distribution only —
> no new capabilities, no manufactured Contradicted/Unresolved states, no forced L1.

## 1. Task corpus

Built from **existing, already-calibrated real-device semantic capabilities** (zero new
capabilities; per gate §1 "where already supported by repository truth"):

| Group | Object | Capability | Path | Runs |
|---|---|---|---|---|
| WiFi | `WifiConnectivity.Enabled` | SetEnabled | SettingsRoot → NetworkAndInternet → WifiInternet (multilevel navigation, cold root start) | 14 |
| ASU | `AutomaticSystemUpdates.Enabled` | SetEnabled | DeveloperOptions page (single container + bounded viewport scroll) | 10 |
| **Total** | | | | **24** |

Task/state mix: OFF→ON (W1–W3, W11–W13, A1–A2, A8–A9), ON→OFF (W7, A5), already-satisfied ON/OFF
(W5, W9–W10, W14, A3–A4, A6, A10), idempotent repeats (duplicated scenario ids), plus the
multilevel navigation / scroll mechanisms themselves. Already-satisfied and repeated/idempotent
goals are explicitly in the gate's suggested list.

## 2. Environment

- Device: Android emulator `scroll-test` (API 35), serial emulator-5554, 1080×1920, swiftshader.
- Production path: PhysicalHost composition — Goal → Runtime.Agent → Navigation → Binding →
  Traversal → PhysicalEnvironment → real Vision (managed `VisionServiceHost`, UDS, healthy) →
  Action (ADB) → fresh verification → terminal.
- L1: **not injected** (production L0 path exactly as the graduated PhysicalHost composition —
  `BuildRuntimeGraph` default null provider). No forced consultation; L1 cannot trigger by design.
- Baseline prep (host-run, outside semantic path): WiFi via `svc wifi disable/enable` (robust —
  recorded-coordinate tap proved layout-dependent on this boot: switch center 969,618 vs 969,824
  across device states); ASU via `settings put global ota_disable_automatic_update` +
  `development_settings_enabled=1` + `sysui_demo_allowed=0` + SystemUI restart (clears a
  SystemUI `DemoMode` activity that intermittently intercepted the DEVELOPER_SETTINGS intent).
- Corpus runner: new `--corpus` PhysicalHost proof mode (evidence tooling only; reuses all
  existing composition/criteria helpers; per-run fresh runtime graph).

## 3. Run matrix (24 runs, single corpus session — run6; earlier sessions polluted by demo-mode/landing)

| # | Task | Desired | Terminal | Classification | Key evidence |
|---|---|---|---|---|---|
| W1 | Wifi | ON | ExecutionFailed | C. SCENARIO_SETUP_DEFECT | initial obs = launcher (Google/Gallery); Settings root never landed |
| W2 | Wifi | ON | Satisfied | A. L0_COMPLETED | OFF→ON, 1 SetSwitch, 2 nav taps |
| W3 | Wifi | ON | Satisfied | A. L0_COMPLETED | OFF→ON, 1 SetSwitch |
| W4 | Wifi | ON | ExecutionFailed | G. ACTION_GROUNDING_FAILURE | beliefPage=NetworkAndInternet; nav transition not proved → refused blind redispatch |
| W5 | Wifi | ON | Satisfied | B. ALREADY_SATISFIED | 0 SetSwitch |
| W6 | Wifi | ON | BindingUnresolved | F. BINDING_UNRESOLVED | SettingsRoot resolved; no binding + no nav candidate |
| W7 | Wifi | OFF | ExecutionFailed | G. ACTION_GROUNDING_FAILURE | nav transition not proved |
| W8 | Wifi | OFF | BindingUnresolved | F. BINDING_UNRESOLVED | same as W6 |
| W9 | Wifi | OFF | Satisfied | B. ALREADY_SATISFIED | 0 SetSwitch |
| W10 | Wifi | OFF | Satisfied | B. ALREADY_SATISFIED | 0 SetSwitch |
| W11 | Wifi | OFF | Satisfied | A. L0_COMPLETED | ON→OFF, 1 SetSwitch |
| W12 | Wifi | ON | Satisfied | A. L0_COMPLETED | OFF→ON, 1 SetSwitch |
| W13 | Wifi | ON | Satisfied | A. L0_COMPLETED | OFF→ON, 1 SetSwitch |
| W14 | Wifi | ON | Satisfied | B. ALREADY_SATISFIED | 0 SetSwitch |
| A1 | ASU | ON | SemanticContradiction | K. BELIEF_CONTRADICTED | scroll×2 + SetSwitch×2; post-action page unresolved |
| A2 | ASU | ON | SemanticContradiction | K. BELIEF_CONTRADICTED | identical signature (reproducible) |
| A3 | ASU | ON | Satisfied | B. ALREADY_SATISFIED | 0 SetSwitch |
| A4 | ASU | ON | Satisfied | B. ALREADY_SATISFIED | 0 SetSwitch |
| A5 | ASU | OFF | SemanticContradiction | K. BELIEF_CONTRADICTED | ON→OFF; post-action page unresolved |
| A6 | ASU | OFF | Satisfied | B. ALREADY_SATISFIED | 0 SetSwitch |
| A7 | ASU | OFF | SemanticContradiction | K. BELIEF_CONTRADICTED | post-SCROLL continuity failure (variant) |
| A8 | ASU | ON | SemanticContradiction | K. BELIEF_CONTRADICTED | reproducible |
| A9 | ASU | ON | SemanticContradiction | K. BELIEF_CONTRADICTED | reproducible |
| A10 | ASU | ON | Satisfied | B. ALREADY_SATISFIED | 0 SetSwitch |

## 4. Completion distribution

- **Satisfied = 13/24 (54.2%)** — 9/14 WiFi, 4/10 ASU.
- Already-satisfied (0 dispatch) = 8/24; L0 state-change completion (≥1 dispatch → Satisfied) = 5/24.
- Natural belief contradiction = 6/24 (25.0%), all in the ASU state-change path.
- BindingUnresolved = 2/24; nav grounding failure = 2/24; setup landing = 1/24.

## 5. Failure distribution (earliest missing system link)

| Class | Count | Share | Note |
|---|---|---|---|
| A/B local closure | 13 | 54.2% | 5 L0 + 8 already-satisfied |
| K. BELIEF_CONTRADICTED | 6 | 25.0% | **reproducible ASU post-action/post-scroll page-unresolved** |
| F. BINDING_UNRESOLVED | 2 | 8.3% | SettingsRoot no nav candidate (perception variance) |
| G. ACTION_GROUNDING_FAILURE | 2 | 8.3% | nav tap landed but transition not proved (perception variance) |
| C. SCENARIO_SETUP_DEFECT | 1 | 4.2% | cold-start root landing failure |
| D/E/H/I/J/L/M/N/O/P/Q | 0 | 0% | not observed |

Earliest-link rule applied: W4/W7 classified G (binding existed → nav tap executed → transition
verification failed), not E; A1/A2/A5/A8/A9 classified K (page identity contradicting post-action,
not binding/state-evidence — belief surface), A7 classified K (post-scroll continuity variant).

## 6. Post-action settle statistics

- **PostActionSettleRate = 0/24** (settle never engaged).
- Count distribution: 0 = 24/24 (1/2/3 = 0).
- Action kinds invoking settle: none in this corpus.
- First settled observation resolving evidence: n/a.
- Budget exhaustion: 0.
- Added latency: 0ms (settle never ran).
- Target re-identification failures: 0 (never needed).
- **Interpretation**: on this device/layout, toggle frames were either immediately valid (fast
  path — T2/T15 behavior) or the run failed before/at dispatch (binding/nav/contradiction) with
  no animation-window null frame observed. The settle's fast path is exercised; the transient
  recovery path is not in this sample. No SETTLE_POLICY_PRESSURE: 300ms/3 unchanged.

## 7. Perception/binding statistics

- PerceptionEvidenceFailureRate: 0 runs failed on state-evidence quality alone (E-class = 0).
- BindingFailureRate: 2/24 (8.3%) — SettingsRoot with no unique navigation candidate (F-class;
  natural OCR/order variance on repeated cold starts; W6/W8).
- ActionGroundingRate (G): 2/24 — nav tap executed but transition proof failed (W4/W7).
- Perception anchor quality worked in 21/24 landings; failures are intermittent, not structural.

## 8. Natural L1 trigger statistics

- **L1ConsultRate = 0/24** — but by construction: the corpus runs the production L0 composition
  (no assistance provider injected), so consultation was structurally impossible. This is
  legitimate evidence per gate §2 ("If L1 never triggers, that is legitimate evidence"), not a
  claim that L1 works on these failures.

## 9. Natural L1 recovery evidence

- None observed (no consults occurred). No recommendation/advice/world-consequence/recovery to
  record. The 6 natural K-class runs would be L1-trigger candidates (belief Contradicted) **if** a
  provider were wired — this is the natural-buyer surface identified, not an L1 success.

## 10. TRUE_PLANNING_GAP evidence

- **None (0/24)**. No run satisfied §7's definition: every failure has an identifiable earlier
  missing link (landing, binding, nav verification, or post-action page identity). Runtime never
  lacked a reliable semantic waypoint while the page/world were understood. **L2_BUYER_PRESSURE = NONE.**

## 11. Latency/step observations

- Average wall time per run ≈ 12.6s (single-session corpus total 302s / 24).
- Average runtime steps: WiFi satisfied runs ≈ 2 nav taps + 1 SetSwitch + 3 journal entries
  (~11–15s); ASU satisfied (already) ≈ 2 scrolls + 2 journal entries (~11s); ASU state-change
  (failed) ≈ 2–4 scrolls + 1–2 SetSwitch + 4–5 journal entries (~11–20s).
- FAST_PATH (already-satisfied / immediate valid): 8 runs, ~10–11s, zero settle, zero dispatch
  (already) or 1 SetSwitch (state change).
- RECOVERY_PATH: no recovery mechanism engaged (no Trap/Recovery observed); failures are
  terminal-per-run (fresh graph per run).

## 12. Buyer-pressure ranking

| Buyer | Pressure | Evidence |
|---|---|---|
| L1 (assistance) | **LOW → candidate** | 6 natural K-class runs are belief-surface triggers; L0 path can't consult in this host. NOT L1_ASSISTANCE_BUYER_CONFIRMED (single-domain, same root cause, no recovery test yet). |
| L2 (planning) | NONE | 0 TRUE_PLANNING_GAP. |
| Perception | LOW | 2 binding + 2 nav-grounding (8.3%+8.3%), intermittent cold-start variance. |
| Binding | LOW | 2/24; same intermittent cause as perception. |
| Action execution | LOW | 2/24 nav-grounding; not dominant. |
| Settle policy | NONE | fast path always valid; no animation-window null observed. |

## 13. Recommended next capability

**No next capability buyer is proven.** The dominant finding is one **reproducible runtime
defect**: the ASU (Developer options) state-change path loses page identity after the toggle
action / scroll (SemanticContradiction 5/6 of ASU state-change runs, 1 post-scroll variant).
This is a bounded runtime-verification defect (post-action continuity for below-fold toggle pages),
**not** an L1/L2/perception/binding buyer. Recommended: a **bounded verification repair** for
post-action/post-scroll page identity on scroll-container toggle pages (same owner: Traversal /
Agent verification mechanics), re-run the ASU slice, and only then re-open the distribution gate.
No new capability change is opened by this gate.

## 14. No-buyer conclusion

- L2_BUYER_PRESSURE = NONE (0 planning gaps).
- SETTLE_POLICY_PRESSURE = NONE (fast path always valid; no tuning justified).
- L1_BUYER_PRESSURE = LOW (candidate surface exists — 6 natural K-class — but the root cause is a
  single-domain runtime verification defect; L1 is not justified until that defect is understood;
  and low L1 usage with high local closure is a GOOD outcome per gate §6, not failure).
- PERCEPTION/BINDING/ACTION_BUYER_PRESSURE = LOW (intermittent, non-structural).
- **Local-first hierarchy supported**: normal tasks close locally (54% in this small corpus; the
  remaining failures are concentrated in one reproducible runtime defect, not in ambiguity or
  missing route knowledge). No L2 purchase criterion met; no L1 purchase justified by this data.

