# shadow-fsm-analyzer 分层知识

> 从 S1-S5 层蒸馏。每条 1-3 句，标注来源层与置信度。来源文档更新时按 INDEX.md 刷新规则重蒸馏。
> 上次全量分析: 2026-08-05（v0.2.1，18 个 StateMachine 测试文件人工审查 + Battle #1 源码交叉验证）

## S5 差距分析 — "应该 vs 实际"

> Battle #1 (2026-08-05): Shadow fsm-design.md v0.2.0 vs TraversalFSM.cs 源码交叉验证。

### Battle #1 验证结论

- **矩阵: 19/19 边 SOURCE-VERIFIED** — shadow 从需求+测试推导的矩阵与源码 TransitionMatrix 完全一致。
- **Handler 决策表: 8/8 核心路径 SOURCE-VERIFIED** — 每个 handler 的主决策路径与源码匹配。
- **异常路由: SOURCE-VERIFIED** — CanTransitionTo 守卫 + 降级链 (NodeSelect→Branch / FrameComplete→NodeSelect / ErrorHandling→FrameComplete) 与源码 lines 130-138 完全一致。
- **双门限: SOURCE-VERIFIED** — ConsecutiveErrors≥3 (line 620) + NodeFailedItems≥5 (line 604) + depth>1 条件全部匹配。
- **LastError 生命周期: SOURCE-VERIFIED** — SetLastError(入口) → 读取分类 → SetLastError(null)(line 637) + gate clears (lines 613,627) 全部匹配。

### Battle #1 表面差异（文档滞后，非实质分歧）

| 差异 | Shadow 声称 | 源码实际 | 结论 |
|------|-----------|---------|------|
| ResultVerify 重试轮数 | 3 轮 (spec/test名) | 2 轮 (首次+单次retry) | 文档滞后 — shadow 已修正 |
| ResultVerify→ErrorHandling 生产者 | vision 异常 (MEDIUM) | StepAsync catch 自动路由 (非 handler 显式) | 异常路由边语义澄清 |
| PopupHandling LastError | 盲区/未设置 (H2) | 已修复 (lines 672-675) | 测试待补，源码已修 |

### 关键发现

- **文档声称 ≠ 源码实现**: spec 说 ResultVerify "3-round retry"，源码实现为 2 轮。文档优先 patterns (紧跟代码的 Tier 2)，spec/charter 可能滞后。
- **异常路由边 ≠ handler 显式返回边**: ResultVerify→ErrorHandling 在矩阵中合法（CanTransitionTo 返回 true），但 handler 不显式返回它。生产者是 StepAsync catch。与 3 死边不同：死边连运行时都不可达。
- **修复状态三层**: 设计文档可能描述修复意图 → 源码可能已落地 → 测试可能尚未验证。shadow 从重构设计文档读取"修复计划"时，需区分三层。

## S1 需求蒸馏 — "FSM 应该做什么"

### 硬约束（Constitution，不可违反）

来源：`docs/system/constitution/constraints.md` + `locked-enums.md`（2026-08-05 已重读）

- **C-1**: TraversalState 锁定 8 值（NodeSelect/PreconditionCheck/Execute/ResultVerify/Branch/FrameComplete/ErrorHandling/PopupHandling）。H-1 事故：DynamicMatch 曾错误插入为第 9 值（它是 ChildrenStrategyType 值）。Guard: `TraversalState_Has8Values`。
- **C-4 / P-7**: TraversalFSM 和 GlobalFSM 独立——不共享状态/转移/回调；仅通过 `ITraversalContext.GlobalState` 数据字段协调。M-14 已在 Phase 2.3 解决（ITraversalContext 已纯只读，setter 移到 TraversalRuntimeContext）。
- **C-7**: GlobalState 锁定 8 值（Idle/Initializing/Traversing/Paused/Error/Recovering/Completed/Terminated）；Completed/Terminated 是终端状态（显式拒绝任何出迁，不依赖空矩阵）。
- **C-10/P-6**: 域校验用 `DomainValidationException`；**FSM 非法转移也抛 DVE**（FieldName="transition", IllegalValue="From→To"）。允许 NotSupportedException（runtime isolation）/ InvalidCastException（ReadOnlySetWrapper）。
- **C-11**: ExpectedBehavior schema 锁定 + Simulation baseline E2E 回归门槛（CI-blocking）。验证维度含 dfs_properties（RootFirst/ParentBeforeChild/BackAfterForward）。
- **P-3**: ITraversalContext 只读（3 个 allowed setter 已移除——D-29/30/31 后纯只读）；mutation 走 TraversalRuntimeContext engine-only 方法。

### 设计意图（Patterns + Refactor）

来源：`docs/system/patterns/fsm-design.md`（2026-08-05 22:07 更新——**比记忆新，已重读**）+ `docs/refactor/2026-08-05-fsm-matrix-hardening-design.md`

- **19 边矩阵（当前）**: 2026-08-05 加固移除 3 死边（Execute→Branch / Branch→PreconditionCheck / FrameComplete→ErrorHandling），22→19 边。每条边至少一个 handler 显式返回。
- **D-1 先例**: PreconditionCheck→Branch 因 handler 从不返回而移除——先例：handler 不生产的边应从矩阵删除。
- **异常路由安全化（重构核心）**: StepAsync catch 不硬编码 ErrorHandling；`CanTransitionTo(ErrorHandling)` 守卫 + 降级链（NodeSelect→Branch, FrameComplete→NodeSelect, ErrorHandling→FrameComplete）。矩阵只管 handler 门，异常路由走独立降级通道。
- **错误计数收敛**: ConsecutiveErrors 语义="同子树内恢复尝试次数"，唯一递增点在 HandleErrorHandlingAsync；门限 ≥3 = "3 次恢复尝试后放弃"。
- **LastError 生命周期**: SetLastError（入口）→ ErrorHandling 读取分类 → 3 返回点前 SetLastError(null)。ResultVerify 不清——"操作验证通过"≠"错误处置完毕"。
- **D-20 HandleBranch 决策矩阵**: STATIC+未访问→NodeSelect；STATIC+全访问→FrameComplete；DYNAMIC_MATCH→NodeSelect（乐观）；NONE+叶子+depth>1→FrameComplete；NONE+叶子+depth==1→NodeSelect；NONE+容器→FrameComplete。
- **D-82 两步终止**: Traversing→Paused("stopping")→Terminated；矩阵无 Traversing→Terminated 直边。
- **Error→Traversing 不可直连**: 必须 Recovering→Initializing→Traversing（Initializing 是恢复校验检查点）。
- **D-81 激活**: SessionContext 持有 GlobalFSM；正常变更走 TransitionTo（矩阵+历史+回调+trace）；ForceState 绕过（仅 PopupHandler/StateRestorer，reason="force_restore"，无回调无 trace）。
- **已知取舍（步数燃烧）**: ErrorHandling 反复崩 → 降级链循环燃烧步数直至 max_steps（优于会话崩溃）。拦截层 Pop 语义（ChildPushed 状态机）不在 FSM 范围。
- **Vision→FSM 传导路径**: OCR 文本变体→fingerprint 变化→DynamicChildManager 找不到子节点→拦截层 PressBack→回 NodeSelect→DynamicMatch 容器内死循环直到 max_steps（R1: 120 步 run FrameComplete=0）。

### 已知问题（重构设计 2026-08-05，18 项发现）

- Bug #1 异常路由不安全（T1/T1a 修复）、Bug #2 计数入口分散 +2/周期（T3）、Bug #3 LastError 残留（T2）、H2 Popup 失败无 LastError（T4/T5）、死边 3 条（T6）。
- 6 个新测试规格已定（T1-T6），含精确 Arrange/Act/Assert；AC1-AC10 验收标准；237 现有测试零破坏目标。

### OpenSpec Change: e2e-dedup-vision-quality（2026-08-05 22:32 读取，D-G12 + V1-V4 已落地）

来源：`openspec/changes/e2e-dedup-vision-quality/`（proposal/design/tasks 全 [x] 除 E2E 7.1-7.3）+ `docs/superpowers/specs/2026-08-05-e2e-dedup-vision-quality-prd.md` + 4 个 spec

- **问题（PRD §2.1）**：Settings 主页 OCR 副标题（"28%used-5.72GBfree"）被标 menuItem，生成独立子节点导航到 sibling 已访问的同一物理页（Storage/Display/Security 三处，指纹 -338237621/-774902943/112375740）。根因：三种去重（`_generatedPairs`、VisitedNodes、VisitedChildren）都在 nodeId 维度，"两个 nodeId→同一物理页"无感知。
- **D-G12（引擎侧，已落地）**：RunAsync 入口初始化 `_childDestinations`（Dictionary<string, HashSet<int>>，per-parent 目的地指纹集）；verification_passed + fromState==ResultVerify 时记录 (parentNodeId, destFp)；sibling 重复→RecordDecision("child_destination_duplicate") + MarkNodeVisited，**不 Pop 不 PressBack 不 continue**（走正常收尾）。fp==0 空页跳过；不跨 run 污染。父子判断：stepFrame 仍=栈顶→父=Peek(1)（容器）；否则父=Peek()（叶子已 pop）。
- **测试已落地**：L6-1（同目标页只 visit 一次）/L6-2（不同目标都访问），fixture 文本驱动（"Storage"/"28% used"→同页 "storage_page"）。测试断言 `VisitedPages` 计数 ≤1。
- **V1-V4（Vision 侧，已落地）**：V1 同排重复合并（Y 阈值 + 相同/包含文本）；V2 副标题降级（Y 差 <0.035 → text）；V3 OCR 按 bbox 独立（不跨 bbox 拼接）；V4 identity 归一化（折叠空格/标点，仅 key 不改 display）。测试：SameRowDuplicates_MergedToOne / SameRow_ContainingText_MergedToLonger / V2 降级（0.033<0.035）/ V4 VariantsMergedByNormalizedKey。
- **Non-Goals**：不改矩阵、不操作栈、不改 handler 签名、不改 Generate 去重（`_generatedPairs` 不动）、不修搜索框 type 波动。设计边界 = 政治稳定边界（与 CPU 架构 Stage 兼容）。
- **已知取舍（design.md Risks）**：重复子节点仍被执行 1 次（多 2-3 步）；指纹碰撞概率极低（32-bit int，hash 输入 = sorted (type,name) 多重集）且只影响去重不改变正确性；fp==0 不记录（空页漏检可接受）。
- **与 CPU 架构的关系**：D-G12 = .visited 分区"内容寻址"的引擎级落地（两个 nodeId→同一物理页）；CPU 架构 line 388 确认 `VisitFingerprint + HashSet<(fingerprint, name)>` dedup 缓存存在。

### 现有指纹公式（PageAnalysis.PageFingerprint，纯类型属性 2026-08-05 发现）

`PageFingerprint` = Items 的 (Type.ToLower, Name) 排序多重集哈希（OrderBy Type,Name 后 17*31 聚合）。**不含坐标、不含 PageTitle、对 Type 和 Name 全敏感**——任何 item 的 Name 变化（On/Off、百分比、时间）或 Type 变化（V2 降级）都改指纹。空/默认→0。这是 R1（文本变体→指纹变化）的公式级证据，也是方案一"文本去重"的现有基础（排序归一化已内建）。

### 纯类型事实（2026-08-05 读取）

- `VisitFingerprint {Level1, Level2, ItemName}`，ToString="L1|L2|Name"，FromString 非法格式抛 DVE（ContentModelsTests 覆盖）——**文本三元组指纹已是领域先例**。
- `MenuItem.GetFingerprint(L1,L2)` = "L1|L2|Name"（ContentModelsTests.MenuItem_GetFingerprint）。
- `MenuItem {Name, Type(MenuItemType), Coordinate, Parent?, Description, ExpectedAction, ExpectsPageChange, ExpectsStateChange}`。
- `PageAnalysis {Level1Dir, Level1Menus, Level2Menus, CurrentPath(ImmutableArray<string>), Items, YoloBboxes, IsPopup, PopupInfo, CloseButton, BackButton, HasScroll, PageFingerprint(计算), IsEndOfList}`——**无 PageTitle 字段**（方案一的 PageTitle 需从 CurrentPath/OCR 推断）。
- Context visited 集（TraversalRuntimeContext 类型文件）：VisitedPages/VisitedNodes=ImmutableHashSet<string>；VisitedChildren=Dictionary<string,IReadOnlySet<string>>；VisitedLevel1Menus/Level2Menus=HashSet<string>；CurrentFingerprint=VisitFingerprint?；PageFingerprint=int。

### OpenSpec 规范

来源：`openspec/specs/traversal-fsm/spec.md` + `handler-error-handling/spec.md` + `handler-popup-handling/spec.md`（**2026-08-05 22:31 全部已同步 19 边加固**）

- ✅ **spec 矩阵已同步 19 边（D-240）**——之前"spec 22 边滞后"已过时，三层文档（spec/patterns/charter）现在一致（charter PopupType 5→6 也已同步）。
- **D-242**: ConsecutiveErrors 唯一递增点 = HandleErrorHandlingAsync；StepAsync catch / PreconditionCheck / Execute catch 均不递增。
- **D-243**: LastError 3 返回点清零（主策略路径 / page-item gate / consecutive gate）；NoStepContext stub 豁免。
- **D-244**: Popup 失败 LastError 消息 `"Popup dismiss failed: dismiss_action=<action>"`（Classification null → `action=`），不得含 "Permission"/"Error"/"Timeout"/"Ad"/"Dialog"/"Anr"（ErrorClassifier substring 碰撞防护）。
- **StepAsync 异常路由**: CanTransitionTo(ErrorHandling) 守卫 + 降级链（NodeSelect→Branch / FrameComplete→NodeSelect / ErrorHandling→FrameComplete / default→FrameComplete）——spec 已文档化。
- ResultVerify 需求：3-round retry + HasChanged + IsPopup→PopupHandling；全失败→Branch。
- TryHandleScroll（D1/D2/D3/D5）：进度差分≤ε→FrameComplete；元素数不增→FrameComplete；IsEndOfList→FrameComplete；DynamicMatch 无未访问子→TryHandleScroll；根（depth=1）滚动耗尽→FrameComplete。
- ErrorPolicy：每节点 MaxRetries 覆盖默认 3；OnError 影响 StrategyChain。
- GlobalFSM：ForceState/回调（异常不传播）/历史（失败不记录）/两步终止/协调经 Context。

### 重构设计（2026-08-05-fsm-matrix-hardening-design.md，新文档 22:00）

- **§1.2 根因：矩阵双重职责**——矩阵同时当 Handler 门 + 异常路由门；重构核心原则 = **矩阵只管 Handler 门，异常路由走独立安全降级通道**（不依赖矩阵）。→ 这是 CPU 隐喻"矩阵=opcode 邻接表、降级链=IVT"的文档化背书。
- 降级链取舍（ISSUE C）：ErrorHandling 反复崩 → 降级循环燃烧步数直至 max_steps（优于崩溃，设计文档化，无测试）。
- §3.1 Vision→FSM 传导路径：OCR 文本变体→fingerprint 变化→DynamicMatch 找不到子→PressBack→循环至 max_steps（R1: 120 步 FrameComplete=0）。FSM 层防御建议：fingerprint 稳定性 guard + FrameComplete 超时兜底（不在本次范围）。
- §3.2 拦截层 NextState 不经过矩阵校验——建议 Debug.Assert(CanTransitionTo)。

### 文档滞后点（发现）

| 文档 | 滞后内容 | 影响 |
|------|---------|------|
| ~~openspec/specs/traversal-fsm/spec.md~~ | ✅ **已同步 19 边（D-240，2026-08-05 22:31）** | 滞后点已消除 |
| ~~charter-specification.md §3.1~~ | ✅ **已同步**（PopupType 6 + 19 边相关描述） | 滞后点已消除 |
| layers/state-machine.md | PopupType 标注 5 值（测试证实 6 值含 Anr） | enum 值数认知错误 |
| locked-enums.md | ✅ 已同步（PopupType=6 正确） | — |

## S2 测试推断 — "测试说了什么"

> 2026-08-05 首次全量：18 个 StateMachine 测试文件人工审查 + test_contract_extractor.py（输出已人工校验）。

### 矩阵证据（StateMachineTests.cs + harness）

- 正面：NodeSelect→Branch（空栈）/PreconditionCheck（有栈）；Branch→NodeSelect/FrameComplete/ErrorHandling（harness 注释确认直接边）；Execute→ResultVerify/ErrorHandling；PreconditionCheck→Execute/ErrorHandling；ResultVerify→Branch/PopupHandling；FrameComplete→NodeSelect；ErrorHandling 5 出边全被策略测试覆盖；PopupHandling→ResultVerify/ErrorHandling。
- 负面：NodeSelect→Execute 拒绝（Assert.Throws DVE）；PreconditionCheck→Branch 拒绝（D-1）。
- **FsmSimulationHarness.ReenterErrorHandling 注释直接写 "19-edge D-1 matrix"**——测试层确认 19 边矩阵。
- GlobalFSM 负面：Error→Traversing、Recovering→Traversing、Idle→非 Initializing、Completed/Terminated 出迁全部拒绝。

### Handler 契约（决策表详见 fsm-design.md §3）

- NodeSelect: 栈空→Branch；有栈→PreconditionCheck
- PreconditionCheck: assume pass→Execute + trace；checker false→ErrorHandling（回归测试）
- Execute: 成功/NoAction/Restore 失败/返回 false→ResultVerify；异常→ErrorHandling+LastError；null ctx→stub ResultVerify
- ResultVerify: 通过/3 轮全失败→Branch；弹窗→PopupHandling；null ctx→stub Branch
- Branch: D-20 矩阵 6 场景全部有测试；无 VisitedChildren 记录→全视为未访问→NodeSelect
- FrameComplete: 恒定 NodeSelect；弹栈在 StepOrchestrator（D5 注释）
- ErrorHandling: 5 策略映射全部有测试；**所有策略都 +1 ConsecutiveErrors（Backtrack 不重置）**；pipeline 异常兜底→Abort→FrameComplete；null ctx→stub NodeSelect
- PopupHandling: Success→ResultVerify；Failure→ErrorHandling；null ctx→stub ResultVerify；顶层异常→back_fallback（H-8）

### 门限值（回归测试 + 单测）

- ConsecutiveErrors ≥3 → PressBack（trace `error_recovery_press_back`）；NodeFailedItems ≥5 → PressBack（trace `error_recovery_page_item_limit_5`，depth>1）
- **成功验证重置 ConsecutiveErrors**（回归测试注释明确）
- MaxDepth=10（NodeStack）；MaxRetries=3（默认）；退避 min(2^attempt, 10s)
- ResultVerify 3 轮重试；MaxSteps 引擎配置（实测 120）；advisor 置信度 0.7 门限（harness 注释）
- PopupType 6 值（含 Anr：isn't responding/keeps stopping；Wait 按钮→AutoClose，无→Back；Urgency High + Modal）

### 测试盲区（= 重构 T1-T6 已全部落地）

- ✅ **T1-T6 全部落地（2026-08-05 22:10-22:20）**：StateMachineTests（T1 SafeDegradeToFrameComplete + T1a SafeDegradeToBranch + T6 DeadEdges_Rejected 6 条拒绝 + 2 条正向）；HandleErrorHandlingTests（T2 LastError 清零 3 子用例 + T3/T3a 完整周期只 +1 双路径）；HandlePopupHandlingTests（T4 失败 LastError + T5 钩子触发）
- ✅ harness 注释更新："19-edge D-1 matrix" + ReenterErrorHandling 显式路由（FrameComplete→NodeSelect→PreconditionCheck→ErrorHandling）
- **仍无测试**：PressBack 后 ConsecutiveErrors 是否重置；双 gate 同时触发优先级；NodeFailedItems 递增点在测试中手动调用（FSM 外）；ISSUE C 步数燃烧（设计文档化，无测试）
- test_contract_extractor.py 输出含驱动路径假阳性（见盲区 10）

## S3 独立 FSM 设计 — "我设计的 FSM"

> 🔑 完整设计见 `fsm-design.md`（v0.2.0，2026-08-05 首次全量）。关键摘要：

- **8 状态语义**: NodeSelect=选节点；PreconditionCheck=前置检查；Execute=执行操作；ResultVerify=验证结果（3 轮重试）；Branch=分支决策；FrameComplete=帧完成（纯返回 NodeSelect）；ErrorHandling=错误恢复（5 策略+双门限）；PopupHandling=弹窗处理（6-step pipeline）。
- **19 边矩阵**: 与 patterns 完全吻合；每条边有推导依据（测试证据或设计意图）；6 条拒绝边有负面证据。
- **8 个 handler 决策表**: 全部有测试支撑（HIGH 置信度），仅 ResultVerify→ErrorHandling 显式生产者 MEDIUM。
- **树结构**: NodeStack MaxDepth=10；容器/叶子（ChildrenStrategy）；FrameComplete 回退 + PressBack 熔断回退 + 拦截层回退三机制。
- **GlobalFSM**: 8 状态矩阵 + 两步终止 + 恢复路径 + ForceState。

## S4 运行时证据 — "实际发生了什么"

> ⚠️ 尚无 run 数据（本任务无 run 目录分析）。已知事实：
- TraceReplay 测试确认真实 run `20260805T052309367Z` 结局 = max_steps (120)（enumerate-settings-safely，R1 DynamicMatch FrameComplete=0 场景）
- 待有 run 目录后运行 `fsm_transition_path.py` / `fsm_cycle_detector.py` 比对

## S5 差距分析 — "应该 vs 实际"

> ⚠️ 待 S4 证据收集后填充。
