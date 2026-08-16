# Tasks: physical-settings-to-wifi-multi-level-traversal

> 本 change 为 SCENARIO_SELECTION_AND_OPENSPEC_ONLY 产物——**tasks 供未来实施 gate 使用**；本 gate 不做任何实现。
> 实施前需另行授权（`IMPLEMENT_PHYSICAL_SETTINGS_TO_WIFI_MULTI_LEVEL_TRAVERSAL` 类 authority）。
> 权威规格：specs/physical-settings-to-wifi-multi-level-traversal/spec.md；HOW 见 design.md。

## 1. 场景接线（宿主侧，裁决 11）

- [x] 1.1 在 PhysicalHost（或等价证明宿主）声明多页页面识别知识：`PageAnalysisCriteria` 含 `SettingsRoot` / `NetworkAndInternet` / `WifiInternet` 三页的正锚与 negative 锚（初值按真实 emulator 观测校准，标注 provenance）
- [x] 1.2 将 `resolveSemanticPage` 从常量「Settings」升级为多页解析：基于 PageAnalysis 四源证据（FOREGROUND / TEXT_ANCHOR / TEXT_ANCHOR_NEGATIVE / SWITCH_DISTRIBUTION）融合唯一页面名；无法唯一融合 → null（Unknown）
- [x] 1.3 证明入口改为 Settings 根页（`am start -a android.settings.SETTINGS`）；宿主零导航（不点按任何行/不预置页面位置）
- [x] 1.4 逐页容器 identity 规则注入（页面名/锚点匹配），供 `IsStillMine` 判断页面变更反证

## 2. 语义环导航相位（Runtime 最小接线）

- [x] 2.1 `Agent.SemanticRun.cs`：目标对象未绑定分支 → 导航决策（D1）：PageAnalysis 唯一候选页 + 唯一锚元素解析；0/多候选 → fail closed（BindingUnresolved）
- [x] 2.2 导航动作经 `Traversal.ExecuteLoweredActionAsync(DeviceAction.Tap(...))` 分发（既有 fresh+seq+Rejected 协议）；禁止绕过 Traversal 直发
- [x] 2.3 导航验证（D5）：fresh 序列推进 ∧ 页面信念==期望页 ∧ `!IsStillMine`；任一失败 → ExecutionFailed 终止，零盲目重发
- [x] 2.4 验证通过 → `CreateContainer(nextPage)` + `Bind(freshObs)` + `RefreshContainerEvidence`；确认毕业路径（SELECT→AUTHORIZE→LOWER→GoalEvidence）零改动
- [x] 2.5 约束测试（架构级）：Runtime/Adapters 不含 Settings 页面名/锚点/坐标常量；导航分支不进入 SetSwitch 语义链；毕业链不引入导航；Guard 1 保持

## 3. Falsifier 套件（确定性 Fake 多页环境）

- [x] 3.1 构造 Fake 多页环境：SettingsRoot（含 "Network & internet" 行）→ NetworkAndInternet（含 "Internet" 行）→ WifiInternet（Wi‑Fi toggle）；页面转换可脚本、可注入
- [x] 3.2 F1：根页无任何已知页锚 → 零导航分发、无编造进度、以未解决状态终止
- [x] 3.3 F2：导航 Tap 分发成功但页面不变 → 当前容器信念权威、零推进、非 SATISFIED
- [x] 3.4 F3：新页面出现但 WifiConnectivity 无 toggle → 重新 reconcile、零陈旧绑定复用（断言 Bind 后绑定清空）
- [x] 3.5 F4：遍历中页面信念 Unknown → fail closed / 有界恢复、零导航分发
- [x] 3.6 F5：页面转换后旧观测索引复用 → 拒绝/无法解析（断言无跨容器绑定缓存）
- [x] 3.7 F6：到达 WifiInternet 时 toggle 已 true → Satisfied 且零 SetSwitch（幂等）
- [x] 3.8 正向 E2E（Fake）：根页 → 两跳导航 → OFF→ON → Satisfied；断言每跳 journal fresh 验证、恰一次 SetSwitch、GoalEvidence.SourceObservationSequence==fresh 序列
- [x] 3.9 回归：毕业 Slice 2 falsifier 8/8 与既有全量套件保持全绿（毕业路径零行为变化）

## 4. 校准与真实证明（§33 emulator-only）

- [x] 4.1 现场录制 Settings 根页 / Network & internet / Internet（WifiSettings）三页真实截图与感知证据，校准三页锚点与 negative 锚（共享标题/摘要文本消歧），更新 provenance
- [x] 4.2 现场回放：Wi‑Fi OFF 基线准备（宿主 run 外，同 Slice 2 先例，不进语义路径）→ 根页启动 → Agent 自主导航 → SetEnabled → fresh GoalEvidence → Satisfied
- [x] 4.3 证明输出：逐跳导航 journal（每跳 fresh 页面名序列）、恰一次 SetSwitch、`GoalEvidence.SourceObservationSequence`、感知 SwitchState=true、`wifi_on` 读回佐证（非成功条件）
- [x] 4.4 F2 live 变体（页面未变）与前置失败 live 变体（device 不可达/截图失败）各自显式证明（exit 2 类，零分发零遍历）
- [x] 4.5 Reality level 记录：EMULATOR_REALITY_MULTI_LEVEL_SEMANTIC_LOOP（不称 REAL_DEVICE_PROVEN）

## 5. 评审与归档（实施 gate 之后的收尾）

- [x] 5.1 毕业评审（等价 `PROJECT_LEADER_PHYSICAL_SETTINGS_TO_WIFI_MULTI_LEVEL_GRADUATION_REVIEW`）：核对 spec 各 Requirement/Scenario + F1–F6 + 禁止机制 + 现实证明 → GRADUATED（`docs/decisions/physical-settings-to-wifi-multi-level-graduation-decision.md`）
- [x] 5.2 `openspec validate physical-settings-to-wifi-multi-level-traversal` 通过
- [x] 5.3 决策记录写入 `docs/decisions/`；按仓库惯例归档 change（`openspec/changes/archive/`）

## Design Docs

> Auto-generated from proposal Impact section.
> Implementation agents: read these before starting.

| Code Path | Design Doc |
|-----------|------------|
| `src/UniClaw.Runtime/Agent/Agent.SemanticRun.cs` | openspec/changes/physical-settings-to-wifi-multi-level-traversal/design.md（§D1/D2/D4/D5）+ docs/system/greenfield-runtime-charter.md（§6 Container / §10 Reconcile / §33 emulator-only） |
| `src/UniClaw.Runtime/World/`（PageAnalysis / Reconcile / BindingReconciler） | design.md（§D2/D4）+ specs（binding 生命周期 Requirement）——**只读复用**，预期零改动 |
| `src/UniClaw.Runtime/Traversal/` | design.md（§D3/D5）——`ExecuteLoweredActionAsync` 复用，预期零改动 |
| `src/UniClaw.Runtime.Adapters/` | 预期零改动（机制不变）；`docs/system/engineering/ci-emulator-precondition.md`（前置基线） |
| `src/UniClaw.Runtime.PhysicalHost/Program.cs` | design.md（§D6/D7）宿主接线 |
| `tests/UniClaw.Runtime.Tests/` | `docs/TEST_GUIDE.md`（若存在）+ Slice 2 falsifier 先例（`SemanticLoopSlice2FalsifierTests.cs`） |
