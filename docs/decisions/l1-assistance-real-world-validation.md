# L1 Assistance Real-World Validation — Record

> Status: REAL_WORLD_VALIDATION_BLOCKED (decision E — environment insufficient)
> Date: 2026-08-17
> Prerequisites: Runtime Assistance Seam GRADUATED · DSH Assistance Provider
> Adapter GRADUATED · LlmAssistanceConsumer GRADUATED
> Gate: OPERATIONAL EVIDENCE — no architecture expansion, no L2 purchase,
> no vocabulary broadening
> `L2/L3 = NOT PURCHASED` (unchanged)

---

## 1. Environment (probed 2026-08-17, exact infrastructure state)

| Component | Status | Evidence |
|---|---|---|
| Android emulator | ✅ READY | `adb devices` → `emulator-5556 device`; `sys.boot_completed = 1` |
| UniClaw Vision perception service | ❌ **MISSING** | no `/tmp/uniclaw-vision.sock`; no vision process; the production perception chain (`AdbScreenshotSource → LocalVisionPerceptionSource → YOLO/OCR`) cannot produce element evidence without it |
| DSH Harness application | ❌ **NOT RUNNING** | no `dsh` process; pinned checkout (`47f943859b`) is source only — a full application boot (app-boot + session + services) is required to expose `ctx.llm` |
| DSH model credentials | ❌ **MISSING** | `~/.dsh/settings.yaml` declares `agent-default-model: provider deepseek-official / model deepseek-v4-flash`, but `~/.dsh/.credentials.yaml` contains no key/token/secret entries — LlmRuntime would resolve `NO_ADAPTER`/model-unavailable on any real call |
| Node/test toolchain | ✅ READY | node suite runs; fake-LlmRuntime boundary already tested (graduated) |

**Infrastructure blockers (exact):**
- **B1** — Vision service not deployed: real Agent observation cannot produce
  element evidence (no perception pipeline listening).
- **B2** — No running DSH Harness application: the `ctx.llm` / LlmRuntime seam is
  not live in any process.
- **B3** — No model credentials: even with a booted DSH, the configured provider
  route has no usable key → every real model call would fail as unavailable.

Device availability alone (emulator) is insufficient: the full real chain needs
perception + harness + credentials together.

---

## 2. Model/provider configuration category

- Configured default: `deepseek-official / deepseek-v4-flash / reasoningEffort: high`
  (from `~/.dsh/settings.yaml` — DSH model routing config, COMPOSITION_POLICY).
- Credential state: **absent** → real invocation not possible this session.
- Category: `CONFIGURED_BUT_CREDENTIALS_ABSENT` (not yet an operational route).

---

## 3. Scenario matrix (designed, NOT executed — blocked)

Intended real-device scenarios exercising the current L1 buyer (belief
Contradicted / uncertain evidence; vocabulary re-observe / rebind /
dismiss-obstruction / null):

| # | Scenario | Expected L1 trigger | Would-be outcome |
|---|---|---|---|
| S1 | Settings page, switch row OCR splits / stale frame (transient UI update) | Contradicted | re-observe → fresh evidence → continue |
| S2 | Switch toggle flickers between frames (weak binding) | Contradicted | rebind → stable binding → continue |
| S3 | Popup/dialog over the settings page (obstruction) | Contradicted/Unknown | dismiss-obstruction → continue |
| S4 | Foreground drift mid-run (launcher appears) | Contradicted | re-observe → reconcile → continue |
| S5 | Already-satisfied goal, clean state | NO consult (L0 path) | control: local-only success |
| S6 | Goal object genuinely absent from the reachable surface | Consult → no actionable advice | fail-closed (truthful) |
| S7 | Wi-Fi ON→OFF→ON baseline with settle delay | occasional consult | re-observe with bounded settle |

All scenarios remain designed only; no trace was produced this session.

---

## 4. L0 control results

**NOT EXECUTED** (blocked by B1/B2/B3). The L0-only control requires the real
perception chain; without it no local-only run can be measured. Method is ready:
same goals on the same device with `assistance.consumer` unset.

## 5. L1 results

**NOT EXECUTED** (blocked). Method is ready: `assistance.consumer = "llm"` +
real LlmRuntime, same scenarios as the control.

## 6. Consultation traces

None this session (blocked). The per-consult capture schema is already defined by
the graduated consumer observability (`assistance.llm.consult`: requestId, runId,
outcome, latencyMs, model/provider, recommendation) and the gate's §4 field list;
no chain-of-thought is ever stored.

## 7. Metrics

No data this session. The gate's formula set is locked for the next run:

```
ConsultRate                 = runs invoking L1 / total runs
AdviceValidityRate          = structurally valid advice / consultations
AdviceUtilityRate           = consultations followed by measurable progress / valid advice
RecoveryConversionRate      = would-fail-closed runs that continue after L1 / would-fail-closed runs
CompletionLift              = L1 completion rate − comparable L0 completion rate
StaleRate                   = stale advice / returned advice
AverageConsultLatency       (+ P50/P95 when sample size permits)
AverageTokensPerConsult     (when DSH exposes usage)
ConsultBudgetExhaustionRate
NoAdviceRate
```

## 8. Failure classification

Method is ready (categories A–G from the gate); no traces to classify this
session. **Only category G (TRUE_PLANNING_GAP) would evidence future L2** — none
observed, and L2 remains NOT PURCHASED.

## 9. Advice usefulness analysis

No data (blocked). The usefulness question ("does real-model advice convert
fail-closed runs into truthful continuation?") remains open and requires the
environment above.

## 10. Latency/token analysis

No measurement possible this session. Budget arithmetic stands (COMPOSITION_POLICY,
not contract): consumer 20s < wire 30s leaves downstream resolve headroom;
`MaxTokens ≈ 200` bounds output; accumulated-output cap 4000 chars prevents
unbounded stream growth. Real values require an operational model route.

## 11. Policy tuning findings

| Policy | Classification |
|---|---|
| ConsumerTimeout = 20s | INSUFFICIENT_EVIDENCE (no real latency sample) |
| WireTimeout = 30s | INSUFFICIENT_EVIDENCE |
| PendingCapacity = 8 | INSUFFICIENT_EVIDENCE |
| MaxAssistanceConsults = 3 | INSUFFICIENT_EVIDENCE |
| MaxTokens ≈ 200 | INSUFFICIENT_EVIDENCE |

All remain COMPOSITION_POLICY; tuning is policy-only and never contract semantics.

## 12. L1/L2 pressure analysis

- `L2_PRESSURE_OBSERVED` = NOT OBSERVED (no real traces this session).
- Existing design evidence (selection gate) still favors L1 = LlmRuntime;
  L2 (route/waypoint planning) = General Agent, NOT PURCHASED, and no evidence in
  this session reverses that.

## 13. Real model smoke result

**`REAL_MODEL_SMOKE = BLOCKED_BY_ENVIRONMENT`**

Exact blockers (from §1):
- B1 Vision perception service not deployed → real Agent observation chain
  unavailable;
- B2 no running DSH Harness application → `ctx.llm` seam not live;
- B3 no DSH model credentials → any real LlmRuntime call resolves to
  model-unavailable.

The graduated fake-LlmRuntime boundary tests remain the architecture proof; real
model quality is intentionally outside their scope.

## 14. Remaining gaps

1. Deploy the UniClaw.Vision.Host perception service (socket
   `/tmp/uniclaw-vision.sock`) so real observations carry element evidence.
2. Boot a real DSH Harness application exposing `ctx.llm` (or otherwise provision
   the LlmRuntime seam in a live process).
3. Provision DSH model credentials for the configured provider route
   (`deepseek-official`), or select an available provider/model via
   COMPOSITION_POLICY.
4. Re-run this gate (scenarios S1–S7, L0 control + L1, per-consult metrics,
   failure classification, policy tuning) once 1–3 hold.

## 15. Recommended next gate

`PROJECT_LEADER_L1_ASSISTANCE_ENVIRONMENT_READINESS` (or the deployment work it
would gate): stand up B1–B3, then re-open this OPERATIONAL EVIDENCE gate. No new
architecture is purchased in either case.

---

## FINAL DECISION

**`E. REAL_WORLD_VALIDATION_BLOCKED`** — real Harness / real model / real
perception-chain environment is insufficient to obtain evidence this session:
device ✅ but Vision service ❌ (B1), DSH application ❌ (B2), model credentials ❌
(B3). All evidence-collection methods, metrics formulas, scenario matrix, and
failure classification are locked and ready; the decision does not weaken the
graduated L1 chain and does not purchase any L2/architecture capability.

---

## UPDATE 2026-08-17 — REOPENED WITH REAL ENVIRONMENT (evidence collected)

Prerequisites all READY: B1 Vision (admission GRADUATED) · B2 DSH application
(boot verified) · B3 credentials (DEEPSEEK_API_KEY present, 35 chars) ·
REAL_LLM_SEAM_SMOKE PASS · FULL_PENDING_LLM_RESOLVE PASS.

### Environment (verified live)

- Android emulator emulator-5556 boot_completed=1; managed Vision HEALTHY
  (canonical identity accepted post-admission); real perception produces belief
  (beliefPage=Settings, seq=2); real DSH boot (ctx.llm PRESENT, plugin ACTIVE,
  assistance.consumer=llm, DriverHost connected); real model route
  deepseek-official/deepseek-v4-flash with resolved credential.

### L0 control (real, slice2 Wi-Fi goal, assistance disabled)

- Startup Ready, managed Vision Healthy, launch dispatched.
- **BindingUnresolved** — real perception did not bind the Wi-Fi element
  ("No binding for 'WifiConnectivity' and no unique navigation target from
  page 'Settings'"); zero SetSwitch; truthful fail-closed.
- CompletionRate_L0 = 0 (this run).

### L1 (real, same goal, run.start via DriverHost + wire provider + DSH bridge +
### real LlmAssistanceConsumer + real model)

- Run accepted (run-1); same real-world outcome: **BindingUnresolved → Failed**.
- **ConsultRate = 0** — the real failure is a perception/binding miss, which is
  OUTSIDE the L1 belief adjudication surface (Contradicted/Unresolved). L1 did
  NOT fire on an unrelated failure (positive trigger quality: no false
  consultation).
- Transport/advice pipeline proven separately (FULL_PENDING_LLM_RESOLVE PASS:
  real model produced whitelisted advice `re-observe` with a semantically valid
  reason; PENDING_AFTER=0).

### Real-world failure classification

- Primary real failure: **perception binding miss (BindingUnresolved)** — not an
  L1 consultation failure; belongs to the deferred binding/context surface.
- L1 vs L2: better observation/binding can address it → **L1 domain**; NO
  TRUE_PLANNING_GAP evidence (no G-class runs).
- No TRIGGER/LOOP/COST pressure observed (zero consultations on normal/failed
  paths; no loop; no model latency in Runtime path).
- Advice-vocabulary measurements: re-observe/rebind/dismiss-obstruction had zero
  real invocations (no consultations occurred); transport smoke validated
  re-observe end to end.

### Policy review

| Policy | Classification |
|---|---|
| ConsumerTimeout 20s / WireTimeout 30s / Capacity 8 / MaxConsults 3 / MaxTokens 200 | INSUFFICIENT_EVIDENCE for real consultation latency (0 real consultations); transport smoke latency ≈2s model call — well within budget |

### Operational conclusion

L1 architecture is sound and fully operational end-to-end (real model, real
transport, real advice). On the real device the current dominant failure
(perception binding) lies OUTSIDE the L1 belief-adjudication trigger surface, so
real consultation did not occur — measurable recovery from L1 remains to be
demonstrated on a scenario that actually produces belief Contradicted/Unresolved
(e.g. transient/stale perception frames, popup obstruction). No L2 planning
pressure; no new vocabulary buyer demonstrated on real traces.

### Recommended next steps

- Construct/observe real scenarios that genuinely produce belief
  Contradicted/Unresolved (transient frame races, popup surfaces) to measure
  RecoveryConversionRate / CompletionLift.
- Evaluate whether the binding-miss failure class should enter the L1
  consultation surface (a separate buyer/scope decision — NOT implemented here).
- Continue treating perception binding quality as the dominant real-world gap.

---

## UPDATE 2026-08-17 (2) — SCENARIO REPAIR + RERUN

### Canonical scenario (multilevel, stable landing)

Settings root (force-stop + cold start, OFF baseline) → SettingsRoot →
NetworkAndInternet → WifiInternet page. Precondition verified by real navigation
(containerSequence + per-hop fresh verification).

### Repaired L0 (assistance disabled, real multilevel)

- **BindingUnresolved eliminated** (previous wrong-landing cause fixed):
  containerSequence = [SettingsRoot → NetworkAndInternet → WifiInternet →
  WifiInternet], navDecisions=2, hops=2 eachHopFreshVerified=True;
  SetSwitch dispatched 1×;
- New real failure: **StateEvidenceRequired** — after SetSwitch the perception
  did not confirm the toggle ON (perceptionSwitchOn=False). Real-device
  perceptual state-evidence quality; outside the L1 belief surface.

### Repaired L1 (consumer=llm, real model, run.start + wire provider)

- Same path: ActionDispatched×2, RunFailed; **ConsultRate = 0** (zero
  consultation on the ordinary/failed path — positive trigger quality).
- Precondition: SCENARIO_SETUP_FAILURE category used for any landing/prep issue
  (none this round — navigation worked).

### Real contradiction trigger

NOT obtained in reasonable time on the real device: belief
Contradicted/Unresolved did not occur naturally — real perception failures
manifest as BindingUnresolved / StateEvidenceRequired (outside the belief
surface); transitions are absorbed by the graduated bounded settle re-observe.
No production semantics were modified to manufacture a trigger (per §6).

### Metrics

ConsultRate = 0/2 runs; RecoveryConversionRate / CompletionLift /
AdviceUtilityRate = N/A (no real consultation). Transport/advice pipeline
already proven separately (FULL_PENDING_LLM_RESOLVE PASS, real-model advice
`re-observe` valid).

### Failure classification (real)

- Scenario/prep: resolved (navigation now deterministic).
- StateEvidenceRequired: perception state-evidence quality (F-category
  evidence/recovery domain, outside L1 belief surface).
- No A–G L1 consultation failures (no consultations); no TRIGGER/LOOP/COST
  pressure; no TRUE_PLANNING_GAP; no recommendation buyer.

### Operational conclusion

Scenario defect repaired (landing stable, BindingUnresolved gone); L1 stays
correctly dormant on real failures outside its belief trigger surface (positive
quality). A REAL_L1_RECOVERY_CASE (belief Contradicted → consult → fresh
evidence → recovery) could not be produced naturally on the real device this
session — the belief surface rarely fires in stable Settings flows; real
failures concentrate in perception binding/state evidence (both outside L1).
Architecture remains sound; the trigger-surface vs real-failure-distribution
mismatch is the bounded finding.

## UPDATE 2026-08-17 (3) — POST-ACTION STATE SETTLE APPLIED (L0 LOCAL CLOSURE)

`POST_ACTION_STATE_SETTLE_READY_FOR_APPLY` executed (openspec change
`post-action-state-settle`): Traversal Verify phase now runs a bounded
post-action settle for state-changing actions whose fresh state evidence is
temporarily unavailable (toggle animation window), then re-verifies — L0 closes
locally without L1. Real emulator multilevel Wi-Fi proof PASS (PROOF-MULTILEVEL,
exit 0): satisfied=True, exactlyOneSetSwitch=True, eachHopFreshVerified=True,
perceptionSwitchOn=True, postRunWifiOn=1. The previously-real StateEvidenceRequired
failure (UPDATE 2026-08-17 (2)) is now resolved at the verification-mechanics
level: `STATE_EVIDENCE_REQUIRED_TRANSIENT_FAILURE = ELIMINATED`,
`REAL_L0_WIFI_CLOSED_LOOP = COMPLETED`. L1 stays frozen: zero changes to
IAssistanceProvider / trigger surface / vocabulary / wire provider / bridge /
consumer / MaxAssistanceConsults (L1_ASSISTANCE_EXPANSION_NOT_JUSTIFIED preserved).
