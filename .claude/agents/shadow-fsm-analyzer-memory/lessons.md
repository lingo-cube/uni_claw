# shadow-fsm-analyzer 案例经验

> 每次分析/对战/推断后精简追加：日期 + 来源 + 事实/方法/局限。同主题合并，重复不追加，错误认知立即纠正删除。每条 ≤3 句。

## 2026-08-06 — D-G11 子页面滚动语义分析

- **maxDepth 与滚动是两个正交维度**：深度约束（dfs-depth-constraint spec + L2/L3 测试）管"树下降（Push 帧）"，滚动管"帧内内容揭示"——滚动不产生新 frame、不跨深度；子页面滚动查看（不点击）不违反 maxDepth。MAXDEPTH 应约束 A（不点击进入更深的页面），非 B（不滚动）。
- **D-G11 结果正确、理由错误**：enumerate 契约（scenario catalog spec）语义是"采样"（"sample safe read-only pages"、"visible menu items"、successCriteria 只查一级条目、maxScrolls=12 预算容不下 11 子页滚动）→ 子页面不滚动是需求一致的；但 D-G11 用 `depth >= maxDepth` 编码是机制事实（maxDepth 处滚动揭示的子节点 push 被拒 → 滚动无访问价值）误当需求投影——侥幸正确。
- **D-G11 无需求锚点也无契约测试**：只存在于 e2e-dedup-vision-quality PRD 指标表 + 代码，无 spec 条目；仿真 fixture 均不可滚动，无法表达"depth=2 可滚动页"场景。若产品意图是"子页面内容记录全"，需求本身歧义（"visible menu items" vs "all items"）必须先澄清——当前正确是预算偶然，不是规格精确。

## 2026-08-05 — Agent 初始化

- Shadow FSM analyzer agent 创建。与 fsm-analyzer（源码优先，L1-L4 自下而上）正交：需求优先（S1-S5 自上而下），刻意不读 C# FSM 源码以保持独立视角。
- 核心约束：绝不读 `TraversalFSM.cs`、`GlobalFSM.cs`、handler 实现、引擎/拦截层源码。信息来源 = 需求文档 + 测试代码 + trace/log。
- 核心产出 `fsm-design.md`：从需求和测试凭空设计的 FSM 模型。fsm-analyzer 的 `matrix_from_source.py` 不可用（它读源码）——必须自己写测试推断脚本。
- 待首次 S1+S2 全量分析以填充初始 FSM 设计。

## 2026-08-05 — 首次全量 S1+S2 分析

- **文档矩阵存在两个版本**：patterns/fsm-design.md 已更新为 19 边（2026-08-05 22:07），但 openspec spec 和 charter §3.1 仍是 22 边旧版。推断矩阵时必须优先 patterns（Tier 2 紧跟代码），spec 滞后要显式标注。
- **test_contract_extractor.py 输出不可直接采信**：其 valid_transitions 通过顺序追踪 TransitionTo 链推断（含测试驱动路径），把拒绝边（NodeSelect→Execute）误报为 valid；handler_returns 按 before_context 猜测 handler 名不可靠。用途=线索+门限，矩阵结论必须人工校验测试源码（本次 19 边矩阵靠 harness 注释 + 单测断言人工确认）。
- **测试 harness 注释是高价值证据**：FsmSimulationHarness.ReenterErrorHandling 注释直接写 "19-edge D-1 matrix" 并列出各状态的矩阵合法路径——测试层独立确认了矩阵结构，比测试断言更有全局性。
- **门限值藏在回归测试注释而非断言**：ConsecutiveErrors≥3 / NodeFailedItems≥5 / 成功验证重置计数，全部只在 FsmSimulationRegressionTests 的注释和 trace action 名（error_recovery_press_back / error_recovery_page_item_limit_5）中出现。
- **C-11 dfs_properties（RootFirst/ParentBeforeChild/BackAfterForward）是树结构验证的契约维度**——树推理可与仿真 baseline 交叉验证。

## 2026-08-05 — Battle #1: 首次源码交叉验证

- **Shadow 独立设计 vs 源码 = 高度一致**: 矩阵 19/19 边精确匹配、8/8 handler 核心路径匹配、异常路由+双门限+LastError 全部 SOURCE-VERIFIED。证明从需求+测试凭空推导 FSM 的方法是可行的——前提是 S1 需求文档齐全 + S2 测试覆盖充足。
- **文档声称 ≠ 源码实现**: spec 声称 ResultVerify "3-round retry"，源码实现为 2 轮。优先相信 patterns (Tier 2，紧跟代码) 而非 spec/charter (可能滞后)。同理，修复状态需区分"设计已定" vs "代码已落地" vs "测试已验证"三层。
- **异常路由边是第三种矩阵边类型**: handler 显式返回边（矩阵主要消费方）、死边（handler 不返回 + 异常路由也不可达）、**异常路由边**（handler 不显式返回但 StepAsync catch 可达）。盲区 #1 的 ResultVerify→ErrorHandling 就是异常路由边——与死边的区别是运行时可达。
- **测试 harness 注释比测试断言更有全局视野**: FsmSimulationHarness.ReenterErrorHandling 注释直接写 "19-edge D-1 matrix" 并列出所有合法路径——一条注释比 10 个分散的 TransitionTo 断言更完整。利用此类"设计注释"是高效的信息提取策略。
- **Battle 方法论**: 不要让 shadow 看源码行号——让 shadow 只描述自己的 FSM 模型，然后由人类/源码侧逐项比对。共识点提升置信度到 SOURCE-VERIFIED；差异点分类为文档滞后 / 源码已修但测试未补 / shadow 推理错误 / 真正的设计分歧。

## 2026-08-05 — Battle #2 准备: ConsecutiveErrors 增量语义

- **重构测试已部分落地**（HandleErrorHandlingTests.cs mtime 22:19 比记忆新，刷新检查捕获）：T2（LastError 清零 3 子用例）+ T3/T3a（完整周期只 +1，Execute catch 与 StepAsync catch 双路径）已存在。修复状态三层（设计已定/代码已落地/测试已验证）中 T2/T3 已达第三层，T4/T5/T6 仍在第二层。
- **递增→判定顺序从测试内部推断**：T2 2c（ConsecutiveErrors=2 → Backtrack +1 → 3 → gate 触发）证明递增在 gate 判定之前——若判定在前则 2<3 不会触发。
- **双 gate 优先级无直接测试**：page-item(608) vs consecutive(621) 判定顺序只能从设计文档 line 号 + T2 子用例顺序推断（MEDIUM），"同时触发"场景无测试——这是问 fsm-analyzer 的候选问题（源码事实）。
- **NodeFailedItems 语义从测试注释而非断言推断**："5 个不同 frame"（2b）+"distinct frame per iteration"（回归）→ 页内不同失败帧计数；递增点在 FSM 外（测试手动调用），FSM 只判定——与 ConsecutiveErrors（事件粒度、恢复尝试次数）正交。

## 2026-08-05 — Battle #3 准备: FSM-as-CPU 架构

- **刷新检查捕获文档收敛**: openspec spec 22:31 已同步 19 边（D-240）+ PopupType 5→6 + T1-T6 全部落地——"spec 滞后"知识过时，三层文档（spec/patterns/charter）首次一致。刷新检查的 mtime 比对机制验证有效。
- **refactor §1.2 "矩阵双重职责"是 CPU 隐喻的最强背书**: 设计文档自己把矩阵拆成 Handler 门 + 异常路由门，异常路由走独立降级通道 = 我的"矩阵=opcode 邻接表、降级链=IVT"论断的文档级确认（HIGH）。
- **e2e-dedup-vision-quality（新 change）给 visited 集升级灵感**: verification_passed 时记录 (parentNodeId, destinationFingerprint)，sibling 重复目的地跳过——"两个 nodeId→同一物理页"用内容寻址解决，正是 Memory 模型 .visited 分区 + .device 帧身份的结合（R1 根因族）。
- **约束=创新边界**: e2e-dedup 的 Non-Goals（不改矩阵、不操作栈、不改 handler 签名）说明当前 FSM 边界是政治性稳定边界——CPU 架构提案必须保持"不改变 C-1 8 值"以兼容加固成果。

## 2026-08-05 — Battle #5 准备: 指纹去重方案对抗评审

- **纯类型文件藏关键公式**: PageAnalysis.PageFingerprint（计算属性，非实现逻辑）揭示了现有指纹公式 = (Type,Name) 排序多重集哈希。这个"允许读"的文件比任何文档都准确地回答"指纹怎么算"——类型文件优先于文档。
- **方案一（Container 文本去重）与现有指纹高度重叠**: PageFingerprint 已内建排序归一化 + 确定性哈希；方案一的真正增量 = 内容选择（类型过滤/动态值排除）+ PageTitle 维度。若只换哈希公式，增量≈0 还丢 Type 区分度。
- **方案二（Node ID 归一化）不解决方案一的问题**: Node ID 稳定=同一元素跨帧关联；D-G12 解决"两个 nodeId→同一物理页"。两者正交互补，但"无文本回退相对位置"与方案一"视觉指纹兜底"是两套兜底键——必须统一。
- **动态文本是方案一的存在性前提**: On/Off 切换→Name 变→PageFingerprint 变→D-G12 判重失效。方案一要回答"V4 修复后还剩什么不稳定"，若答案是动态值，类型过滤必须先行。
- **已落地机制不可破坏**: D-G12（L6-1/L6-2）+ V1-V4 测试绑定了现有指纹语义；方案一若换键必须同步迁移 fixture——这是最大的实现风险面。

## 方法论笔记

- **区分硬约束与设计意图**：constitution 条目（C-*）是硬约束，必须无条件满足。patterns/refactor 条目（D-*）是设计意图，理解动机但设计可独立推导。
- **测试是行为 oracle**：测试断言了什么，什么就是契约。测试编码了期望行为——比自然语言需求更精确。但测试也可能编码了 bug（如 Bug #2 的测试手动绕过 catch 路径）。
- **差距类型分类**：S3 vs S4 差距四类——需求→实现保真度（一致）、需求未覆盖（设计有、实际无）、实现超出需求（实际有、设计无）、需求歧义（两者都合理但不同）。
- **Battle 准备**：与 fsm-analyzer 对战时，不看对方的源码引用——只看 FSM 模型描述。共识点 = 高置信度；争议点 = 调查目标。分歧根因分类：需求歧义 / 实现偏差 / 测试盲区 / 自己的推断错误。
