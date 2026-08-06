# fsm-analyzer 精简知识

> 从 L1–L4 层文档蒸馏。每条 1–3 句，结论必须仍可溯源到层文档或源码行。来源文档更新时按 INDEX.md 刷新规则重精简。

## L1 FSM 架构层 — 转移矩阵与类型体系

### TraversalFSM 转移矩阵（8 状态 · 19 边 · D-240）
来源：`TraversalFSM.cs` `TransitionMatrix` 字段 + `docs/system/patterns/fsm-design.md`

```
NodeSelect       → PreconditionCheck, Branch
PreconditionCheck → Execute, ErrorHandling          (D-1: Branch 已移除)
Execute          → ResultVerify, ErrorHandling       (D-240: Branch 已移除)
ResultVerify     → Branch, PopupHandling, ErrorHandling
Branch           → NodeSelect, FrameComplete, ErrorHandling  (D-240: PreconditionCheck 已移除)
FrameComplete    → NodeSelect                        (D-240: ErrorHandling 已移除)
ErrorHandling    → NodeSelect, Execute, FrameComplete, Branch
PopupHandling    → ResultVerify, ErrorHandling
```

- **D-240 矩阵职责分离**（2026-08-05）：矩阵只做 Handler 门。异常路由走 CanTransitionTo 守卫 + 降级链（D-241），不经过矩阵。移除 3 条死边后每条剩余边均有至少一个 handler 显式生产。
- **D-241 降级链**：StepAsync catch → `CanTransitionTo(ErrorHandling)` → false 时按状态降级：NodeSelect→Branch / FrameComplete→NodeSelect / ErrorHandling→FrameComplete。降级后有步数燃烧风险（FrameComplete 不弹栈），但优于崩溃。

### GlobalFSM 转移矩阵（8 状态 · 12 边）
来源：`GlobalFSM.cs` `TransitionMatrix` 字段

```
Idle → Initializing · Initializing → Traversing, Error · Traversing → Paused, Error, Completed
Paused → Traversing, Terminated · Error → Recovering, Terminated · Recovering → Initializing, Terminated
Completed / Terminated → (锁定态)
```
- 两步终止（D-82）：Traversing→Paused→Terminated。ForceState 绕过矩阵不触发 callback。

### 错误生命周期（D-242~D-244）
- **D-242 递增收敛**：ConsecutiveErrors = 恢复尝试次数。唯一递增点 = HandleErrorHandlingAsync:592。入口点（StepAsync catch / HandlePreconditionCheckAsync / HandleExecuteAsync catch）**不递增**。全路径 +1/周期，门限 ≥3 = 精确 3 次。
- **D-243 LastError 清零**：HandleErrorHandlingAsync 3 条返回路径均 `SetLastError(null)`（主返回 + page-item 门限 + consecutive 门限）。NoStepContext stub 除外。
- **D-244 Popup 失败 LastError**：弹窗 dismiss 失败设 `InvalidOperationException("Popup dismiss failed: dismiss_action=...")`。消息不含枚举名——ErrorClassifier 是 substring 匹配，"Permission"/"Timeout" 会误分类。

### 关键 gates（源码验证）
- stale-click fuse: limit=3 → MarkNodeVisited+Pop
- page-item limit: ≥5 distinct failures + depth>1 → PressBack+FrameComplete
- consecutive errors: ≥3 + depth>1 → PressBack+FrameComplete
- consecutive error reset: ResultVerify 验证成功时执行，保 page-item gate 可达

### Handler 决策表摘要
- HandleNodeSelectAsync: stack empty→Branch, has node→PreconditionCheck
- HandlePreconditionCheckAsync: checker→Execute/ErrorHandling; trace `precondition_assume_pass`（D-23 有意为之）
- HandleExecuteAsync: NoAction→ResultVerify; dispatch Click/Swipe/Back/InputText; Text→Coordinate 三链解析
- HandleResultVerifyAsync: 3-round retry + popup检出 + stale-click熔断 + consecutive-reset
- HandleBranchAsync: STATIC→unvisited check; DynamicMatch→NodeSelect（滚动委托拦截层）; NONE→depth判断
- HandleFrameCompleteAsync: 恒定 NodeSelect（不弹栈——Pop 点在引擎/拦截层）
- HandleErrorHandlingAsync: 5-strategy + advisor(≥0.7) + 两个 gate + LastError 清零
- HandlePopupHandlingAsync: 6-step pipeline → ResultVerify/ErrorHandling; 失败设 LastError（不含枚举名）

### 类型体系
- Enums: TraversalState(8), GlobalState(8), PopupType(6, 含Anr), ErrorType(6), ErrorStrategy(5), CompletionReason(4) 等
- NodeStack: DefaultMaxDepth=10, Push 在 ≥MaxDepth 返回 false
- TraversalRuntimeContext: 5 subsystems (Navigation=12, Error=5, Session=4, Progress=5, Cache=2+2)

## L2 Handler 管线层 — 决策逻辑与数据流

### StepAsync 异常契约（D-241 后）
- `_currentStepContext` 在 dispatch 前设置，finally 清除
- handler 抛异常 → catch 块：`SetLastError(ex)`（不递增）→ `CanTransitionTo(ErrorHandling)` 守卫 → 合法则 ErrorHandling，否则降级链
- 日志：`FSM {From}→{To} step={Step}` (Info)

### StepOrchestrator 14-step 拦截
- Step 8 (Branch): fromState∈{Execute,ResultVerify,NodeSelect} 时触发 OnBranch。拦截 NextState ∈ {Branch, NodeSelect}
- Step 9 (DynamicMatch NodeSelect): OnDynamicMatchNodeSelect → navigation/scroll/PressBack。**NextState 恒为 NodeSelect——纯副作用操作**
- Step 10 (FrameComplete): OnFrameComplete。**永远不从 FrameComplete 状态触发**（handler 返 NodeSelect）
- 拦截层 NextState 不经过矩阵校验。已知 NextState 合法范围 = {Branch, NodeSelect, FrameComplete}

### DynamicChildManager dedup 机制
- cache: `(fingerprint, children)`; dedup key = `(fingerprint, template_text)`; pairs 跨 Invalidate 持久
- OCR 文本变体（空格/拼写差异）生成不同 key + 不同 NodeId → visited + dedup 双双失配 → 潜在重复点击
- Fingerprint = sorted (type,name) 全量 hash——任何 item 文本变化改变指纹

### ErrorClassifier 碰撞风险
- ErrorHandler.cs:13-48 大小写不敏感 substring 匹配——消息含 "Permission"→ErrorType.Permission，"Timeout"→ErrorType.Timeout
- 构造描述性错误消息时必须避开枚举名

## L3 引擎集成层

### TraversalEngine FSM 生命周期
- Initialize → RunAsync（step + 拦截 + visited 记账 + termination check）
- Pre-step 页面分析在 ResultVerify 时跳过（D-G9）——保护 before 快照
- 终止路径：FrameCompleted@depth≤1→AllVisited, AntiLoopTriggered, TargetFound, Timeout, MaxSteps, Cancelled, Error

### FrameComplete 不弹栈
- 全仓 Pop 仅 3 处：引擎 leaf-pop(:355)、stale-click 熔断(:427)、DynamicMatch 拦截(InterceptionHandler:240/269)
- NodeStackAdapter 无 Pop-on-FrameComplete——任何"降级→FrameComplete=弹出"假设错误

### run.log 速查
```
grep "TraversalFSM:"        → FSM 转移序列
grep "\[ERROR\]"            → 严重错误
grep "→ deny"               → 安全门拒绝
grep "Engine terminated"    → 终止原因（未污染）
grep "Error classified:"    → ErrorClassifier 输出
```

## L4 分析诊断层

### 脚本库（`scripts/`）
| 脚本 | 用途 |
|------|------|
| `matrix_from_source.py` | 从 C# 源码提取两矩阵 + `--diff-docs` 交叉比对 + `--check` CI 自检 |
| `fsm_transition_path.py` | trace.jsonl / run.log → FSM 转移序列 + 频次直方图 · `--validate` 自动加载源码矩阵 |
| `fsm_cycle_detector.py` | 检测 stuck_state / short_cycle / no_progress / error_loop |

### 双轨分析标准工作流
- 静态轨：`--diff-docs` → 矩阵结构验证 → handler 返值×矩阵边可达性审计 → gate 交互审计
- 运行时轨（有 run 产物时）：转移路径 + 循环检测 + 状态分布 + deny 模式 + 行动效率
- 静态找"可能错什么"，运行时找"实际在怎么错"

### TraceTool CLI 退出码
0=成功 · 1=diff差异 · 2=用法错误/run不存在 · 3=空trace（0 span）

### 委托 trace-analyzer 时机
需要 span 树解析、verify 判定归因、完整性自评、跨 run diff → 委托。轻量查询（list/diagnose 快速扫 verdict）自行调用 CLI。
