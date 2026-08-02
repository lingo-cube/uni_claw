## Context

60+ 次真机集成测试揭示 Core 观测层三重断裂：

1. **UIA/AI 双路径分裂**：`UiAutomatorAugmentingPageAnalyzer`（Core 内，阈值 ≥3）和 `AdbScenarioObservationSource.useUiAutomatorAnalysis`（Host 开关）各自独立决策。弹窗检测 `HasPopupItems` 只在 augmeter 内生效，Host 路径绕过。
2. **FSM ErrorHandling 无闸门**：同页 item 全部失败后反复 Backtrack→NodeSelect 循环，虽有 `ConsecutiveErrors≥3 → PressBack` 但 `ErrorStrategy.Backtrack` 会 reset counter。
3. **AI 空响应死等**：Sensenova 返回空内容后重试 2 次，每次 180-190s，3 次运行合计死等 18.9 分钟，且最终仍失败。

locate 已收敛（79s, last 4/4 success），enumerate 最佳 38 steps/5 scrolls。`IUniBrain.Advisor` 已定义但 FSM 未调用。Qwen 视觉模型切换进行中。

## Goals / Non-Goals

**Goals:**
- 统一观测管线到 Core 层 `ObservationPipeline`，消除 Host 分散开关
- FSM ErrorHandling 增加同页失败闸门 + Advisor 决策集成
- AI 空响应不重试，快速失败
- UIA 动态开关（设备不支持时自动降级到 AI-only）
- 坐标/过滤补丁合入（bounds 归一化、y-clamp、summary 跳过、导航类 ImageButton 过滤）
- 意图提取 AI 失败时正确回退机械映射

**Non-Goals:**
- 不拆分 UIA / AI 双路径（统一到 Pipeline 内部）
- 不修改 EntryPolicy / SafetyPolicy 决策逻辑
- 不修改 Trace / Asset 存储格式
- 不修改 GlobalFSM（引擎级生命周期）
- 不修改 Hook 接口

## Decisions

### D1: ObservationPipeline 三级级联（UIA → AI → Fail）

```
Screenshot + UIA XML
  ├─ UIA dump failed? → [2] AI directly (skip UIA)
  ├─ [1] UIA.Parse → ≥N items + no popup → return UIA-only
  │     └─ <N items or popup detected → [2]
  ├─ [2] AI vision → success → return AI
  │     └─ empty response → throw DomainValidationException (no retry)
  └─ No fallback to UIA on AI failure — stale UIA data is worse than no data
```

**Rationale**: UIA 在标准 Settings 命中率 >90%（1s vs 60s），但弹窗/WebView 识别能力为零。AI 空响应表示模型结构性失败，重试无用。降级到 UIA 在弹窗场景会返回错误数据，不如直接失败让上层处理。

**Alternatives considered**: (a) AI 失败后回退 UIA — 弹窗场景下 UIA 返回错误 item 导致后续行为更糟；(b) 3 次重试后回退 UIA — 时间成本过高（3×190s=570s）。

### D2: UIA-first 保持（不是 AI-first）

UIA 在标准 Settings 上命中率 >90%，1s vs 60s 的差距无法用 AI 补。back 导航后 UIA dump 可跳过（复用缓存）。

**Config**: `ObservationConfig { UIA_MinItems: 3, UIA_Enabled: true (auto-set false on first dump failure), SkipUIAOnBackNavigation: true }`

### D3: Advisor 接入 ErrorHandling 策略选择链

```
ErrorClassifier → [Advisor.DecideAsync] → StrategySelector → RecoveryExecutor
```

Advisor 接收错误上下文 + 当前 PageAnalysis，返回 `{ recommendedStrategy, confidence, reasoning }`。StrategySelector 将 Advisor 输出与 ErrorHandler 合并（Advisor 权重 > Handler 默认）。

### D4: 空响应 = 结构性错误，不重试

`ModelResponse` 新增 `IsEmpty` 属性。`PageAnalyzer.AnalyzeOnceAsync` 检测空响应 → 直接抛 `DomainValidationException`（不满足 `IsTransient` → 不重试）。

### D5: 坐标/过滤补丁

| 补丁 | 位置 |
|------|------|
| `TryParseBounds` 归一化（Min/Max swap） | ScenarioObservation.cs |
| `MapItem` y-clamp 0.08-0.90 | ScenarioObservation.cs |
| `MapItem` 跳过 `android:id/summary` | ScenarioObservation.cs |
| `IsInteractive` 过滤 content-desc="Navigate up" 的 ImageButton | ScenarioObservation.cs |
| 不滤 "More options"——溢出菜单是合法 UI，safety 会 deny | ScenarioObservation.cs |

### D6: UIA 动态开关

`AdbScreenStateProvider` 首次 `RefreshAsync` 失败 → 设置 `static bool UIA_Available = false`。`ObservationPipeline` 读此标志跳过 L1。

### D7: ValidateBoundary package prefix

`ScenarioRunnerBase.ValidateBoundary` 改为前缀匹配（`StartsWith(appPackage + ".")`），不再因子包名（`com.android.settings.intelligence`）误报越界。

### D8: ErrorHandling 双闸门解耦（仿真回归暴露，2026-08-02）

仿真测试（`FsmSimulationRegressionTests`）暴露两个真实缺陷，修复后确认语义：

- **`IncrementNodeFailedItems` 原为 no-op**：`NodeFailedItems` 由 `_failedNodes.Count` 支撑，但 FSM 路径从未调用 `AddFailedNode`，同页 item 失败闸门（≥5 → PressBack）永不触发。修复：`IncrementNodeFailedItems(nodeId)` 按节点去重记录失败项（`_navigation.CurrentFrame?.NodeId`），同一节点重复失败不重复计数。
- **`ConsecutiveErrors` 成功后从未重置**：连续闸门（≥3 → PressBack）总是先于 item 闸门（≥5）触发，item 闸门成为死代码（纯连续失败在第 3 次就触发连续闸门）。修复：`verification_passed` / `verification_passed_retry` 时重置 CE——已验证成功打破连续失败链。两个闸门各自只重置自己的计数器。
- **语义**：连续闸门 = 3 次连续失败（页面卡死/retry 死循环）；item 闸门 = 交错 deny/success 场景（5 个不同 item 在同页失败，成功重置 CE 但不重置 item 计数）。

### D9: Advisor 属性访问在 try 外（防御性修正）

`HandleErrorHandlingAsync` 中 `Brain.Advisor` 属性访问位于 try/catch 之外，adapter 抛异常会击穿 ErrorHandling 路径。测试侧 FakeBrain 已改 null-object advisor（Confidence 0.0 < 0.7 阈值，FSM 忽略）；生产侧保留（UniBrainService 属性不抛）。

## Risks / Trade-offs

| Risk | Mitigation |
|------|------------|
| Pipeline 引入后 locate 性能退化 | 保持 UIA-first 阈值不变（≥3），locate 应继续 0 AI 调用 |
| Advisor 决策质量未知（从未实机测试） | Advisor 输出作为 advisory，ErrorHandler 有最终裁决权 |
| Qwen 视觉模型兼容性未验证 | 保留 OpenAiCompatible 协议层，prompt template 不变 |
| 空响应不重试可能漏掉偶发抖动 | 统计显示 Sensenova 空响应率 ~5%，且 3 次重试均失败的案例中 100% 最终仍失败 |
| ImageButton 过滤 "More options" 被 safety deny 后 planner 无 item 可点 | 已在 enumerate 测试验证：denied click 会触发 scroll→discover new items |

## Acceptance Criteria

实施完成后，以下条件必须全部满足：

### AC1: 仿真测试（基线门禁 —— 每次 commit 必须通过）

```bash
dotnet test --filter "FullyQualifiedName!~EmulatorScenarioIntegrationTests&FullyQualifiedName!~RealVisionIntegrationTests"
```

| 模块 | 基线值 | 门禁 |
|------|--------|------|
| Host.Tests | 129/0 | 必须 |
| Core.Tests | 全绿 | 必须 |
| 含 ErrorHandling 测试 | 全部通过 | 必须 |
| 含 PageAnalyzer 测试 | 全部通过 | 必须 |
| 含 TraversalFSM 测试 | 全部通过 | 必须 |
| 含 EnumerateScenarioRunner 测试 | 全部通过 | 必须 |

**基线意义**：仿真测试是唯一不依赖外部设备/网络的确定性门禁。所有 Core FSM、ErrorHandling、PageAnalyzer、ObservationPipeline 逻辑变更必须在此通过。locate/enumerate 集成测试依赖 emulator 和 AI 服务，不能作为每次 commit 的门禁。

### AC2: Locate 集成测试（实机验证 · 待办）

```bash
UNICLAW_ADB_SERIAL=<serial> UNICLAW_INTEGRATION_SCOPES=scenario-locate \
  dotnet test --filter "IntegrationScope=scenario-locate"
```
| 指标 | 基线值 |
|------|--------|
| status / completionReason | `success` / `target_found` |
| 耗时 | ≤120s |
| 引擎内 AI 调用 | ≤1（仅 finalAnalysis） |
| safety allowed / denied | ≥5 / 0 |

> 需 emulator（`uniclaw-lite-api35`）+ Sensenova API key。不适合 CI 自动触发，记入实施后 checklist。

### AC3: Enumerate 集成测试（实机验证 · 待办）

```bash
UNICLAW_ADB_SERIAL=<serial> UNICLAW_INTEGRATION_SCOPES=scenario-enumerate \
  dotnet test --filter "IntegrationScope=scenario-enumerate"
```
- `status: "success"`, `visitedEntries ≥ 5`, `scrollsConsumed ≥ 1`
- 无 `package_boundary` / `click_did_not_leave_home`

> 需 emulator + AI 服务，不适合 CI 自动触发，记入实施后 checklist。

### AC4: AI 空响应快速失败

模拟或实机触发 AI 空响应 → 确认：
- 空响应后不重试（1 次即终止）
- 异常类型为 `DomainValidationException`（`IsTransient=false`）
- 不返回 UIA-only 分析

### AC5: UIA 动态开关

- UIA dump 连续失败 1 次 → UIA 标记为不可用
- 后续 `AnalyzeCurrentPageAsync` → 直接调 AI，不尝试 UIA dump
- Trace 记录 "UIA_disabled" 决策

### AC6: 回退不引入额外 AI 调用

back 导航后 (`SkipUIAOnBackNavigation=true`)：
- 不执行 ADB UIA dump
- AI 调用数为 0（复用缓存的 PageAnalysis）

## Open Questions

- Advisor prompt template 需要设计（输入：错误类型 + PageAnalysis，输出：策略建议 JSON）
- Qwen `max_tokens`/`temperature`/`top_p` 最佳值待实测标定
