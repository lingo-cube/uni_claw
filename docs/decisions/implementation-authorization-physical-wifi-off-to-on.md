# PROJECT_LEADER_IMPLEMENTATION_AUTHORIZATION_DECISION — physical-wifi-off-to-on-minimum-semantic-loop

- **Authority**: `PROJECT_LEADER_PHYSICAL_WIFI_OFF_TO_ON_IMPLEMENTATION_GATE`
- **Date**: 2026-08-12
- **Input**: `openspec/changes/physical-wifi-off-to-on-minimum-semantic-loop/`（proposal + design + specs + tasks，已验证 valid）
- **Mode**: Review + decision. No implementation in this gate.
- **Predecessor**: `PROJECT_LEADER_CREATE_OPENSPEC_PHYSICAL_WIFI_OFF_TO_ON_MINIMUM_SEMANTIC_LOOP_GATE`（OpenSpec validated，4/4 artifacts）

---

## 1. Task ordering validation（对 Required Split 的核对）

tasks.md 已重构为两切片顺序结构（Slice 1 → Slice 2，Slice 2 明确标注开始条件）：

| Required split | tasks.md 映射 | 判定 |
|---|---|---|
| **Slice 1 — REALITY_COMPOSITION_FOUNDATION**：生产组合根 / Host 项目运行时接线 / 真实 IEnvironment 构造路径 / Fake·Replay·Simulation 保持测试侧 / Startup.AttachAsync 落地 / async seam 修复 | Group 1（§33 门与基线前置）+ Group 2（组合根 + F1）+ Group 3（async seam + attach + Tier 0 回归）+ Group 4（Slice 1 证明 4.1 + F2） | ✅ 顺序正确 |
| **Slice 2 — WIFI_SEMANTIC_LOOP**：WiFi capability 执行 / ADB-backed action path / 动作后 fresh screenshot / perception 验证 / GoalEvidence 闭合 | Group 5（emulator 校准）+ Group 6（闭环 + F3–F6 + 约束断言 + trace）+ Group 7（验证/归档） | ✅ 顺序正确（校准先于闭环；闭环依赖 Slice 1 组合根） |

**修正项**（相对初版 tasks.md）：① §33 门决策记录从 Group 5 前移至 Slice 1 前置（真实接线前必须完成）；② 新增 4.1 Slice 1 证明任务（Agent 以真实环境依赖图运行，不含 WiFi 行为）；③ 六项门槛 falsifier（F1–F6）作为显式任务；④ 新增 6.7 约束断言任务（svc wifi / cmd wifi / 隐藏 emulator API / WorldState 改写 / 场景状态注入检索断言）。

**Slice 禁止范围核对**：Slice 1 不含任何 WiFi 行为实现（4.1 仅观测闭环，Goal 不做开关切换）；两切片均不含 provider registry / provider discovery / capability redesign。

## 2. Authority boundaries audit（复核确认）

| 层 | 边界 | 代码证据 | 判定 |
|------|------|----------|------|
| Agent | 拥有 decision + verification | `SelectCapability`（Agent.SemanticRun.cs:111-117,:185-191）、authorize（:120-125）、`GoalEvidence(..., observation.SequenceNumber)`（:96）、`CompleteSemantic`（:201-204） | ✅ 与设计一致 |
| Capability | 拥有 intent-level action 的定义维度（声明式；实例由 Agent 产生、lowerer 无状态降低） | `Capability(Name, ApplicableToCategory, StateDimension)`（Model/Capability.cs:19）；`SemanticAction`（Agent.SemanticRun.cs:120-121）；`SemanticActionLowerer`（:78-83） | ✅ 与设计一致（本切片不替换模型） |
| Provider（Operator） | 只拥有机制执行；成功 ≠ 世界效果证据 | `AdbDispatchTarget`（Operator/AdbDispatchTarget.cs:6-7）；`DeviceActionTranslator` 无状态（Operator/DeviceActionTranslator.cs:9-15） | ✅ 与设计一致 |
| Perception | 只拥有 evidence | `LocalVisionPerceptionSource` → `PerceptionCandidate`；`ImageSwitchStateProvider` 确定性 heuristic；`SwitchStateValidation` 陈旧帧 fail-closed | ✅ 与设计一致 |
| Environment | 只拥有 observation/action transport | `IEnvironment`（Environment/IEnvironment.cs:8-11）；`PhysicalEnvironment`（Adapters/PhysicalEnvironment.cs:20-22） | ✅ 与设计一致 |

**结论**：五层边界与既有代码一致，本切片不改变任何一层 authority；无需架构变更。

## 3. Implementation constraints（已加入 spec/design/tasks）

1. 无 `svc wifi` / `cmd wifi`（spec Requirement 5 + 新「实施约束」条目）
2. 无隐藏 emulator API（emulator console wifi 命令、UiAutomator 隐藏接口）—— 新增
3. 无直接 WorldState/WorldBelief 改写（世界状态仅经 fresh Observation evidence 进入判定，裁决 2）—— 新增
4. 无场景特定状态注入生产路径（校准资产/RealitySeeded fixture 仅测试侧）—— 新增
5. 物理效果仅经 UI 语义环达成（tap → screencap → perception → 验证）

已写入：spec.md 新 Requirement「实施约束——无隐藏 API、无直接状态改写、无场景状态注入」（3 scenarios）；design.md「Implementation Constraints」节；tasks.md 6.7 检索断言任务。

## 4. Required implementation falsifiers（批准前核对，全部可强制）

| # | 要求 | 可强制？ | 强制方式 | 任务 |
|---|------|---------|----------|------|
| F1 | Fake 环境不能意外进入生产路径 | ✅ | Guard 1（Runtime 零 Adapters 引用）+ 宿主项目唯一接线点 + 无环境选择 flag 架构断言 | 2.6 |
| F2 | 无真实设备 → 无 dispatch | ✅ | 预检门控：未 Ready 不执行 Traversal；预检失败 NotReady + 零动作双入口断言 | 4.2 |
| F3 | dispatch 成功无观测不能满足 Goal | ✅ | I-10/裁决 10：SATISFIED 仅由 fresh Observation evidence 产生；「Dispatched 但世界未变」测试必须非 SATISFIED | 6.2 |
| F4 | 陈旧截图不能验证成功 | ✅ | 序列推进强制（Traversal.cs:245 fail step）+ `SwitchStateValidation` 陈旧帧 fail-closed | 6.3 |
| F5 | 失败动作不能误触发恢复 | ✅ | `TraversalStepResult.Failed` → Container → Agent 决策（SC-P1-004 escalate 不偷权）；恢复仅按 Agent scope（Recovery 入口断言） | 6.4 |
| F6 | provider 失败不能产生语义成功 | ✅ | perception/dispatch 失败 fail-closed → Unknown/STATE_EVIDENCE_REQUIRED 或 step Failed；任何路径不得 SATISFIED | 6.5 |

**结论**：六项 falsifier 均以既有架构机制（Guard 1 / 预检门控 / evidence 纪律 / 序列推进 / escalate 协议 / fail-closed 诊断）可强制，无需架构变更。

---

## DECISION: APPROVED_SLICE_1_AND_SLICE_2

顺序执行（Slice 2 开始条件：Slice 1 证明 4.1/4.2 + Tier 0 回归 3.3 全绿）。批准附带条件：

1. emulator-only（宪章 §33 门决策记录为 Slice 1 前置任务 1.1，任何真实接线实现前必须落档）。
2. Slice 2 唯一成功条件 = Action receipt + Fresh Observation + Perception Evidence + GoalEvidence；dispatch receipt ≠ world state change（spec Requirement 4 / F3）。
3. 实施约束 1–5 为硬约束（spec SHALL；6.7 检索断言 + 架构测试强制）。
4. 六项 falsifier（F1–F6）每项至少一条非 SATISFIED/零分发证据，作为验收门槛（7.4）。
5. 未另行授权前，任何超出两切片范围的工作（含真实手机、ReleasePolicy、Candidate-vs-ACTIVE）一律不做。

## Status

- Status: **APPROVED_SLICE_1_AND_SLICE_2**（approval recorded；实施需由下一 authority 启动执行）
- 产出：tasks.md 两切片重构 + spec.md 新增 3 条 Requirement（实施约束/陈旧证据/失败语义）+ design.md 新增 Implementation Slices / Implementation Constraints / Authority Boundary Audit / Gate-required falsifiers
- 校验：`openspec validate physical-wifi-off-to-on-minimum-semantic-loop` 复跑通过（见执行记录）
