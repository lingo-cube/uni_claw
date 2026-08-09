# Tasks — phase3-recovery-progress-resume

> 实施前必读: `proposal.md` + `design.md` + `specs/recovery-progress-resume/spec.md` + `scenarios/SC-P3-CAND-005-recovery-progress-resume.md`。
> 完成一项立即勾选 `- [x]`。一次只执行一个 Task ID；H4-3 每项完成后重新读取 repository truth 再决定下一项。
> Reopened SC-P3-CAND-005 Gate 已批准为 `SEMANTIC_PURCHASE_REQUIRED`；Production Delta Budget = exactly one optional immutable `PlanStep.BranchEffectEvidenceEvaluator: Func<Observation, bool?>?` field；model types/enums/interfaces/components/new mutable-state owners = 0；Ownership Delta = NONE；Authority Delta = NONE。
> 任一执行者若发现需要第二字段/新类型/enum/interface/component/owner、改变 frozen ownership/authority/Recovery dependency boundary、引入 validity/replay/recovery framework，或无法保持 criterion 为 deterministic side-effect-free Observation-only evaluation，立即停止并返回对应 Semantic/Architecture Gate。

## Dependency Order

```text
1.1 Branch Criterion and Deterministic Recovery-Progress Fixture
→ 2.1 Agent Revalidation and Bounded Resume Behavior
→ 3.1 Formal Scenario Proof
→ 4.1 Independent Validation
```

## 1. Minimum Criterion Semantic and Deterministic Scenario Capability

- [x] 1.1 **Add the approved branch-effect criterion field and deterministic Recovery-progress fixture**
  - **Task ID:** 1.1
  - **Role:** runtime-coder
  - **Tier:** standard
  - **Depends On:** NONE
  - **Scenario Receipt:** SC-P3-CAND-005
  - **Goal:** 在严格批准预算内给 existing immutable `PlanStep` 增加唯一 optional `BranchEffectEvidenceEvaluator: Func<Observation, bool?>?` 字段，并建立纯测试侧确定性 world/fixture，使 Fake 能表达：P 的 A/B inventory、A external effect proven、Launcher drift、verified Recovery fresh P evidence、effect survived/contradicted/unobservable 三分支，以及相同输入重放。
  - **Required Semantic:** criterion 是 Agent-owned Plan hypothesis，不是 proof；必须 deterministic、side-effect-free、Observation-only；`true` = fresh positive proof holds，`false` = fresh positive proof does not hold，`null`/absent = unresolved。Observation/Fake 只报告外部证据，不输出 branch validity、resume、completion 或 Agent decision。
  - **Approved Production Purchase:** exactly one optional immutable production field on `PlanStep`；new model types/enums/interfaces/components/mutable-state owners = 0；Production Behavior Change = NONE；Ownership/Authority Delta = NONE。
  - **Allowed Scope:** `src/UniClaw.Runtime/Model/Plan.cs` only for the approved backward-compatible field；`tests/UniClaw.Runtime.Tests/Scenario/Fakes/**`、existing ScriptedEnvironment test-side extension if strictly needed、SC-P3-CAND-005 fixture/helper/tests；必要时仅更新本 tasks.md progress。
  - **Forbidden Scope:** `src/UniClaw.Runtime/Agent/**`、Container/Traversal/Recovery/Environment production behavior；second production field/type/enum/interface/component/state；criterion parser/registry/framework；validity state；提前实现 Task 2.1；修改 approved semantic/ownership/authority；Capstone/Harness/refactor。
  - **Required Assertions:** existing two-argument `PlanStep` construction remains source-compatible and yields absent criterion；fixture branch-entry A carries an evaluator and B may remain independent；same fresh survived Observation deterministically returns `true`；contradicted Observation returns `false`；unobservable Observation returns `null`；evaluator reads only its Observation；world scripts P→A→local effect→P→drift→Recovery P and remaining B transitions without emitting semantic conclusions；ActionHistory/Observation/world evidence replay equally for equal inputs；existing ScriptedEnvironment variants unchanged。
  - **Verification:** Plan/Fake/fixture targeted tests；existing `ScriptedEnvironmentTests`；`dotnet build src/UniClaw.Runtime.sln`；production-delta audit proves exactly one field and no behavior change outside the value surface。
  - **Deferred Boundary:** Agent revalidation/invalidation/resume behavior、formal end-to-end proof、persistent validity state、epoch/freshness field、EffectRegistry、idempotence taxonomy、checkpoint/ResumeToken/manager/planner/graph/stack/FSM、autonomous safety、Capstone、Runtime refactor。
  - **Return Contract:** `TASK_RESULT`；success requires `Status: DONE` and exact one-field delta。完成后停止并由 H4-3 重读 repository truth。

## 2. Agent Revalidation and Bounded Resume Behavior

- [x] 2.1 **Derive recovered-world branch validity and resume without blind prefix replay**
  - **Task ID:** 2.1
  - **Role:** runtime-coder
  - **Tier:** standard
  - **Depends On:** 1.1 DONE
  - **Scenario Receipt:** SC-P3-CAND-005
  - **Goal:** 在 existing Agent Recovery/control flow 内实现 one-parent/one-Recovery approved behavior：pre-drift completion becomes retained history；`RecoveryResult.Verified` 后使用 fresh Observation 评估 matching branch-entry criterion；true refreshes evidence and directly resumes remaining B；false excludes A；null/absent remains unresolved；所有非 true 分支显式 non-completion/escalation且不 blind replay A。
  - **Required Semantic:** `Trap.Observed` is freshness boundary；criterion/Plan is hypothesis；fresh Observation is evidence；Agent alone interprets progress validity and owns resume/escalation/final RunState；Recovery verifies position only；`IsSubtreeComplete` historical coverage cannot authorize post-Recovery contribution before revalidation；Goal completion remains satisfied GoalEvidence only。
  - **Approved Production Purchase:** Task 1.1 exact one `PlanStep` field；this task adds no production model/type/field/enum/interface/component/mutable-state owner and may modify only existing Agent private helpers/control flow；Ownership/Authority Delta = NONE。
  - **Allowed Scope:** `src/UniClaw.Runtime/Agent/Agent.cs` minimum private helpers/control-flow changes；direct unit/Scenario tests under `tests/`；read-only use of existing `BranchProgressEvidence`, Trap, Recovery, Observation, GoalEvidence, Trace, Container, Traversal surfaces；必要时仅更新本 tasks.md progress。
  - **Forbidden Scope:** production changes to `BranchProgressEvidence`/Plan field beyond Task 1.1、Container/Traversal/Recovery/Environment/Goal/Observation/Trap/Result；new state field/type/enum/interface/component；Recovery → Container/Traversal dependency；predicate/validity/replay/recovery framework；generic prefix planner/idempotence taxonomy/checkpoint；Runtime refactor；other Scenario/Capstone/Harness。
  - **Required Assertions:** completion sequences at/before `LastTrap.Observed` do not contribute automatically；criterion evaluated only after verified Recovery against strict-fresh Observation；correct P identity/inventory/Recovery success alone do not validate A；true refreshes A completion sequence beyond boundary, leaves B pending, skips A-entry/A-work/A-return prefix, dispatches A external-effect action exactly once, and continues B；false removes/excludes A, preserves historical journal/Trace, produces no subtree/Goal success, and does not redispatch A；null/absent does not promote A, produces explicit Agent failure/escalation, and does not redispatch A；Recovery owns no progress mutation/decision；final Completed only from satisfied GoalEvidence；SC-P1/P2/P3-001/002/003/CAND-004 regressions unchanged。
  - **Verification:** targeted Agent/recovery-progress positive/false/null/absent/no-blind-replay tests；relevant frozen Recovery and sibling-progress regressions；full `dotnet build` + `dotnet test`；Architecture Guards；`scripts/check-consistency.sh`；strict OpenSpec validation；exact production-delta/ownership/authority audit。
  - **Deferred Boundary:** no persistent validity state, generic predicate/effect registry, action taxonomy, checkpoint/ResumeToken, replay/recovery planner, graph/stack/FSM, second Recovery, autonomous safety, Capstone, new component/owner, or structural refactor。
  - **Return Contract:** `TASK_RESULT`；若不能在 exact one-field budget和existing Agent authority内实现，返回正式 `BLOCKED_FOR_SEMANTIC_REVIEW` / `BLOCKED_FOR_ARCHITECTURE_REVIEW`。完成后停止并由 H4-3 重读 repository truth。

## 3. SC-P3-CAND-005 Formal Scenario Verification

- [x] 3.1 **Prove revalidated, contradicted, unresolved, no-blind-replay, and deterministic replay branches**
  - **Task ID:** 3.1
  - **Role:** runtime-coder
  - **Tier:** standard
  - **Depends On:** 2.1 DONE
  - **Scenario Receipt:** SC-P3-CAND-005
  - **Goal:** 使用 Task 1.1 deterministic fixture 与 Task 2.1 behavior 建立正式 end-to-end SC-P3-CAND-005 evidence，覆盖 positive、contradicted、unresolved、missing criterion、position-only negative、no-blind-replay、completion authority 与 replay；不新增生产行为。
  - **Required Semantic:** historical completion != recovered-world valid effect；world position recovered != prior effect survived；criterion != proof；false != null；revalidated branch progress != Goal completion；no blind replay belongs only to the bounded same-parent Scenario。
  - **Approved Production Purchase:** Production Model Delta = 0；Production Behavior Delta = 0。本任务只购买 formal Scenario evidence。
  - **Allowed Scope:** `tests/UniClaw.Runtime.Tests/Scenario/**` SC-P3-CAND-005 formal tests、Task 1.1 fixture、必要最小 test-side plans/goals/helpers，以及本 tasks.md Task 3.1 progress。
  - **Forbidden Scope:** `src/UniClaw.Runtime/**` unless an explicit implementation regression blocks the approved proof；approved Spec/Scenario semantic changes；new production artifact/state；Runtime refactor；other Scenario/Capstone/Harness。
  - **Required Assertions:** positive proves A historical sequence ≤ drift boundary, verified Recovery fresh sequence > boundary, criterion true, A sequence refreshed, A external-effect action exactly once, no A prefix replay, B independently executes/proves, final completion only from GoalEvidence；contradicted proves false removes/excludes A and no completion/replay；unresolved and missing criterion prove no contribution/no fabricated success/no replay/explicit Agent outcome；position-only parent/Container/Recovery evidence cannot validate A；Recovery has no progress authority；same RunId/world/Plan criteria/disturbance/actions replay equal criterion outcomes/progress/ActionHistory/Observations/journal/Trace/GoalEvidence/final RunState。
  - **Verification:** SC-P3-CAND-005 targeted/replay tests；all SC-P1/P2/P3-001/002/003/CAND-004 regressions；full build/test；Architecture Guards；consistency；strict OpenSpec validation；Scenario Required Assertions 1–12 audit。
  - **Deferred Boundary:** no production change, validity state/enum, generic evidence/replay/recovery framework, idempotence taxonomy, checkpoint/ResumeToken/planner/graph/stack/FSM, autonomous safety, Capstone, Runtime refactor, Phase completion。
  - **Return Contract:** `TASK_RESULT`；完成后停止并由 H4-3 重读 repository truth。

## 4. Independent Regression and Boundary Validation

- [x] 4.1 **Independent SC-P3-CAND-005 slice acceptance**
  - **Task ID:** 4.1
  - **Role:** runtime-validator
  - **Tier:** standard
  - **Depends On:** 3.1 DONE
  - **Scenario Receipt:** SC-P3-CAND-005
  - **Goal:** fresh reload repository truth，独立审计 Task 1.1/2.1/3.1 actual diff、formal evidence、exact one-field budget、ownership/authority、Recovery boundary、no-blind-replay、deterministic replay 与全部既有回归。
  - **Required Semantic:** branch criterion represents a proposition not proof；only fresh post-verified-Recovery evidence derives true/false/null；Agent owns validity/resume/Goal completion；Recovery owns position mechanics only；historical progress cannot silently become current truth。
  - **Approved Production Purchase:** exactly one optional immutable `PlanStep.BranchEffectEvidenceEvaluator` field；new model types/enums/interfaces/components/new mutable-state owners = 0；Ownership/Authority Delta = NONE；validation behavior delta = 0。
  - **Allowed Scope:** read-only production/test/OpenSpec/diff audit；run build/tests/guards/consistency/strict validation；all PASS 后 only mark Task 4.1 complete。
  - **Forbidden Scope:** repair production/tests/spec；accept coder summary instead of fresh evidence；add artifact/state/dependency；change frozen architecture；start autonomous safety/Capstone/other Scenario/Phase completion。
  - **Required Assertions:** formal Scenario Required Assertions 1–12 all satisfied；exact one field and no other production model delta；positive/contradicted/unresolved/missing/position-only/no-blind-replay branches correct；A action exactly once；Agent/Recovery/Container/Traversal/Environment ownership unchanged；Goal completion authority unchanged；deterministic replay PASS；Phase 1/2 and SC-P3-001/002/003/CAND-004 PASS；all deferred capabilities absent。
  - **Verification:** `dotnet build src/UniClaw.Runtime.sln`；`dotnet test src/UniClaw.Runtime.sln`；Architecture Guard tests；`scripts/check-consistency.sh`；`openspec validate phase3-recovery-progress-resume --strict`；production-delta/ownership/authority/determinism/evidence audit。
  - **Deferred Boundary:** all capabilities outside bounded one-Recovery progress validity remain absent；structural pressure may be recorded but not repaired。
  - **Return Contract:** `VALIDATION_RESULT`；Verdict only `PASS | CONDITIONAL_PASS | FAIL`。PASS 后 H4-3 may create Scenario-specific closeout and then stop FROZEN。

## Design Docs

> Auto-generated from proposal Impact section.
> Implementation agents: read these before starting.

| Module | Design Doc |
|--------|------------|
| `src/UniClaw.Runtime/Model/` | `docs/system/constitution/runtime-architecture-contract.md` |
| `src/UniClaw.Runtime/Agent/` | `docs/system/layers/agent-runtime.md` |
| `src/UniClaw.Runtime/Recovery/` | `docs/system/layers/recovery-runtime.md` |
| `tests/UniClaw.Runtime.Tests/Scenario/` | `openspec/changes/phase3-recovery-progress-resume/scenarios/SC-P3-CAND-005-recovery-progress-resume.md` |
| `openspec/changes/phase3-recovery-progress-resume/` | `openspec/changes/phase3-recovery-progress-resume/design.md` |
