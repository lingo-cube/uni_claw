# Gate Recalibration Shadow Ledger（实验台账）

> 实验: `docs/design/uniflow-gate-recalibration-proposal-v0.2.md`（§2 分类定义、§3 协议）
> 所属 Change: `changes/GATE-001/state.md`
> 起始: 2026-09-20 · 出口: 2026-10-04 或 ≥30 个决策事件（先到者）
>
> **规则**：人工门照常执行，本台账只做并行记录，不改变任何现行流程。
> 每当出现一个「本会需要人裁决」的决策点，agent 在下表追加一行——
> 在人裁决**之前**填好建议分类与推导链，人裁决**之后**补 ruling/override/wait。
> 不删行、不改历史行；追加即事实。
>
> 分类速查（完整定义见 v0.2 §2）：
> - **C1 阻塞**：方向两可 / 无法由已接受工件唯一推出（含 Owner 边界、acceptance 产品语义、风险取舍、回退成本需人批）/ 推翻 ADR 或冻结不变量 / 不可逆外部影响 / 新开顶层谱系
> - **C2 干了再报告**：可推出（ADR/冻结不变量/已裁决 Decisions 经确定性推导或单一显然步骤）+ 零外部副作用 + 不碰冻结面 + 回退路径具体
> - **C3 机械判定**：有确定性判据，机器自判
>
> 「可推出」的推导链必须给出工件级引用（文件 §节 / 决策编号）。

## Events

| # | date | change | question | class | derivation | human ruling | override | wait |
|---|---|---|---|---|---|---|---|---|
| 1 | 2026-09-20 | GATE-001（批量 closure：CORE-008/010/012） | 三个 DECISION-HEAVY 规划/裁决记录型 Change，实质工作均由已 closed 的后继交付（CORE-011 544/544、CORE-013 567/567、ADR-0023 accepted），是否关闭 | C1（规则保留）／事实面 C2 | v0.2 §4 P-D′「DECISION-HEAVY 永久保留人工 closure」；但事实可推出性成立：CORE-011/013 closed 状态 + GATE-001 CORE-008 absorption 记录，单一显然步骤 | 全部批准（三选三，与建议一致） | 否 | ≈0（同会话批量，一次问答） |
| 2 | 2026-09-20 | CORE-016（立项 grill round 1，5 槽：域/依赖边界/验证范围/落点/反例处置） | 第二 realization tracer 的方向与边界裁决 | 场合 C1（Q1 域选择=方向）；Q2–Q5 各自 C2 | Q1 新谱系方向（触发器①）；Q2 由 realization 契约 §1「按 Core 契约实现的领域 realization」+ 独立域语义单一显然步骤；Q3 由 qspec §3 双测试 + NO_REAL_BUYER 最小开门；Q4 由 GEV-004 D1「纪律升级为 build 层强制」先例；Q5 由 CORE-011 先例（测试侧产证明，产品改动走后续 change） | 全按建议（5/5） | 否 | ≈0（一次问答） |
| 3 | 2026-09-20 | CORE-016 spec 评审 | spec v0.1 是否通过 | C1（语义判断：有效性检查归属、演化等价范围定义）；S3 收紧与 ResourceVersion 条件部分 C2/C3（append-only 纪律与代码事实可推出） | **事后补分类（协议偏差）**：用户转述异步评审，到达时未经预分类。S6 缺陷本质是 spec-claim-vs-code-fact 一致性问题（CanDispatch 实际语义 vs spec 声称），确定性核对可拦截 | CHANGES_REQUIRED 全盘接受，四项修正 | **是**（spec 主体两处被拒——真实高价值修正） | 异步 |
| 4 | 2026-09-20 | CORE-016 closure（批量） | DECISION-HEAVY change 验收全绿（579/579、四元组落档、review 三发现已修、零产品代码）是否关闭 | 事实面 C2／规则面 C1（P-D′ DECISION-HEAVY 保留人工 closure） | 事实可推出性：evidence 四元组齐 + 全量绿 + git 范围核对（同事件 #1 张力） | 批准关闭（与建议一致） | 否 | ≈0（同会话，一次问答） |
| 5 | 2026-09-20 | HOST-001（立项 grill round 1，5 槽：形态/闭包/journal 默认/入口/执法） | Product Host v0 的形态与边界裁决 | Q1/Q2 C1（产品形态、闭包边界=架构边界）；Q3 C2（CORE-014 Q4 显式移交本 change，约束集已定只解默认值）；Q4 C2（G23 双 Host 方向单一显然步骤）；Q5 C2（RFS-001 遗留债点名「待 Host 存在」） | Q1 新谱系产品形态（触发器①）；Q2 闭包=检验主张本体（「装配同一 Product Runtime 可运行」）；Q3 上游裁决 ADR-0023/CORE-014 Q4；Q4 roadmap G23 Human-selected 方向；Q5 RFS-001 state 明文债 | 全按建议（5/5） | 否 | ≈0（一次问答） |
| 6 | 2026-09-20 | HOST-001 spec 评审 #2 | spec v0.1 是否放行 IMPLEMENT | C1（Kernel 架构边界：驱动面可见性、freshness 产品归属——评审方正确拒绝在 Host change 内质补）；#1/#2 事实核对部分 C3（grep 源码即证） | **事后补分类（协议偏差，同事件 #3）**：异步评审到达未经预分类。#1/#2/#3 坐实（Leader 源码复核）；#4 驳回——评审树过期（DocsMetadata 修复 d5612615 之前），HEAD 复跑 579/579 | CHANGES_REQUIRED：三条裁决（association/freshness/驱动缝），IMPLEMENT_BLOCKED；A1/A2/A3 人裁决三选三全按建议（D6 产品默认 null / D7 前置 freshness realization / D8 前置可见性 change） | **是**（三条实质缺陷成立——又一次高价值修正）；另含一次对评审项的驳回（首次：证据=新鲜全量复跑） | 异步 |
| 7 | 2026-09-20 | FRS-008 mini-grill（1 槽：freshness 规则语义） | 产品 freshness 的授权关语义：锚点存在性 / 注入时钟+窗口 / 扩 Basis 加 revision id | C1（产品语义=授权关语义，触发器②）；形状支撑 C2（缝文档「无时间权威」约束 + Func<DateTimeOffset> 仓库先例把选项收窄） | seam 注释 Deferred ⑪ + FreshnessBasis(AsOf) 无 revision id + 四处 clock 注入先例 | 人回应「这是干嘛的」（未裁决语义）→ **过度上报**：推导链已足够硬，改按 C2 处置——按建议 b 执行（注入时钟+窗口），veto 窗口至下次触点 | 否（无反对；非主动裁决） | ≈0 |
| 8 | 2026-09-20 | FRS-008 + RUN-003 批量 closure | 两个 STANDARD 前置 change（freshness 7/7 + 白名单 1/1、全量 587/587、零回归、范围干净）是否关闭 | 事实面 C2 ×2（四元组齐、全量绿、git 范围核对）；规则面一期零自动关闭（P-D′ phase 1：全部 closure 过人） | 同事件 #1/#4 张力：closure 类决策事实可推出 | 批准关闭（二选二，与建议一致） | 否 | ≈0（一次问答） |
| 9 | 2026-09-20 | 组件化方向裁决（World 拆包与否） | Kernel 定位为 UI 运行时后，UIWorld 是否拆独立包 | C1（方向两可——推翻 Leader 在先的拆包建议与地图终点线）；约束推导 C2（单消费者包无买家 = NO_REAL_BUYER 单一推论） | 用户以本仓库自己的纪律否决 Leader 建议：Kernel=UI 运行时 ⇒ World 单消费者 ⇒ 拆包=仪式。附带修正：CORE-016 并未满足 CORE-015 升格条件（未消费 UiRealization 类型），此前「已满足」为 Leader 误记 | **不拆包**（用户裁决，推翻 Leader 建议）；终点线修订为 Host 落地即完成 | **是**（高价值修正：拦下一个无买家拆包 + 纠正两处记录错误） | ≈0 |

## 边界案例池（「单一显然步骤」判例积累）

- **2026-09-20 · 事件 #1**：DECISION-HEAVY closure 在事实面可推出（后继全 closed、证据绿、残余无），但 P-D′ 规则将其永久保留给人工——出现「规则面 C1 / 事实面 C2」的张力。人裁决与事实面建议一致（零 override、零等待成本）。校准报告需决定：规则保留是否过宽，或是否正是「闭门类决策」本就该从 C1 排除的信号。

## 里程碑

- 2026-09-20 · 实验启动（GATE-001）；首批批量 closure 候选：CORE-008 / CORE-010 / CORE-012。
- 2026-09-20 · 事件 #2：CORE-016 立项 grill 5/5 按建议、零 override。校准注意：Q1 是有真实备选的方向题仍一轮过——前置分析充分时「一轮过」未必是橡皮章，但也提示 Q2–Q5 类问题（推导链在案）在正式期本可不自决而未自决；报告期统计「C2 类槽占比」。
- 2026-09-20 · 事件 #3：spec 评审（汇聚点类触点）产出**真实修正**（override=是）——对 09-19「汇聚点=橡皮章」假设的反例。关键区别变量可能是「提交前是否做过机器事实核对」：S6 缺陷属 spec-claim-vs-code-fact 一致性，确定性检查（引用的 API 语义 vs 源码）可在提交前拦截。校准报告考虑：spec/docket 提交前置一道确定性事实核对，可再降 C1 触点率。
