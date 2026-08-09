# Tasks — phase3-viewport-exploration-exhaustion

> 实施前必读: `proposal.md` + `design.md` + `specs/viewport-exploration-exhaustion/spec.md` + `scenarios/SC-P3-CAND-007-viewport-exploration-exhaustion.md`。
> 完成一项立即勾选 `- [x]`。一次只执行一个 Task ID；H4-3 每项完成后重新读取 repository truth 再决定下一项。
> SC-P3-CAND-007 Gate 已批准为 `SEMANTIC_PURCHASE_REQUIRED`；Production Delta Budget = exactly one immutable two-field `ViewportExplorationEvidence` value + one optional immutable `Goal.ViewportExplorationEvaluator` field + one Container-owned retained-evidence field；enums/interfaces/components/new mutable-state owners = 0；Ownership Delta = NONE；Authority Delta = NONE。
> 任一执行者若发现需要扩大预算、创建 stable Viewport/content identity、改变 frozen ownership/authority、让 Container/Traversal 决定 Goal relevance、把 bound/rejection/snapshot equality 解释为 exhaustion，或引入 generic scroll/policy/retry framework，立即停止并返回对应 Semantic/Architecture Gate。

## Dependency Order

```text
1.1 Minimum Evidence Semantic and Deterministic Fixture
→ 2.1 Agent Repeated-Exploration Decision Behavior
→ 3.1 Formal Scenario Proof
→ 4.1 Independent Validation
```

## 1. Minimum Evidence Semantic and Deterministic Capability

- [x] 1.1 **Add the approved exploration evidence surfaces and deterministic multi-viewport fixture**
  - **Task ID:** 1.1
  - **Role:** runtime-coder
  - **Tier:** standard
  - **Depends On:** NONE
  - **Scenario Receipt:** SC-P3-CAND-007
  - **Goal:** 在严格批准预算内增加 immutable `ViewportExplorationEvidence(ContinueExploration, Reason)`、optional `Goal.ViewportExplorationEvaluator`、Container-owned bounded retained Observation evidence，并扩展测试 Fake/fixture 确定性表达 V1(A/B/C) → V2(B/C/D) → V3(C/D/E + positive end evidence)、same-evidence ambiguous、rejected/stale/identity-conflict 及 equal-input replay。
  - **Required Semantic:** true/false/null 分别表示 positive continuation / positive exhaustion / unresolved；Reason non-empty；retained evidence 只包含同一 Container 已接受的 fresh Observations；sequence 仅证明 freshness/order；same snapshot/rejection/bound/no-new-text 不是 exhaustion；本任务不购买 Agent continue/stop control flow。
  - **Approved Production Purchase:** exactly one immutable production type with two fields；one optional immutable Goal criterion field；one Container-owned retained-evidence field；enums/interfaces/components/new mutable-state owners = 0；Ownership/Authority Delta = NONE。
  - **Allowed Scope:** one new Model file for `ViewportExplorationEvidence`；`src/UniClaw.Runtime/Model/Goal.cs` approved backward-compatible optional field；`src/UniClaw.Runtime/Container/Container.cs` one retained field/read-only snapshot plus Bind initialization and append only after existing viewport continuity succeeds；`tests/UniClaw.Runtime.Tests/Scenario/Fakes/**` SC-P3-CAND-007 fixture/helpers and direct Model/Container/Fake tests；必要时本 tasks.md progress。
  - **Forbidden Scope:** `src/UniClaw.Runtime/Agent/**`、Traversal/Recovery/Environment interface behavior；second production type、fifth field、enum/interface/component/new owner；Agent exploration decisions；stable Viewport/ViewportId/content identity；generic history/policy/scroll framework；Task 2.1 behavior；Capstone/Harness/refactor。
  - **Required Assertions:** evidence value has exactly nullable continuation + non-empty reason and rejects empty reason；existing Goal construction remains compatible and evaluator defaults absent；evaluator fixture is deterministic/side-effect-free over supplied immutable evidence；Bind initializes one-item retained evidence；accepted fresh viewport continuity appends in V1/V2/V3 order；stale/conflicting evidence does not append or erase prior evidence；fixture independently scripts dispatch outcome/world transition/end indicator；same inputs replay equal retained evidence and Fake history；existing SC-P3-003 fixtures remain unchanged。
  - **Verification:** targeted Model/Container/Fake tests；existing viewport continuity and ScriptedEnvironment tests；`dotnet build src/UniClaw.Runtime.sln`；production-delta audit proves exactly 1 type/4 fields and no Agent behavior change。
  - **Deferred Boundary:** Agent continue/exhausted/unresolved behavior、formal Scenario proof、stable viewport/content identity、Fingerprint、geometry、reverse scroll、dynamic planning/discovery、generic scroll/retry/uncertainty framework、multi-Container state、Recovery semantic、Capstone/Harness/refactor。
  - **Return Contract:** `TASK_RESULT`；success requires `Status: DONE` and exact approved delta。完成后由 H4-3 重读 repository truth。

## 2. Agent Repeated-Exploration Decision Behavior

- [x] 2.1 **Interpret retained evidence and authorize only bounded evidence-backed viewport movement**
  - **Task ID:** 2.1
  - **Role:** runtime-coder
  - **Tier:** standard
  - **Depends On:** 1.1 DONE
  - **Scenario Receipt:** SC-P3-CAND-007
  - **Goal:** 在 existing Agent fixed-Plan control flow 内实现 opt-in repeated-exploration decision：evaluator present 时，在 first viewport movement 前及每次 accepted post-viewport evidence 后求值并记录 reason；true 最多授权 next approved ScrollForward；false 停止 viewport movement；null 显式 unresolved；approved viewport Plan steps consumed while latest true 时报告 bound-reached unresolved。
  - **Required Semantic:** Agent alone owns Goal relevance and continue/stop/escalate authority；Container owns retained page-local evidence only；Traversal owns one movement mechanics；positive exhaustion is criterion evidence, not snapshot/rejection/budget truth；GoalEvidence alone retains final completion authority。
  - **Approved Production Purchase:** Task 1.1 exact one-type/four-field delta；this task adds no production type/field/enum/interface/component/mutable-state owner and may change only existing Agent private helpers/control flow；Ownership/Authority Delta = NONE。
  - **Allowed Scope:** `src/UniClaw.Runtime/Agent/Agent.cs` minimum private helper/control flow；direct Agent/Scenario behavior tests；read-only use of existing Plan, Trace, Container snapshot, Traversal journal, GoalEvidence, SC-P3-001/003 behavior；必要时本 tasks.md progress。
  - **Forbidden Scope:** production changes to Model/Container beyond Task 1.1、Traversal/Recovery/Environment/DeviceAction/Trace/Trap/journal；new state/type/field/enum/interface/component；criterion evaluation in Container/Traversal；hardcoded end text in Runtime；dynamic plan mutation；generic loop/policy/retry/manager；Capstone/Harness/refactor。
  - **Required Assertions:** evaluator absent preserves frozen fixed-Plan behavior；Agent evaluates only Container-owned immutable accepted evidence；true authorizes at most one next existing ScrollForward；after dispatch, fresh Observation and SC-P3-003 continuity precede another decision；false records exhaustion reason and prevents remaining viewport actions without completing from exhaustion；null records unresolved reason, performs no further viewport action, and does not fabricate completion；rejected/stale/continuity failure does not produce false exhaustion；latest true with no remaining approved viewport step reports bound-reached unresolved；same snapshot alone can remain null；only satisfied GoalEvidence completes；no blind redispatch。
  - **Verification:** targeted continue/exhausted/unresolved/bound/rejected/continuity/compatibility tests；full build/test；Architecture Guards；`scripts/check-consistency.sh`；strict OpenSpec validation；exact production-delta/ownership/authority audit。
  - **Deferred Boundary:** no dynamic planner, auto-generated PlanStep, stable viewport/content identity, generic ScrollPolicy/loop/retry/uncertainty framework, reverse scroll, Fingerprint/geometry/Vision, multi-Container state, Recovery change, Capstone, Harness, refactor。
  - **Return Contract:** `TASK_RESULT`；若不能在 exact budget 与 Agent-only authority 内实现，返回正式 `BLOCKED_FOR_SEMANTIC_REVIEW` / `BLOCKED_FOR_ARCHITECTURE_REVIEW`。完成后由 H4-3 重读 repository truth。

## 3. SC-P3-CAND-007 Formal Scenario Verification

- [x] 3.1 **Prove repeated continuation, positive exhaustion, unresolved, bound, failure, completion boundary, and replay**
  - **Task ID:** 3.1
  - **Role:** runtime-coder
  - **Tier:** standard
  - **Depends On:** 2.1 DONE
  - **Scenario Receipt:** SC-P3-CAND-007
  - **Goal:** 使用 Task 1.1 deterministic fixture 与 Task 2.1 behavior 建立 formal end-to-end SC-P3-CAND-007 evidence：V1→V2→V3 two-scroll positive exhaustion、ambiguous same/different evidence、bound reached、dispatch/continuity failure、GoalEvidence-only completion、retained evidence/replay；不新增生产行为。
  - **Required Semantic:** movement != progress；changed evidence != relevant work；no new content != exhaustion；bound != exhaustion；positive exhaustion stops movement only；unresolved required exploration cannot silently complete；GoalEvidence remains final completion authority。
  - **Approved Production Purchase:** Production Model Delta = 0；Production Behavior Delta = 0。本任务只购买 formal Scenario evidence。
  - **Allowed Scope:** `tests/UniClaw.Runtime.Tests/Scenario/**` SC-P3-CAND-007 formal tests、Task 1.1 fixture and minimum test-side harness/goals/plans/helpers，以及本 tasks.md Task 3.1 progress。
  - **Forbidden Scope:** `src/UniClaw.Runtime/**` unless an explicit implementation regression blocks approved proof；approved Spec/Scenario changes；new production artifact/state；other Scenario/Capstone/Harness/refactor。
  - **Required Assertions:** formal Scenario Required Assertions 1–12；positive branch retains V1/V2/V3, returns true/true/false with reasons, dispatches exactly two ScrollForward, never dispatches third, preserves same Container/local progress, and completes only from independent GoalEvidence；same-evidence no-end branch null/zero further dispatch/no completion；bound branch latest true + no remaining movement → unresolved/not exhausted；rejected/stale/identity-conflict do not append invalid evidence or fabricate exhaustion；equal inputs replay equal evidence/outcomes/actions/journal/Trace/GoalEvidence/RunState。
  - **Verification:** targeted formal/replay tests；all frozen SC-P1/P2/P3 regressions；full build/test；Architecture Guards；consistency；strict OpenSpec validation；Scenario assertions audit。
  - **Deferred Boundary:** no production change, stable viewport identity, generic scrolling framework, dynamic discovery/planning, Fingerprint/geometry/Vision, multi-Container state, Recovery change, Capstone, Harness, refactor, Phase completion。
  - **Return Contract:** `TASK_RESULT`；完成后由 H4-3 重读 repository truth。

## 4. Independent Regression and Boundary Validation

- [x] 4.1 **Independent SC-P3-CAND-007 slice acceptance**
  - **Task ID:** 4.1
  - **Role:** runtime-validator
  - **Tier:** standard
  - **Depends On:** 3.1 DONE
  - **Scenario Receipt:** SC-P3-CAND-007
  - **Goal:** fresh reload repository truth，独立审计 Task 1.1/2.1/3.1 actual diff、formal evidence、exact one-type/four-field budget、Container-only retained evidence owner、Agent-only decision authority、honest exhaustion/unresolved/bound behavior、GoalEvidence completion boundary、deterministic replay 与全部既有回归。
  - **Required Semantic:** current viewport exhaustion != Container exploration exhaustion；movement != progress；same/changed snapshot != semantic conclusion；bound/rejection != exhaustion；only positive criterion evidence may stop as exhausted；GoalEvidence alone completes。
  - **Approved Production Purchase:** exactly one immutable two-field `ViewportExplorationEvidence` + one optional immutable Goal field + one Container-owned retained-evidence field；enums/interfaces/components/new mutable-state owners = 0；Ownership/Authority Delta = NONE；validation behavior delta = 0。
  - **Allowed Scope:** read-only production/test/OpenSpec/diff audit；run build/tests/guards/consistency/strict validation；all PASS 后 only mark Task 4.1 complete。
  - **Forbidden Scope:** repair production/tests/spec；accept coder summary instead of fresh evidence；add artifact/state/dependency；change frozen architecture；start Capstone/Harness/other Scenario/Phase completion。
  - **Required Assertions:** formal Scenario assertions 1–12 all satisfied；exact budget/no other production delta；continue/exhausted/unresolved/bound/rejected/stale/conflict/replay branches correct；Container retained evidence has one owner；Agent decision authority remains unique；Traversal/Environment/Recovery boundaries unchanged；Goal completion unchanged；all frozen regressions PASS；all deferred capabilities absent。
  - **Verification:** `dotnet build src/UniClaw.Runtime.sln`；`dotnet test src/UniClaw.Runtime.sln`；Architecture Guard tests；`scripts/check-consistency.sh`；`openspec validate phase3-viewport-exploration-exhaustion --strict`；production-delta/ownership/authority/determinism/evidence audit。
  - **Deferred Boundary:** all capabilities outside bounded repeated forward exploration inside one semantic Container remain absent；structural pressure may be recorded but not repaired。
  - **Return Contract:** `VALIDATION_RESULT`；Verdict only `PASS | CONDITIONAL_PASS | FAIL`。PASS 后 H4-3 may create Scenario-specific closeout and then stop FROZEN。

## Design Docs

> Auto-generated from proposal Impact section.
> Implementation agents: read these before starting.

| Module | Design Doc |
|--------|------------|
| `src/UniClaw.Runtime/Model/` | `docs/system/constitution/runtime-architecture-contract.md` |
| `src/UniClaw.Runtime/Container/` | `docs/system/layers/container-runtime.md` |
| `src/UniClaw.Runtime/Agent/` | `docs/system/layers/agent-runtime.md` |
| `tests/UniClaw.Runtime.Tests/Scenario/` | `openspec/changes/phase3-viewport-exploration-exhaustion/scenarios/SC-P3-CAND-007-viewport-exploration-exhaustion.md` |
| `openspec/changes/phase3-viewport-exploration-exhaustion/` | `openspec/changes/phase3-viewport-exploration-exhaustion/design.md` |
