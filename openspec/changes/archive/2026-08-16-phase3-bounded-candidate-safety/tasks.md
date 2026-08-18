# Tasks — phase3-bounded-candidate-safety

> 实施前必读: `proposal.md` + `design.md` + `specs/bounded-candidate-safety/spec.md` + `scenarios/SC-P3-CAND-006-bounded-candidate-safety.md`。
> 完成一项立即勾选 `- [x]`。一次只执行一个 Task ID；H4-3 每项完成后重新读取 repository truth 再决定下一项。
> SC-P3-CAND-006 Gate 已批准为 `SEMANTIC_PURCHASE_REQUIRED`；Production Delta Budget = exactly one immutable `CandidateAuthorizationEvidence` type with two fields plus one optional immutable `Goal.CandidateAuthorizationEvaluator` field；enums/interfaces/components/new mutable-state owners = 0；Ownership Delta = NONE；Authority Delta = NONE。
> 任一执行者若发现需要扩大预算、改变 frozen ownership/authority、让 Traversal 成为第二 semantic authorization authority、引入 policy/safety framework、general discovery/planning、universal interception，或无法保持 evaluator deterministic side-effect-free Observation-only，立即停止并返回对应 Semantic/Architecture Gate。

## Dependency Order

```text
1.1 Authorization Value and Deterministic Candidate Fixture
→ 2.1 Agent Bounded Pre-Dispatch Authorization Behavior
→ 3.1 Formal Scenario Proof
→ 4.1 Independent Validation
```

## 1. Minimum Authorization Semantic and Deterministic Scenario Capability

- [x] 1.1 **Add the approved authorization value, Goal criterion, and deterministic candidate fixture**
  - **Task ID:** 1.1
  - **Role:** runtime-coder
  - **Tier:** standard
  - **Depends On:** NONE
  - **Scenario Receipt:** SC-P3-CAND-006
  - **Goal:** 在严格批准预算内增加 immutable `CandidateAuthorizationEvidence(Authorized, Reason)` 与 optional `Goal.CandidateAuthorizationEvaluator` 字段，并建立纯测试侧 deterministic fixture，使 Fake 能表达 one fresh Settings candidate set：safe S、destructive navigation-like D、state-changing T、unresolved U，以及 equal-input replay。
  - **Required Semantic:** Observation/ObservedElement 只证明 candidate existence；authorization value 是 Agent-owned bounded intent evidence，不是 dispatch/world/completion truth；true/false/null = authorized/rejected/unresolved；Reason non-empty；evaluator deterministic、side-effect-free、Observation-only，candidate 必须来自 supplied Observation。
  - **Approved Production Purchase:** exactly one immutable production type with two immutable fields plus one optional immutable Goal field；enums/interfaces/components/mutable-state owners = 0；Production Behavior Change = NONE；Ownership/Authority Delta = NONE。
  - **Allowed Scope:** one new Model file for `CandidateAuthorizationEvidence`；`src/UniClaw.Runtime/Model/Goal.cs` for the approved backward-compatible optional field；`tests/UniClaw.Runtime.Tests/Scenario/Fakes/**` deterministic SC-P3-CAND-006 fixture/helper/tests；existing Model immutability tests；必要时仅更新本 tasks.md progress。
  - **Forbidden Scope:** `src/UniClaw.Runtime/Agent/**`、Container/Traversal/Recovery/Environment production behavior；second production type、fourth field、enum/interface/component/state；Trace/journal/Trap/action shape；SafetyManager/policy/rule engine；Task 2.1 behavior；Capstone/Harness/refactor。
  - **Required Assertions:** `CandidateAuthorizationEvidence` has exactly Authorized/Reason and rejects empty Reason；existing one-argument Goal construction remains source-compatible and evaluator absent；fixture candidates share one fresh Observation and have stable Text/SwitchState/Index；S deterministically true with reason；D false despite navigation-like text shape；T false from state-changing evidence without dangerous keyword；U null；candidate not contained in supplied Observation is rejected by fixture protocol；same inputs replay equal authorization values and external evidence；existing ScriptedEnvironment behavior unchanged。
  - **Verification:** targeted Model/fixture tests；existing Model immutability and ScriptedEnvironment tests；`dotnet build src/UniClaw.Runtime.sln`；production-delta audit proves exactly 1 type/3 fields and no behavior change。
  - **Deferred Boundary:** Agent authorization control flow、formal proof、persistent authorization state、policy/rule framework、universal interception、general discovery/planning、Confidence/policy hash/coordinates/Fingerprint/Vision/LLM、new action/Trace/journal/Trap surface、Capstone、Harness、Runtime refactor。
  - **Return Contract:** `TASK_RESULT`；success requires `Status: DONE` and exact approved delta。完成后停止并由 H4-3 重读 repository truth。

## 2. Agent Bounded Pre-Dispatch Authorization Behavior

- [x] 2.1 **Evaluate one fresh candidate set and dispatch only one authorized safe navigation candidate**
  - **Task ID:** 2.1
  - **Role:** runtime-coder
  - **Tier:** standard
  - **Depends On:** 1.1 DONE
  - **Scenario Receipt:** SC-P3-CAND-006
  - **Goal:** 在 existing Agent Run control flow 内实现 opt-in bounded classification：evaluator present 时对 initial active-Container fresh Observation candidates 按稳定顺序求值；false/null 记录 Agent Trace 且不进入 Traversal；first true 可作为 one transient existing Tap step 进入 Container/Traversal；随后使用 normal post-action Observation/GoalEvidence；evaluator absent 时 frozen fixed-Plan behavior 不变。
  - **Required Semantic:** Agent alone owns semantic authorization；Traversal only enforces already-authorized local execution；Observation != authorization；authorization != dispatch/world/Goal success；rejected/unresolved candidate != approved required safe work；absence of authorization never defaults to dispatch。
  - **Approved Production Purchase:** Task 1.1 exact one type/three fields；this task adds no production model/type/field/enum/interface/component/mutable-state owner and may modify only existing Agent private helpers/control flow；Ownership/Authority Delta = NONE。
  - **Allowed Scope:** `src/UniClaw.Runtime/Agent/Agent.cs` minimum private helper/control-flow changes；direct tests under `tests/`；read-only use of existing Goal/Observation/ObservedElement/Container/Traversal/Trace/GoalEvidence/ActionHistory surfaces；必要时同步 Tier 2/3 docs 与本 tasks.md progress。
  - **Forbidden Scope:** production changes to Model beyond Task 1.1、Container/Traversal/Recovery/Environment/DeviceAction/Trace/Trap/journal/BranchProgressEvidence；new state/type/field/enum/interface/component；semantic authorization in Traversal；hardcoded destructive vocabulary in Runtime；policy/safety engine；general candidate discovery/planning；universal interception；Capstone/Harness/refactor。
  - **Required Assertions:** evaluator invoked only for candidates contained in one fresh active-Container Observation and in stable order；false/null Trace contains candidate text/index, source sequence, outcome, non-empty reason, no Action/ActionId；D/T/U never enter Traversal and have zero matching actions；first true S alone may enter existing Tap protocol and obtains fresh post-action Observation；normal Traversal mechanical rejection remains possible but cannot reverse Agent denial；no true candidate yields explicit existing non-completion/failure with zero dispatch；denied/unresolved candidates are not added to BranchProgress approved inventory；Completed only from satisfied GoalEvidence；evaluator absent preserves all existing Phase 1/2/3 behavior。
  - **Verification:** targeted safe/false/null/no-true/zero-dispatch/authority tests；full build/test；Architecture Guards；`scripts/check-consistency.sh`；strict OpenSpec validation；exact production-delta/ownership/authority audit。
  - **Deferred Boundary:** no persistent authorization cache/state, generic policy/rule/safety framework, all-source gate, dynamic planner, candidate registry, multi-page discovery, Vision/VLM, coordinate/Fingerprint/Confidence, new action/audit/Trap surface, Capstone, Harness, refactor。
  - **Return Contract:** `TASK_RESULT`；若不能在 exact budget 与 Agent-only authority 内实现，返回正式 `BLOCKED_FOR_SEMANTIC_REVIEW` / `BLOCKED_FOR_ARCHITECTURE_REVIEW`。完成后停止并由 H4-3 重读 repository truth。

## 3. SC-P3-CAND-006 Formal Scenario Verification

- [x] 3.1 **Prove safe, destructive, state-changing, unresolved, completion-boundary, and replay branches**
  - **Task ID:** 3.1
  - **Role:** runtime-coder
  - **Tier:** standard
  - **Depends On:** 2.1 DONE
  - **Scenario Receipt:** SC-P3-CAND-006
  - **Goal:** 使用 Task 1.1 deterministic fixture 与 Task 2.1 behavior 建立 formal end-to-end SC-P3-CAND-006 evidence，覆盖 safe S、destructive D、state-changing T、unresolved U、no-authorized negative、zero dispatch、required-work boundary、GoalEvidence authority 与 deterministic replay；不新增生产行为。
  - **Required Semantic:** observed != authorized != executed；false != null；authorized != required work；denied visible candidate != unfinished safe branch；authorization/dispatch/local success != Goal completion。
  - **Approved Production Purchase:** Production Model Delta = 0；Production Behavior Delta = 0。本任务只购买 formal Scenario evidence。
  - **Allowed Scope:** `tests/UniClaw.Runtime.Tests/Scenario/**` SC-P3-CAND-006 formal tests、Task 1.1 fixture、必要最小 test-side harness/goals/helpers，以及本 tasks.md Task 3.1 progress。
  - **Forbidden Scope:** `src/UniClaw.Runtime/**` unless an explicit implementation regression blocks approved proof；approved Spec/Scenario semantic changes；new production artifact/state；other Scenario/Capstone/Harness/refactor。
  - **Required Assertions:** formal Scenario Required Assertions 1–12；S true enters existing Tap and fresh Observe/Verify exactly once；D false overrides otherwise navigation-like evidence and zero dispatch；T false without dangerous keyword and zero dispatch；U null and zero dispatch；each false/null has Agent Trace reason with no Action/ActionId and no matching journal action；no-authorized set fails explicitly without fabricated completion；denied/unresolved candidates never appear as approved required branch inventory；only satisfied GoalEvidence completes；same inputs replay equal outcomes/reasons/Trace/journal/actions/Observations/GoalEvidence/RunState。
  - **Verification:** targeted formal/replay tests；all SC-P1/P2/P3-001/002/003/CAND-004/CAND-005 regressions；full build/test；Architecture Guards；consistency；strict OpenSpec validation；Scenario assertions audit。
  - **Deferred Boundary:** no production change, persistent safety state, policy/rule framework, universal interceptor, dynamic planning/discovery, Confidence/policy hash/coordinates/Fingerprint/Vision/LLM, new audit/Trap/action surface, Capstone, Harness, refactor, Phase completion。
  - **Return Contract:** `TASK_RESULT`；完成后停止并由 H4-3 重读 repository truth。

## 4. Independent Regression and Boundary Validation

- [x] 4.1 **Independent SC-P3-CAND-006 slice acceptance**
  - **Task ID:** 4.1
  - **Role:** runtime-validator
  - **Tier:** standard
  - **Depends On:** 3.1 DONE
  - **Scenario Receipt:** SC-P3-CAND-006
  - **Goal:** fresh reload repository truth，独立审计 Task 1.1/2.1/3.1 actual diff、formal evidence、exact one-type/three-field budget、Agent-only authorization authority、zero-dispatch denial、required-work/completion boundary、deterministic replay 与全部既有回归。
  - **Required Semantic:** observed candidate != authorization != execution；Agent alone authorizes；Traversal only executes mechanically；false/null never dispatch；authorization does not define required work or completion；GoalEvidence remains final authority。
  - **Approved Production Purchase:** exactly one immutable two-field `CandidateAuthorizationEvidence` type plus one optional immutable Goal field；new enums/interfaces/components/mutable-state owners = 0；Ownership/Authority Delta = NONE；validation behavior delta = 0。
  - **Allowed Scope:** read-only production/test/OpenSpec/diff audit；run build/tests/guards/consistency/strict validation；all PASS 后 only mark Task 4.1 complete。
  - **Forbidden Scope:** repair production/tests/spec；accept coder summary instead of fresh evidence；add artifact/state/dependency；change frozen architecture；start Capstone/Harness/other Scenario/Phase completion。
  - **Required Assertions:** formal Scenario assertions 1–12 all satisfied；exact budget and no other production delta；safe/destructive/state-changing/unresolved/no-authorized/replay branches correct；D/T/U zero actions；Agent/Container/Traversal/Environment/Recovery ownership unchanged；no duplicate safety authority；Goal completion unchanged；Phase 1/2 and all frozen Phase 3 slices PASS；all deferred capabilities absent。
  - **Verification:** `dotnet build src/UniClaw.Runtime.sln`；`dotnet test src/UniClaw.Runtime.sln`；Architecture Guard tests；`scripts/check-consistency.sh`；`openspec validate phase3-bounded-candidate-safety --strict`；production-delta/ownership/authority/determinism/evidence audit。
  - **Deferred Boundary:** all capabilities outside the bounded one-Observation classification round remain absent；structural pressure may be recorded but not repaired。
  - **Return Contract:** `VALIDATION_RESULT`；Verdict only `PASS | CONDITIONAL_PASS | FAIL`。PASS 后 H4-3 may create Scenario-specific closeout and then stop FROZEN。

## Design Docs

> Auto-generated from proposal Impact section.
> Implementation agents: read these before starting.

| Module | Design Doc |
|--------|------------|
| `src/UniClaw.Runtime/Model/` | `docs/system/constitution/runtime-architecture-contract.md` |
| `src/UniClaw.Runtime/Agent/` | `docs/system/layers/agent-runtime.md` |
| `tests/UniClaw.Runtime.Tests/Scenario/` | `openspec/changes/phase3-bounded-candidate-safety/scenarios/SC-P3-CAND-006-bounded-candidate-safety.md` |
| `openspec/changes/phase3-bounded-candidate-safety/` | `openspec/changes/phase3-bounded-candidate-safety/design.md` |
