# Tasks — phase1-deterministic-runtime

> 实施前必读: proposal.md + design.md + specs/**（含 scenarios/catalog.md — SC-P1-001..005 正式契约）。
> 完成一项立即勾选 `- [x]`。
> 实施遵守: 宪章 §54（Responsibility → Authority → State Owner → Interfaces → Implementation → Verification）、
> §48（核心类九个问题）、§49（接口证明价值）；5 个 Scenario 共享单一 Runtime slice（裁决 7）—
> 不得为任一 Scenario 创建独立 Runner / framework / production subsystem。

## Phase A — A. Architecture Proposal + B. Minimum Contracts（本 change 已完成）

- [x] A1. Architecture Proposal（design.md: component model / ownership / dependency / state model / normal lifecycle / trap lifecycle / structure / deferred decisions）
- [x] A2. Minimum Contracts（specs/run-lifecycle、specs/environment、specs/container-traversal、specs/normal-wifi-scenario）
- [x] A3. 实施清单（本 tasks.md）；AGENTS.md 导航更新（根 + src/UniClaw.Runtime 目录表）
- [x] A4. Contract Reconciliation: Scenario Catalog（scenarios/catalog.md — SC-P1-001..005 正式 execution contract）+ 全部 spec / design / tasks 同步（裁决 1–9）

## Phase B — C. Fake Environment + D. 5 Scenario 实施（共享 Runtime Slice — 裁决 7）

- [x] B1. **模型层**（Model/，全部不可变 sealed record + ImmutableArray 约定）:
      Observation（Elements / ForegroundApplication / SequenceNumber — 无 Fingerprint，裁决 2）/
      ObservedElement（Text / SwitchState? / Index — SC-P1-005）/ WorldBelief（SemanticPage +
      SourceObservationSequence，无场景字段）/ RecoveryAnchor（ApplicationIdentity /
      ExpectedSemanticEntry / VerificationCriteria）/ Goal（evidence evaluator 注入点）/
      GoalEvidence（Satisfied / Reason / SourceObservationSequence — SC-P1-003）/
      Plan（target/action 由调用侧注入）/ DeviceAction（LaunchApp | Tap | SetSwitch，
      Tap/SetSwitch 携带 TargetElementIndex — SC-P1-005）/ ActionResult（Dispatched / TimedOut /
      Rejected）/ StartupResult（Ready(anchor) | NotReady(reason) — SC-P1-002）/
      TraversalStepResult（Succeeded | Failed(reason) — SC-P1-004）/
      TraceEvent（RunId/ContainerId/StepId/ActionId + Action?/Reason?/RunState?）/
      RunState（Trap 模型不在本切片 — Phase 2 引入，裁决 4）
- [x] B2. **端口**: `Environment/IEnvironment.cs`（ObserveAsync / ExecuteAsync，均带 CancellationToken）
- [x] B3. **Fake**: `tests/UniClaw.Runtime.Tests/Scenario/Fakes/ScriptedEnvironment.cs`
      （Screen 配置驱动: Screen A + Click X → Screen B；元素身份稳定（Index）；action history；
      同文本多元素 — SC-P1-005 数据变体；SetSwitch 作用于非开关元素 → Rejected；确定性、可重放）
- [x] B4. **Startup**（Startup/）: Attach → Launch → Observe → Verify ForegroundApplication（裁决 7 消费者）
      → Resolve Initial Semantic World → Establish Initial Container → Establish RecoveryAnchor → Ready；
      产出 StartupResult（Ready / NotReady(原因) — SC-P1-002 失败路径）
      （配合 World/ 的 Reconcile 初建 WorldBelief，含 Confidence/Evidence/SourceObservationSequence）
- [x] B5. **Container**（Container/）: Semantic Identity 显式规则注入 / 局部状态 /
      still-mine 判断 / 局部完成判断 / 步骤失败结果只读转交 Agent（SC-P1-004）
- [x] B6. **Traversal**（Traversal/）: Select → Check → Execute → Observe → Verify → Branch +
      step journal + TraversalStepResult（阻塞 → Failed(原因)，非异常非静默 — SC-P1-004）+
      grounding 消歧规则（同一 Text 多候选时 state-bearing 优先，仅 Text + SwitchState? — SC-P1-005；
      Trap 模型 Phase 2 引入，本阶段不建 — 裁决 4）
- [x] B7. **Agent**（Agent/）: RunState 全生命周期（含 Initializing→Failed — SC-P1-002；
      Running→Failed — SC-P1-003 负向 / SC-P1-004）+ WorldBelief 代持 + Active Container Stack +
      Plan 驱动（bind/traverse/navigate 循环；target/action 来自调用侧，不硬编码 WiFi 字符串 — 裁决 11）+
      每步 post-action Observation 后 evidence evaluator 评估（SC-P1-003，I-10，裁决 3）+
      最终 failure authority（Run 终止只能由 Agent 发出 — SC-P1-004）+ TraceEvent 列表持有（裁决 5）
- [x] B8. **TraceEvent 字段落地**（Model/，Agent 持有 `List<TraceEvent>`）: RunId/ContainerId/StepId/ActionId
      因果链 + Action?（动作载荷 — SC-P1-005）+ Reason?（显式原因 — SC-P1-001/002/003/004）+
      RunState?（生命周期转移 — SC-P1-001/002），只追加不改写（I-2）；
      不建独立 Observability/ 组件（persistence/export/metrics/spans DEFER — 裁决 5）
- [x] B9. **共享 Scenario 测试基建**: Goal / Plan / evidence evaluator / container identity 规则构造
      helpers + ScriptedEnvironment 数据变体工厂
      （happy / startup-fg-fail / switch-stuck / missing-target / same-text）
- [x] B10. **SC-P1-001 场景测试**（Scenario/NormalWifiHappyPathTests.cs）:
      §34 生命周期顺序断言 + Startup Verify ForegroundApplication + GoalEvidence 断言 +
      Trace 因果链断言 + 确定性重复运行断言
- [x] B11. **SC-P1-002 场景测试**（Scenario/StartupForegroundVerificationFailureTests.cs）:
      never enters Running + RecoveryAnchor 未建立 + NotReady(显式原因) 记录 +
      无恢复动作（action history 仅 Launch）+ 无 Container/Traversal 执行
- [x] B12. **SC-P1-003 场景测试**（Scenario/GoalEvidenceCompletionTests.cs）:
      正向: dispatch ≠ completed（Completed 位于 dispatch 与 post-action Observation 评估之后）+
      GoalEvidence.SourceObservationSequence == post-action Observation 序号；
      负向（switch-stuck 变体）: Plan 耗尽 + 证据不满足 → Failed（非 Completed）+ 显式原因 + 无恢复动作
- [x] B13. **SC-P1-004 场景测试**（Scenario/EscalationWithoutStealingAuthorityTests.cs）:
      missing-target 变体: TraversalStepResult.Failed（结构化、含原因）→ Container 转交 →
      Agent 判定 Failed + 显式原因 + 无恢复动作（action history 仅 Launch + Tap）+
      架构断言（Model 层无 Trap / TrapKind / TrapScope / RecoveryRequest）
- [x] B14. **SC-P1-005 场景测试**（Scenario/SameTextElementDisambiguationTests.cs）:
      same-text 变体: Trace 中 SetSwitch.TargetElementIndex == 开关元素 Index（≠ 标题）+
      开关 SwitchState=true 且标题仍 null + 错误路径对照（作用于标题 → Rejected）+
      架构断言（无 coordinate / hierarchy 模型）
- [x] B15. **验证**: `dotnet build` 0 警告 0 错误；`dotnet test` 全绿（B10–B14）；
      `scripts/check-consistency.sh` ALL PASS；ArchitectureGuardTests 保持通过

## Phase C — F. Architecture Review（实施完成后）

- [x] C1. §60-F Review checklist（对 SC-P1-001..005 逐一核验）: 无 God Object / 无重复 authority /
      Runtime State 与 World State 未混淆 / Plan 未当 Reality / 无不必要 FSM / 每个状态 owner 可解释 /
      低层 escalate 不偷权（SC-P1-004）/ grounding 无泄漏（SC-P1-005）/ 无需真实手机与 LLM 完成核心测试
      （2026-08-16 `PROJECT_LEADER_RECONCILE_PHASE1_DETERMINISTIC_RUNTIME` 判定:
      SATISFIED_BY_EXISTING_DURABLE_EVIDENCE — 9 项逐项有独立证据: ArchitectureGuardTests 8/8、
      B13/B14 架构断言（SC-P1-004 escalate 不偷权 / SC-P1-005 grounding 无泄漏）、
      phase2-architecture-receipt Frozen Ownership/Authority 表（owner/authority 可解释）、
      phase2-human-gate-decision 记载 Phase 1 独立验收 PASS（104/104 tests, 6/6 guards, 5/5 scenarios,
      validator PASS）。此勾选声明 §60-F 架构核验已满足，不构成 GRADUATED —— 毕业评审另行独立进行。）
- [x] C2. 评审通过后归档本 change（/opsx:archive），提取 decisions（含 裁决 1–11 与本次 Reconciliation 裁决 1–9）
      （2026-08-16 reconcile 分类: POST_GRADUATION_ARCHIVE_PENDING — "评审通过后归档" 语义明确要求
      先 graduation 后 archive；毕业前不得勾选，保持 unchecked。）
      （2026-08-16 GRADUATED → archived as `2026-08-16-phase1-deterministic-runtime`：
      C2 由实际归档操作 fulfilled — 归档前保持 unchecked，归档后按仓库惯例勾选；
      毕业记录见 `docs/decisions/phase1-deterministic-runtime-graduation-decision.md`。）
