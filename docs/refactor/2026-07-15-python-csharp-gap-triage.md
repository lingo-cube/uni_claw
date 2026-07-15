# Python ↔ C# 功能差距梳理与裁剪 (Gap Triage)

> 基于 `main` 分支 Python 源码与 `feature/refactor` 分支 C# 代码的全量对比，
> 对「未实现点」做合理性裁剪：区分 **永久移除**（被 C# 架构取代）、**阶段推迟**（Phase 3+ 门槛）、**保留 backlog**（当前值得做）。
> 有意偏差（scroll 重构、to_dict 禁令、D-1/D-3/D-10~D-13 等已裁决项）不计为差距。
> 对比方法: 4 路并行子代理逐文件比对 (AI/exception, StateMachine/Traversal, Simulation/Trace, Graph/Domain)。
> 日期: 2026-07-15

---

## 结论速览

原始差距清单约 40 项，裁剪后:

| 桶 | 数量 | 含义 |
|----|------|------|
| A. 永久移除 | 17 项 | 被 C# 架构决策取代，**不移植** |
| B. 阶段推迟 | 8 组 | 有明确 Phase 门槛，保留在 roadmap，当前不动 |
| C. 保留 backlog | 8 项 | 当前值得实施（以 fail-fast 校验 + Plan 管线正确性为主） |

净效果: **当前实际待办 = 8 项**，其余全部有据可依地关闭或推迟。

---

## A. 永久移除 — 被 C# 架构取代，不移植

| # | 差距项 (Python 侧) | 移除理由 | 依据 |
|---|---|---|---|
| A-1 | Handler 统计计数器 (Popup/Container/Error 的 `processed_count`、`recovery_rate` 等) | Observability 是唯一遥测路径，ad-hoc 计数器与 `ITraceRecorder` 重复；统计可由 trace 查询派生 | D-17, D-18~D-22 |
| A-2 | `_detection_cache` / `_strategy_cache` / `_decision_cache` | 纯计算不缓存，已裁决 | D-3 |
| A-3 | `UrgencyLevel.CRITICAL`、`CompletionReason.Error` | 不保留/不新增死值，已裁决 | D-11, D-12 |
| A-4 | `PopupClassifier._calculate_confidence()` | 弹窗权威判定是 vision `IsPopup`，classifier 只对已知弹窗分类；置信度分数在 C# 中无消费者 | D-24 |
| A-5 | GlobalFSM 12 个便利包装 (`pause`/`resume`/`report_error`/`reset`…) + `get_current_state_duration` | `TransitionTo` 显式传状态已覆盖；error context 归 Error 子系统；时长归 Observability | D-15 |
| A-6 | NodeStack/StackFrame 18 个辅助成员 (`get_next_child`、`contains_node`、`to_list`…) | 子节点迭代由 StepOrchestrator step 流程持有；调试视图走 `ITraceService.ReconstructTree` | D-27 职责边界 |
| A-7 | 三级 `SessionNode`/`StepNode`/`SpanNode` 树、ULID、`DynamicNodeLifecycleSpan`、`StateDecisionSpan` | 被扁平 record + `TraceContext` 关联信封取代 (Locked) | D-18~D-22 |
| A-8 | `ScrollDataStore`、`ScrollSegment`/`ScrollState`/`ScrollPage` | 被 `IScrollContentSource` + `SimulatedScreen` 取代 (scroll = action + judgment 重构) | scroll 重构 |
| A-9 | `PageAnalyzer` (缓存 + 页面类型/action hint 推断) | `StatefulMockVisionService` 直接从确定性 fixture 构建 `PageAnalysis`；推断层只对真实含噪屏幕有意义 | 仿真架构 |
| A-10 | `analysis/server.py` (Starlette 仪表盘)、`structured_logging.py`、`results.py` (HTML/MD 报告)、analysis 层 `trace_analyzer.py` | 仪表盘/工具不属于 Core classlib；仓库已有 Python dashboards 消费 trace 文件。只需导出桥 (→ C-7) | 分层定位 |
| A-11 | `DynamicMatcher.instantiate_match`/`_build_context`/match history/statistics/`MatchStatus`/`__bool__` | 该编排已迁移至 `DynamicChildManager`（经核实比 Python 更完整）；record + null 语义取代 status/bool | 代码核实 |
| A-12 | `_build_ai_call_metrics` 仿真伪造延迟/token 注入 | 伪造指标与 `TraceIntegrityExpectation` 矛盾；仿真应记录真实耗时 | trace 完整性 |
| A-13 | `SimulationRunner` + `PlanDebugger` | xUnit 基线测试 + `StateFixtureBuilder` 已承担该角色；`PlanDebugger` 是 Python REPL 交互工具，无 C# 工作流对应物 | 测试架构 |
| A-14 | `BehaviorValidator` 模糊匹配 / 置信度分级 (精确/模糊ID/模糊文本) | 模糊匹配是为真机噪声补偿；仿真 ID 精确。`ExpectedBehavior` 8 维验证是更丰富的替代 | 仿真架构 |
| A-15 | `TemplateRegistry` 具体类 + `DEFAULT_TEMPLATES` | 经核实 `ITemplateRegistry` 无任何消费者 — `DynamicChildManager` 内联构建 Template (`TraversalEngine.cs:601`)。⚠️ 建议连同删除未使用的接口（待用户裁决 → 见「待裁决」） | 代码核实 |
| A-16 | `MatchCondition` 正则文本匹配 | D-8 锁定 Contains 默认；`TextMatchMode` 枚举已锁定 — 仅当真实 plan 需要正则时再议 | D-8, locked-enums |
| A-17 | `ProblemDetector` 作为运行时组件 | 引擎已有 AntiLoop (`TraversalEngine.cs:244`)；重复动作/DFS 检查已在 `ExpectedBehavior`。残余价值（孤立动态节点检测）更适合作为一个新 ExpectedBehavior 维度吸收，而非新子系统 | 代码核实 |

---

## B. 阶段推迟 — 非缺失，有明确门槛

| 项 | 门槛 / 依据 |
|---|---|
| Precondition 纠偏 + `classify_relation()` (MATCH/NAVIGABLE/DEEPER/UNKNOWN) | D-23 显式推迟到 Phase 3（需真实 vision 才有意义）。当前 assume-pass + trace 是已裁决行为 |
| AUTO_ESCAPE 点击+验证执行 | D-27 minimal handler 是有意为之；ContainerHandler 已决策 `FallbackAction` — 执行需真实 vision |
| `EntryPolicyExecutor` 真实执行 + `wait_for_condition` (fast/polling 页面验证) | 需设备 + vision (Phase 3) |
| 整个 AI 栈 (providers/UniBrain/prompts/cache/task parser)、真实 `VisionService`、ADB client、`SafetyFilter`、API key 配置 | Phase 3+，且被 CLAUDE.md 记录的 Mode A/B 决策阻塞 |
| 类型化异常层级 + handler chain + ExceptionHistory | C# 已选 classifier-based 架构 (D-25)；类型化异常只在真实 executor 出现后才有回报 |
| `trace/recovery.py` ContextRebuilder、Prometheus/类型化指标 | 只对长时真机运行 / 生产运维有意义 |
| `inference_history` / `goal_attempts` 上下文字段 | 随 AI advisor 实现一起加 |
| Scroll 故障注入 (`fail_next_scroll`, `simulate_delay_ms`) | 可选项；仅当需要故障注入基线测试时再加 |

---

## C. 保留 backlog — 当前值得实施 (8 项)

| # | 项 | 内容 | 优先理由 |
|---|---|---|---|
| C-1 | **Graph 模型构造期校验** | 将 Python `__post_init__` 的范围/非空检查移植为 `DomainValidationException` fail-fast: Precondition (timeout 0<x≤300)、DynamicRule (RuleId/ChildTemplate 非空)、ChildrenStrategy (max_children 0-10000)、ErrorPolicy (max_retries 0-100)、ExitCondition (DEPTH_LIMITED 需 max_depth >0 ≤1000)、CompletionPolicy (target_name/timeout≤86400/max_steps≤1000000)、EntryPolicy (timeout 0<x≤300)、IntentSlots (scope 枚举值)、TraversalNode (NodeId/Name 非空 + container 不得 ChildrenStrategyType.None) | 项目自身声明的 fail-fast 约定当前被违反；性价比最高 |
| C-2 | **PlaceholderResolver fail-fast + TemplateInstantiator 丢字段** | 不支持/缺失 placeholder → 抛异常（当前静默替换为空串）；补 `Target.Meta`、Restore `Target`/`Params`、`ui_condition`；统一 context key (`parent_id` vs `parent_node_id`) | 静默数据丢失类 bug |
| C-3 | **Per-node `error_policy` 接线** | `ErrorPolicy` 模型存在但是死代码；错误处理路径从不读取 `node.ErrorPolicy` (retry/skip/backtrack/abort/fallback + max_retries + failed_nodes 重试跟踪) | 模型已建、行为缺失 |
| C-4 | **Plan 根节点校验** | root 必须存在 / 为 Container / operation 为 no_action；当前 `BuildDefaultRoot()` 静默掩盖配置错误。并入 PlanCompiler，不单独移植 `PlanValidator` | fail-fast 约定 |
| C-5 | **PlanCompiler 对齐裁决** | `_build_exit_condition` (navigation slot → ExitCondition)、多段 `target_path` 静态节点链（当前只建单叶）、默认值偏差（timeout 60↔300s、DirectDeeplink↔ColdLaunch、Screen↔Container root、`steps`↔`max_steps`、target_path scope NONE↔TargetFound）。每条需显式 align-or-diverge 决策条目，不允许静默偏差 | 行为正确性偏差 |
| C-6 | **`TraversalPlan` JSON 读写** | 经 `DomainJsonOptions` 序列化/反序列化；从文件运行 plan 的前提 | 功能前提 |
| C-7 | **Trace `FileStorage` (JSONL 导出桥)** | sync-first (D-22)；analysis 栈中唯一值得保留的部分 — 让既有 Python dashboards 消费 C# trace | 工具链互通 |
| C-8 | **P3 五项** | `ContentNode.ToMarkdown()`、`Region.Id` 非空校验、`TypeHint [JsonPropertyName]`、`TypeHint.Values` 改 `IReadOnlyList<string>`、`IsCanonical(string)` | 已跟踪，工作量极小 |

建议实施切分: C-1~C-4 + C-8 可合并为一个 OpenSpec change（fail-fast 补齐）；C-5 先出决策条目再实施；C-6、C-7 各自独立 change。

---

## 已确认对齐（无差距，无需动作）

- Domain 24 类型（除 C-8 的 P3 项）
- `ExpectedBehavior` 8 维验证（比 Python 更丰富: auto_derive、OperationRules、TraceIntegrity 为 C# 独有）
- `StateFixture` / Stateful mock 双件套 / Scrollable mock 双件套
- `PageCacheManager` / `PageSnapshotManager` / `DynamicChildManager` / `TraceCoordinator` / `StepOrchestrator`（C# 达到或超过 Python 水位，且新增 Python 没有的 scroll/navigation 处理）
- `InMemoryTraceRecorder` 生命周期（等价 Python `TraceRecorder`）

---

## 待裁决（需用户确认）

| # | 问题 | 建议 |
|---|---|---|
| Q-1 | 是否删除无消费者的 `ITemplateRegistry` 接口 (`Graph/Abstractions/ITemplateRegistry.cs`，仅 guard test 引用)? | 删除（若未来 plan 携带声明式模板定义再重建，届时签名需补 `parentPath` 参数） |
| Q-2 | 桶 C 是否走 `/opsx:propose` 立项? | C-1~C-4+C-8 合并一个 change；C-5 先决策后实施；C-6/C-7 独立 change |

---

## 附: 全量对比原始发现（裁剪前）

<details>
<summary>展开查看按子系统的原始差距清单</summary>

### 整个子系统无 C# 实现

| Python 子系统 | C# 现状 | 缺失内容 |
|---|---|---|
| `src/ai/providers/*` + `provider.py` (UniBrain) | 仅接口 (`IAIStrategyAdvisor`) | 全部 provider (Claude/DeepSeek/MiMo/MCP)、UniBrain 编排、路由配置、工厂 |
| `src/ai/prompts/` | 无 | PromptManager (markdown+YAML 模板、版本、热加载)、PromptValidator |
| `src/ai/cache.py` | 无 | AIResponseCache (LRU+TTL)、DebounceTracker |
| `src/ai/vision_service.py` 真实实现 | 接口+mock | `ClaudeVisionService` — 无任何真实 `IVisionProvider` 实现 |
| `src/ai/task_parser.py` | 无 | 自然语言任务 → IntentSlots |
| `src/ai/mock_advisor.py` / `noop_advisor.py` | 无 | `IAIStrategyAdvisor` 连 Mock/NoOp 实现都没有 |
| `src/adb/adb_client.py` | 无 | ADBClient 抽象、Real/Mock 实现 |
| `src/safety/filter.py` | 仅方法签名 | SafetyFilter (白/黑名单 + 审计日志) |
| `src/exception/*` | 架构不同，大部分缺失 | 完整异常层级、handler chain、ExceptionHistory、初始化异常 |
| `src/analysis/*` | 无/部分 | ResultManager+报告、AnalysisServer、StructuredLogger、分析层 TraceAnalyzer、树导出 |
| `src/trace/recovery.py` | 无 | ContextRebuilder 重放重建 |
| `src/config/settings.py` | 部分 | 仅遍历配置存在；API key、vision 管线配置、ADB 配置、`get_settings()` 全缺 |

### 现有 C# 代码中的 stub / 空壳（行为影响最大）

1. `HandlePreconditionCheckAsync` assume-pass stub (D-23) — Python 有 3 轮重试: vision 检查 → `classify_relation()` → 纠偏（点菜单/按返回）→ 复验。`classify_relation()` 在 C# 完全没有对应物
2. `HandleFrameCompleteAsync` trampoline — Python 的 AUTO_ESCAPE（找未访问兄弟菜单→点击→vision 验证→重试一次→回退 back）和 BACK/SKIP/ABORT 分支未实现
3. `EntryPolicyExecutor` stub — 不执行任何动作直接返回成功。缺: deeplink 执行、冷启动 (press_home + find_app_icon)、`wait_for_condition()`、入口 trace 记录
4. Per-node `error_policy` 从不被读取
5. `PlanValidator` 不存在 — `BuildDefaultRoot()` 静默掩盖配置错误

### Graph 层部分差距

- `TemplateRegistry` 具体类缺失（仅接口，接口缺 `parentPath` 参数）
- `PlanCompiler`: 无 `_build_exit_condition()`、无多段 `target_path` 静态节点链、root 缺 Precondition/meta；默认值偏差 (timeout 60↔300s、DirectDeeplink↔ColdLaunch、target_path scope TargetFound↔NONE、Screen↔Container)
- `DynamicMatcher`: 核心匹配在，缺模板集成/历史/统计/正则；`MatchResult` 缺 status/template_id/context
- `PlaceholderResolver`: 静默替换未知/缺失 placeholder；TemplateInstantiator 丢 `Target.Meta`、Restore `Target`/`Params`、`ui_condition`，context key 不一致
- Graph 模型构造期校验全缺（9 个模型）
- `TraversalPlan` 无 JSON 读写；8 个 Graph 枚举缺 `Values`/`FromValue`/`IsValid` 扩展

### Simulation / Observability 部分差距

- `ProblemDetector` 缺失（运行时异常检测: 死循环、重复动作、未访问节点、FSM 非法迁移、孤立动态节点）
- `SimulationRunner` + `PlanDebugger` 缺失
- `PageAnalyzer` 缺失
- `BehaviorValidator` 模糊匹配缺失
- Trace 侧: 无 `FileStorage` (JSONL 持久化)；无三级 span 树/ULID/生命周期 span；TraceAnalyzer 缺约一半提取视图（错误统计、时间百分位、操作树、span chain）；无类型化 AI 调用指标/Prometheus 导出
- Scroll 故障注入在重构中丢失

### 次要 / 已知推迟（确认仍开放）

- CLAUDE.md P3 五项全部确认仍缺失
- Phase 2 已文档化推迟: `ContentTree`、`SimulationState`
- 便利/诊断层: GlobalFSM 12 个生命周期辅助、18 个 StackFrame/NodeStack 辅助、handler 统计、popup 置信度、`inference_history`/`goal_attempts`

</details>
