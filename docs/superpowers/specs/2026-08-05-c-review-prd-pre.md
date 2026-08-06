# FSM-CPU 架构设计对抗审阅报告 — PRD-pre

> 状态: 对抗审阅完成 (Battle #6 输出)
> 审阅对象: `docs/refactor/2026-08-05-fsm-cpu-architecture-design.md`
> 审阅方: fsm-analyzer (源码事实核查) × shadow-fsm-analyzer (需求对抗评审)
> 日期: 2026-08-05

---

## 0. 总体裁定

**需修正（不重做）。** 架构方向正确、克制度可嘉。但存在 3 处事实硬伤、2 个结构性缺陷（R1 排期错误、Gap #8 与今日 spec 冲突）、多处内部矛盾，修正后可安全推进。

---

## 1. 双方共识 (HIGH confidence)

### 1.1 成立的设计判断

| 判断 | fsm-analyzer 源码验证 | shadow-fsm-analyzer 需求验证 |
|------|----------------------|---------------------------|
| 矩阵 19 边 = opcode 邻接表 | ✅ 行号锚定 (TraversalFSM.cs:26-47) | ✅ Battle #3 5/5 共识 |
| 降级链 = 独立 IVT | ✅ CanTransitionTo 守卫 (TraversalFSM.cs:125-138) | ✅ refactor §1.2 文档背书 |
| D-2 快照零消费者 | ✅ CreateReadOnlySnapshot 生产零调用 | ✅ 独立证实 (零引用 grep) |
| Step 8-10 拦截 = Brain 插槽 | ✅ NextState 不经过矩阵 (9 处直接赋值) | ✅ Battle #3 共识 |
| Advisor 是元数据非决策者 | ✅ extraMeta 不流入 ErrorStrategySelector | ✅ 独立证实 (NullAdvisor Confidence=0.0) |
| §9 的 10 个 Gap 诊断准确 | ✅ 全部行号锚定确认 | ✅ 6/6 Battle #4 确认 |
| §9.3 精简设计方向正确 | ✅ 砍掉 280 行正确 | ✅ ErrorContext +4 字段 + 三级异常 + Backtrack 重置 |
| §9.4 P0 重排正确 | ✅ Gap #1+#5 优先于 #2 | ✅ 纯确定性、无依赖、是 AI 决策前提 |

### 1.2 成立但需修正的设计

| 设计 | 问题 | 修正方 |
|------|------|--------|
| Brain 4 级阶梯 L0-L3 | L3 无场景, 与 Phase 3 DSL 冻结一致 | 双方同意: L3 冻结 |
| PlanPatch | 5 操作中只有 Skip 有锚定场景 | Shadow: 砍到 Skip-only; fsm-analyzer: 无异议 |
| Phase 3 DSL | 已冻结 | 双方同意 |
| ISA 指令表 | 10/12 条是重命名, 零新增行为 | Shadow: 降格为词汇表; fsm-analyzer: 无异议 |
| Memory 8 分区 | 6/8 是现有字段重命名 | Shadow: 保留快照+.plan; fsm-analyzer: 33→34 字段计数需统一 |
| Vision=Camera | 零改动, 纯修辞 | Shadow: 砍掉作为设计元素; fsm-analyzer: 无异议 |
| C-6 修订 "唯一通道=快照" | 与 Phase 1.1 双参数签名矛盾 | fsm-analyzer: 必须先验证快照完备性 |

---

## 2. 事实硬伤 (必须修正)

| # | 文档声称 | 源码事实 | 修正 |
|---|---------|---------|------|
| 1 | ErrorPolicyType "4 种 + Default + ConsultBrain" | **实际 5 值: Retry/Skip/Abort/Fallback/Backtrack，无 Default**。null ErrorPolicy 走默认链是 ErrorHandler.cs:102-110 的逻辑，不是枚举值 | §9.3 枚举草图改为实际 5 值 |
| 2 | 安全门 3 "已在 SafeActionExecutor 实现" + 门 7 "复用其操作目标校验" | SafeActionExecutor 在 **UniClaw.Host/Safety/SafetyGate.cs** (Host 层), 是 ISafetyEvaluator deny 包装器 (失败 `return false`), **无 "操作目标∈PageAnalysis.Items" 校验**。文档影响表只列 Core 文件 | 门 3/7 改述: Host 层安全 deny 已有, Core 侧操作目标校验需新增 |
| 3 | "237 现有测试零破坏" | 实测 Core [Fact]/[Theory] ~1017 个; 且实现 HandleExceptionAsync 直接破坏 `HandleExceptionAsync_ThrowsNotImplemented` 等 3 个测试; 接口加 overload 编译破坏 3 个实现者 | "237 零破坏"→"N 处需同步适配"清单 (3 实现者 + 3 断言测试) |
| 4 | 门数 "6 条" vs 实际列 8 条 | §3.3 开头 "6 条安全门", §3.4 "安全门 1-6", 表格含 8 行 | 统一为 8, 全文一致 |
| 5 | 门 2 置信度 0.7 vs §9.3 采纳 0.8 | 同一决策点两个不同门限 | 统一为 0.7 (当前实际执行值, TraversalFSM.cs:558) |
| 6 | Gap #2 排期两处冲突 | §9.4 P1 (Phase 1.5) vs §3.4/§7 Phase 2 #5 (Phase 2) — 同一行为两个排期 | 统一为 Phase 1.5 P1 |

---

## 3. 结构性缺陷

### 3.1 R1 进展门排期错误 (双方共识)

**问题**: R1 (120 步 FrameComplete=0) 是唯一有 trace 证据的旗舰问题 (TraceReplay 测试确认 run `20260805T052309367Z` 结局 = max_steps 120)。Phase 1 + 1.5 合计 ~280 行改动**一个都不解决 R1**。R1 修复被挂到 500 行 Phase 2 #7。

**且 Phase 2 #7 的 "DynamicMatch >N 次无 FrameComplete" 检测设计本身有缺陷**: 错误循环 (ErrorHandling→FrameComplete→NodeSelect→Execute→异常→...) **每圈都有 FrameComplete**，该 detector 不会触发。需要的是"进展门"——以"无新 VisitedNode 达 K 次"为判据 (不是"无 FrameComplete")。

**修正**: R1 进展门提升到 Phase 1 头号任务 (拦截层, ~30 行)。建议 K=10 (当前 maxSteps=120, 10 步无进展 = 异常)。同时覆盖 DynamicMatch 循环和 ISSUE C (错误燃烧循环)。

### 3.2 Gap #8 (Backtrack 语义) 与今日 spec 冲突

**问题**: 文档 §9.3/§9.4 把 Backtrack → ResetConsecutiveErrors 当作 5 行小修。但实际上:

- **spec 冲突**: `openspec/specs/handler-error-handling/spec.md` 今日工作区改动刚写入 "SHALL NOT reset ConsecutiveErrors on any strategy" (D-242)
- **测试冲突**: `HandleErrorHandlingTests.cs:86-87` `ErrorHandling_Backtrack_GoesToNodeSelect_IncrementsConsecutive` 直接断言 +1
- **保护比变更**: Backtrack 重置后 consecutive gate (3 次) 几乎不可达, 熔断只剩 page-item gate (5 次), 双门限保护比从 3→5

**修正**: 二选一:
- **选项 A (推荐)**: 撤回 Gap #8, 保持 spec "never reset" + 现有测试。理由: 源码注释 (TraversalFSM.cs:593-596) 已论证 "resetting prevents PressBack gate from ever triggering", 这是有意的设计取舍, 不是 bug
- **选项 B**: 发起 spec 修订 + 重命名测试 + 分析保护比影响。需额外 ~30 行 spec diff + 1 测试重命名 + 新增保护比分析文档

### 3.3 拦截层 override 双状态分歧

fsm-analyzer 发现: StepAsync 内已 `TransitionTo(nextState)` (TraversalFSM.cs:145), Step 8-10 只覆盖 orchestrator 局部 nextState。引擎终止/弹栈逻辑依赖 **StepResult 标志位而非 nextState 本身**。Brain override 到 FrameComplete 若不置 FrameCompleted 标志位, 引擎无动作。

**修正**: §3.2 的 "自由区"声称需补充限定——Brain override 不仅需要 CanTransitionTo (门 1), 还需要设置对应的 StepResult 标志位 (FrameCompleted/ErrorOccurred/StepExecuted)。

---

## 4. Phase 排期修正

双方综合建议的最终排期:

| 顺序 | 内容 | 改动量 | 来源 |
|------|------|--------|------|
| **P0** | R1 进展门 (无新 VisitedNode 达 K 次 → 强制 FrameComplete, 拦截层) | ~30 行 | Shadow 建议, fsm-analyzer 无异议 |
| **P1** | 1.5 P0: ErrorContext +4 字段 + 恢复验证 (Step 4) | ~50 行 | 文档 §9.4, 双方同意 |
| **P2** | 快照接线 (验证完备性 → 接线) | ~30 行 | 文档 Phase 1 #1, 双方同意 |
| **P3** | 安全门 #4 (页面一致性) + #5 (速率限制) + #9 (进展门, 参数化) | ~50 行 | 文档 Phase 1 #4, shadow 建议加 #9 |
| **P4** | Gap #2 采纳 (advisor confidence≥0.7 直接判 decision) + ErrorPolicyType.ConsultBrain | ~20 行 | 文档 §9.4 P1, 双方同意 |
| **P5** | Gap #4 弹窗检测 (PressBack 后, 含双门限路径) | ~25 行 | 文档 §9.4 P1, shadow 修正覆盖范围 |
| **P6** | Phase 1 #2 HandleExceptionAsync (定义契约 → 实现) | ~60 行 | 文档 Phase 1 #2, 推迟到 P1-P5 完成后 |
| **冻结** | Gap #8 Backtrack 语义 | — | 双方建议撤回 (选项 A) |
| **冻结** | Phase 1 #3 Step 8/9 advisor 咨询 | — | Shadow: 在采纳能力之前接入 = 第二个死 extraMeta 消费者 |
| **冻结** | Phase 3 DSL, Brain L3, PlanPatch Insert/Replace/Append/Reroute | — | 双方同意 |

---

## 5. 安全门修正

文档现有 8 条门 → 修正为 **12 条** (新增 6 条, 砍掉 ISA 表 "候选"噪声后门表独立):

| # | 门 | 状态 | 备注 |
|---|-----|------|------|
| 1-8 | 原有 8 条 | 保留 | 门 2 阈值统一 0.7; 门 3/7 改述 Host/Core 分层 |
| 9 | **进展门** (no-progress gate) | **新增** | K 步无新 VisitedNode → 强制熔断; R1 + ISSUE C 共用 |
| 10 | **决策去重门** | **新增** | 同帧同建议 ≥3 → 拒绝降级 |
| 11 | **栈一致性门** | **新增** | Brain 决策引用的帧在 commit 时仍为当前帧 (消费 Gap #1 FailingStackDepth) |
| 12 | **C-4 提交门** | **新增** | Brain 提案唯一生效路径 = CPU 执行 TransitionTo (消歧 "写经 TransitionTo") |

**门限参数化**: 门 5 的 N=3 (对齐决策去重门), 门 8 的时间预算 = advisor 调用历史 P95。

---

## 6. 隐喻克制度

| 元素 | 原设计 | 修正 |
|------|--------|------|
| ISA 指令表 (§1.1) | 12 条指令, 设计元素 | 降格为**词汇表 (glossary)** — 显式声明 "ISA 不产生新代码" |
| Memory 8 分区 (§2.1) | 设计元素, 含权限矩阵 | 保留快照 + `.plan`; `.brain_workspace` 已冻结移除; 权限矩阵降格为文档 |
| Brain 4 级阶梯 (§3.4) | 保留 L0-L2, L3 冻结 | 全文统一 0.7 门限; Gap #2 排期统一 |
| Vision=Camera (§5) | 独立章节 | **删除** — 保留 "Vision 写 framebuffer" 一句 |
| PlanPatch 5 操作 (§4.4) | InsertBefore/Replace/Skip/Append/Reroute | **砍到 Skip-only** — 其余 4 个无锚定场景 |

---

## 7. 遗漏问题

双方共同发现的 12 项遗漏:

| # | 遗漏 | 发现方 |
|---|------|--------|
| 1 | R1 stall detector 设计缺陷 (测 FrameComplete 不测 VisitedNode) | 双方 |
| 2 | ISSUE C 步数燃烧零提及 | Shadow |
| 3 | Gap #8 与今日 spec/测试冲突 | Shadow, fsm-analyzer 确认测试存在 |
| 4 | C-11 baseline 影响零评估 | Shadow |
| 5 | 门数/门限/排期内部矛盾 3 处 | 双方 |
| 6 | HandleExceptionAsync 无契约 | Shadow |
| 7 | 安全门 1 对拦截层 hook 不适用 (作用域未定义) | Shadow |
| 8 | ISA 表 "候选" 列无 Phase 排期 | Shadow |
| 9 | C-4 修订条款措辞歧义 ("写经 TransitionTo" 可误读) | Shadow |
| 10 | 快照信息缺口 (8 字段缺 framebuffer/错误上下文) | fsm-analyzer |
| 11 | 拦截层 override 不更新 FSM.CurrentState (双状态分歧) | fsm-analyzer |
| 12 | 影响表不完整 (缺 6 个必改文件) | fsm-analyzer |

---

## 8. 行动项

| 优先级 | 行动 | 状态 |
|--------|------|------|
| **立即** | 修正 6 项事实硬伤 (ErrorPolicyType 枚举 / SafeActionExecutor 层级 / 测试数 / 门数 / 门限 / Gap #2 排期) | 待文档修订 |
| **立即** | 撤回 Gap #8 (Backtrack 语义) — 选选项 A (保持 spec "never reset") | 待决策 |
| **P0** | R1 进展门提到 Phase 1 头号任务 (~30 行) | 待实施 |
| **P1** | 修正 Phase 排期 (见 §4 新顺序) | 待文档修订 |
| **P1** | 统一门表 (12 条, 编号+参数化) | 待文档修订 |
| **P1** | PlanPatch 砍到 Skip-only | 待文档修订 |
| **P2** | 隐喻克制度修正 (ISA→词汇表, Vision→删除, Memory 精简) | 待文档修订 |
| **P2** | C-11 baseline 影响评估 | 待分析 |
| **P3** | HandleExceptionAsync 契约定义 | 待设计 |

---

## 9. Battle 统计 (累计)

| # | 主题 | 共识 | 争议 | 关键发现 |
|---|------|------|------|---------|
| 1 | 全量矩阵+Handler 交叉验证 | 全部核心结构 | 3 (文档滞后) | 异常路由边是第三种矩阵边 |
| 2 | ConsecutiveErrors 增量语义 | 5/5 | 0 | PressBack 重置无测试断言 |
| 3 | FSM-as-CPU 架构 | 5/5 | 0 | 结构已存在, 只需接线 |
| 4 | 异常恢复完备性 | 6/6 (10 Gap) | 0 (精简后) | 砍掉 60% 过度设计 |
| 5 | 指纹去重方案 | 6/6 用户提案方向保留 | 0 | PageFingerprint 已含排序归一化 |
| **6** | **CPU 架构设计对抗审阅** | **诊断方向正确, 需修正** | **0 (双方独立收敛)** | **3 处事实硬伤 + R1 排期错误 + Gap #8 spec 冲突** |
