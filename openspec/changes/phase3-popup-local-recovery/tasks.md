# Tasks — phase3-popup-local-recovery

> 实施前必读: `proposal.md` + `design.md` + `specs/popup-local-recovery/spec.md` + `scenarios/SC-P3-002-popup-obstruction-recovery.md`。
> 完成一项立即勾选 `- [x]`。一次只执行一个 Task ID；每项完成并验证后，由 phase-evolution-controller 选择下一项。
> SC-P3-002 已批准为 `BEHAVIOR_PURCHASE_ONLY`；Production Model Delta Budget = 0；Ownership Delta = NONE；Authority Delta = NONE。
> 任一执行者若发现必须新增 production type / field / enum / interface / component / mutable state，或必须改变 frozen ownership / authority / dependency boundary，立即停止并返回对应 Semantic/Architecture Gate，不得扩大任务。

## Dependency Order

```text
1.1 Deterministic Popup Fixture
→ 2.1 Container-scope Runtime Behavior
→ 3.1 Formal Scenario Proof
→ 4.1 Independent Validation
```

## 1. Deterministic Popup Scenario Capability

- [x] 1.1 **Scripted Popup obstruction and continuity proof fixture**
  - **Task ID:** 1.1
  - **Role:** runtime-coder
  - **Tier:** standard
  - **Depends On:** NONE
  - **Scenario Receipt:** SC-P3-002
  - **Goal:** 扩展现有测试 Fake/Harness，使其确定性表达：已有 active Container local progress 后出现 Popup/Overlay；批准的有界 dismiss action；dismiss 成功且底层 Container 连续；dismiss 失败；dismiss 成功但连续性无法证明或页面已改变；以及相同输入的确定性重放。
  - **Required Semantic:** Popup 是外部 obstruction evidence，不是语义页面变化或 Container 连续性的直接真相；dismiss dispatch outcome 也不是 local recovery success。测试必须能够在不新增生产语义的情况下分别重放正向和升级分支。
  - **Approved Production Purchase:** Production Model Delta = 0；Production Changes = NONE。本任务只购买测试侧确定性证明能力。
  - **Allowed Scope:** `tests/UniClaw.Runtime.Tests/Scenario/Fakes/ScriptedEnvironment.cs`、`tests/UniClaw.Runtime.Tests/Scenario/Fakes/ScriptedEnvironmentVariants.cs`、`tests/UniClaw.Runtime.Tests/Scenario/ScriptedEnvironmentTests.cs`，以及表达既有 local progress、Popup action 和三种结果分支所必需的最小 Scenario helper/fixture 文件。
  - **Forbidden Scope:** `src/UniClaw.Runtime/**`；OpenSpec Scenario/Spec 意义修改；生产 type/field/enum/interface/component/mutable state；新测试 framework；Harness 重写；任何 Runtime behavior、ownership 或 authority 变化；提前实现 Task 2.1。
  - **Required Assertions:** Fake 可先建立可观察的 Container local progress，再出现 obstruction；local dismiss action 的 dispatch 与世界转场可独立配置；连续分支返回严格递增的新 Observation 并重新显露同一语义页；dismiss-failed 分支不伪造世界变化；dismiss-succeeded-but-page-changed/unproven 分支返回可区分的 fresh evidence；ActionHistory 证明处理有界；相同配置、RunId 与动作序列产生相同 Observation、ActionHistory 和 fixture evidence；现有 Fake 变体行为不变。
  - **Verification:** 运行 `ScriptedEnvironmentTests` 和新增 fixture 定向测试；运行现有 Fake variant tests；检查 `src/UniClaw.Runtime/**` 零改动；确认三种分支均可 deterministic replay。
  - **Deferred Boundary:** 不实现 Container-scope Runtime 行为；不新增 Popup production model、TrapKind、RecoveryResult、Container state、recovery component、Fingerprint、Confidence、Scroll、generic overlay/retry/uncertainty/recovery framework、multi-container、FSM 或 SC-P3-003。
  - **Return Contract:** `TASK_RESULT`，遵守 `.ai/result-contract.md`；成功时 `Status: DONE` 且 `Production Delta: NONE`，否则仅使用正式 `BLOCKED_FOR_*` 状态。完成后停止，不执行 Task 2.1。

## 2. Container-Scope Popup Behavior

- [x] 2.1 **Handle bounded local obstruction and verify Container continuity**
  - **Task ID:** 2.1
  - **Role:** runtime-coder
  - **Tier:** standard
  - **Depends On:** 1.1 DONE
  - **Scenario Receipt:** SC-P3-002
  - **Goal:** 在现有 Container/Traversal/Agent 控制流中实现最小批准行为：Container-scope obstruction classification → bounded local handling → fresh Observation → existing semantic continuity verification；连续性成立时保留同一 Container 与 local progress 并继续，否则将结构化 evidence 升级给 Agent。
  - **Required Semantic:** Observation 是 evidence 而非 semantic truth；Popup 不立即等于 Agent drift；Container 是 local obstruction classification、local progress 和 local continuity judgement authority；Traversal 只拥有确定性局部执行机制；Agent 保留 active Container transition、Agent Recovery、GoalEvidence 和 final RunState authority；dispatch outcome 不证明 dismiss 或 continuity success。
  - **Approved Production Purchase:** Production Model Delta = 0。仅允许修改现有 `Container` / `Traversal` / `Agent` 的方法与控制流来表达 SC-P3-002；不得新增生产 artifact 或 mutable state。
  - **Allowed Scope:** `src/UniClaw.Runtime/Container/Container.cs`、`src/UniClaw.Runtime/Traversal/Traversal.cs`、`src/UniClaw.Runtime/Agent/Agent.cs` 中满足 SC-P3-002 所必需的最小现有控制流调整，以及直接验证这些分支的既有单元测试文件。允许不修改其中不需要变化的文件。
  - **Forbidden Scope:** `src/UniClaw.Runtime/Model/**`、`src/UniClaw.Runtime/Recovery/**`、生产 Environment contract；任何新 production type/field/enum/interface/component/mutable state；新的 Popup TrapKind 或 RecoveryResult；直接 Container → Recovery 或 Recovery → Container/Traversal 依赖；active Container stack 形状或 owner 变化；新 Agent recovery policy；PopupManager、PopupRecoveryEngine、RecoveryPlanner、ContainerRecoveryManager、FSM 或通用 framework。
  - **Required Assertions:** Container 使用既有 Observation/local evidence 进行 Container-scope obstruction classification，不把 Popup 直接当 Agent drift；local handling 通过既有 Container → Traversal → Environment 方向且有界、无 blind repeat；处理后取得 `SequenceNumber` 严格推进的 fresh Observation；dismiss dispatch outcome 本身不构成 handled verdict；foreground compatible + existing `IsStillMine` + reconciled semantic-page non-conflict 共同证明 continuity；正向保持同一 active Container 且不调用会清空既有 progress 的 rebind，pre-obstruction progress 仍存在，执行协议可继续；失败/陈旧/不兼容/Unknown/conflicting 分支不伪造成功、不清空 progress，并用现有结构化 evidence 向 Agent 升级；Agent 决定既有 higher-scope outcome；Goal completion 仍只来自 satisfied GoalEvidence；使用现有 Trap vocabulary 且不新增或重新定义 enum，若现有 vocabulary 无法诚实表达则返回 `BLOCKED_FOR_SEMANTIC_REVIEW`；Recovery 代码与依赖边界不变；SC-P2-002 pre-dispatch retry 与 SC-P3-001 post-timeout verification 不回归。
  - **Verification:** 运行 Task 2.1 定向 Container/Traversal/Agent tests；运行 SC-P2-002、SC-P3-001 与 GoalEvidence 定向回归；`dotnet build src/UniClaw.Runtime.sln`；`dotnet test src/UniClaw.Runtime.sln`；Architecture Guards；`scripts/check-consistency.sh`；审计 production model/type/field/enum/interface/component/mutable-state 增量全部为 0。
  - **Deferred Boundary:** 不决定新的 Agent recovery response；不购买 Popup classification algorithm、generic overlay/local recovery/retry/uncertainty framework、Scroll、Fingerprint、Confidence、multi-container progress、FSM、real-device/Vision 或 SC-P3-003。
  - **Return Contract:** `TASK_RESULT`，遵守 `.ai/result-contract.md`；必须报告实际 production control-flow delta、Scenario evidence、build/tests/guards/consistency 与 Semantic Drift。若需要 model/ownership/authority/dependency delta，返回相应 `BLOCKED_FOR_SEMANTIC_REVIEW` 或 `BLOCKED_FOR_ARCHITECTURE_REVIEW`。完成后停止，不执行 Task 3.1。

## 3. SC-P3-002 Formal Scenario Verification

- [x] 3.1 **Prove continuity, escalation, progress preservation, and replay**
  - **Task ID:** 3.1
  - **Role:** runtime-coder
  - **Tier:** standard
  - **Depends On:** 2.1 DONE
  - **Scenario Receipt:** SC-P3-002
  - **Goal:** 使用 Task 1.1 的确定性 Fixture 和 Task 2.1 的 Runtime 行为，建立 SC-P3-002 正向、升级与确定性重放的正式 Scenario proof，不新增生产行为。
  - **Required Semantic:** 正向分支只能由 fresh Observation 和既有 semantic identity evidence 证明同一 Container 连续；local progress 必须保留；升级分支不得伪造 handled/Goal success、不得静默重置 progress，且 Agent 保留 higher-scope authority。
  - **Approved Production Purchase:** Production Model Delta = 0；Production Behavior Delta = 0。本任务只购买正式 Scenario 测试证据与必要测试侧组合。
  - **Allowed Scope:** `tests/UniClaw.Runtime.Tests/Scenario/**` 中 SC-P3-002 专用 Scenario 测试、Task 1.1 已建立的 Fake/fixture、`ScenarioHarness` 与直接复用的最小 helper；必要时仅更新本 `tasks.md` 的 Task 3.1 progress。
  - **Forbidden Scope:** `src/UniClaw.Runtime/**`，除非发现明确 implementation regression；OpenSpec Scenario/Spec 意义修改；新生产 model/state/component；新的 recovery/retry/identity policy；Popup/Overlay 以外的 Phase 3 candidate；提前执行 Task 4.1。
  - **Required Assertions:** 正向分支在 Popup 前具有可观察 local progress；只发生批准的有界 dismiss handling；取得 fresh Observation；foreground、`IsStillMine` 与 reconciled semantic page 共同证明同一 Container；active Container 未替换/rebind；pre-obstruction progress 未丢失；execution protocol 继续；无由 dismiss 导致的 Goal completion或 unconditional Agent Recovery。升级分支分别证明 dismiss fails 与 dismiss succeeds-but-continuity-unproven/page-changed；两者均无 fabricated success、无 blind repeat、无 progress reset，且存在 Container-scope structured evidence 到 Agent；Agent 保留 rebind/recovery/failure 与 final RunState authority。相同 RunId、Environment 与 action sequence 重放得到相同 ActionHistory、Observation、journal、Trace、continuity evidence、progress、GoalEvidence 和 final state。
  - **Verification:** 运行 SC-P3-002 专用 Scenario tests；运行 deterministic replay tests；运行 SC-P1 全部 Scenario、SC-P2-001/002/003、SC-P3-001 定向回归；`dotnet build src/UniClaw.Runtime.sln`；`dotnet test src/UniClaw.Runtime.sln`；Architecture Guards；`scripts/check-consistency.sh`；核对 formal Scenario Evidence Required 1–9。
  - **Deferred Boundary:** 不修订 Runtime semantic；不引入新 TrapKind/RecoveryResult/Container state/component/interface；不购买 Scroll、Fingerprint、Confidence、generic overlay/local recovery/retry/uncertainty、multi-container、FSM、新 Agent recovery semantics 或 SC-P3-003。
  - **Return Contract:** `TASK_RESULT`，遵守 `.ai/result-contract.md`；必须报告 Formal Scenario Evidence、Production Delta、tests/build/guards/consistency 与 Semantic Drift。若测试揭示规范或 architecture 不可满足，返回正式 `BLOCKED_FOR_*`。完成后停止，不执行 Task 4.1。

## 4. Independent Regression and Boundary Validation

- [x] 4.1 **Independent SC-P3-002 slice acceptance**
  - **Task ID:** 4.1
  - **Role:** runtime-validator
  - **Tier:** standard
  - **Depends On:** 3.1 DONE
  - **Scenario Receipt:** SC-P3-002
  - **Goal:** 独立 reload repository truth，验证 Task 1.1/2.1/3.1 的实际 diff 和证据是否完整满足 SC-P3-002，并审计零模型增量、冻结 ownership/authority、Recovery boundary、所有既有 Phase/Scenario 回归及 deferred leakage。
  - **Required Semantic:** 只有 fresh external evidence + existing semantic identity rule 能证明 local continuity；dispatch success、同一 screenshot、同一 Observation 或 Fingerprint 均不是 identity proof；Container 只能决定 local proof/upgrade boundary，Agent 仍独占 higher-scope response、GoalEvidence 和 final RunState。
  - **Approved Production Purchase:** Production Model Delta = 0；Production Behavior Purchase = NONE。本任务是 acceptance，不修改生产或测试实现。
  - **Allowed Scope:** read-only 检查 repository diff、OpenSpec/proposal/design/spec/scenario/tasks、frozen Phase 2 receipts、SC-P3-001 closeout、Runtime/Test evidence；执行 build/tests/guards/consistency/OpenSpec strict validation。仅当全部 PASS 时，任务机制可把本 `tasks.md` 的 4.1 标记完成。
  - **Forbidden Scope:** 修补 production 或 tests；接受 coder summary 代替 fresh evidence；修改 Scenario/Spec/architecture；新增任何 production artifact/state/dependency；修改 Recovery ownership；开始 SC-P3-003 或其他 deferred capability。
  - **Required Assertions:** SC-P3-002 Evidence Required 1–9 全部由 repository evidence 满足；正向为 bounded handling → fresh Observation → same Container verified → local progress preserved → continue；升级为 handling failure或 continuity unproven → no fabricated success/progress reset/blind repeat → structured evidence to Agent；新 production types/fields/enums/interfaces/components/mutable state 均为 0；ownership/authority delta 为 NONE；Recovery → Container/Traversal 仍禁止；Agent/Container/Traversal/Environment/Recovery 各自 authority 未漂移；Phase 1、Phase 2、SC-P3-001 全部回归；deterministic replay 通过；无 Popup manager/engine/planner、new TrapKind/RecoveryResult/state、Scroll、Fingerprint、Confidence、generic framework、multi-container、FSM、新 Agent recovery semantics 或 SC-P3-003 泄漏。
  - **Verification:** `dotnet build src/UniClaw.Runtime.sln`；`dotnet test src/UniClaw.Runtime.sln`；Architecture Guard tests；`scripts/check-consistency.sh`；`openspec validate phase3-popup-local-recovery --strict`；独立审计 production delta、Scenario mapping、ActionHistory/Observation/journal/Trace/progress/GoalEvidence/final-state replay evidence。
  - **Deferred Boundary:** SC-P3-002 之外的 Phase 3 candidates 和未来能力保持缺席；任何 semantic、ownership、authority、invariant、dependency 或 production-model pressure 必须按正式 Gate 分类，不得由 validator 修复。
  - **Return Contract:** `VALIDATION_RESULT`，遵守 `.ai/result-contract.md`；Verdict 仅可为 `PASS | CONDITIONAL_PASS | FAIL`，必须包含独立 verification evidence、violations、failure classification 和 required follow-up。完成后停止，不宣称 Phase 3 complete。

## Design Docs

> Auto-generated from proposal Impact section.
> Implementation agents: read these before starting.

| Module | Design Doc |
|--------|------------|
| `src/UniClaw.Runtime/Container/` | `docs/system/layers/container-runtime.md` |
| `src/UniClaw.Runtime/Traversal/` | `docs/system/layers/traversal-runtime.md` |
| `src/UniClaw.Runtime/Agent/` | `docs/system/layers/agent-runtime.md` |
| `tests/UniClaw.Runtime.Tests/Scenario/` | `docs/system/scenarios/05-popup-local-recovery.md` |
| `openspec/changes/phase3-popup-local-recovery/` | `openspec/changes/phase3-popup-local-recovery/design.md` |
