# Trace 父链打通 + 字段目录与分级 — 提案素材

> 状态：提案素材（尚未立项）。准则：**span 树父子关系清晰正确 + 业务代码低侵入**。
> 背景：`trace-span-helpers`（2026-08-03 已归档前置）落地了 `TraceSpanScope`/`RecordEventAsync`，AC1 快照闸门（`SpanTreeEquivalenceTests` S1–S5）已冻结。本文基于实际调用链/代码事实，提出三个可合并的子项：**父链打通**、**字段目录**、**字段分级**。

## 1. 目标

1. **父链打通**：`ai.call`/`ai.analyze` 从孤儿根变为 `engine.step` 的子节点，span 树不再有孤儿。
2. **字段目录**：span 属性键名从散落手写字符串集中为常量目录（`TraceFields`），防拼写漂移，并成为未来 source generator 校验（TSG002 式）的输入。
3. **字段分级**：同一 span 类型按 `TraceLevel` 记录不同字段粒度（Basic=核心结果、Detailed+=耗时/计费类细节），对齐现有 `TraceLevel`（None/Basic/Detailed/Full）语义。

三个子项共享同一批文件与一次快照重冻结，建议合并为一个 change 落地（见 §6）。

## 2. 现状事实（均已核实）

### 2.1 `AnalyzeCurrentPageAsync` 调用链——父链可携带

`IPageAnalyzer.AnalyzeCurrentPageAsync` 的 4 个调用点**全部位于 engine.step 上下文内**：

| 调用点 | 位置 | parent 可得性 |
|---|---|---|
| step 主流程 | `TraversalEngine.cs:290` | `stepScope.SpanId` / `TraceCoordinator.CurrentEngineStepSpanId` 在作用域内 |
| 稳定化分析 | `InterceptionHandler.cs:217` | `ctx.Trace.CurrentEngineStepSpanId`（entry.visited 已在用此通道） |
| 之后分析 | `InterceptionHandler.cs:479` | 同上 |
| 状态机分析 | `TraversalFSM.cs:368, 385` | FSM 由 step 驱动，上下文可达 |

**结论**：D-134 P3 注释"PageAnalyzer 跨层无通道"是组件边界惰性结论，非技术不可行。调用链携带 parent_id 即可打通，机制已被 `entry.visited`（parent = `CurrentEngineStepSpanId`）验证。

### 2.2 父链现状（trace-span-helpers 落地后）

```
engine.run（根）
└─ engine.step
   ├─ entry.generate → entry.observed / entry.ignored（同步 passthrough seam）
   └─ entry.visited → entry.skipped（parent = 最近 visited）
ai.call（孤儿根——本次要打通）
└─ ai.analyze
analyze.completion / analyze.error_loop（分析器独立层，无父）
```

- `entry.observed`/`entry.ignored` 走 `TraceCoordinator` 同步 passthrough（`IDynamicChildManager.Generate` 同步 guard 冻结，async 扩展不可 await），保持不动。
- 快照闸门：`SpanTreeEquivalenceTests` S1–S5（Host.Tests，6/6 绿）——**S4（AI 失败路径）将随父链打通重冻结**。

### 2.3 字段记录现状——全量、无目录、无分级

- 所有 span 属性为无条件全量记录的自由字典，键名散落手写字符串。
- 现有字段清单：

| spanType | 字段 |
|---|---|
| `ai.call` | start: `ai.capability`, `ai.mode`；end: `ai.provider_id`, `ai.model`, `ai.mode`, `ai.tokens`, `ai.success`, `ai.latency_ms` |
| `ai.analyze` | `ai.item_count`, `ai.retry_count` |
| `action.wait` | `action.type`；end: `action.result`, `action.wait_ms` |
| `action.click/scroll/back/launch` | `action.type`；end: `action.result`, `action.adb_ms` |
| `entry.visited` | `entry.name`, `entry.node_id`, `entry.step`, `entry.depth` |
| `entry.skipped` | `entry.name`, `entry.rule_id`, `entry.reason` |
| `entry.generate` | end: `entry.match_count`, `entry.ignored_count` |
| `entry.observed`/`entry.ignored` | `entry.name`, `entry.reason`, `entry.node_id`（等） |
| `analyze.completion` | `analyze.observed`, `analyze.visited`, `analyze.skipped`, `analyze.pending`, `analyze.end_reached`, `analyze.p50`, `analyze.p95`, `analyze.cold_start`, `analyze.rule`, `analyze.abnormal_spike` |
| `analyze.error_loop` | `error.reason`, `error.consecutive_steps` |
| `engine.run`/`engine.step` | 无属性（status 承载 reason） |

### 2.4 TraceLevel 现状——仅事件级，无字段级

- `TraceLevel`：`None`/`Basic`/`Detailed`/`Full`（`Graph/Models/EntryConfig.cs:20`，对齐 Python，**枚举值不可新增**）。
- 现有 gating 仅事件开关：`ShouldRecordEntryAttempt(level) => level >= Basic`、`ShouldRecordVisionCall(level) => level >= Detailed`（`TraversalEngine.cs:1561-1562`）。
- **无"同 span 按级别记不同字段"的机制**。

### 2.5 约束与 guard（不可触碰）

- `ITraceRecorder` 接口恰好 9 个声明方法（`ArchitectureGuardTests.ITraceRecorder_Has9Methods`）；扩展 helper 可 additive（本次的 `BeginSpanAsync`/`RecordEventAsync` 已示范）。
- `ITraceCoordinator` 恰好 27 个 public 成员（guard 冻结）；internal 成员可加（`TrackEngineStepSpan`/`Recorder` 已示范）。
- `IPageAnalyzer.AnalyzeCurrentPageAsync` 签名——`IScreenCapture.cs` 有"§12-B 截图归属：签名零改动"注释（约束对象是截图归属，但改接口签名仍属宪章级，需提案明确裁决）。
- `SpanType` enum 11 值锁定；`SpanTypes` 目录 18 常量冻结。
- AC3 白名单：`src/` 中 `StartSpanAsync`/`EndSpanAsync` 仅允许出现在 helper、TraversalEngine passthrough、recorder 实现。

## 3. 变更设计

### 3.1 父链打通

三个形态（可组合）：

| 形态 | 做法 | 代价 |
|---|---|---|
| **A. 签名携带** | `AnalyzeCurrentPageAsync(string? parentSpanId = null, ...)`，4 调用点传入 | `IPageAnalyzer` 接口签名变更（宪章级，需裁决 §2.5） |
| **B. 注入 provider（推荐）** | 新增 `ITraceContextProvider.CurrentSpanId`（`TraceCoordinator` 实现），PageAnalyzer 构造注入，内部取 | 零接口签名改动；PageAnalyzer 新增一个依赖 |
| **C. 不记录孤儿** | parent 为 null（非引擎入口）时跳过 `ai.call`/`ai.analyze` 记录 | 非引擎入口失去 ai 观测；S4 快照形态变化更大 |

建议 **B**（零签名改动、非引擎入口自然得到 null）；null 分支策略（保留孤儿 vs 按 C 跳过）在提案时裁决。

目标树：

```
engine.run
└─ engine.step
   ├─ entry.generate → observed / ignored
   ├─ entry.visited → entry.skipped
   └─ ai.call ← parent = engine.step（新）
      └─ ai.analyze
```

### 3.2 字段目录（`TraceFields`）

```csharp
public static class TraceFields
{
    public const string AiProviderId = "ai.provider_id";
    public const string AiTokens = "ai.tokens";
    public const string ActionAdbMs = "action.adb_ms";
    // …全部 2.3 清单键名
}
```

- 键名常量化，业务代码引用常量；`SpanTypeCatalog_ContainsAllEmittedSpanTypes` 式测试可扩展为"字段目录完整"。
- 未来 source generator 的 TSG002 字段校验输入。
- 注意：键名本身是 trace JSONL 的持久化字段，**常量值不得改变**（快照/下游消费兼容）。

### 3.3 字段分级（`TraceLevel` 门控）

描述符模式（helper 层 additive，业务代码不感知分级逻辑）：

```csharp
// 每 spanType 的字段分级描述符
TraceSpanFields.AiCall      // Basic: [ai.success, ai.mode, ai.capability]
                            // Detailed+: [ai.provider_id, ai.model, ai.tokens, ai.latency_ms]
TraceSpanFields.ActionClick // Basic: [action.type, action.result]
                            // Detailed+: [action.adb_ms]
```

- helper 演进（additive，guard 不受影响）：`BeginSpanAsync(spanType, ..., fields: TraceSpanFields.AiCall, level)` 或等效扩展——记录时按 `level` 过滤扩展键。
- `level` 来源：`EntryConfig.TraceLevel`（引擎配置）或调用方传入；缺省 = Detailed/Full（现状全量行为，向后兼容）。
- 分级划分草案（提案时可调整）：
  - **Basic（核心结果）**：`ai.success`, `ai.mode`, `action.result`, `entry.name`, `analyze.observed/visited/skipped/pending/end_reached/rule`, `error.reason` 等
  - **Detailed+（耗时/计费/定位细节）**：`ai.tokens`, `ai.latency_ms`, `ai.provider_id`, `ai.model`, `action.adb_ms`, `action.wait_ms`, `analyze.p50/p95/cold_start/abnormal_spike`, `entry.node_id/step/depth/rule_id/match_count/ignored_count`, `error.consecutive_steps`, `ai.item_count/retry_count` 等

## 4. 验收草案

1. **快照更新**：S4（AI 失败路径）重冻结——`ai.call` 从根节点变为 `engine.step` 子节点；S1–S3/S5 快照**必须 unchanged**（除非字段分级改变其字段集，分级默认 Detailed 兼容全量则不变）。
2. **新增场景 S6（或扩展 S4）**：完整父链断言 `engine.run → engine.step → ai.call → ai.analyze`，含重试路径（`ai.retry_count`）。
3. **字段目录测试**：目录含全部 2.3 键名；常量值非空且以 `layer.` 命名空间。
4. **分级测试**：Basic 级别记录核心字段、不记录 Detailed+ 字段；Detailed 与现状全量行为一致（向后兼容证明）。
5. **C 形态测试**（若选 C）：非引擎入口调用（parent null）→ 零 `ai.*` span。
6. **AC 框架复用**：oracle 零改动、基线计数（M0 基线 Core 1049/2、Host 141/7 之后的当前计数）、`SpanType` enum 11 值、AC3 白名单。

## 5. 决策点清单（提案时需拍板）

1. **形态**：B（注入 provider，推荐）还是 A（改接口签名）？是否组合 C（null 时跳过）？
2. **孤儿策略**：非引擎入口保留孤儿 vs 跳过不记录（C）。
3. **字段分级是否本轮做**：可拆出独立 change（父链+目录先行，分级押后）。
4. **`IPageAnalyzer` 签名改动许可**：§12-B 注释的裁决。
5. **`level` 来源**：`EntryConfig.TraceLevel` 还是调用方参数透传。

## 6. 建议落地形态

- change 命名建议：`trace-parent-linkage`（父链 + 字段目录 + 字段分级，合并）。
- 拆分备选：`trace-parent-linkage`（父链 + 目录）→ `trace-field-levels`（分级）。
- 合并理由：三子项共享 PageAnalyzer/4 调用点/SafetyGate 等文件与一次快照重冻结；分开则两次快照变动 + 两轮闸门。
- 该 change 的 design 可引用 `openspec/changes/trace-span-helpers/` 的 AC1–AC6 框架与 Deferred 段（`[TraceSpan]` generator 约束同步纳入 §3.2/§3.3 作为其输入）。
