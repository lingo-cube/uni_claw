# Tasks — phase3-discovered-branch-effect-revalidation

> 实施前必读: `proposal.md` + `design.md` + `specs/discovered-branch-effect-revalidation/spec.md` + `scenarios/SC-P3-CAND-009-discovered-branch-effect-revalidation.md`。
> 完成一项立即勾选 `- [x]`。一次只执行一个 Task ID；H4-3 每项完成后重新读取 repository truth 再决定下一项。
> SC-P3-CAND-009 Gate 已批准为 `SEMANTIC_PURCHASE_REQUIRED`（Human Decision: `ACCEPT_OPTION_C_BOUNDARY`）；Production Delta Budget = exactly one immutable `BranchEffectCriterion` type with two immutable fields plus one optional immutable `Goal.DiscoveredBranchEffectCriterion` field（`BranchEffectCriterion?`）；production fields 总计 `+3`；enums/interfaces/components/new mutable-state fields/new mutable-state owners = 0；Ownership Delta = NONE；Authority Delta = NONE。
> 任一执行者若发现需要扩大预算、创建 collection/registry/resolver/identity service/route/frontier/depth state、新增 global semantic identity authority、引入 generic predicate framework、把 criterion 变成 stored validity/lifecycle/Recovery/completion state、让 Recovery/Container/Traversal 解释 branch effect 或选择分支、改变 frozen Plan/`BranchProgressEvidence`/`BranchInventoryEvidence`/`GoalEvidence` 语义，或无法保持 evaluator deterministic side-effect-free Observation-only，立即停止并返回对应 Semantic/Architecture Gate。

## Dependency Order

```text
1.1 Effect Criterion Semantic and Deterministic Post-Recovery Fixture
→ 2.1 Agent Bounded Post-Recovery Effect Revalidation Behavior
→ 3.1 Formal Scenario Proof
→ 4.1 Independent Validation
```

## 1. Minimum Effect Criterion Semantic and Deterministic Scenario Capability

- [x] 1.1 **Add the approved effect criterion, Goal carrier field, and deterministic post-Recovery fixture**
  - **Task ID:** 1.1
  - **Role:** runtime-coder
  - **Tier:** standard
  - **Depends On:** NONE（frozen SC-P3-CAND-004 / SC-P3-CAND-005 / SC-P3-CAND-006 / SC-P3-CAND-008 既有语义与 Fakes 只读复用）
  - **Scenario Receipt:** SC-P3-CAND-009
  - **Goal:** 在严格批准预算内增加 immutable `BranchEffectCriterion(BranchIdentity, Evaluator)` 与 optional `Goal.DiscoveredBranchEffectCriterion` 字段，并建立纯测试侧 deterministic fixture，使 Fake 能表达 one parent P 下：accepted CAND-008 inventory evidence 证明 required siblings A 与 B；A 不在 initial Plan 且由 CAND-006 独立授权；CAND-004 historical progress 证明 A 已证据化完成；singular Goal-held carrier 的 identity 恰为 A；verified Recovery 后 fresh recovered-world Observation 分别表达 positive true、contradicted false、unresolved null、absent carrier、identity mismatch、ambiguous parent scope、stale pre-Recovery evidence，以及 equal-input replay。
  - **Required Semantic:** carrier 是 durable external-effect hypothesis，本身不证明 inventory membership、authorization、historical completion、current validity、lifecycle、Recovery、completion 或 Goal outcome；`BranchIdentity` 只命名 bounded active parent scope 内既有语义 identity；`Evaluator` deterministic、side-effect-free、只读 supplied fresh Observation 与 caller 已捕获的 immutable 值；true/false/null = positively revalidated/positively contradicted/unobservable-or-unresolved；absent 或 identity mismatch = unresolved；非空 identity 与可空字段语义不变。
  - **Approved Production Purchase:** exactly one immutable production type with exactly two immutable fields plus one optional immutable Goal field；enums/interfaces/components/new mutable-state fields/new mutable-state owners = 0；Production Behavior Change = NONE；Ownership/Authority Delta = NONE。
  - **Allowed Scope:** one new Model file for `BranchEffectCriterion`；`src/UniClaw.Runtime/Model/Goal.cs` for the approved backward-compatible optional field；`tests/UniClaw.Runtime.Tests/Scenario/Fakes/**` deterministic SC-P3-CAND-009 fixture/helper/tests；existing Model immutability and ScriptedEnvironment tests；read-only reuse of SC-P3-CAND-004 branch progress provenance、SC-P3-CAND-005 three-way effect vocabulary、SC-P3-CAND-006 authorization、SC-P3-CAND-008 inventory evidence；必要时仅更新本 tasks.md progress。
  - **Forbidden Scope:** `src/UniClaw.Runtime/Agent/**`、Container/Traversal/Recovery/Environment production behavior；second production type、fourth production field、enum/interface/component/state；collection/map/registry/resolver service/identity service/route/frontier/depth state；Plan/`BranchProgressEvidence`/`BranchInventoryEvidence`/`GoalEvidence` 语义变更；把 carrier 挂到 `BranchProgressEvidence` 或 `BranchInventoryEvidence`；复用 `Goal.EvidenceEvaluator`；Task 2.1 behavior；Capstone/Harness/refactor。
  - **Required Assertions:** value 有恰好 BranchIdentity/Evaluator 两个字段且拒绝空/空白 identity；evaluator 为 null 时不得调用；existing Goal construction remains source-compatible and carrier defaults absent；fixture 中 A 的 identity 同时出现在 accepted inventory evidence 与 historical progress provenance 下同一 active parent；A 不出现在 initial Plan targets；B 保持 required 且 unresolved；carrier identity 恰为 A 时 fixture 能给出 fresh post-verification Observation 的 true/false/null 三态；identity mismatch 与 ambiguous parent scope 表达 unresolved；stale pre-Recovery Observation 与 fresh post-verification Observation 可区分且不能互相替代；same inputs replay equal carrier、Observations、actions、and world state；existing Fakes 保持 unchanged。
  - **Verification:** targeted Model/fixture tests；existing Model immutability、BranchProgress、effect-evaluation、authorization、inventory、ScriptedEnvironment tests；`dotnet build src/UniClaw.Runtime.sln`；production-delta audit proves exactly 1 type/3 fields and no Agent behavior change；strict OpenSpec validation。
  - **Deferred Boundary:** Agent identity-match/interpretation/control flow、formal proof、persistent validity/lifecycle/Recovery/completion state、freshness epoch、criterion registry、generic predicate framework、global semantic identity、route/frontier/depth state、DynamicPlan/planner/manager/workflow/FSM、Recovery during unfinished dynamic-discovery continuation、Capstone/Harness/refactor。
  - **Return Contract:** `TASK_RESULT`；success requires `Status: DONE` and exact approved delta。完成后停止并由 H4-3 重读 repository truth。

## 2. Agent Bounded Post-Recovery Effect Revalidation Behavior

- [x] 2.1 **Match the bounded carrier and reconcile fresh post-Recovery evidence honestly without redispatch**
  - **Task ID:** 2.1
  - **Role:** runtime-coder
  - **Tier:** standard
  - **Depends On:** 1.1 DONE
  - **Scenario Receipt:** SC-P3-CAND-009
  - **Goal:** 在 existing Agent Run control flow 内实现 opt-in bounded revalidation：carrier 存在且其 identity 在 accepted CAND-008 inventory evidence 与 CAND-004 historical completion provenance 下同一 active parent 中均精确匹配 A，且已发生一次 verified Recovery、Agent 取得 fresh post-verification Observation 之后，仅对该 fresh Observation 求值一次；true 时 A 的 historical completion 可视为对当前 reconciliation revalidated，A 可贡献且零重复 dispatch，Agent 继续独立 unresolved 的 B；false 时 A 不贡献当前 subtree/Goal 评价、historical provenance 保持可观察、零 fabricated repair/success/redispatch；null/absent/mismatch 时 A 保持 unresolved、贡献为零、不盲目 redispatch，使用 explicit existing 非完成/escalation 面；carrier 缺席时 existing frozen 行为不变。
  - **Required Semantic:** Agent 是唯一 retain/invalidate/unresolved、resume/escalation、跨 Container progress、GoalEvidence 与最终 RunState 权威；Recovery 只是 restore → observe → verify mechanics，`RecoveryResult.Verified` 不等于 branch-effect verification；historical completion、parent identity、refreshed inventory、dispatch history、pre-Recovery Observation 各自独立都不足以证明 current effect；evaluation result 是 derived nullable 结果，不得持久化为 validity/lifecycle/Recovery/completion state；criterion != proof；只有 Agent 消费的 independently satisfied GoalEvidence 才可完成 Run。
  - **Approved Production Purchase:** Task 1.1 exact one type/three fields；this task adds no production model/type/field/enum/interface/component/mutable-state owner and may modify only existing Agent private helpers/control flow；Ownership/Authority Delta = NONE。
  - **Allowed Scope:** `src/UniClaw.Runtime/Agent/Agent.cs` minimum private helper/control-flow changes；direct tests under `tests/`；read-only use of existing Goal/Observation/ObservedElement/Container/Traversal/Recovery/Trace/GoalEvidence/BranchProgressEvidence/BranchInventoryEvidence/ActionHistory surfaces；必要时同步 Tier 2/3 docs 与本 tasks.md progress。
  - **Forbidden Scope:** production changes to Model beyond Task 1.1、Container/Traversal/Recovery/Environment/DeviceAction/Trace/Trap/journal/Plan/`BranchProgressEvidence`/`BranchInventoryEvidence`/`GoalEvidence`；new state/type/field/enum/interface/component；stored validity/lifecycle/Recovery/completion status、freshness epoch、registry/collection/map；global identity authority 或 fuzzy matching/generated identity；Recovery 对 branch progress 的解释或新 Recovery ownership/dependency；Recovery during unfinished dynamic-discovery continuation；generic planner/re-plan/manager/workflow/FSM；硬编码 branch-effect 判定词汇；Capstone/Harness/refactor。
  - **Required Assertions:** 只有 inventory+progress+carrier 三者在同一 parent 下精确命中 A 才求值；求值只发生在 verified Recovery 之后且输入仅为 fresh post-verification Observation；stale/pre-Recovery evidence、正确 P identity、refreshed inventory、成功 local mechanics、`RecoveryResult.Verified` 任一单独均不触发求值或贡献；true 分支 A 零重复 dispatch 且 B 可继续；false 分支 historical provenance 仍可观察、A 零贡献、无 fabricated repair/completion/redispatch；null/absent/mismatch/ambiguous parent 分支 A unresolved、零贡献、零 blind redispatch、有 explicit 非完成/escalation 记录；carrier 不使 A 成为 PlanStep、不证明 inventory/authorization/completion/validity；evaluation result 不落入任何新持久字段；GoalEvidence 语义与 completion authority 不变；evaluator absent 时冻结的 Phase 1/2/3 既有行为逐一保持；equal inputs replay equal 三态 outcome、贡献、actions、journal、Trace、GoalEvidence、RunState。
  - **Verification:** targeted positive/contradicted/unresolved/absent-carrier/identity-mismatch/no-redispatch/authority/replay tests；full build/test；Architecture Guards；`scripts/check-consistency.sh`；strict OpenSpec validation；exact production-delta/ownership/authority audit。
  - **Deferred Boundary:** no persistent validity/lifecycle/Recovery/completion state、criterion collection/registry、generic predicate framework、global semantic identity、route/frontier/depth/checkpoint/ResumeToken、DynamicPlan/planner、BranchManager/ProgressManager/workflow/FSM、generalized multi-parent routing、generalized branch lifecycle、Recovery during unfinished discovery continuation、Capstone/Harness/refactor。
  - **Return Contract:** `TASK_RESULT`；若不能在 exact budget 与 Agent-only authority 内实现，返回正式 `BLOCKED_FOR_SEMANTIC_REVIEW` / `BLOCKED_FOR_ARCHITECTURE_REVIEW`。完成后停止并由 H4-3 重读 repository truth。

## 3. SC-P3-CAND-009 Formal Scenario Verification

- [x] 3.1 **Prove positive, contradicted, unresolved, absent-carrier, identity-boundary, and replay branches**
  - **Task ID:** 3.1
  - **Role:** runtime-coder
  - **Tier:** standard
  - **Depends On:** 2.1 DONE
  - **Scenario Receipt:** SC-P3-CAND-009
  - **Goal:** 使用 Task 1.1 deterministic fixture 与 Task 2.1 behavior 建立 formal end-to-end SC-P3-CAND-009 evidence，覆盖 historical A completion + one external drift + verified Recovery + fresh Observation 的 positive revalidation、contradicted effect、unresolved effect、absent carrier、identity mismatch、stale evidence、ambiguous parent scope、zero duplicate dispatch、B 继续、GoalEvidence authority、Recovery boundary 与 deterministic replay；不新增生产行为。
  - **Required Semantic:** historical completion != Recovery verification != current effect validity；`RecoveryResult.Verified` != branch-effect verification；criterion != proof；identity 只可在同一 bounded parent 下精确匹配；null != false != true；observed/inventoried/authorized/completed != revalidated；contribution != completion；只有独立满足的 GoalEvidence 完成 Run。
  - **Approved Production Purchase:** Production Model Delta = 0；Production Behavior Delta = 0。本任务只购买 formal Scenario evidence。
  - **Allowed Scope:** `tests/UniClaw.Runtime.Tests/Scenario/**` SC-P3-CAND-009 formal tests、Task 1.1 fixture、必要最小 test-side harness/goals/helpers，以及本 tasks.md Task 3.1 progress。
  - **Forbidden Scope:** `src/UniClaw.Runtime/**` unless an explicit implementation regression blocks approved proof；approved Spec/Scenario semantic changes；new production artifact/state；other Scenario/Capstone/Harness/refactor。
  - **Required Assertions:** formal Scenario Required Assertions 1–12 全部成立；A 从 accepted evidence 被发现且不在 initial Plan targets；carrier 不产生 PlanStep、不证明 inventory/authorization/completion/validity；求值仅发生在一次 verified Recovery 后且只使用 fresh Observation；true 分支 A 可贡献、A 零重复 dispatch、B 可继续；false 分支 provenance 可观察但 A 零贡献且无 fabricated repair/success；null/absent/mismatch/stale/ambiguous 分支 A unresolved、零贡献、零 blind redispatch、有 explicit 非完成/escalation 证据；derived nullable 结果不落入持久 validity/lifecycle/Recovery/completion 状态；`BranchProgressEvidence`/`BranchInventoryEvidence`/Plan/`GoalEvidence` 语义不变；Recovery 保持 restore → observe → verify mechanics 且无 branch-effect 解释；Agent 仍是唯一 retain/invalidate/unresolved、resume/escalation、progress、GoalEvidence、RunState 权威；equal inputs replay equal 三态 outcome、progress contribution、actions、journal、Trace、GoalEvidence、final RunState；全部 frozen Phase 1/2/3 切片回归通过。
  - **Verification:** targeted formal/replay tests；all SC-P1/P2/P3-001/002/003/CAND-004/CAND-005/CAND-006/CAND-008 regressions；full build/test；Architecture Guards；consistency；strict OpenSpec validation；Scenario assertions audit。
  - **Deferred Boundary:** no production change、persistent effect-validity state、criterion registry、generic predicate framework、global identity authority、route/frontier/depth/checkpoint/ResumeToken、DynamicPlan/planner/manager/workflow/FSM、Recovery during unfinished discovery continuation、Capstone/Harness/refactor、Phase completion。
  - **Return Contract:** `TASK_RESULT`；完成后停止并由 H4-3 重读 repository truth。

## 4. Independent Regression and Boundary Validation

- [x] 4.1 **Independent SC-P3-CAND-009 slice acceptance**
  - **Task ID:** 4.1
  - **Role:** runtime-validator
  - **Tier:** standard
  - **Depends On:** 3.1 DONE
  - **Scenario Receipt:** SC-P3-CAND-009
  - **Goal:** fresh reload repository truth，独立审计 Task 1.1/2.1/3.1 actual diff、formal evidence、exact one-type/three-field budget、carrier 的 criterion-not-proof 语义、identity 只在同一 bounded parent 下精确匹配、求值仅发生在 verified Recovery 后的 fresh Observation、三态 reconciliation（true 可贡献可继续 B / false 零贡献 / null/absent/mismatch unresolved）、zero duplicate/zero blind redispatch、Agent-only authority、无持久 effect-validity state、deterministic replay 与全部既有回归。
  - **Required Semantic:** historical completion != Recovery verification != current effect validity；`RecoveryResult.Verified` 不 imply branch-effect verification；carrier 不建立 discovery/membership/authorization/completion/freshness；identity mismatch/ambiguous/stale 保持 unresolved；Recovery 只是 restore → observe → verify mechanics；Agent 是唯一 retain/invalidate/unresolved、resume/escalation、progress、GoalEvidence、RunState 权威；GoalEvidence remains final completion authority。
  - **Approved Production Purchase:** exactly one immutable two-field `BranchEffectCriterion` type plus one optional immutable `Goal.DiscoveredBranchEffectCriterion` field；production fields 总计 `+3`；new enums/interfaces/components/new mutable-state fields/new mutable-state owners = 0；Ownership/Authority Delta = NONE；validation behavior delta = 0。
  - **Allowed Scope:** read-only production/test/OpenSpec/diff audit；run build/tests/guards/consistency/strict validation；all PASS 后 only mark Task 4.1 complete。
  - **Forbidden Scope:** repair production/tests/spec；accept coder summary instead of fresh evidence；add artifact/state/dependency；change frozen architecture；start Capstone/Harness/other Scenario/Phase completion。
  - **Required Assertions:** formal Scenario assertions 1–12 all satisfied；exact budget 且无其他生产 delta；positive/contradicted/unresolved/absent-carrier/identity-mismatch/stale/ambiguous-parent/replay branches 正确；true 分支零重复 dispatch 且 B 可继续；false 分支无 fabricated repair/success；null/absent/mismatch 分支零 blind redispatch 且有 explicit 非完成/escalation；derived nullable 结果无持久化；`BranchProgressEvidence`/`BranchInventoryEvidence`/Plan/`GoalEvidence` 语义不变；Recovery/Container/Traversal/Environment 无新 branch-effect ownership/authority；Agent 唯一 authority 保持不变；Goal completion 不变；Phase 1/2 与全部 frozen Phase 3 slices PASS；所有 deferred capabilities 缺席。
  - **Verification:** `dotnet build src/UniClaw.Runtime.sln`；`dotnet test src/UniClaw.Runtime.sln`；Architecture Guard tests；`scripts/check-consistency.sh`；`openspec validate phase3-discovered-branch-effect-revalidation --strict`；production-delta/ownership/authority/determinism/evidence audit。
  - **Deferred Boundary:** all capabilities outside the bounded one-carrier one-parent one-Recovery revalidation round remain absent；structural pressure may be recorded but not repaired。
  - **Return Contract:** `VALIDATION_RESULT`；Verdict only `PASS | CONDITIONAL_PASS | FAIL`。PASS 后 H4-3 may create Scenario-specific closeout and then stop FROZEN。

## Design Docs

> Auto-generated from proposal Impact section.
> Implementation agents: read these before starting.

| Module | Design Doc |
|--------|------------|
| `src/UniClaw.Runtime/Model/` | `docs/system/constitution/runtime-architecture-contract.md` |
| `src/UniClaw.Runtime/Agent/` | `docs/system/layers/agent-runtime.md` |
| `tests/UniClaw.Runtime.Tests/Scenario/` | `openspec/changes/phase3-discovered-branch-effect-revalidation/scenarios/SC-P3-CAND-009-discovered-branch-effect-revalidation.md` |
| `openspec/changes/phase3-discovered-branch-effect-revalidation/` | `openspec/changes/phase3-discovered-branch-effect-revalidation/design.md` |
