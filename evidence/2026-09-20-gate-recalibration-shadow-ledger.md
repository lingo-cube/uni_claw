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
| 10 | 2026-09-20 | UIW-005 立项方式（HOST-001 D6 回退点） | 自驱环路需铸根容器、产品默认（null）做不到（已实证）；a = Host 内 15 行临时件 vs b = 前置正式 realization | C1（D6 预登记的回退点；零件真伪=产品语义归属） | D6 裁决时 Leader 提供了错误事实（「产品默认路径存在」——存在但=不启用容器的旧路径；WorldModel:264 门实证） | **选 b**（推翻 Leader 推荐 a——正式零件先行） | 是 | ≈0（两轮 plain-language 重述后裁决） |
| 11 | 2026-09-20 | Host 外部件方针（用户主动纠正） | 外部组件（感知/执行/agent 智能）可先仿真/模拟实现；先跑通核心模型+能力接口；仿真流程作为正式能力完善（可复现、落盘可离线检视） | C1（方针级，用户主动指令非被动裁决） | 与 HOST-001 spec 既有三替身方向一致并升格为方针；新增可复现要求（RFS digest 先例） | 采纳为 Host v0 方针（spec v0.3 落档） | —（主动方向指令） | — |
| 12 | 2026-09-20 | UIW-005 + HOST-001 批量 closure | UIW-005（D4 装配修正后 7/7、全量 598/598）；HOST-001（首跑 Completed/Completion/EXIT=0、digest 复现、闭包 GREEN、四元组落档）是否关闭 | 事实面 C2 ×2（四元组齐、全量绿、git 范围核对）；规则面一期零自动关闭 | 同事件 #1/#4/#8 张力：closure 类决策事实可推出。HOST-001 落地 = 组件化终点线达成 | 批准关闭（二选二，与建议一致；ee790f7 落档 2026-09-21） | 否 | ≈0（裁决 2026-09-20 同触点；落档迟一天，HYG-001 2026-09-22 回填） |
| 13 | 2026-09-22 | HYG-001（用户主动指令：安全子集先落地） | harness 优化清单执行范围：陈旧引用 / open 索引 / 台账 #12 回填 / 载荷保留规则 / 本地日志（安全子集）立即执行；机械事实核对、docket 新鲜度、C2 正式化、skill 长尾留待校准报告后再议 | C1（方针级，用户主动指令非被动裁决）；各执行项 C2/C3（零外部副作用、回退 = git revert） | GATE-001 scope P-E′ 已含 open-gates 生成索引；#12 回填依据 ee790f7 + 两份 state.md 状态日志；载荷删除项被引用证据否决（docs/analysis/harness-v2-compatibility-matrix.md 等 5 份）改为保留规则 | 采纳（执行安全子集 HYG-001） | —（主动指令） | — |
| 14 | 2026-09-22 | ARCH-DOC-016（用户主动指令：把值得做的做完） | 软工价值裁决的执行范围：不变量×执法矩阵 + 完整性守护脚本落地；按矩阵补强化测试经证据判定为 NO_REAL_BUYER（高危组零缺口）；形式化证明文档不成文（NO_REAL_BUYER） | C1（方针级，用户主动指令）；各执行项 C2/C3 | 2026-09-22 会话价值评估（买家=评审/防漂移/AI coder 记忆）+ 高危组 grep 命中证据（TwoStepBarrierTests / Outcome9-12 / FreshnessEnforcementTests / KernelRunDriverTests 等） | 采纳（矩阵 + 守护落地） | —（主动指令） | — |
| 15 | 2026-09-22 | ARCH-DOC-017（用户主动指令：场景推理文档） | 数学/哲学推理的可落地化形态：与 VNext 文档族同放的场景推理文档，一两百个实际场景内做推理论证可靠性与设计正确性；采用 150 场景 × 13 等价类参数扫描法（决策 D1） | C1（方针级，用户主动指令）；产出为 C2/C3 文档工件 | capability-coverage-derivation A–E/F 判定谱系 + 不变量矩阵 47 行 + 既有测试锚点（✅122 项全锚定，空缺均为已登记 ◐/D 面） | 采纳（文档 + 结构守护落地） | —（主动指令） | — |
| 16 | 2026-09-22 | PER-009（立项 grill，三轮 12 问） | 多源观察与信任的立项裁决：并行常开全页面 / XML 标准控件终审 / CSS 级联底牌表 / Tier 2 触发公式 / 竖切 change / 60s×3 探测 / 常量类词汇 | C1（新能力方向 + 多项语义取舍；含用户两项对 Leader 初版的关键修正：XML 终审权、级联覆盖维度） | 基线 §24.3 native signal 合法性 + WorldModel L233/A4 冲突语义 + LiveVisionStrategy 合同核对（视觉不写 *.state，跨源碰头今为 0）+ Android 规范速览；全程推导链在 docket | 全按建议或用户修正落定（ADR-0027/0028 + PER-009 立项） | 否（两项用户修正为高价值：拦下深模型终审错误方向） | ≈0（同会话三轮） |
| 17 | 2026-09-22 | PER-009 修订（用户携官方文档主动校准） | Tier 0 判据由类名前缀改为「语义字段权威∧身份唯一解析∧dump 新鲜∧属性有效」；冲突分权威域内（XML 销案·confidence 盲·不升档）/域外（视觉域+升档）；升档触发改为「权威体系无法定案」与 confidence 脱钩；checked 三态（API 34+，目标机 api35 即时相关）；新鲜度门（dump 可能描述另一时刻） | C1（裁决语义修订，推翻 Leader 初版三处：类名判据、0.72-vs-OFF 升档例、二态假设） | 官方文档（AccessibilityNodeInfo/UiSelector/UiObject2）：checked 等为原生序列化非视觉推断；accessibility 树与 view 层级非一一对应；节点可 stale 需重取；tri-state getChecked() | 全盘采纳（D3 修订 + D12/D13/D14 新增 + D6/D7 升级；对抗域防线显式定位到级联覆盖+权限层） | 否（高价值：堵住 confidence 偷渡回权威体系的暗洞） | ≈0（异步文档调研后同会话） |
| 18 | 2026-09-22 | PER-009 rule-freeze（用户终审 + Leader 官方页校验） | 两处终审修正后冻结裁决规则：① checkable 不与 checked 并列——降为 checked 的 validity guard（checkable=false 时 checked 无权威）；② text 权威范围 = Accessibility 语义文本，非像素渲染字符真相（canvas/游戏/图片内文字/视觉截断=像素域） | C1（字段权威表终版 + 冻结指令）；官方页校验为 C3 机械核对 | Leader 抓取 developer.android.com 参考页逐字证实："This is only meaningful when isCheckable() returns true"；CHECKED_STATE_FALSE/TRUE/PARTIAL；"isChecked() deprecated in API level 36"（连带纠正 Leader 此前 API 34 错误；api35 真机 dump 仍布尔，partial=前向兼容通道） | 采纳并冻结（D3/D12/D7/Acceptance#12 定稿；实现期改动需新裁决） | 否（含对 Leader 版本事实错误的纠正） | ≈0（异步调研后同会话） |
| 19 | 2026-09-22 | PER-009 mechanism-freeze（用户终审流程图） | 机制图两处修正后冻结：① 观察层措辞按冻结字段表重写（状态/能力字段分离，防 checkable 被误读为"开关开着"）；② 事后 XML 验证从"标准控件+XML可用"收紧为四门（权威域 ∧ dispatch 后重新唯一解析目标节点——防同名 Switch 错配假验证 ∧ PropertyValid ∧ dump 时序在 dispatch 后；事后新鲜度=时序约束≠操作前窗口） | C1（机制图冻结 + 假验证 bug 类预防裁决） | 六原则与七段职责链用户确认；同名控件错配 = SR-035 身份失效类搬进验证路径，四门在结构上堵死 | 采纳并冻结（mechanism.md 落档：七段链/六原则/主图/速查/实现纪律） | 否（第二处为高价值：拦下一类假验证成功 bug） | ≈0（同会话终审） |

## 边界案例池（「单一显然步骤」判例积累）

- **2026-09-20 · 事件 #1**：DECISION-HEAVY closure 在事实面可推出（后继全 closed、证据绿、残余无），但 P-D′ 规则将其永久保留给人工——出现「规则面 C1 / 事实面 C2」的张力。人裁决与事实面建议一致（零 override、零等待成本）。校准报告需决定：规则保留是否过宽，或是否正是「闭门类决策」本就该从 C1 排除的信号。

## 里程碑

- 2026-09-20 · 实验启动（GATE-001）；首批批量 closure 候选：CORE-008 / CORE-010 / CORE-012。
- 2026-09-20 · 事件 #2：CORE-016 立项 grill 5/5 按建议、零 override。校准注意：Q1 是有真实备选的方向题仍一轮过——前置分析充分时「一轮过」未必是橡皮章，但也提示 Q2–Q5 类问题（推导链在案）在正式期本可不自决而未自决；报告期统计「C2 类槽占比」。
- 2026-09-20 · 事件 #3：spec 评审（汇聚点类触点）产出**真实修正**（override=是）——对 09-19「汇聚点=橡皮章」假设的反例。关键区别变量可能是「提交前是否做过机器事实核对」：S6 缺陷属 spec-claim-vs-code-fact 一致性，确定性检查（引用的 API 语义 vs 源码）可在提交前拦截。校准报告考虑：spec/docket 提交前置一道确定性事实核对，可再降 C1 触点率。
