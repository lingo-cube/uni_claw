## Why

UniClaw 已具备 Android 模拟器、页面分析、TraversalPlan、遍历引擎和 trace 等纵向能力，但尚缺少一个可重复运行的产品级闭环，把“启动设备、观察页面、生成小步计划、执行、复验、跳过危险操作、沉淀问题”组织成可验收资产。当前最合适的收敛入口是 Android Settings：先验证定位单个设置项，再验证受边界约束的安全遍历。

## What Changes

- 新增版本化的 Settings 场景目录，首批包含：
  - `locate-one-item`：在限定步数内找到指定设置项并验证目标页面。
  - `enumerate-settings-safely`：遍历 Settings 首页一级条目，进入后采集可见内容并返回，危险项只记录、不执行。
- 新增迭代式设备测试运行器，将设备准备、Settings 入口、截图/UI dump、页面分析、下一小段计划、动作执行、页面复验和终止判断串为统一状态闭环。
- 新增确定性动作安全门，在动作发送给 ADB 前进行默认拒绝判断；危险、未知或越界动作不得依赖 AI 自行放行。
- 新增标准化运行资产目录，保存 run manifest、场景快照、逐步证据、trace、最终结果和问题记录，使每次迭代可复现、可比较、可回归。
- 建立分阶段执行顺序：先用固定目标跑通 `locate-one-item`，稳定后再启用 `enumerate-settings-safely`；每个阶段均先做确定性/模拟测试，再做 emulator smoke 和重复性测试。
- 修复本闭环暴露的既有实现缺口，包括真实入口策略、ADB 失败分类、滚动状态可信度和 Host 组装，但不改变现有锁定枚举与跨层依赖约束。

## Capabilities

### New Capabilities

- `android-settings-scenario-catalog`: 定义 Settings 测试场景、参数、边界、成功条件、预算与可版本化输入格式。
- `iterative-device-test-runner`: 定义单次运行的设备准备、感知、短计划、执行、复验、终止和资源清理闭环。
- `deterministic-action-safety`: 定义动作执行前的确定性 allow/deny/skip 策略、危险匹配规则和默认拒绝语义。
- `run-artifact-reporting`: 定义运行证据、trace、结果摘要、问题记录、迭代关联和敏感信息处理格式。

### Modified Capabilities

<!-- No canonical requirement changes. The proposal composes existing emulator,
     page analysis, traversal, plan serialization, screen-state, and trace
     capabilities and closes implementation gaps against their current specs. -->

## Impact

- 新增 Host/CLI 组合根，用于 `doctor`、场景校验和场景运行；Core 不依赖 Device 或具体模型 Provider。
- 影响 `src/UniClaw.Device/` 的 ADB 命令执行、截图、动作与屏幕状态实现。
- 影响 `src/UniClaw.Core/Traversal/`、`Graph/`、`UniBrain/` 和 `Observability/` 的组装与既有规格缺口收敛，不新增锁定 enum 值。
- 新增场景与运行资产目录，建议分别位于 `scenarios/android-settings/` 和可忽略的 `artifacts/runs/`。
- 新增 unit/simulation/emulator 分层测试；emulator 测试保持显式执行，不进入默认无设备测试集。
- 复用现有 `android-emulator-integration`、`page-analyzer`、`graph-foundation`、`traversal-plan-serialization`、`traversal-engine`、`screen-state-provider`、`file-trace-storage` 和 `trace-service` 规格。
