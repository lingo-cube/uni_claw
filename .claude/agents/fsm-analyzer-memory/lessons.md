# fsm-analyzer 案例经验

> 每次诊断/审查后精简追加：日期 + 来源 + 事实/方法/局限。同主题合并，重复不追加，错误认知立即纠正删除。每条 ≤3 句。

## 2026-08-05 — CPU 架构设计文档审阅（事实核查结论）

- **"237 现有测试"不可验证**：实测 Core 目录 [Fact(]/[Theory(] 属性 ~1017 个（StateMachine=156, Simulation=75, Architecture=56）；文档零破坏声称虚假——TraversalAdvisorTests.cs:120-151 显式断言 3 个 advisor 方法抛 NotImplementedException，Phase 1 实现 HandleExceptionAsync 必破其中 1-2 个；ITraversalAdvisor 加 overload 破坏 3 个实现者编译（TraversalAdvisor/MockTraversalAdvisor/FsmSimulationHarness.NullAdvisor）。
- **ErrorPolicyType 无 Default 值**：实际 5 值 = Retry/Skip/Abort/Fallback/Backtrack（TraversalNode.cs:157-173）；"Default"是 null ErrorPolicy 路径（ErrorHandler.cs:102-110），非枚举值。PolicyChainFor 只映射 4/5（Fallback→null 走 ErrorType 默认链）。
- **SafeActionExecutor 在 UniClaw.Host**（SafetyGate.cs:281）：安全规则 deny 包装器（ISafetyEvaluator），失败 return false 非 DomainValidationException，无"操作目标 ∈ PageAnalysis.Items"校验——文档门 3/门 7"已实现"声称错误。
- **拦截层 override 不改 FSM.CurrentState**：StepOrchestrator Step 8-10 只覆盖 orchestrator 局部 nextState（StepAsync 内已 TransitionTo）；引擎终止检查（TraversalEngine:409 FrameCompleted&&depth≤1）依赖 StepResult 标志——Brain override 到 FrameComplete 需同时置 FrameCompleted，否则引擎无动作。矩阵门 1 不足以约束 Brain override。
- **HandleErrorTracedAsync 是 source generator 产物**（TraceHandlerGenerator），extraMetadata 仅 trace 用，ErrorStrategySelector 不消费——Gap #2 声称成立。

## 2026-08-05 — Battle #3 CPU-FSM 迁移差距（源码审计）

- **D-2 快照是死代码**：`CreateReadOnlySnapshot`/`TraversalContextSnapshot`（TraversalRuntimeContext.cs:337/19-67）零消费者；ITraversalAdvisor 刻意解耦 ITraversalContext（ITraversalAdvisor.cs:9），但 advisor 实际入参只有 primitive——快照从未接通。AI memory 视图缺失。
- **advisor 4 方法仅 1 个活**：DecideNextActionAsync 唯一调用点 = TraversalFSM.cs:552（ErrorHandling 内，confidence≥0.7 才入 extraMetadata，策略仍全确定性）；InferContainerTypeAsync/HandleExceptionAsync/ScreenSafetyAsync 全 NotImplementedException 且零调用点。IUniBrain.Text 亦零调用点。
- **C-6 已预期 AI consumer**：constitution C-6 "ReadOnlySetWrapper cast-back 阻断" 明写防止 "AI advisor 或外部 consumer 篡改引擎状态"——快照（D-2）是 constitution 预设的安全通道，只是未接线。
- **拦截层 = Brain hook 现成插槽**：Step 8-10 拦截（InterceptionHandler OnBranch/OnDynamicMatchNodeSelect/OnFrameComplete）NextState 覆盖 {Branch,NodeSelect,FrameComplete} 不经过矩阵——AI override 不需要改矩阵，但需 constitution 边界条款。

## 2026-08-05 — 指纹去重机制全景（源码审计，e2e-dedup-vision-quality 支持）

- **NodeId 内嵌原始 OCR 文本**：TemplateInstantiator.cs:58 + DynamicChildManager.cs:995 两处构造 `dyn_{template}_{itemText}_{parent}`——OCR 文本变体（"App security" vs "Appsecurity"）→ 新 NodeId → VisitedNodes / _generatedPairs / stale-click fuse 三处同时绕过。这是文本抖动穿透去重的单一根因点。
- **指纹三重实现**：PageSnapshotManager.Fingerprint（TraversalEngine.cs:1948，活跃主链）与 PageAnalysis.PageFingerprint（PageAnalysisRecords.cs:78，同算法重复，唯一消费链 NavigationContext.PageFingerprint 无生产消费者 = 死路径）；InterceptionHandler.ItemFingerprint（:751，count+首 item 名 hash，仅 ROI content guard）。
- **VisitedChildren/VisitedPages 生产侧零写入**：AddVisitedChild/MarkVisited 仅测试调用；TraversalFSM.HasUnvisitedStaticChildren（:500-506）读恒空集合 → STATIC 帧完成判定实际由拦截层 ContainerHandler 兜底，VisitedChildren 是死结构。
- **Invalidate 唯一触发点 = TryHandleScrollAsync**（InterceptionHandler:550/601/620）；StepOrchestrator:116-120 注释声称"fingerprint-based invalidation moved to TraversalEngine"与实际不符（RunAsync 无 Invalidate 调用）——注释过期。
- D-G12 childDestinations 是 RunAsync 局部变量（跨 run 隔离）+ `preFp != postFp` 守卫（TraversalEngine:385）——只记录真导航；目的地指纹 = 全量 item hash，副标题出现/消失仍会漏判同页。

## 2026-08-06 — Transition Gateway 方案审阅（源码裁决）

- **9 个 NextState 赋值点全部证实，但值域恒为 {NodeSelect}**：TryHandleScrollAsync 14 个返回路径（InterceptionHandler.cs:485-644）+ DecideFrameCompletionAsync:392 恒 NodeSelect，:253/:289/:336 显式 NodeSelect——拦截层从不产生矩阵外值。"绕过矩阵" = 无 CanTransitionTo 校验（StepOrchestrator.cs:83/:92/:101），非非法转移。Step 9 覆盖是恒等变换（初始构造即 NodeSelect）。
- **双状态写入第三个点（用户漏报）**：TraversalEngine.cs:372-373（ChildPushed && CanTransitionTo(NodeSelect) → TransitionTo）。ChildPushed=false 的覆盖路径（:116/:124/:134 滚动/帧完成、:253/:289 PressBack+Pop、:344 完成判定）→ FSM.CurrentState（Branch/FrameComplete）与 stepResult.NextState（NodeSelect）分裂一轮，下一轮从 FSM.CurrentState dispatch（StepOrchestrator.cs:44）。
- **Step 10 实际触发源收敛为 ErrorHandling→FrameComplete**（Abort/page-item/consecutive gate，帧须 DynamicMatch）：STATIC/NONE 的 Branch→FrameComplete 不满足 DynamicMatch 条件不触发。:336 override 语义 = "Abort 后 DynamicMatch 还有子节点则不要完成帧"。
- **state_version/observation_id 全仓零存在**（grep 排除 obj/bin）——方案 8 是全新机制，非现状缺陷。
- 引擎终止检查（TraversalEngine.cs:409）纯消费 FrameCompleted flag——因果上因拦截层把 nextState 恒覆盖为 NodeSelect，终止检查才必须依赖 flag；leaf-pop（:351）/OnError（:345）/TargetFound（:464）消费 NextState。

## 核心原则（跨任务有效）

- **源码是权威**：C# `TransitionMatrix` 字段是 ground truth。trace 反映"某次 run 发生了什么"，文档反映"设计意图"——两者都可能漂移。handler 决策逻辑以源码为准；测试是行为 oracle。
- **脚本只做提取器**：凡是能从源码确定的常量，不在脚本里维护第二份副本。`matrix_from_source.py` 从 C# 源码实时提取矩阵；`fsm_transition_path.py --validate` 自动调用之。
- **源码↔文档差异 = 最高价值 FSM 信号**：`matrix_from_source.py --diff-docs` 比对，CODE_ONLY / DOC_ONLY 分类，退出码 1 = 有差异。
- **C# 符号查询走 MCP**（cwm-roslyn-navigator / csharper-mcp），不用 grep。

## 2026-08-05 — fsm-matrix-hardening 完成（D-240~D-244，254 tests）

- 矩阵 22→19 边：移除 Execute→Branch、Branch→PreconditionCheck、FrameComplete→ErrorHandling 三死边。每条剩余边均有 handler 显式生产。StepAsync catch 改为 CanTransitionTo 守卫 + 降级链（NodeSelect→Branch / FrameComplete→NodeSelect / ErrorHandling→FrameComplete）。
- ConsecutiveErrors 递增收敛到 HandleErrorHandlingAsync 单点（D-242）。LastError 在 3 条返回路径清零（D-243）。PopupHandling 失败设 LastError，消息不含枚举名防 ErrorClassifier 碰撞（D-244）。
- 零现有测试破坏。254 tests pass（161 FSM + 93 simulation）。已 archive。
- 教训：双轨分析（静态审计 + 运行时诊断）是完整 FSM 诊断的最低标准；纯矩阵审计会漏掉 FrameComplete=0、拦截层接管、deny.default 循环等运行时问题。

## 2026-08-05 — FSM 审查关键发现（设计审阅）

- **LastError 消费者不止 HandleErrorHandlingAsync**：TraversalEngine:345（OnErrorAsync 钩子，入口步触发）与 PopupHandler:354/391（preserve/restore，restore 用 `new Exception(msg)` 重建丢失原始类型）——改 LastError 清/设必须排查这两处。
- **FrameComplete 不弹栈**：全仓 Pop 点仅 3 处——引擎 leaf-pop（:355）、stale-click 熔断（:427）、DynamicMatch 拦截（InterceptionHandler:240/269）。任何"降级到 FrameComplete = 弹出到父级"假设错误，需显式 Pop。
- **ErrorClassifier 是 substring 匹配**（ErrorHandler.cs:13-48，大小写不敏感）——错误消息内嵌 PopupType 枚举名（"Permission"→ErrorType.Permission）或 DismissStrategy（"WaitTimeout"→ErrorType.Timeout）会误分类。设计 popup 错误消息时必须避免。

## 2026-08-05 — E2E enumerate 诊断要点

- "重复进入子页"根因优先查分析层 item 语义（subtitle 是否独立成 navigate item），不要先怀疑 FSM dedup。OCR 文本变体（"App security" vs "Appsecurity"）可绕过 dedup+visited → 潜在重复点击。
- `settings_home_not_restored` 失败不一定是导航错误——本 run 引擎在 Settings 主页，level1MenuNames 全程空 → 身份链断裂。fingerprint 稳定性用 analysis.jsonl fingerprint 列逐时间窗比对。
- DynamicMatch 容器不进 FrameComplete 是可观测性缺陷非功能缺陷——拦截层 PressBack 正确导航。

## 2026-08-05 — D-G12：verification_passed 后的栈假设错误

- RunAsync:351-365 在 Execute→ResultVerify 时就 pop 叶子节点——verify 步栈顶已是父节点，leaf 场景 Peek(1)=null → null 键异常 → run 以 Error 终止。verification_passed 时物理页已变但裸 pop 不恢复物理页 → D-74 推子帧重进 → dedup 击穿。
- 重复进入的真正决策点在 GetNextUnvisitedChild/Generate + TryHandleNavigation，不在 verification_passed。unchanged 路径还伴随熔断 pop——必须用 decision trace 区分。

## 早期洞察（2026-08-05，已确认有效）

- **StepOrchestrator 拦截覆盖 FSM handler 输出**：诊断 FSM 行为必须检查 Step 8-10。Branch 拦截仅 fromState∈{Execute,ResultVerify,NodeSelect} 触发。`intercepted` flag 防止 default(InterceptionResult) 污染。
- **ResultVerify 的 consecutive-error reset 是 page-item gate 可达前提**：无此 reset 则 consecutive gate≥3 先触发 → page-item gate≥5 永远不可达。
- **文档漂移已修复**：fsm-design.md 矩阵表已与 19 边源码一致（`--diff-docs` exit 0）。

## 2026-08-06 — D-G11 maxDepth 滚动门语义混淆（源码裁决，用户挑战后修正）

- **EffectiveMaxDepth 是栈深边界不是行为边界**：唯一硬执行点 = NodeStack.Push 拒绝（NodeStack.cs:37-38）；拦截层 D-G7（InterceptionHandler.cs:421-426 挡子帧 push）与之同义。D-G11（:487-490 挡滚动）把"不能下钻"过度扩展到"不能观察"。
- **枚举的记账在观察层不在遍历层**：entry.observed 由 Generate 每 item 发射（TraversalEngine.cs:1004-1012，GetNextUnvisitedChild 惰性触发 :816-819），analysis.jsonl 每 AnalyzeCurrentPageAsync 全量写盘（AnalysisWritingDecorator.cs:52-62），EnumerateCompletionAnalyzer（observed/visited/skipped span 计数 + entry.generate 零 observed = end-of-list）——三者都吃"滚动后新 item"，与 P3 push 拒绝无关。P3（child_depth_limit_skipped）只负责不 push，从不负责不记录。
- 结论：depth==maxDepth 时滚动是 record-only 模式，天然安全（子节点仍被 P3 吸收）；旧结论"滚动无意义"只对遍历成立。改动点 = 删 D-G11 门（Depth > EffectiveMaxDepth 恒不可达，等价删除）。"discoveryEntries" 在代码库中不存在，实际映射 = entry.observed + analysis.jsonl Items。
