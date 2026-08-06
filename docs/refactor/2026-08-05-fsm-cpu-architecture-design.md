# TraversalFSM → CPU 架构演进设计

> 状态: 设计阶段 (Battle #3-#7 对抗审阅修正)
> 来源: shadow-fsm-analyzer × fsm-analyzer 双轨对战 + brainstorming 对质 + Battle #6/#7 对抗审阅
> 日期: 2026-08-06 (v1.1 — Battle #7 Transition Gateway 裁决综合)

---

## 0. 动机

整个 UniClaw.Core 应像一个 **PC**：FSM 作为执行单元，Memory 提供共享状态视图，AI 作为决策大脑监控并介入，Vision 提供外部感知输入。

```
FSM    = 执行单元（管理控制流，不做高层决策）
Plan   = 可执行程序（遍历计划 = 指令序列 + 子程序 + 异常处理块）
AI     = 大脑（一等公民，可在异常时 / 计划受阻时介入，读 Memory 做推理）
Vision = 外部输入（写入 Memory 帧缓冲区）
Memory = 共享工作内存（分区管理，权限约束）
```

**注意**: CPU/Memory/Brain 是描述性隐喻，不是新抽象设计。以下设计优先解决问题而非满足隐喻——ISA 表是词汇表，Memory 分区是现有字段归类的文档化，不产生新代码。

### 当前架构距离

**结论：结构差距小、接线差距大。**

| 维度 | 需求 | 当前状态 | 差距 |
|------|------|---------|------|
| Memory 模型 | 共享内存 + 快照隔离 | **已有** — ~33 字段 Container 模式 + ITraversalContext 只读 + D-2 快照 | 快照零消费者 — AI 无 Memory 视图 |
| AI 集成 | 感知 + 决策均一等公民 | **感知已一等** (PageAnalyzer) / **决策二等** (1/4 advisor 方法活着) | 3 个 advisor 方法 NotImplemented |
| Plan 动态性 | AI 可运行时修改 | **静态** immutable record | 无 Plan 修订 API |
| FSM 决策点 | 可插拔 hook | **已有** — StepOrchestrator Step 8-10 拦截 NextState 不经过矩阵 | 拦截是确定性逻辑，需安全门 |
| Constitution | AI 访问规则明确 | C-6 防篡改 / C-4 限定协调通道 | 需补 AI 决策门限 + 访问边界条款 |

**关键发现** (Battle #3-#6 双方独立确认):

1. 矩阵 19 边 = 合法转移表，降级链 (CanTransitionTo 守卫) = 独立异常路由——refactor 设计 §1.2 "矩阵只管 Handler 门、异常路由走独立降级通道" 是文档级背书。
2. `TraversalContextSnapshot` (D-2) **已完整实现但零消费者**——AI 读取 Memory 的唯一通道已建好，只是没接线。
3. StepOrchestrator Step 8-10 拦截的 `InterceptionResult.NextState` **不经过矩阵校验**——这恰好是 Brain hook 的"自由区"，不需要改 19 边矩阵即可接入 AI 决策。**但需注意**: 拦截层 override 不更新 FSM.CurrentState (在 StepAsync 内已 `TransitionTo`)，引擎终止/弹栈逻辑依赖 `StepResult` 标志位 (FrameCompleted/ErrorOccurred/StepExecuted)——Brain override 必须设置对应标志位，不是只改 nextState。
4. `ITraversalAdvisor` 4 个方法中仅 `DecideNextActionAsync` 活着 (ErrorHandling 内 confidence≥0.7 咨询，但**全测试套件无 advisor 决策被采纳的用例**——NullAdvisor Confidence=0.0 门限下所有测试走确定性路径)，其余 3 个全是 NotImplementedException。
5. **Battle #7 (Transition Gateway 审阅) 修正**: 拦截层 9 个赋值点 (InterceptionHandler.cs:116/:124/:134/:216/:253/:289/:298/:336/:344) 产生的值域**恒为 NodeSelect**，全部 ∈ 矩阵合法边——若严格执行 CanTransitionTo，9/9 通过，零拒绝。因此"绕过矩阵"的风险定性应精确化为: (a) **矩阵对拦截层零拦截力** (值域天然合法，校验空转)； (b) **双真相源分裂** — FSM.CurrentState 由 handler 返回值 (TraversalFSM.cs:145) 或引擎二次写入 (TraversalEngine.cs:372-373，ChildPushed 时) 设定，stepResult.NextState 由拦截层恒覆盖；覆盖路径 ChildPushed=false 时两者分裂一轮 (自愈，但 trace `ToState` 失真)； (c) **真正的旁路是副作用** — 拦截层直接执行 PressBack/Swipe/Stack.Pop/Push/Invalidate 修改物理状态，矩阵与状态校验均不可见。治理 = Phase 0d/0e + 安全门，**不引入 Gateway 抽象** (见 §8.1)。

---

## 1. 现有架构词汇表 (Glossary)

> 以下将当前 8 个 handler 映射为描述性术语。不引入新代码——矩阵 19 边本身就是转移合法性表。

| 当前 Handler | 描述 | 确定性/AI? | 备注 |
|-------------|------|-----------|------|
| HandleNodeSelectAsync | 取下一节点 | 确定性 | 栈空→Branch, 有栈→PreconditionCheck |
| HandlePreconditionCheckAsync | 前置检查 | 确定性 (已可注入 IPreconditionChecker) | assume pass→Execute, checker false→ErrorHandling |
| HandleExecuteAsync | 执行操作 | 确定性 | Click/Swipe/Back/InputText/NoAction via OperationDispatcher |
| HandleResultVerifyAsync | 验证操作效果 (2 轮微循环) | 确定性 | 首次+单次 retry; popup→PopupHandling; unchanged→Branch; stale-click 熔断 |
| HandleBranchAsync | 分支决策 | **候选 AI 介入** | D-20 6 场景; Static/DynamicMatch/None |
| HandleFrameCompleteAsync | 帧完成 | 确定性 | 恒定 return NodeSelect; 弹栈在拦截层 |
| HandleErrorHandlingAsync | 错误恢复 | **已部分接入 advisor** (metadata only) | classify→select→execute; 5 strategy→FSM; 双门限 |
| HandlePopupHandlingAsync | 弹窗处理 | 确定性 | 6-step pipeline; Success→ResultVerify; Failure→ErrorHandling |

**矩阵 = 转移合法性表 (19 边)**。`CanTransitionTo(dst)` = 查合法性。降级链 (ErrorHandling 抛异常→FrameComplete etc.) = 独立异常路由，不依赖转移表——实现了 refactor §1.2 的 "矩阵只管 Handler 门，异常路由走独立通道"。

---

## 2. Memory 模型 — FSM 的认知辅助

> Memory 不是现有字段的归类文档。Memory 是 **FSM 查询"世界是什么样的"以做出更好决策** 的认知层。
> 每个 Memory 维度回答一个 FSM 面临的实际问题。维度按"当前缺失度"排序——缺失越多, Memory 帮助越大。

### 2.1 FSM 因"没记忆"而犯错的 8 个场景

| # | FSM 面临的问题 | 当前怎么做 | 为什么犯错 | Memory 应该帮什么 |
|---|-------------|----------|----------|-----------------|
| 1 | "我是不是又回到了同一页?" | 比对新旧 PageFingerprint | OCR 文本抖动→指纹不同→把同一页当新页面 (R1 根因) | **页面身份记忆**: 不只记指纹, 记文本集合+元素数量+布局特征, 新指纹来时模糊匹配 |
| 2 | "这个容器遍历了多少?" | VisitedNodes (nodeId 维度) | 文本变体→新 nodeId→"未访问" (穿透) | **探索进度记忆**: 容器级进度 (已访问/熔断/剩余), 不受 nodeId 文本影响 |
| 3 | "连续失败是因为同一个原因吗?" | ConsecutiveErrors 只计数 | 3 次不同类型的错误和 3 次同一类型的错误, FSM 反应相同 (PressBack) | **异常模式记忆**: 按 (ErrorType, PageFingerprint) 分组, 同模式累积→升级策略 |
| 4 | "出错时我在做什么?" | LastError.Message | 只有异常文本, 不知道在哪个节点/页面/深度 | **异常现场记忆**: PC+SP+页面指纹快照, 恢复后验证是否回到合法状态 |
| 5 | "上次遇到类似情况, 什么策略有效?" | 无 | 每次都从 Retry 开始尝试, 即使已知 Retry 无效 | **决策历史记忆**: 最近 N 个决策+结果, 环形缓冲, 用于跳过已知无效策略 |
| 6 | "页面变化符合预期吗?" | HasChanged(bool) | 只知"变了没", 不知"变对了没"。弹窗=变了, 崩溃白屏=也变了, 都是 `true` | **页面期望记忆**: 记录"操作前页面+预期变化类型", VERIFY 判断"是预期的变化吗?" |
| 7 | "还要等多久?" | 固定 WaitAfterActionMs + 通用退避 min(2^n,10s) | 同一操作每次等待相同, 不管历史上实际需要多久 | **性能记忆**: per-action 耗时统计, 动态调整 wait/backoff |
| 8 | "我是怎么到这里的?" | CurrentPath (nodeId 列表) | 只知道节点 ID 序列, 不知道经过了哪些物理页面/是否在循环 | **导航路径记忆**: (pageFingerprint, nodeId, action) 三元组序列, 检测循环和重复访问 |

### 2.2 页面身份记忆 (Page Identity) — 解决场景 #1, #6, #8

**FSM 需要回答**: "这个页面我见过吗? 如果见过, 是哪一次?"

当前 PageFingerprint = (Type,Name) 全量 item 排序哈希。任何 item 文本抖动→指纹变化→"新页面"。

**Memory 帮助**:
```
PageIdentityMemory {
    // 已知页面目录
    knownPages: Map<StablePageId, PageRecord>
    
    PageRecord {
        fingerprints: List<int>,           // 该页面的所有历史指纹 (OCR 抖动产生多个)
        itemCount: Range,                  // 元素数量范围 (排除动态 item)
        navigableTexts: Set<string>,       // 可导航元素的文本集合 (归一化后)
        layoutSignature: int,              // 布局特征哈希 (元素 Y 坐标序列, 不含文本)
        firstSeen: StepNumber,
        lastSeen: StepNumber,
    }
}

// FSM 查询: "这个指纹我见过吗?"
PageIdentityMatch? Lookup(int fingerprint, PageAnalysis page) {
    // 1. 精确指纹匹配 → MATCH (已有 D-G12)
    // 2. 模糊匹配: 归一化文本集合 overlap > 80% → LIKELY_MATCH (新增)
    // 3. 布局匹配: 元素 Y 坐标序列相似 → POSSIBLE_MATCH (新增, 纯图标页面兜底)
    // 4. 都不匹配 → NEW_PAGE
}
```

**当前缺失**: D-G12 的 `_childDestinations` 做了精确指纹去重, 但没有模糊匹配层——指纹抖动时漏判。V4 文本归一化只在 Vision 侧, 引擎不消费。

**Phase 实现**:
- Phase 1 (P0): 文本归一化 `NormalizeTextForIdentity` 移入 Core, 应用于 PageFingerprint 输入 (字符级稳定, ~3 行)
- Phase 1 (P0): 类型白名单过滤——只有可导航类型参与指纹和文本集合 (~5 行)
- Phase 2: `PageIdentityMemory` 模糊匹配层 (~60 行)

**对 FSM 的帮助**:
- HandleBranchAsync (JUMP): 子节点选择时, 若目标页面的身份已被 Memory 识别为 "已访问", 可跳过 (减少 D-G12 的 1 次浪费)
- HandleResultVerifyAsync (VERIFY): 用页面身份而非指纹判断 HasChanged——"页面身份变了" 比 "指纹变了" 更可靠
- R1 进展门: "无新 VisitedNode" 判据不受指纹抖动影响——Memory 告诉它 "这 5 步其实都在同一页"

### 2.3 探索进度记忆 (Exploration Progress) — 解决场景 #2

**FSM 需要回答**: "这个容器的子节点, 探索了多少? 还能继续吗?"

当前 VisitedChildren 是死结构 (零生产写入)。VisitedNodes 用 nodeId 去重——文本变体穿透。

**Memory 帮助**:
```
ExplorationProgress {
    // 每容器进度
    containerProgress: Map<NodeId, ContainerProgress>
    
    ContainerProgress {
        totalGenerated: int,        // 总共生成了多少子节点 (含已访问+已熔断)
        visitedCount: int,          // 已成功访问
        skippedCount: int,          // 已熔断/跳过
        remaining: bool,            // 是否还有未探索的
        stuckCount: int,            // 连续无新 visited 次数 (进展门输入)
        lastNewVisitedStep: int,    // 上次有新访问的步号
    }
}
```

**当前缺失**: `VisitedNodes` 是 nodeId 去重, 但容器级聚合不存在。HasUnvisitedStaticChildren 恒真 (读空集)。

**Phase 实现**:
- Phase 1 (P0): `ContainerProgress` 数据结构 + 每步更新 (~20 行)
- Phase 1 (P0): R1 进展门消费 `stuckCount`——比 "无 FrameComplete" 精确, 且覆盖错误燃烧循环

**对 FSM 的帮助**:
- HandleBranchAsync (JUMP): 决策 "全部子节点已探索" → FrameComplete, 不需要等 VisitedChildren 死结构
- R1 进展门: 直接消费 `stuckCount`, 不依赖指纹

### 2.4 异常模式记忆 (Error Pattern) — 解决场景 #3

**FSM 需要回答**: "这 3 次异常是同一个原因吗? 该 PressBack 还是换个方法?"

当前 ConsecutiveErrors 只计数, 不分组。NodeFailedItems 按 nodeId 去重——不同节点的同类型异常被视为独立事件。

**Memory 帮助**:
```
ErrorPatternMemory {
    patterns: Map<ErrorFingerprint, ErrorPattern>
    
    ErrorFingerprint = (ErrorType, PageFingerprint, ActionType)
    
    ErrorPattern {
        count: int,                 // 同模式出现次数
        nodes: Set<NodeId>,         // 涉及的节点
        firstSeen: StepNumber,
        strategies: List<Strategy>, // 尝试过的恢复策略 + 结果
        escalated: bool,            // 是否已升级
    }
}
```

**FSM 查询示例**:
```
// HandleErrorHandlingAsync 中:
var fp = (errorType, ctx.PageFingerprint, actionType);
var pattern = memory.errorPatterns[fp];

if (pattern.count >= 3 && !pattern.escalated) {
    // 同模式 ≥3 次 → 不是个别节点的问题, 是页面/操作类型的问题
    // 升级: Skip 当前页所有同类型节点, 而非 PressBack 单个节点
    trace.RecordDecision("error_pattern_escalated", ...);
    pattern.escalated = true;
    return FrameComplete;  // 放弃当前帧
}
```

**当前缺失**: 完全不存在。Gap #6 的 "同 page 同类型 ≥3 → trace 观测" 是简化版——只观测不决策。

**Phase 实现**:
- Phase 1.5 (P2): 异常模式记录 + 观测 (只 record, 不自动决策)
- Phase 2: 自动升级决策

**对 FSM 的帮助**:
- HandleErrorHandlingAsync (TRAP): 3 次同一类型异常 → 跳过整页, 而非逐节点 PressBack。节省 2-3 步/节点

### 2.5 异常现场记忆 (Fault Context) — 解决场景 #4

**FSM 需要回答**: "出错时我在哪? 恢复后我回到了正确的地方吗?"

当前 SetLastError(ex) 只存异常对象。`_exceptionChain` 字段零调用者。

**Memory 帮助**: ErrorContext +4 字段 (如 §5.3 设计):
- FailingNodeId (PC — 哪个节点触发)
- FailingStackDepth (SP — 异常时栈深)
- FailingPageFingerprint (异常时页面)
- NestingLevel (嵌套异常计数, 替代死字段 `_exceptionChain`)

**对 FSM 的帮助**:
- HandleErrorHandlingAsync 的 Step 4 verify: Backtrack 后验页面变了 / Retry 后验栈深不变 / Skip 后验节点换了
- Brain 决策: 完整的异常时刻上下文——不只是 "TimeoutException", 而是 "在节点 X (深度 3) 点击 Storage 按钮时 Timeout, 页面指纹 = -338237621"

### 2.6 决策历史记忆 (Decision History) — 解决场景 #5

**FSM 需要回答**: "上次遇到类似情况, 什么策略有效?"

当前完全不存在。

**Memory 帮助**:
```
DecisionHistory {
    recentDecisions: CircularBuffer<DecisionRecord>(capacity: 20)
    
    DecisionRecord {
        step: int,
        fromState: TraversalState,
        decision: string,          // "Branch→NodeSelect", "Error→Retry→Execute", "StaleClickFuse"
        context: { nodeId, pageFingerprint, depth },
        result: DecisionResult,    // Success / Failed / Retried / Skipped
        subsequentSteps: int,      // 该决策后正常执行了多少步才遇到下一个异常
    }
}
```

**FSM 查询示例**:
```
// ErrorStrategySelector 中:
var similarDecisions = memory.decisionHistory
    .Where(d => d.fromState == ErrorHandling 
             && d.context.pageFingerprint == currentFp
             && d.result == Failed)
    .TakeLast(3);

if (similarDecisions.All(d => d.decision == "Retry→Execute")) {
    // 这个页面上的 Retry 从未成功 → 直接跳过 Retry, 选下一个策略
    return SelectNextStrategy(after: Retry);
}
```

**当前缺失**: 完全不存在。

**Phase 实现**: Phase 2 (~40 行)

**对 FSM 的帮助**:
- ErrorStrategySelector: 跳过已知无效策略。在 "10 个 menu item 全部 UiElement 异常" 场景中, 第 3 个就 Skip 而非每个都 Retry→Backtrack→...

### 2.7 页面期望记忆 (Page Expectation) — 解决场景 #6

**FSM 需要回答**: "点击后页面变了——但这是预期的变化吗?"

当前 VERIFY 只返回 HasChanged(bool)。弹窗 = true, 进入子页 = true, 崩溃白屏 = true, 都一样。

**Memory 帮助**:
```
PageExpectation {
    beforeAction: {
        pageFingerprint: int,
        navigableItemCount: int,
        isPopup: bool,
    },
    expectedChange: ExpectedChange,  // EnterSubPage / DismissPopup / ScrollList / NoChange / Any
    afterAction: {
        pageFingerprint: int,
        navigableItemCount: int,
        isPopup: bool,
    },
    actualResult: ExpectationResult,  // Matched / Mismatched / Uncertain
}
```

**FSM 使用**:
```
// HandleResultVerifyAsync 中:
var expectation = new PageExpectation {
    beforeAction = { fp: preFp, itemCount: preActionItemCount },
    expectedChange = node.Action.ExpectedChange,  // 从 Plan 数据读取
};

// 分析后:
expectation.afterAction = { fp: postFp, itemCount: postActionItemCount };
expectation.actualResult = Evaluate(expectation);

switch (expectation.actualResult) {
    Matched → verification_passed (高置信度)  // 页面变化符合预期
    Mismatched → verification_anomaly         // 页面变了但不符合预期 → 可能导航到了错误页面
    Uncertain → verification_passed (低置信度) // 不知道预期, 按当前逻辑走
}
```

**当前缺失**: 完全不存在。D-G12 preFp/postFp 是最简版 "期望"——只判断 "变了没"。

**Phase 实现**: Phase 2 (~50 行, 可选)

**对 FSM 的帮助**:
- HandleResultVerifyAsync: "点击 Storage 后进入 Storage 子页" vs "点击 Storage 后弹出了权限对话框" → 两者 HasChanged 都是 true, 但前者是预期, 后者需要弹窗处理
- D-G12: 配合页面身份记忆, 识别 "又回到了已访问页面" → 是 Back 的预期行为 (NavigatingBack) 还是意外的重复 (DuplicateDestination)

### 2.8 对 FSM 各 Handler 的帮助汇总

| Handler | 当前决策依据 | Memory 增强后 |
|---------|------------|-------------|
| **NodeSelect** | NodeStack.IsEmpty | (不变, 确定性) |
| **PreconditionCheck** | IPreconditionChecker? | (不变, 已可注入) |
| **Execute** | node.Action | 性能记忆→动态 wait; 异常现场记忆→操作前保存上下文 |
| **ResultVerify** | HasChanged(bool) | 页面身份记忆→"身份变了吗"替代"指纹变了吗"; 页面期望记忆→"变对了吗" |
| **Branch** | D-20 6 场景 | 探索进度记忆→"容器遍历完了吗"; 页面身份记忆→"这个目标页去过吗" |
| **FrameComplete** | 恒定 return NodeSelect | (不变) |
| **ErrorHandling** | classify→select→execute | 异常模式记忆→"同类型异常≥3→跳过整页"; 决策历史→"跳过已知无效策略"; 异常现场记忆→恢复验证 |
| **PopupHandling** | 6-step pipeline | 异常现场记忆→弹窗处理前保存; 异常模式记忆→"这个弹窗见过吗" |

### 2.9 实现优先级

Memory 维度按"对 FSM 帮助大小 × 实现成本"排序:

| 优先级 | 维度 | 解决的问题 | 改动量 | FSM 收益 |
|--------|------|----------|--------|---------|
| **P0** | 异常现场记忆 | Gap #1 (TrapFrame), Gap #5 (恢复验证) | ~20 行 | ErrorHandling 知道"从哪出错、恢复到哪" |
| **P0** | 探索进度记忆 | R1 (进展门), VisitedChildren 死结构 | ~20 行 | Branch 知道"容器遍历完了"; 进展门有输入 |
| **P1** | 页面身份记忆 (归一化+白名单) | R1 指纹抖动, D-G12 漏判 | ~15 行 | ResultVerify/Branch/D-G12 不受 OCR 抖动影响 |
| **P1** | 异常模式记忆 (观测) | Gap #6 (同模式重复) | ~25 行 | ErrorHandling 知道"同类异常≥3 次" |
| **P2** | 决策历史记忆 | ErrorStrategySelector 盲试 | ~40 行 | ErrorHandling 跳过已知无效策略 |
| **P2** | 页面身份记忆 (模糊匹配) | 指纹抖动 (字符级归一化覆盖不到的) | ~60 行 | 页面身份稳定 |
| **P2** | 性能记忆 | wait/backoff 动态优化 | ~30 行 | Execute 自适应等待 |
| **P2** | 导航路径记忆 | 循环检测, 重复访问 | ~30 行 | Branch 决策依据 |
| **P3** | 页面期望记忆 | HasChanged 增强 | ~50 行 | ResultVerify 精准判断 |
    string? currentNodeId = null,
    CancellationToken ct = default);
```

注意: 这与 C-6 修订 "AI 读 Memory 唯一通道=快照" 存在张力——当前 Phase 1.1 仍需 pageAnalysis 参数，快照自身不含帧缓冲。在快照扩展覆盖 framebuffer 前 (Phase 2)，双参数是过渡方案。

---

## 3. AI Brain 集成

### 3.1 当前 AI 状态

| 组件 | 状态 | 活跃度 |
|------|------|--------|
| `PageAnalyzer.AnalyzeCurrentPageAsync` | ✅ 每步调用 | **一等** — 指令级感知 |
| `Advisor.DecideNextActionAsync` | ✅ ErrorHandling 内 confidence≥0.7 咨询 | **二等** — 仅元数据, 全测试套件无采纳用例 |
| `Advisor.InferContainerTypeAsync` | ❌ NotImplementedException | 死代码 |
| `Advisor.HandleExceptionAsync` | ❌ NotImplementedException | 死代码 (契约未定义) |
| `Advisor.ScreenSafetyAsync` | ❌ NotImplementedException | 死代码 |
| `Text` (ITextUnderstanding) | ❌ 零调用 | 死代码 |
| `TraversalContextSnapshot` (D-2) | ❌ 零消费者 | 死代码 |

### 3.2 Brain 介入点

```
  ┌──────────────────────────────────────────────┐
  │           Instruction Cycle                  │
  │                                              │
  │  [1] FETCH → GUARD → EXEC → VERIFY           │
  │         ↓                                    │
  │  [2] JUMP ─→ NodeSelect (next)               │
  │       └──→ FrameComplete → RET               │
  │         ↓                                    │
  │  [3] COMMIT (memory.state/history)           │
  │                                              │
  │  Exception path:                             │
  │  [EX] TRAP → HandleErrorHandlingAsync        │
  │        │   advisor consulted (confidence≥0.7) │
  │        │   → metadata only (当前)             │
  │        │   → direct decision (Phase 1.5 P4)   │
  │        │                                     │
  │  Popup path:                                 │
  │  [POPUP] → HandlePopupHandlingAsync           │
  │                                            │
  │  Stuck path:                                 │
  │  [STALL] ProgressGate (Phase 1 P0, 新增)     │
  │          K 步无新 VisitedNode → 强制熔断      │
  └──────────────────────────────────────────────┘
```

**接入现状**:
- ErrorHandling 内 advisor 已接入但只作元数据 (confidence≥0.7 → extraMeta, 不流入 ErrorStrategySelector 决策)
- Step 8-10 拦截层: 现成插槽，NextState 覆盖不经过矩阵，但需注意双真相源 (见 §0 #5): FSM.CurrentState 写入点共 3 个 — handler (TraversalFSM.cs:145)、引擎 (TraversalEngine.cs:372-373, ChildPushed)、GlobalFSM.ForceState (恢复通道)；Brain override 必须设置对应标志位，不是只改 nextState
- Stall detector: 不存在 (R1: 120 步 FrameComplete=0)

### 3.3 安全门 (Brain 决策防护)

Brain 决策必须经过这些门才能执行。12 条:

| # | 门 | 检查内容 | 违反时 | 状态 |
|---|-----|---------|--------|------|
| 1 | **转移合法性** | Brain 建议的 nextState ∈ CanTransitionTo(fromState)? 仅适用 handler 内采纳路径 | 拒绝 → fallback | 已有 (:548-573 部分) |
| 2 | **置信度门限** | Brain 决策 confidence ≥ 0.7? | 降至确定性策略 | 已有 (:558) |
| 3 | **操作合法性** | Brain 建议的 action 参数合法? (Core 侧操作目标校验需新增; Host 侧 SafetyGate deny 包装已有) | DomainValidationException | 部分 (Host 已有, Core 待补) |
| 4 | **页面一致性** | Brain 建议时 framebuffer fingerprint 与决策时刻一致? | 重新分析 | **新增** |
| 5 | **速率限制** | Brain 连续介入次数 ≤ N? (N=3) | 强制确定性路径 | **新增** |
| 6 | **可用性降级** | Advisor 调用失败 → `advisor_unavailable` trace | fallback 确定性 | 已有 (:569-571) |
| 7 | **操作原子性** | AI 建议的操作目标 ∈ CurrentPageAnalysis.Items? | DomainValidationException | **新增** (Core 侧) |
| 8 | **时间预算** | Brain 单步耗时 > P95? 总耗时占比 > 预算? | 强制确定性 + trace | **新增** |
| 9 | **进展门** | K 步内无新 VisitedNode? (K=10) | 强制 FrameComplete | **新增** (Phase 1 P0) |
| 10 | **决策去重** | 同帧同建议 ≥3 次? | 拒绝并降级 | **新增** |
| 11 | **栈一致性** | Brain 决策引用的帧在 commit 时仍为当前帧? (消费 ErrorContext.FailingStackDepth) | 重新决策 | **新增** |
| 12 | **C-4 提交** | Brain 提案唯一生效路径 = CPU 执行 TransitionTo | 拒绝直接写 | 结构性 (权限矩阵隐含, 门表显式化) |

### 3.4 Brain 升级阶梯

| 级别 | Brain 角色 | 当前? |
|------|-----------|-------|
| L0: 确定性 | Brain 不参与 | ✅ 当前默认 |
| L1: 辅助 | Brain 建议 → confidence≥0.7 → extraMetadata → 确定性策略消费 | ✅ 当前 advisor |
| L2: 决策 | Brain 建议 → 安全门全通过 → 直接采纳为 nextState | 目标 (Phase 1.5 P4) |

L3 (权威: Brain 覆盖 FSM handler 输出) — 冻结。无当前场景锚定。

---

## 4. Plan 可执行程序

### 4.1 当前 Plan 模型

`TraversalPlan` immutable record, 12 字段。`TraversalNode.ChildrenStrategy` 三型 (Static/DynamicMatch/None)。动态性来自确定性运行时机制 (DynamicChildManager + InterceptionHandler sub-frame 合成)。

### 4.2 扩展策略 (替代 DSL)

> **DSL 冻结。** 引入 TraversalProgram DSL 需 5000+ 行 (语法/解析器/调试器)，收益不明确。
> 改为扩展现有 `ChildrenStrategy` 和 `ErrorPolicy`:
> - `ChildrenStrategy.AIDriven` — Brain 决定子节点遍历顺序 (远期)
> - `ErrorPolicyType.ConsultBrain` — 该节点异常时直接咨询 Brain (Phase 1.5 P4)

### 4.3 PlanPatch 补丁通道 (Phase 2, Skip-only)

Brain 通过 `PlanPatch` 跳过卡死的子节点 (不改原始 Plan):

```csharp
public sealed record class PlanPatch {
    public string TargetNodeId { get; init; }
    public PatchOperation Operation { get; init; }  // 仅 Skip (Phase 2)
    // 冻结: InsertBefore, Replace, Append, Reroute — 零锚定场景
    public string Reason { get; init; }
    public double Confidence { get; init; }
}
```

**Skip 的场景锚定**: R1 卡死子节点 / Gap #6 同页同类型异常≥3 / Gap #4 弹窗后跳过当前项。

**安全约束**: 只修改当前节点及之后; 过安全门; audit journal; 原始 Plan 不可变 (补丁是 overlay)。

---

## 5. 异常流与异常恢复 (Bug 修复 + 增强)

### 5.1 当前异常流

```
Handler 抛异常
  → StepAsync catch (line 125): SetLastError(ex)
    CanTransitionTo(ErrorHandling) ? ErrorHandling : degradation_chain
      │
      ▼
HandleErrorHandlingAsync (line 514-639):
  [1] ErrorClassificationContext + StrategySelectionContext
  [2] Advisor.DecideNextActionAsync() → confidence≥0.7 → extraMeta (METADATA ONLY)
  [3] ErrorHandler.HandleErrorTracedAsync → classify→select→execute
  [4] Map strategy→FSM: Retry→Execute, Backtrack→NodeSelect, Skip→Branch, Continue→NodeSelect, Abort→FrameComplete
  [5] IncrementConsecutiveErrors() (所有策略统一 +1)
  [6] IncrementNodeFailedItems()
  [7] Gate 1: NodeFailedItems≥5 + depth>1 → PressBack→FrameComplete
  [8] Gate 2: ConsecutiveErrors≥3 + depth>1 → PressBack→FrameComplete
  [9] SetLastError(null) (3 返回路径: 主/ gate1/ gate2)
```

### 5.2 10 个缺口 (源码锚定, 全部 Battle #4 确认)

| # | 缺口 | 根因 |
|---|------|------|
| 1 | 无异常上下文保存 | 只保存 LastError, 缺 PC/栈深/页面指纹 |
| 2 | Advisor 不决策 | extraMeta 不流入 ErrorStrategySelector |
| 3 | 策略链静态 | 6 ErrorType × 固定链 |
| 4 | 弹窗-错误矩阵缺边 | 无 ErrorHandling→PopupHandling (双门限 PressBack 后弹窗漏检) |
| 5 | 无恢复后验证 | Retry/Backtrack/Skip 后不验证是否回到合法状态 |
| 6 | 同模式异常无去重 | NodeFailedItems 按 nodeId 去重, 不同节点同类型 = 独立事件 |
| 7 | 无跨步骤学习 | 每次异常处理独立 |
| 8 | ~~Backtrack 语义~~ | **撤回** — spec 已锁定 "SHALL NOT reset" (D-242), 设计取舍非 bug (TraversalFSM.cs:593-596) |
| 9 | Double fault 未显式化 | ErrorHandler pipeline 异常不经过降级链 |
| 10 | PopupHandler 丢失异常类型 | RestoreState `new Exception(msg)` 重建 |

**R1 进展门 (不在 10 个 Gap 中，但优先级最高)**: 120 步 FrameComplete=0 — 不是异常流 (无异常抛出)，而是无进展但不停步。错误循环 (ErrorHandling→FrameComplete→...→Execute→异常→...) 每圈都有 FrameComplete，现有设计中的 "无 FrameComplete" 检测器不会触发。**需要 "无新 VisitedNode 达 K 次" 判据**。

### 5.3 异常上下文 (ErrorContext +4 字段)

```csharp
// 加到 ErrorContext (复用现有结构)
public sealed class ErrorContext {
    // ... 现有 5 字段 ...

    // 新增 4 字段: 异常时刻最小快照
    public string? FailingNodeId { get; private set; }
    public int FailingStackDepth { get; private set; }
    public int FailingPageFingerprint { get; private set; }
    public int NestingLevel { get; private set; }

    public void CaptureFaultContext(TraversalRuntimeContext ctx) {
        FailingNodeId = ctx.CurrentFrame?.NodeId;
        FailingStackDepth = ctx.NodeStack.Depth;
        FailingPageFingerprint = ctx.PageFingerprint;
        NestingLevel++;
    }
    public void ClearFaultContext() { NestingLevel = 0; }
}
```

### 5.4 三级异常处理 (ErrorHandler + 两个扩展点)

```
异常入口 (SetLastError + CaptureFaultContext)

  Step 1: classify (ErrorClassifier, 不变)
  Step 2: select  ← Brain 决策插槽
      ├── advisor.Confidence ≥ 0.7 且 NestingLevel ≤ 1:
      │       直接采纳 advisor strategy → 跳过 ErrorStrategySelector
      │       安全门: CanTransitionTo(advisor_strategy)
      └── 否则: ErrorStrategySelector 确定性链 (不变)

  Step 3: execute (RecoveryExecutor, 不变)

  Step 4: verify  ← 新增
      ├── Backtrack 后: 验证 PageFingerprint ≠ FailingPageFingerprint
      ├── Retry 后: 验证 StackDepth 不变
      ├── Skip 后: 验证当前节点 ≠ FailingNodeId
      └── 验证失败 → 重新 classify verify 失败 → 最多升级 1 次

异常出口 (SetLastError(null) + ClearFaultContext)
```

### 5.5 弹窗优先检测 (拦截层 Post-PressBack)

```csharp
// StepOrchestrator: PressBack 后检查是否有弹窗
// 覆盖双门限 (ErrorHandling→FrameComplete) 和 Backtrack (→NodeSelect) 两条路径
if (justPressedBack) {
    var pageAnalysis = await Brain.PageAnalyzer.AnalyzeCurrentPageAsync();
    if (pageAnalysis.IsPopup) {
        nextState = TraversalState.PopupHandling;
    }
}
```

### 5.6 R1 进展门 (Phase 1 P0, 最高优先级)

```csharp
// 拦截层, 每步执行后:
if (stepsSinceLastNewVisitedNode >= K) {  // K=10
    RecordDecision("no_progress_gate_triggered");
    ForceFrameComplete();  // 设置 StepResult.FrameCompleted + nextState=FrameComplete
}
```

**覆盖**: R1 (DynamicMatch 循环) + ISSUE C (错误燃烧循环) — 两个问题共用一个判据: "无新 VisitedNode" 而非 "无 FrameComplete"。

---

## 6. 实施路径 (修正后)

### Phase 0: R1 进展门 + 状态写入治理 + 安全门基础 (~110 行, 不改矩阵)

| # | 改动 | Memory 维度 | 文件 | 行数 |
|---|------|-----------|------|------|
| 0a | R1 进展门 (K 步无新 VisitedNode → FrameComplete) + ContainerProgress 数据结构 | **探索进度记忆** | StepOrchestrator.cs | ~30 |
| 0b | 安全门 #4 (页面一致性) + #5 (速率限制, N=3) + #9 参数化 (K=10) | — | TraversalFSM.cs | ~40 |
| 0c | 安全门 #10 (决策去重) + #11 (栈一致性) + #12 (C-4 提交) | — | TraversalFSM.cs | ~20 |
| 0d | 收编引擎二次写入 → StepOrchestrator Step 11 单一写入点 (消除 ChildPushed 路径双真相源分裂, Battle #7) | — | TraversalEngine.cs:372-373, StepOrchestrator.cs | ~10 |
| 0e | Step 8-10 拦截层 CanTransitionTo 防御断言 (现有覆盖值全合法, 零行为变化, Battle #7) | — | StepOrchestrator.cs:83/:92/:101 | ~10 |

### Phase 1: 异常上下文 + 指纹稳定 + 弹窗检测 (~110 行)

| # | 改动 | Memory 维度 | 文件 | 行数 |
|---|------|-----------|------|------|
| 1a | ErrorContext +4 字段 + CaptureFaultContext/ClearFaultContext | **异常现场记忆** | ErrorContext.cs | ~20 |
| 1b | ErrorHandler Step 4 verify (3 分支验证逻辑) | **异常现场记忆** | ErrorHandler.cs | ~30 |
| 1c | `NormalizeTextForIdentity` 移入 Core + 应用于 PageFingerprint 输入 (字符级稳定) | **页面身份记忆** (归一化) | TraversalEngine.cs:1956 | ~3 |
| 1d | PageFingerprint 加类型白名单 (只哈希可导航 MenuItemType) | **页面身份记忆** (内容选择) | TraversalEngine.cs:1956 | ~5 |
| 1e | 弹窗优先检测 (Post-PressBack popup check, 覆盖双门限+Backtrack) | — | StepOrchestrator.cs | ~25 |
| 1f | 快照完备性验证 → 接线 snapshot 参数 | — | ITraversalAdvisor.cs, TraversalAdvisor.cs, TraversalFSM.cs:552 | ~30 |

### Phase 1.5: AI 决策采纳 (~25 行)

| # | 改动 | 文件 | 行数 |
|---|------|------|------|
| 1.5a | Advisor confidence≥0.7 + NestingLevel≤1 → 直接采纳 strategy | TraversalFSM.cs HandleErrorHandlingAsync | ~15 |
| 1.5b | ErrorPolicyType.ConsultBrain | ErrorPolicyType.cs (新增枚举值, 实际 5→6) | ~5 |
| 1.5c | PopupHandler RestoreState 保留原始 Exception 类型 | PopupHandler.cs | ~5 |
| 1.5d | 完整 Transition Gateway + transition_intents + 拦截器结果模型重构 (与 1.5a AI 决策采纳同批 — 三方控制源竞争出现时才需要; Battle #7 裁决推迟至此) | StepOrchestrator.cs, InterceptionHandler.cs, TraversalFSM.cs | ~150 |

### Phase 2: 异常模式 + 决策历史 + PlanPatch Skip (~200 行)

| # | 改动 | Memory 维度 | 行数 |
|---|------|-----------|------|
| 2a | 异常模式记录 (ErrorType, PageFingerprint, ActionType) 分组 → trace `error_pattern_repeated` (先观测) | **异常模式记忆** | ~25 |
| 2b | DecisionHistory 环形缓冲 (20 条) + ErrorStrategySelector 查询 "同页同类型 Retry 是否曾失败" | **决策历史记忆** | ~40 |
| 2c | PlanPatch Skip 通道 + audit journal | — | ~60 |
| 2d | 安全门 #8 (时间预算) + 门限参数化 (K=10, N=3, 时间=P95) | — | ~30 |
| 2e | 快照扩展 (覆盖面身份+错误上下文) | — | ~30 |
| 2f | HandleExceptionAsync 契约定义 + 实现 | — | ~60 |
| 2g | 页面身份模糊匹配层 (文本集合 overlap + 布局签名, 指纹精确匹配之后的第二层) | **页面身份记忆** (模糊匹配) | ~60 |

### Phase 3 (远期, P3)

| 改动 | Memory 维度 | 状态 |
|------|-----------|------|
| 导航路径记忆 (循环检测+重复访问) | **导航路径记忆** | P3 |
| 页面期望记忆 (HasChanged 增强) | **页面期望记忆** | P3 |
| 性能记忆 (自适应 wait/backoff) | **性能记忆** | P3 |
| TraversalProgram DSL | — | **FROZEN** |
| Brain L3 权威模式 | — | 远期 |
| PlanPatch InsertBefore/Replace/Append/Reroute | — | **FROZEN** |

### Constitution 修订

| 条款 | 修订 |
|------|------|
| C-1 | **保留** — 19 边矩阵不变 |
| C-4 | **新增**: "AI 的决策经 CPU 在合法 Transition 中执行; AI 不直接写 FSM state" (修正原 "写经 TransitionTo" 歧义) |
| C-6 | **扩展**: "AI 读 Memory 通道 = TraversalContextSnapshot" (待快照扩展覆盖 framebuffer 后生效; 当前 Phase 1.1 双参数为过渡) |
| **新** | **AI 决策门限**: "advisor confidence≥0.7 + 确定性 fallback + advisor_unavailable 降级" 升格为 constitution 级 |

---

## 7. 影响范围

### 源码变更

| 文件 | Phase | 变更 | Memory 维度 |
|------|-------|------|-----------|
| `StepOrchestrator.cs` | 0-1 | R1 进展门 + ContainerProgress + 弹窗优先检测 + Step 11 单一写入点 (0d) + Step 8-10 防御断言 (0e) | 探索进度记忆 |
| `ErrorContext.cs` | 1 | +4 字段 + CaptureFaultContext/ClearFaultContext | 异常现场记忆 |
| `ErrorHandler.cs` | 1 | Step 4 verify (3 分支验证) | 异常现场记忆 |
| `TraversalEngine.cs` | 0-1 | 收编 :372-373 二次写入至 Step 11 + NormalizeTextForIdentity 移入 + 类型白名单过滤 | 页面身份记忆 |
| `TraversalFSM.cs` | 0-1.5 | 安全门 #4/#5/#7/#9/#10/#11/#12 + advisor 采纳 | — |
| `ITraversalAdvisor.cs` | 1 | snapshot 参数 overload | — |
| `TraversalAdvisor.cs` | 1-2 | snapshot 消费; HandleExceptionAsync 实现 | — |
| `MockTraversalAdvisor.cs` | 1 | 适配新 overload | — |
| `FsmSimulationHarness.cs` | 1 | NullAdvisor 适配 | — |
| `PopupHandler.cs` | 1.5 | RestoreState 保留原始 Exception 类型 | — |
| `TraversalNode.cs` (ErrorPolicyType) | 1.5 | +ConsultBrain 枚举值 | — |
| `ErrorContext.cs` / 新文件 | 2 | 异常模式记录 (ErrorType×PageFingerprint 分组) | 异常模式记忆 |
| `ErrorHandler.cs` | 2 | DecisionHistory 环形缓冲 + 策略跳过 | 决策历史记忆 |
| `InterceptionHandler.cs` | 2 | PlanPatch Skip 通道 + audit journal | — |
| `PageSnapshotManager` (TraversalEngine.cs) | 2 | 页面身份模糊匹配层 | 页面身份记忆 |
| `docs/system/constitution/constraints.md` | 1 | C-4 修正 + C-6 扩展 + 新 AI 门限条款 | — |

### 测试适配 (不可 "零破坏")

| 测试 | Phase | 适配 |
|------|-------|------|
| `TraversalAdvisorTests.cs:121-145` (3 个 NotImplemented 断言) | 2 | 实现 HandleExceptionAsync 后需更新 |
| `TraversalAdvisorTests.cs:56` (4 参签名) | 1 | 新增 snapshot 重载后补 snapshot 断言 |
| `DecideNextActionEndToEndTests.cs:60` (4 参签名) | 1 | 同上 |
| `FsmSimulationHarness.NullAdvisor` (3 个 NotImplemented) | 1 | 实现方法后更新 |
| `SimulationBaselineTests.cs` (step 数/访问页期望) | 1.5 | advisor 接入后 ErrorHandling 路径 trace/步数变化需更新 baseline |
| 新增测试: `ErrorContext_FaultCapture*` (P0), `HandleErrorHandling_Verify*` (P1), `HandleErrorHandling_AdvisorAdopted*` (P1.5), `ProgressGate_*` (P0), `PlanPatch_Skip*` (P2) | 0-2 | ~15 个新测试 |

### 不改动的

- **矩阵 19 边** — 零改动
- **8 handler 决策表** — 零改动 (仅 HandleErrorHandlingAsync 加 adopt 分支)
- **GlobalFSM** — 零改动
- **StepOrchestrator 14-step 编排** — 零改动 (加 hook 不改编排)

---

## 8. 对战验证

### Battle #6 对抗审阅 (fsm-analyzer × shadow-fsm-analyzer)

**双方独立收敛到相同裁定: 需修正 (不重做)。**

| 维度 | fsm-analyzer (源码核查) | shadow-fsm-analyzer (需求评审) | 判定 |
|------|------------------------|---------------------------|------|
| 架构方向 | 成立 | 成立 | ✅ CONSENSUS |
| 3 处事实硬伤 | ErrorPolicyType/SafetyGate/测试数 全部源码锚定 | 独立证实 | ✅ 已修正 |
| R1 排期 | — | Phase 2→Phase 0, 设计从 "无 FrameComplete" 改为 "无 VisitedNode" | ✅ 已修正 |
| Gap #8 Backtrack | 与 spec "never reset" + 测试冲突 | 独立证实 | ✅ 已撤回 |
| 隐喻克制度 | — | ISA→词汇表, Vision→删除, Memory→文档化 | ✅ 已精简 |
| 安全门 | — | +4 条新门 (进展/去重/栈一致性/C-4) | ✅ 已补 |
| PlanPatch | — | 5→1 操作 (仅 Skip) | ✅ 已精简 |
| Phase 排期 | 支持重排 | 提出新顺序 (P0 进展门→P1 上下文→...) | ✅ 已采用 |
| C-11 baseline | — | 影响未评估 | ✅ 已补测试计划 |

### Battle #7: Transition Gateway 对抗审阅 (用户三层架构分析 × fsm-analyzer 源码裁决)

**背景**: 用户提出三层架构分析，诊断 L3 (StepOrchestrator/InterceptionHandler) 的 NextState override 绕过矩阵校验为"未受治理的旁路控制流"，建议 Transition Gateway + transition_intents + 拦截器 4 结果 + state_version + PopupHandling 中断子状态机。

**裁决: 定性部分成立；方案方向采纳但形态调整——不建 Gateway 抽象，治理下沉为 Phase 0d/0e + 安全门。**

| 用户论断 | 裁决 | 关键证据 |
|---------|------|---------|
| 9 个直接赋值点存在 (:116/:124/:134/:216/:253/:289/:298/:336/:344) | ✅ 证实 | 行号精确命中 (InterceptionHandler.cs) |
| 这些 override 绕过矩阵校验 | ✅ 证实 + 精确化 | 覆盖值域**恒为 NodeSelect**，全部 ∈ 矩阵合法边——若严格执行 CanTransitionTo，9/9 通过，零拒绝。矩阵对拦截层是"零拦截力"而非"非法转移" |
| 双状态写入 (FSM.CurrentState vs nextState) | ✅ 证实 + 补充 | 第三写入点: TraversalEngine.cs:372-373 (ChildPushed → TransitionTo(NodeSelect))。分裂仅发生在覆盖路径 ChildPushed=false 时 (滚动/完成/Pop)，自愈但 trace `ToState` 失真 |
| L3 是"未受治理的旁路控制流" | ⚠️ 部分成立 | 无非法转移；分裂自愈；**真正的旁路是副作用** — 拦截层直接执行 PressBack/Swipe/Stack.Pop/Push/Invalidate (:259/:514/:240/:447/:550) 修改物理状态，矩阵与状态校验均不可见 |
| 拦截层 50% 语义是动作执行 | 补充发现 | 4 结果模型 (CONTINUE/BLOCK/MODIFY/EMIT) 无动作通道 — 需副作用通道或保留执行职责 |
| 现有先例 | 补充发现 | TryHandleNavigation (:455-458 "Don't override result.NextState") 已有"意图优先"先例；GlobalFSM.ForceState (GlobalFSM.cs:71-77) 是"恢复绕过矩阵"先例 |

**方案逐条裁决** (详见审阅记录):

| 方案 | 裁决 | 理由 | 去向 |
|------|------|------|------|
| transition_intents 替代 NextState | 部分可行 | 意图 (滚动/完成/Pop) 比状态 (NodeSelect) 语义准确，但 Step 9 恒等覆盖是副作用载体，需新增副作用通道 | Phase 1.5d |
| 统一 Transition Gateway | 部分可行 | 矩阵校验对拦截层空转 (值恒合法)；真正要治理的是副作用；侵入面 = 全部 3 个写入点 + StepResult 契约 + 引擎 5 个消费点 | Phase 1.5d (推迟) |
| 拦截器 4 结果 | 部分可行 | 表达不了 `FrameCompleted=true + NextState=NodeSelect + 不弹栈` (:344 路径 — 终止检查生存前提)；无动作通道 | 否决当前形态 |
| Memory 绑定 events | ✅ 可行 | 与 §2 Memory 模型方向一致，正交工程 | §2 保留 |
| PopupHandling 中断子状态机 | 部分可行 | StateRestorer (PopupHandler.cs:328-433) 已有 preserve/restore/validate 基础设施，与 resume_state 在 GlobalFSM 维度**完全等价**；缺 TraversalFSM.CurrentState 保存 (:353 只存 GlobalState) + ErrorHandling→PopupHandling 缺边 (Gap #4) | 扩展 StateRestorer 而非新建机制 |
| state_version + observation_id | 部分可行 | 全仓零命中，是全新机制；当前单线程同步循环无陈旧决策问题；AI 接入 (Phase 1.5 P4) 后才需要 | 安全门 #4/#11 已覆盖 |
| 控制源优先级 | ✅ 可行 | 隐式优先级已稳定: handler (:145) → 拦截层 (Step 8-10) → 引擎 (:373/:355/:345/:409)；只需文档化 | §3.3 门 #12 |

**采纳的最小侵入路径** (4 步, 已入实施路径):
1. **0d**: 收编引擎二次写入 → Step 11 单一写入点 (~10 行, 消除 ChildPushed 分裂)
2. **0e**: Step 8-10 CanTransitionTo 防御断言 (~10 行, 零行为变化 — 现有覆盖值全合法)
3. **副作用可观测化**: Pop/PressBack/Swipe/Invalidate 补 decision trace (中量, 排期未定)
4. **1.5d**: 完整 Gateway + intents + 版本化推迟到 AI 决策采纳 (1.5a) 同批 — 那时才有三方控制源竞争的真实需求

**与 §0 #3 的整合**: "拦截层 NextState 自由区" 保留为 Brain hook 插槽；治理 = 防御断言 + 单一写入点 + 安全门 12 条，**不移除 override**——移除会消灭现成插槽且破坏 :344 终止语义。

### Battle 累计统计

| # | 主题 | 共识 | 争议 | 关键发现 |
|---|------|------|------|---------|
| 1 | 全量矩阵+Handler | 全部核心结构 | 3 (文档滞后) | 异常路由边 |
| 2 | ConsecutiveErrors | 5/5 | 0 | PressBack 重置无测试 |
| 3 | FSM-as-CPU 架构 | 5/5 | 0 | 结构已存在 |
| 4 | 异常恢复完备性 | 10 Gap + 精简 | 0 | 砍掉 60% |
| 5 | 指纹去重方案 | 6/6 用户提案方向 | 0 | PageFingerprint 已有排序归一化 |
| 6 | CPU 架构对抗审阅 | **需修正 (不重做)** | 0 | 3 硬伤 + R1 排期 + Gap #8 冲突 |
| 7 | Transition Gateway (用户三层架构分析) | 定性部分成立, 方案形态调整 | 0 | 值域恒 NodeSelect 零非法转移; 第三写入点 :373; 真旁路=副作用; 最小侵入 4 步 |
