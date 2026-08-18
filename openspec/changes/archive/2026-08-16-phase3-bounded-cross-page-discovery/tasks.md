# Tasks — phase3-bounded-cross-page-discovery

> 实施前必读: `proposal.md` + `design.md` + `specs/bounded-cross-page-discovery/spec.md` + `scenarios/SC-P3-CAND-008-bounded-cross-page-discovery.md`。
> 完成一项立即勾选 `- [x]`。一次只执行一个 Task ID；H4-3 每项完成后重新读取 repository truth 再决定下一项。
> SC-P3-CAND-008 Gate 已批准为 `SEMANTIC_PURCHASE_REQUIRED`；Production Delta Budget = exactly one immutable `BranchInventoryEvidence` type with two immutable fields plus one optional immutable `Goal.BranchInventoryEvaluator` field；enums/interfaces/components/new mutable-state fields/new mutable-state owners = 0；Ownership Delta = NONE；Authority Delta = NONE。
> 任一执行者若发现需要扩大预算、创建 route/frontier/depth state、改变 frozen ownership/authority、让 Container/Traversal 解释 inventory 或选择 branch、把 observed/authorized/required/selected/completed 合并，或引入 generic planner/backtracking/retry/recovery abstraction，立即停止并返回对应 Semantic/Architecture Gate。

## Dependency Order

```text
1.1 Minimum Inventory Semantic and Deterministic Cross-Page Fixture
→ 2.1 Agent Bounded Cross-Page Discovery Behavior
→ 3.1 Formal Scenario Proof
→ 4.1 Independent Validation
```

## 1. Minimum Inventory Semantic and Deterministic Scenario Capability

- [x] 1.1 **Add the approved inventory value, Goal criterion, and deterministic cross-page fixture**
  - **Task ID:** 1.1
  - **Role:** runtime-coder
  - **Tier:** standard
  - **Depends On:** NONE
  - **Scenario Receipt:** SC-P3-CAND-008
  - **Goal:** 在严格批准预算内增加 immutable `BranchInventoryEvidence(RequiredBranchEvidence, Reason)` 与 optional `Goal.BranchInventoryEvaluator` 字段，并建立纯测试侧 deterministic fixture，使 Fake 能表达 P depth 0 inventory `{A}` → A depth 1 inventory `{C}` → C depth 2 positive empty inventory；concrete A/C targets 不在 initial Plan；同时表达 unresolved inventory、authorization rejected/unresolved、depth-bound child、viewport evidence、parent revisit/progress preservation、stale/conflicting evidence与 equal-input replay。
  - **Required Semantic:** non-null non-empty map = complete bounded required inventory proven；empty non-null map = bounded leaf positively proven；null map = inventory unresolved；map entry associates branch identity with accepted source Observation sequence；Reason non-empty；inventory membership != candidate authorization != selection != dispatch != branch completion != Goal completion；evaluator deterministic、side-effect-free、bounded accepted Observation evidence only。
  - **Approved Production Purchase:** exactly one immutable production type with exactly two immutable fields plus one optional immutable Goal field；enums/interfaces/components/new mutable-state fields/new mutable-state owners = 0；Production Behavior Change = NONE；Ownership/Authority Delta = NONE。
  - **Allowed Scope:** one new Model file for `BranchInventoryEvidence`；`src/UniClaw.Runtime/Model/Goal.cs` for the approved backward-compatible optional field；`tests/UniClaw.Runtime.Tests/Scenario/Fakes/**` deterministic SC-P3-CAND-008 fixture/helper/tests；existing Model immutability and ScriptedEnvironment tests；read-only reuse of SC-P3-CAND-004 branch progress, SC-P3-CAND-006 authorization, and SC-P3-CAND-007 retained viewport evidence；必要时仅更新本 tasks.md progress。
  - **Forbidden Scope:** `src/UniClaw.Runtime/Agent/**`、Container/Traversal/Recovery/Environment production behavior；second production type、fourth production field、enum/interface/component/state；route/frontier/depth/parent stack field；new Back action；generic planner/re-plan/backtracking/discovery framework；Task 2.1 behavior；Capstone/Harness/S1/S2/S3/refactor。
  - **Required Assertions:** value has exactly nullable immutable required-branch map plus non-empty Reason and rejects blank branch identity, negative source sequence, or empty Reason；existing Goal construction remains source-compatible and evaluator defaults absent；fixture proves A/C absent from initial Plan yet visible from their supplied accepted Observations；positive empty and null unresolved inventories remain distinct；fixture independently scripts candidate authorization and world transitions without claiming semantic success；viewport evidence retains the same semantic Container/depth；stale/conflicting source evidence remains distinguishable；same inputs replay equal inventory evidence, Observations, actions, and world state；existing Fakes remain unchanged.
  - **Verification:** targeted Model/fixture tests；existing Model immutability, BranchProgress, candidate authorization, viewport retained-evidence, and ScriptedEnvironment tests；`dotnet build src/UniClaw.Runtime.sln`；production-delta audit proves exactly 1 type/3 fields and no Agent behavior change；strict OpenSpec validation。
  - **Deferred Boundary:** Agent inventory acceptance/selection/control flow、formal proof、persistent route/depth/frontier state、generic dynamic planning/backtracking、new Back、Fingerprint/Confidence/coordinates/Vision/VLM、generic retry/uncertainty/new Recovery、Capstone/Harness/S1/S2/S3/refactor。
  - **Return Contract:** `TASK_RESULT`；success requires `Status: DONE` and exact approved delta。完成后停止并由 H4-3 重读 repository truth。

## 2. Agent Bounded Cross-Page Discovery Behavior

- [x] 2.1 **Interpret complete fresh inventory and nominate one independently authorized branch at a time**
  - **Task ID:** 2.1
  - **Role:** runtime-coder
  - **Tier:** standard
  - **Depends On:** 1.1 DONE
  - **Scenario Receipt:** SC-P3-CAND-008
  - **Goal:** 在 existing Agent control flow 内实现 opt-in bounded route continuation：从 active Container 的 accepted fresh evidence 与 evidence-backed semantic depth 求 inventory；验证 non-null map 的 Container identity/source sequences；复用 existing `BranchProgressEvidence` 保存仍属于 fresh inventory 的 proven sibling progress；对 next unresolved required branch 独立调用 existing candidate authorization；每轮最多 transient nominate one existing Tap；post-action fresh Observe/Reconcile 成功进入 child Container 后才评价 child inventory；evaluator absent 时 frozen behavior 不变。
  - **Required Semantic:** Agent alone owns inventory interpretation、semantic depth、next selection、cross-Container progress、active Container、GoalEvidence、RunState；Container owns page-local accepted evidence/progress；Traversal executes one nominated local step；Environment reports Observation/dispatch outcome；Plan may constrain mechanics but cannot prove inventory；observed != required != authorized != selected != dispatched != complete。
  - **Approved Production Purchase:** reuse Task 1.1 exact one type/three fields；this task adds no production model/type/field/enum/interface/component/mutable state or owner；may modify only existing Agent private helpers/control flow；Ownership/Authority Delta = NONE。
  - **Allowed Scope:** `src/UniClaw.Runtime/Agent/Agent.cs` minimum private helper/control-flow changes；direct tests under `tests/`；read-only reuse of existing Goal/Observation/Container/Traversal/BranchProgressEvidence/CandidateAuthorizationEvidence/ViewportExplorationEvidence/Trace/GoalEvidence/ActionHistory surfaces；必要时同步 Tier 2/3 docs 与本 tasks.md progress。
  - **Forbidden Scope:** production changes to Model beyond Task 1.1、Container/Traversal/Recovery/Environment/DeviceAction/Trace/Trap/journal/BranchProgressEvidence；new field or route/depth/frontier/stack/graph/tree state；new Back or generic parent-return mechanics；dynamic planner/re-plan/action synthesis；manager/FSM/workflow；Fingerprint/Confidence/Vision/VLM；generic retry/uncertainty/new Recovery；Capstone/Harness/S1/S2/S3/refactor。
  - **Required Assertions:** evaluator receives immutable accepted same-Container evidence whose final Observation is current/fresh plus Agent-derived semantic depth；null/stale/conflicting/ambiguous-parent inventory dispatches zero and cannot overwrite valid progress or fabricate leaf；required+authorized may nominate at most one Tap；required+false/null authorization dispatches zero；authorized but not required candidate is not selected；P→A→C executes Tap A and Tap C exactly once with fresh reconciliation between them although targets are absent from initial Plan；depth-bound child is not dispatched；same-Container viewport movement does not increment semantic depth；parent revisit preserves valid completed subset and does not redispatch proven work；empty inventory alone cannot complete local branch, GoalEvidence, or RunState；evaluator absent preserves all frozen behavior。
  - **Verification:** targeted inventory/authorization/depth/progress/zero-dispatch/completion-boundary tests；full build/test；Architecture Guards；`scripts/check-consistency.sh`；strict OpenSpec validation；exact production-delta/ownership/authority audit。
  - **Deferred Boundary:** no persistent route/depth/frontier/parent stack, generic planner/backtracking/discovery framework, new Back, semantic identity algorithm, Fingerprint/Confidence/coordinates/Vision/VLM, generic retry/uncertainty, Recovery change, Capstone, Harness, S1/S2/S3, Runtime refactor。
  - **Return Contract:** `TASK_RESULT`；若不能在 exact budget 与 Agent-only authority 内实现，返回正式 `BLOCKED_FOR_SEMANTIC_REVIEW` / `BLOCKED_FOR_ARCHITECTURE_REVIEW`。完成后停止并由 H4-3 重读 repository truth。

## 3. SC-P3-CAND-008 Formal Scenario Verification

- [x] 3.1 **Prove multi-level discovery, negative boundaries, progress composition, completion authority, and replay**
  - **Task ID:** 3.1
  - **Role:** runtime-coder
  - **Tier:** standard
  - **Depends On:** 2.1 DONE
  - **Scenario Receipt:** SC-P3-CAND-008
  - **Goal:** 使用 Task 1.1 deterministic fixture 与 Task 2.1 behavior 建立 formal end-to-end SC-P3-CAND-008 evidence，覆盖 P→A→C positive route、unresolved inventory、authorization denied/unresolved、authorized-not-required、depth bound、same-Container viewport movement、stale/conflicting evidence、parent revisit/progress preservation、empty-leaf completion boundary与 deterministic replay；不新增生产行为。
  - **Required Semantic:** candidate observed != authorized != required inventory member != selected next != dispatched != branch completed；complete inventory != progress complete；empty leaf != Goal completion；semantic depth derives only from accepted parent-to-child Container transition；GoalEvidence remains final completion authority。
  - **Approved Production Purchase:** Production Model Delta = 0；Production Behavior Delta = 0。本任务只购买 formal Scenario evidence。
  - **Allowed Scope:** `tests/UniClaw.Runtime.Tests/Scenario/**` SC-P3-CAND-008 formal tests、Task 1.1 fixture and minimum test-side goals/plans/helpers，以及本 tasks.md Task 3.1 progress。
  - **Forbidden Scope:** `src/UniClaw.Runtime/**` unless an explicit implementation regression blocks approved proof；approved Spec/Scenario semantic changes；new production artifact/state；other Scenario/Capstone/Harness/S1/S2/S3/refactor。
  - **Required Assertions:** formal Scenario Required Assertions 1–12；positive route derives `{A}`, dispatches A once, fresh-reconciles A, derives `{C}`, dispatches C once, fresh-reconciles C, receives positive empty inventory, and completes only when independent GoalEvidence is satisfied；A/C absent from initial Plan targets；null inventory and required false/null authorization yield zero matching dispatch and no fabricated leaf/completion；authorized-not-required is not selected；depth-bound child zero dispatch；viewport evidence does not consume depth；stale/conflicting evidence preserves valid inventory/progress；parent revisit does not redispatch proven A and leaves B unresolved；equal inputs replay equal inventories/reasons/progress/actions/journal/Trace/GoalEvidence/RunState。
  - **Verification:** targeted formal/replay tests；all frozen SC-P1/P2/P3 regressions；full build/test；Architecture Guards；consistency；strict OpenSpec validation；Scenario assertions audit。
  - **Deferred Boundary:** no production change, dynamic planning/backtracking, graph/tree/stack/frontier/route state, new Back, generic discovery/policy framework, Fingerprint/Confidence/Vision/VLM, generic retry/uncertainty, Recovery change, Capstone, Harness, S1/S2/S3, refactor, Phase completion。
  - **Return Contract:** `TASK_RESULT`；完成后停止并由 H4-3 重读 repository truth。

## 4. Independent Regression and Boundary Validation

- [x] 4.1 **Independent SC-P3-CAND-008 slice acceptance**
  - **Task ID:** 4.1
  - **Role:** runtime-validator
  - **Tier:** standard
  - **Depends On:** 3.1 DONE
  - **Scenario Receipt:** SC-P3-CAND-008
  - **Goal:** fresh reload repository truth，独立审计 Task 1.1/2.1/3.1 actual diff、formal evidence、exact one-type/three-field budget、zero new mutable state、Agent-only inventory/depth/selection authority、Container/Traversal/Environment/Recovery boundaries、honest completion、deterministic replay 与全部既有回归。
  - **Required Semantic:** inventory membership、authorization、selection、dispatch、branch progress、leaf evidence、GoalEvidence、RunState remain distinct；Agent is sole cross-Container semantic authority；lower scopes provide local evidence/mechanics only；GoalEvidence alone may complete。
  - **Approved Production Purchase:** exactly one immutable two-field `BranchInventoryEvidence` type plus one optional immutable Goal field；new enums/interfaces/components/mutable-state fields/mutable-state owners = 0；Ownership/Authority Delta = NONE；validation behavior delta = 0。
  - **Allowed Scope:** read-only production/test/OpenSpec/diff audit；run build/tests/guards/consistency/strict validation；all PASS 后 only mark Task 4.1 complete。
  - **Forbidden Scope:** repair production/tests/spec；accept coder summary instead of fresh evidence；add artifact/state/dependency；change frozen architecture；start Capstone/Harness/S1/S2/S3/other Scenario/Phase completion。
  - **Required Assertions:** formal Scenario assertions 1–12 all satisfied；exact budget/no other production delta；positive/unresolved/authorization/depth/viewport/stale/conflict/progress/leaf/replay branches correct；A/C absent from Plan yet dispatched once each only from required+authorized fresh evidence；Agent/Container/Traversal/Environment/Recovery ownership unchanged；Goal completion unchanged；all frozen regressions PASS；all deferred capabilities absent。
  - **Verification:** `dotnet build src/UniClaw.Runtime.sln`；`dotnet test src/UniClaw.Runtime.sln`；Architecture Guard tests；`scripts/check-consistency.sh`；`openspec validate phase3-bounded-cross-page-discovery --strict`；production-delta/ownership/authority/determinism/evidence audit。
  - **Deferred Boundary:** all capabilities outside bounded fresh-evidence one-branch-at-a-time forward discovery remain absent；structural pressure may be recorded but not repaired。
  - **Return Contract:** `VALIDATION_RESULT`；Verdict only `PASS | CONDITIONAL_PASS | FAIL`。PASS 后 H4-3 may create Scenario-specific closeout and then stop FROZEN。

## Design Docs

> Auto-generated from proposal Impact section.
> Implementation agents: read these before starting.

| Module | Design Doc |
|--------|------------|
| `src/UniClaw.Runtime/Model/` | `docs/system/constitution/runtime-architecture-contract.md` |
| `src/UniClaw.Runtime/Agent/` | `docs/system/layers/agent-runtime.md` |
| `src/UniClaw.Runtime/Container/` | `docs/system/layers/container-runtime.md` |
| `src/UniClaw.Runtime/Traversal/` | `docs/system/layers/traversal-runtime.md` |
| `tests/UniClaw.Runtime.Tests/Scenario/` | `openspec/changes/phase3-bounded-cross-page-discovery/scenarios/SC-P3-CAND-008-bounded-cross-page-discovery.md` |
| `openspec/changes/phase3-bounded-cross-page-discovery/` | `openspec/changes/phase3-bounded-cross-page-discovery/design.md` |
