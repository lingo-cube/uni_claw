# Spec: physical-settings-to-wifi-multi-level-traversal

> Agent 自主多页语义闭环：Settings 根页 →（Agent 导航）→ Wi‑Fi 开关页 → `WifiConnectivity.Enabled=true`。
> 本规格定义 WHAT（SHALL）；HOW 见 design.md；实施步骤见 tasks.md。
> 权威约束：宪章 §33（emulator-only）、I-4（Observation 是 evidence 不是 truth）、
> I-10（Completion 必须由 Goal Evidence 证明）、裁决 10（dispatch 结果 ≠ 世界效果证据）、
> 裁决 11（场景字符串由调用侧注入）、裁决 7（单一 Runtime slice，不建独立 Runner/registry）。
> 上游：`docs/decisions/physical-wifi-slice2-graduation-decision.md`（毕业链原样复用，本 change 零改动）。

## ADDED Requirements

### Requirement: 初始世界与宿主设置边界

系统 SHALL 在 Settings 应用**根页**启动证明（宿主仅执行 emulator ready + `am start -a android.settings.SETTINGS`）。宿主 SHALL NOT 导航到 Internet 页、点按 Network & internet / Internet、定位 UI 于 Wi‑Fi 开关、或以任何方式注入预期路线；Settings 启动后，一切后续导航 SHALL 由 Agent 自主执行。

#### Scenario: 宿主只建立根页起点

- **WHEN** 证明开始且 Settings 应用已启动
- **THEN** 初始观测 reconciled 到 `SettingsRoot` 页面，且初始容器绑定该页面；此前宿主未分发任何导航 Tap / 未预置页面位置

#### Scenario: 初始观测与期望入口不符

- **WHEN** 初始观测不能 reconciled 到宿主声明的根页
- **THEN** 运行以 `SemanticContradiction` 终止且零设备动作分发（既有语义）

### Requirement: 目标对象未绑定时的证据驱动导航

当目标对象 `WifiConnectivity` 在当前容器未绑定时，Agent SHALL 从**当前 fresh 观测** + 声明页面识别知识决定下一跳：恰好一个已知且非当前页的页面识别锚点 → 导航；零或多个 → **fail closed 零导航分发**。导航路由 SHALL 由每跳观测涌现，SHALL NOT 来自预编排的屏幕序列或坐标脚本。

#### Scenario: 唯一导航候选

- **WHEN** 当前 fresh 观测中恰好一个已知非当前页面的识别锚点元素存在（正锚命中、negative 锚不命中）
- **THEN** Agent 授权并以该锚元素为目标分发一个 `DeviceAction.Tap`，随后必须获得序列严格推进的 fresh 观测

#### Scenario: 无导航候选（F1）

- **WHEN** 当前观测不含任何已知页面的识别锚点元素
- **THEN** 零导航分发，无坐标猜测、无编造进度，运行以未解决状态终止（BindingUnresolved/StateEvidenceRequired）

#### Scenario: 导航候选歧义

- **WHEN** 当前观测命中多个已知页面的识别锚点
- **THEN** 零导航分发，运行以未解决状态终止（fail closed，不猜测方向）

### Requirement: 每跳导航动作验证

每个导航动作 SHALL 满足「Action receipt + fresh Observation + 验证页面/容器变更」：分发返回后必须获得 fresh 观测，且该观测的序列严格推进、页面信念等于期望目标页、`IsStillMine` 为假；任一不满足 SHALL 终止且零盲目重发。单独分发成功 SHALL NOT 推进语义进度。

#### Scenario: 导航分发成功但页面未变（F2）

- **WHEN** 导航 Tap 分发成功且获得 fresh 观测，但 fresh 观测仍 reconciled 到当前页面（或 `IsStillMine` 为真）
- **THEN** 当前容器页面信念保持权威，运行以失败终止（ExecutionFailed），绝不将 Traversal `Succeeded` 视为页面变更证明

#### Scenario: 导航分发被拒

- **WHEN** Traversal 以 Rejected 结果拒绝导航动作
- **THEN** 运行以 `ExecutionFailed("Semantic action rejected: …")` 终止（既有协议，无自动恢复尝试）

### Requirement: 容器/页面转换

导航验证通过后，Agent SHALL 创建并绑定新页面容器：`CreateContainer(nextPage)` + `Bind(freshObs)`；绑定与页面局部进度 SHALL 在新容器上从 fresh 观测重置。页面信念（当前）、期望语义对象（目标）、可达下一容器（唯一候选页）SHALL 由 Agent/Container 按既有职责区分。

#### Scenario: 已验证转换进入新容器

- **WHEN** fresh 观测证明页面/容器变更（新页面名 ≠ 当前页 ∧ `!IsStillMine` ∧ 序列推进）
- **THEN** 新容器创建并绑定该 fresh 观测，后续 belief 读取来自新容器

#### Scenario: 页面信念 Unknown（F4）

- **WHEN** 导航后（或遍历中）观测无法 reconciled 出唯一页面名
- **THEN** 按既有 Agent authority fail closed / 有界恢复，零导航分发、不假装确定（§10）

### Requirement: 跨页绑定生命周期

对象绑定 SHALL 为观测局部：每个 fresh 观测经 `BindingAnalysis`/`BindingReconciler` 重新计算，容器切换经 `Bind` 清空。页面 N 的元素索引 SHALL NOT 被当作页面 N+1 的元素身份复用；陈旧绑定 SHALL NOT 用于断言或分发。

#### Scenario: 新页面目标子对象缺席（F3）

- **WHEN** 页面转换后 fresh 观测不含目标对象绑定（如 `WifiConnectivity` 无 toggle 候选）
- **THEN** 从 fresh 世界重新 reconcile；不得回退复用上一页的绑定；继续导航（若存在唯一候选）或 fail closed

#### Scenario: 旧观测元素索引复用（F5）

- **WHEN** 页面转换后尝试以旧观测的绑定/元素索引解析动作目标
- **THEN** 解析失败/被拒绝（结构性：Bind 清空 + 索引 observation-local + 逐观测刷新），零分发

### Requirement: 最终状态动作与完成

一旦 `WifiConnectivity` 在当前容器绑定，系统 SHALL 复用毕业语义链：capability 选择 → 授权 → `SetEnabled` → `SetSwitch` → 物理效果 → fresh 观测 → 感知 `SwitchState=true` → `GoalEvidence` → `Satisfied`。完成 SHALL 仅由 fresh post-dispatch 观测的 GoalEvidence 证明；ADB 分发收据或 `settings get global wifi_on` 读回 SHALL NOT 作为成功 authority（仅佐证）。

#### Scenario: OFF→ON 完整多页闭环

- **WHEN** 初始 Wi‑Fi OFF，Agent 从 Settings 根页自主导航到达 Wi‑Fi 开关页并执行 SetEnabled(true)
- **THEN** 运行以 `Satisfied` 终止；`GoalEvidence.SourceObservationSequence` 等于含 `SwitchState=true` 的 fresh post-dispatch 观测序列；恰一次 SetSwitch；每跳导航均经 fresh 观测验证（真实多页 hopSequence ≥ 2）

#### Scenario: 到达时已 ON（F6 / 幂等）

- **WHEN** Agent 自主导航到达 Wi‑Fi 开关页且感知 `SwitchState=true`
- **THEN** 运行以 `Satisfied` 终止且 **零 SetSwitch** 变更（毕业幂等语义复用）

#### Scenario: 目标状态无法证明

- **WHEN** 分发后 fresh 观测 SwitchState 仍非目标值或为 UNKNOWN
- **THEN** 非 SATISFIED 终止（StateEvidenceRequired/ExecutionFailed），不得以分发收据完成

### Requirement: 禁止机制

系统 SHALL NOT 引入：新 Provider / Provider registry / workflow engine / 导航 DSL / 硬编码屏幕序列 / 坐标脚本 / WiFi 专用 navigator / WorldState 注入 / 隐藏 emulator API / 新语义 authority。导航 SHALL 复用既有 Agent 裁决、Container 状态、Traversal 协议、`DeviceAction.Tap` 与毕业 SetEnabled 链。

#### Scenario: 无场景专用导航代码进入 Runtime

- **WHEN** 构建 `src/UniClaw.Runtime` 与 `src/UniClaw.Runtime.Adapters`
- **THEN** 不包含 Settings 页面名/锚点/坐标常量（场景知识仅存在于宿主注入的 criteria 与 identity 规则）；Guard 1（零 ProjectReference）保持；ADB 机制不变

#### Scenario: 毕业链零改动回归

- **WHEN** 实施本 change 后运行既有毕业回归（940/940 全量含 Slice 2 falsifier 8/8）
- **THEN** 全部通过，且 `Agent.SemanticRun` 的毕业路径（SELECT→AUTHORIZE→LOWER→ExecuteLoweredActionAsync→GoalEvidence）行为不变

### Requirement: 现实证明

证明 SHALL 在 §33 emulator-only 边界内进行（emulator-5554 等既有前置），以真实 Settings 应用现场回放为现实证据；页面锚点 SHALL 以现场观测校准并记录 provenance。成功判定 SHALL NOT 依赖 `REAL_DEVICE_PROVEN`。

#### Scenario: 现场多页回放

- **WHEN** emulator 前置满足且 Wi‑Fi OFF 基线已准备
- **THEN** 证明输出显示逐跳 fresh 验证的导航序列（≥2 跳）、恰一次 SetSwitch、fresh GoalEvidence 与感知 SwitchState=true；`wifi_on` 读回仅打印佐证

#### Scenario: 前置失败显式终止

- **WHEN** emulator 不可达 / 截图探针失败 / 基线准备失败
- **THEN** 以显式原因终止（exit 2 类），零导航分发、零遍历（F2/F6 live 变体各自显式证明）

## Falsifiers

| ID | 条件 | 必证结果 |
|---|---|---|
| F1 | 第一导航目标缺失 | 零导航分发、无坐标猜测、无编造进度 |
| F2 | 导航分发成功但页面未变 | 当前容器/页面信念保持权威，零推进 |
| F3 | 新页面出现但期望子对象缺席 | 从 fresh 世界重新 reconcile，无陈旧绑定复用 |
| F4 | 遍历中观测 UNKNOWN | fail closed / 依既有 Agent authority 有界恢复 |
| F5 | 页面转换后复用旧观测元素索引 | 拒绝/无法解析 |
| F6 | 到达最终页时已 ON | Satisfied 且零 SetSwitch 变更 |
