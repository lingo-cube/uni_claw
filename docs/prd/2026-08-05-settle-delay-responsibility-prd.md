# 步间延迟职责下沉 PRD

> Date: 2026-08-05
> Status: draft
> Scope: `src/UniClaw.Core/Traversal/` + `src/UniClaw.Host/Runner/` + `src/UniClaw.Host/Commands/`

## 1. 问题诊断

### 1.1 当前时序

```
引擎循环（每步）:
  ├─ DelayPerStepMs 300ms         ← 无条件等待，包括非操作步
  ├─ AnalyzeCurrentPageAsync      ← 缓存命中或真实截图
  │
  ├─ FSM ExecuteStepAsync:
  │   ├─ Execute: tap() → 成功    ← 操作执行，无 settle
  │   └─ ResultVerify:
  │       ├─ after 截图            ← ⚠️ 裸奔：操作后 ~50ms 立即截图
  │       ├─ 没变化（截到动画帧）  ← 不是真没变化，是截图截早了
  │       └─ 重试截图             ← 隐式 settle，浪费 ~4.5s
  │
引擎循环（下一步）:
  ├─ DelayPerStepMs 300ms         ← 300ms，上一步的 after 截图已经是 4.5s 前
  ├─ AnalyzeCurrentPageAsync      ← UI 早就稳了，白等
```

### 1.2 三个缺陷

| # | 缺陷 | 后果 |
|---|------|------|
| D1 | **非操作步也等 300ms**（决策、验证、缓存命中） | 单 run ~5-7 步白等，1.5-2s 浪费 |
| D2 | **引擎管 UI settle**（循环头） | 职责错位——引擎不操作设备，不知道是否需要等 |
| D3 | **ResultVerify after 首帧裸奔**（操作后无 settle） | 动画帧进视觉分析 → 假阴性 → 重试兜底（~4.5s） |

### 1.3 根因

**等待放在了错误的层和错误的位置。** "UI 稳定"是操作执行方的职责，应该由**知道操作成功、且处于视觉管线中的组件**负责，放在**操作成功后、缓存失效前**。引擎只负责遍历决策，不应介入 UI 物理特性。

## 2. 目标架构

### 2.1 职责划分

```
引擎:  遍历决策、状态转换、验证判定 → 不管 UI settle
执行器: ADB 操作 + 操作后 settle → 谁动 UI 谁等
缓存层: 失效 + 命中等 → 不管时序
```

### 2.2 目标时序

```
引擎循环（每步）:
  ├─ （无等待）                  ← 引擎不管
  ├─ AnalyzeCurrentPageAsync      ← 缓存命中或真实截图
  │
  ├─ FSM ExecuteStepAsync:
  │   ├─ Execute: tap() → 成功
  │   │   └─ settle 300ms        ← ✅ 操作后立即 settle（谁动 UI 谁等）
  │   └─ ResultVerify:
  │       ├─ after 截图           ← ✅ 截到稳定画面
  │       └─ 变化 → Branch       ← ✅ 大概率一次过，重试不再需要
```

### 2.3 对比

| | 现在 | 目标 |
|---|---|---|
| 操作步 settle | 有（循环头），但对 after 截图无效 | 操作成功后立即等，after 截图受益 |
| 非操作步 | 白等 300ms | 零开销 |
| 职责 | 引擎循环头 | `PageInvalidatingActionExecutor`（动 UI 的装饰器） |
| 重试次数 | 动画帧被误判 → 重试 | 稳定画面 → 一次过 |
| IActionExecutor 接口 | 不变 | 不变（WaitAsync 已存在） |

## 3. 实现

### 3.1 `HostCommands.cs` — 去掉引擎 delay

```diff
// line ~872
- DelayPerStepMs = 300,
+ DelayPerStepMs = 0,
```

`TraversalEngineConfig.DelayPerStepMs` 属性保留（默认 0），循环守卫 `if (_config.DelayPerStepMs > 0)` 代码不动。生产不再使用，模拟/测试仍可独立设置。

### 3.2 `InvalidatingPageAnalysisCache.cs` — 操作后 settle

`PageInvalidatingActionExecutor` 是唯一知道"ADB 操作成功"的组件。新增 `_settleDelayMs`，在 `ExecuteAsync` 中注入：

```csharp
public sealed class PageInvalidatingActionExecutor : IActionExecutor
{
    private readonly IActionExecutor _inner;
    private readonly Action _invalidate;
    private readonly Action? _onBackSuccess;
    private readonly int _settleDelayMs;                      // ← NEW

    public PageInvalidatingActionExecutor(
        IActionExecutor inner,
        Action invalidate,
        Action? onBackSuccess = null,
        int settleDelayMs = 300)                              // ← NEW param
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _invalidate = invalidate ?? throw new ArgumentNullException(nameof(invalidate));
        _onBackSuccess = onBackSuccess;
        _settleDelayMs = settleDelayMs;                       // ← NEW
    }

    private async Task<bool> ExecuteAsync(
        Func<CancellationToken, Task<bool>> execute,
        CancellationToken cancellationToken,
        Action? onSuccess = null)
    {
        var success = await execute(cancellationToken);
        if (success)
        {
            _invalidate();
            onSuccess?.Invoke();
            if (_settleDelayMs > 0)                          // ← NEW
                await Task.Delay(_settleDelayMs, cancellationToken);
        }
        return success;
    }
}
```

### 3.3 配置

settle 延时通过环境变量 `UNICLAW_SETTLE_DELAY_MS` 配置，默认 300ms，设为 0 关闭。

```csharp
// HostCommands.cs — settleDelayMs 读取
var settleDelayMs = int.TryParse(
    Environment.GetEnvironmentVariable("UNICLAW_SETTLE_DELAY_MS"),
    out var v) ? v : 300;
```

### 3.4 `HostCommands.cs` — 接线

```diff
// line ~578
new PageInvalidatingActionExecutor(
    adbExecutor,
    cache.Invalidate,
    onBackSuccess,
+   settleDelayMs: settleDelayMs)
```

### 3.5 环境变量

| 变量 | 默认 | 说明 |
|------|------|------|
| `UNICLAW_SETTLE_DELAY_MS` | `300` | 操作成功后 settle 等待（毫秒），`0` 关闭 |

契约注册位置：[`docs/testing/integration-config.md`](../testing/integration-config.md)，L4 层，author = `HostCommands.cs`，consumer = `PageInvalidatingActionExecutor`。

### 3.6 不动

- `TraversalEngineConfig.DelayPerStepMs` 属性 — 保留，默认 0
- `TraversalEngine.cs` 循环内 delay 守卫 — 保留 `> 0` 判断
- `IActionExecutor` 接口 — 不变
- `AdbActionExecutor` — 不变

### 3.7 测试影响

所有测试通过自己构造 config，明确设置 `DelayPerStepMs`，不受生产配置变化影响：

| 测试 | DelayPerStepMs 设置 | 状态 |
|------|---------------------|------|
| `EnginePathTests.cs` | 0（显式） | ✅ 无影响 |
| `TraversalEnginePauseResumeTests` | 10 | ✅ 自己设的 |
| `TraversalEngineTests.cs:1210` 超时测试 | 50 | ✅ 自己设的 |
| `TraversalHookTests.cs:183` 取消测试 | 50 | ✅ 自己设的 |
| `TraversalEngineTests.cs:967` 默认值断言 | 默认 0 | ✅ 属性保留 |

## 4. 验证清单

| # | 验证项 | 方法 |
|---|--------|------|
| V1 | 核心测试无回归 | `dotnet test tests/UniClaw.Core.Tests --filter "FullyQualifiedName~TraversalEngine\|FullyQualifiedName~TraversalHook"` |
| V2 | Host 构建通过 | `dotnet build src/UniClaw.Host -c Debug` |
| V3 | scenario-locate 集成测试通过 | host-test-runner skill |
| V4 | ResultVerify 首帧验证通过率提升 | 对比去掉前后 run 的 verification_passed vs verification_passed_retry 比例 |
| V5 | 非操作步零等待 | `run.log` 确认非操作步步间间隔（现在为 0，操作步间仍有 settle） |

## 5. 风险

- **swipe settle 叠加**：swipe 本身有 `durationMs` 参数（手势时长默认 300ms），加上 settle 300ms → 总计 600ms。swipe 手势时长和 post-animation settle 是不同阶段，语义正确，不冲突。
- **值未调优**：300ms 沿用现有值。可通过 `UNICLAW_SETTLE_DELAY_MS` 按设备/场景调整，真机可能降到 100-150ms。设置为 `0` 完全关闭 settle（回退到当前裸奔行为）。
