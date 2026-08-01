# 显式集成测试阶梯

UniClaw 的外部集成测试默认全部跳过，不属于日常无设备代码基线。只有修改了
对应边界时，才通过 `UNICLAW_INTEGRATION_SCOPES` 显式启用相关 scope；不要为一次
视觉修改顺带执行会操作设备的场景测试。

## 递进顺序

| 层级 | Scope | 验证内容 | 外部依赖 | 是否操作设备 |
|---|---|---|---|---|
| 1 | `vision-smoke` | 固定截图能被真实视觉 provider 解析为非空 `PageAnalysis` | provider 凭据 | 否 |
| 2 | `vision-golden` | 关键元素、行为映射和坐标与人工审阅 golden 一致 | provider 凭据 | 否 |
| 3 | `adb-connectivity` | 唯一/指定 serial 在线且统一 ADB runner 可达 | emulator/真机 | 否 |
| 4 | `adb-read` | screencap、Settings 启动和 UIAutomator 解析可用 | emulator/真机 | 只启动 Settings |
| 5 | `adb-action` | 安全导航行 click → 页面变化 → back 恢复 | 固定 Settings fixture | 是，仅读导航 |
| 6 | `adb-vision-action` | 真实视觉定位安全行 → ADB click → 验证 → back | emulator + provider | 是，单步 |
| 7 | `scenario-locate` | `locate-one-item` 经 Host → Core `TraversalEngine`/`TraversalFSM` 完成 | emulator + provider | 是 |
| 8 | `scenario-enumerate` | `enumerate-settings-safely` 经同一 Core/FSM 链完成 | emulator + provider | 是 |

`all` 可一次启用所有 scope，但只用于专门的端到端诊断，不用于普通验证。

## 默认基线

```bash
dotnet test src/UniClaw.Core.sln --filter "Category!=Integration"
```

即使不加 filter，未设置 `UNICLAW_INTEGRATION_SCOPES` 时这些测试也会在 discovery
阶段显示为 skipped，不会访问网络、ADB 或修改设备。

## 视觉模型检查

先跑低约束 smoke，再跑人工审阅的 golden：

```bash
UNICLAW_INTEGRATION_SCOPES=vision-smoke \
dotnet test tests/UniClaw.Core.Tests --filter "IntegrationScope=vision-smoke"

UNICLAW_INTEGRATION_SCOPES=vision-golden \
dotnet test tests/UniClaw.Core.Tests --filter "IntegrationScope=vision-golden"
```

默认读取 `SENSENOVA_API_KEY` 或 `~/.litellm/secrets.json`。可用
`SENSENOVA_BASE_URL`、`SENSENOVA_MODEL` 和 `UNICLAW_VISION_IMAGE` 覆盖。

固定资产位于 `tests/UniClaw.Core.Tests/Fixtures/Screenshots/`。golden 是人工审阅的
语义资产，不应把一次偶发模型输出直接视为正确答案。首次增加截图时：

```bash
UNICLAW_INTEGRATION_SCOPES=vision-golden \
UNICLAW_VISION_UPDATE_EXPECTED=1 \
dotnet test tests/UniClaw.Core.Tests --filter "IntegrationScope=vision-golden"
```

审阅生成的 `*.expected.json` 后再提交；`*.actual.json` 只用于诊断。

## ADB 边界

先启动项目固定模拟器，并在多设备时指定 serial：

```bash
scripts/android-emulator.sh start
export UNICLAW_ADB_SERIAL=emulator-5554
```

按影响面逐层运行：

```bash
UNICLAW_INTEGRATION_SCOPES=adb-connectivity \
dotnet test tests/UniClaw.Host.Tests --filter "IntegrationScope=adb-connectivity"

UNICLAW_INTEGRATION_SCOPES=adb-read \
dotnet test tests/UniClaw.Host.Tests --filter "IntegrationScope=adb-read"

UNICLAW_INTEGRATION_SCOPES=adb-action \
dotnet test tests/UniClaw.Host.Tests --filter "IntegrationScope=adb-action"
```

ADB 证据写入 `artifacts/runs/integration/adb-*`。`adb-action` 只点击白名单中的
Settings 导航行并按 Back 恢复；找不到固定目标时测试明确失败，不回退到任意坐标。

## 视觉 + ADB 单步闭环

完整场景前先证明视觉坐标可以安全驱动一次设备导航：

```bash
UNICLAW_ADB_SERIAL=emulator-5554 \
UNICLAW_INTEGRATION_SCOPES=adb-vision-action \
dotnet test tests/UniClaw.Host.Tests --filter "IntegrationScope=adb-vision-action"
```

该测试先用生产 `analyze` 组合读取真实截图，要求分析阶段发送零设备动作；随后只从
视觉结果中选择 `ExpectedAction.Navigate` 且名称命中 Wi-Fi/WLAN/Network 的行，执行
一次 click 并 Back。找不到安全目标时失败，不点击未知元素。

## 两个模拟器实机场景

场景门禁直接调用生产 `HostCompositionFactory.RunScenarioAsync`。生产 Host 组装
`TraversalEngine`，注入唯一的安全装饰器、屏幕状态、hooks 与 trace；因此测试不会
经过旧的自包含 runner loop。测试同时要求 `result.json` 有成功状态和非零 FSM steps。

```bash
UNICLAW_ADB_SERIAL=emulator-5554 \
UNICLAW_INTEGRATION_SCOPES=scenario-locate \
dotnet test tests/UniClaw.Host.Tests --filter "IntegrationScope=scenario-locate"

UNICLAW_ADB_SERIAL=emulator-5554 \
UNICLAW_INTEGRATION_SCOPES=scenario-enumerate \
dotnet test tests/UniClaw.Host.Tests --filter "IntegrationScope=scenario-enumerate"
```

默认 provider 为 `sensenova`，模型为 `sensenova-6.7-flash-lite`。可用
`UNICLAW_INTEGRATION_PROVIDER` 和 `UNICLAW_INTEGRATION_MODEL` 覆盖。输出分别落在
`artifacts/runs/integration/scenario-locate/` 和
`artifacts/runs/integration/scenario-enumerate/`。

失败时按顺序检查：

1. `result.json` 的 status、completion reason 和 steps；
2. `steps/<nnnn>/` 的 before/after、analysis、safety 和 verification；
3. safety journal 是否有 deny；
4. trace 中 `TraversalFSM` transition 与失败 step；
5. ADB、provider、场景数据或模型方差分类。

## 何时运行

- 改视觉 prompt、provider、`PageAnalyzer` 或类型→动作派生：跑层级 1–2。
- 改 ADB runner、截图或 UIAutomator：跑层级 3–4。
- 改 `IActionExecutor`、坐标、back/scroll 或 safety gate：跑层级 3–5。
- 改视觉坐标到设备动作的衔接：额外跑 `adb-vision-action`。
- 改 Host composition、Graph plan、TraversalEngine、FSM、hooks 或 analyzer：
  先跑默认 simulation/engine 测试，再跑两个受影响的场景 scope。
- 仅改无关 Domain 数据类型或文档：不运行外部集成测试。
