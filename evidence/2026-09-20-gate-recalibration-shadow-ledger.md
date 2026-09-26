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
| 20 | 2026-09-22 | PER-009 S6 接口级裁决（用户主动裁决"长期架构更优者胜"） | 聚焦复查传导通道二选一：A=经 Kernel 驱动面（ObservationDirective，白名单授权 +2）vs B=Host 侧编排（零面变更）。用户问"长期是否 A 更优，是则执行" | C1（公开驱动面变更授权） | Leader 三条长期论证：①Observation Control 是 Control Loop 宪法权威（baseline §14），B 会把权威执行挪进组合根；②双 Host 对称（§24.8）——Host 层编排需仿真 Host 复刻，Kernel 缝则生而同构；③mechanism.md 冻结图本就写着 ObservationRequest 经 Control 发起——A 是实现已冻结设计，B 是 Leader 为省面造的偏离 | 用户按长期架构标准选 A，立即执行（S6a 落地：缝迁移 + 驱动面聚焦分支 + 白名单 +2） | 否 | ≈0（同会话一问一答） |

| 21 | 2026-09-22 | RUN-004 mini-grill（1 槽：裁决⑧ 完成证明路径） | Agent「做完了」算数吗：全信/全不信/核验后信 | C1（产品诚实性语义=授权关同族）；设计三方案真实分叉（语料 SR-073/074/150 判例压力） | 前序十轮用户实质 grill 已覆盖边界/粒度/协议/状态机核验（本事件仅剩真分叉——校准正例：grill 该多小就多小） | **c 核验后信 + 人为终极裁定**（用户加第三层：锚不住时升级人工裁定——新合法等待态，TRW/RFS awaiting-human-review 先例） | 否（采纳推荐并加严） | ≈0 |


| 22 | 2026-09-22 | RUN-004 spec 评审 #1 | spec v0.1 是否放行 IMPLEMENT | C1（白名单授权纪律 + 完成证明闭环=产品诚实性）；claim-vs-fact 部分可机核 | 评审方逐行核对 7 源码文件+白名单条目：抓出 blocker 算术错（+6 实为 +9）+ 预算通道断裂（View 无字段=驱动器读不到合同预算）+ 层2 锚点无归档源（自封完成后门面） | CHANGES_REQUIRED：1B+9maj+6min，v0.2 全项处置（处置表 spec §9） | **是**（第四次高价值评审修正） | 异步 |


| 23 | 2026-09-22 | RUN-004 spec 复评（评审 #2） | v0.2 处置核验 + 复评 | C1（claim-vs-fact 持续执法）；复评专属问题四项定向复核（新 claim/表层里层/RED 可构造性/投机复查） | 评审方逐行核 v0.2 新增内容自身的事实性；判定 12/4/0/0——收敛型复评的范式：上轮 16 项零回退，残留全为文字收口无设计改动 | CHANGES_REQUIRED（收口型）：F5 幂等不可达 + G2 折抵落点缺失两 major → v0.3 闭合 | **是**（第五次高价值评审——且首抓「承诺在既有代码下不可达」的可达性类缺陷） | 异步 |

| 24 | 2026-09-23 | RUN-004 spec 评审 #3（实现后首审，HEAD b4b6865c） | v0.3 处置表 vs 实现逐行核对 | C1（claim-vs-fact 持续执法）；首次实现层核对 | 五 major：F5 预算链（AdmitContract 不解析/无签名比较/无 B1B2——合同声明丢失）、M1/T6（无 MaxRounds/无 DeferRoundsExhausted/豁免不可达/defer-exhausted 缺失——验收 9 无可行轨迹）、G4（nbounds 两侧缺席）、验收 4/6/9/10/11 零测试承载、StepRejected 相位恒不可达（PhaseForCurrent 恒 VerificationFailed）；另层3 rejected 无持久 marker | CHANGES_REQUIRED 维持；**§10 两条 closed 被代码证伪——「处置表 ≠ 仓库真相」首例**；用户裁决：验收承载升 RED-first P0 Gate、rejected 升 P1/P2；放行条件四条；Gate 0-7 修复顺序（Gate 0 文档 claim 纠正已落，见 spec §11/state status） | **是**（第六次高价值评审——首抓处置表 closed 与实现缺席不一致） | 异步 |

| 25 | 2026-09-23 | UAR-003（立项 grill，6 槽：D0 推荐组合 / D1 adapter 宿主 / D2 绑定文件 / D3 强制闭环 / D4 DSH_HOME 与凭证 / D5 sandbox+C8） | DSH 嵌入 R1 前置决议：批准研究推荐组合 + 裁决研究 §9 五未决问题 + 产出 Tracer Bullet 预定授权边界 | C1（方向两可——DSH 轨道形态与授权边界=产品语义取舍，触发器①新战线开工）；D2/D3 各自 C2（研究 §5.2 倾向 + MRB-001/baseline 约束单一显然步骤）；D4/D5 混合（隔离落点可推出，最小化范围=ADR-0026 buyer-driven 推导） | 研究 §1–§8 全部一手证据（路径:行号 + [已核实] 标注）+ baseline §9 deferred 边界 + 用户 2026-09-23 前台裁决选「开 R1 决议 change（推荐）」（前序判断：DSH 实现不提前、SIM-002 队列不动摇） | D0/D2/D3/D4/D5 全按建议（5/5）；D1 用户委托「哪种符合架构正确性」→ Leader 依工件推导链（baseline §4 调用者不得依赖 Plugin/Profile/transport + §5 Host 物理保存 Product records 禁令 + ADR-0022 D4 + 研究 §3.4/§8.5 发布链路未决 + §5.2 Kernel 唯一 authority）裁定 a（C# 进程内），veto 窗口至下次触点 | 否（零 override；D1 为委托裁定非默认采纳） | ≈0（一次问答） |

| 26 | 2026-09-23 | UAR-003 closure | DECISION-HEAVY 决议 change（六槽裁决全留痕、Acceptance 1–5 CONTRACT 核验过、零实现改动、Review PASS）是否关闭 | 事实面 C2（四元组齐 + git 范围核对 + 台账 #25 同步）；规则面 C1（P-D′ DECISION-HEAVY 保留人工 closure） | 同事件 #1/#4/#8/#12 张力：closure 类决策事实可推出，规则永久保留 | 批准关闭（与建议一致，零 override，同会话一次问答） | 否 | ≈0 |

| 27 | 2026-09-23 | RUN-004 Gate 1 ACCEPTED + Gate 2（F5 修复） | RED-first 验收测试 + F5 预算链修复 | C1（验收可执行化；claim→可执行失败→再转 GREEN 的正向闭环） | Gate 1：6 测试 = 5 有效 RED + 1 GREEN（E3 对照），**V5 自引用新发现**（首个 Defer 即被拒——验收 4 连带不可行）；Gate 2：RunModel 三处落地（View 解析、合同签名幂等比较、MintRunId B1/B2），两个 Acceptance2 RED → GREEN，其余保持 RED；Kernel 463/3（3 = 保留 RED）；Sim 3 失败 stash 隔离证明 pre-F5（import/redrive `AgentConsultations` 1→2 旧期望未升档） | ACCEPTED（V5 记 Gate 3）；Gate 2 目标达成 | **是**（RED-first 首次把「处置表≠代码」转成稳定失败再转 GREEN 的完整闭环） | 异步 |
| 28 | 2026-09-23〔补录 2026-09-26〕 | AGENTS.md §1.6 工作汇报规则（23bca220） | 一切对人汇报改 programmer-style handoff 格式（唯一真相源 `docs/agents/work-reporting.md`；ID 索引形态保留） | C1（方针级，用户主动指令——同 #13–#15 族） | 骨架六段：做了什么→原来的问题→这次修改→验证结果→新发现→下一步；AGENTS.md §1.6 自注「2026-09-23 所有者指令；对所有工作生效」 | 采纳为常设规则 | —（主动指令） | 未记录（与 #27 同晚相邻触点） |
| 29 | 2026-09-23〔补录 2026-09-26〕 | SCN-002 closure（1bcc510e） | 生成式场景能力（Acceptance 1–6 于 fb3d6d87 复验绿 + 存量 RED 基线 23bca220 worktree 归属复验）是否关闭；closure 边界四项界定 | 事实面 C2（四元组齐 + 全量绿 + git 范围核对）；规则面 C1（P-D′ 决策重型保留人工 closure） | 同 #1/#4/#8/#12 张力；commit 显式「pending separate owner ruling per P-D′」——SIM-002 不捆绑 | 批准关闭（与建议一致） | 否 | 未记录（同会话） |
| 30 | 2026-09-23〔补录 2026-09-26〕 | SIM-002 closure（5b8860ce） | 仿真基线合规（G1–G4 + S5/S6/S8 执法面于 fb3d6d87 复验绿）是否关闭；按 owner 指示以自身验收证据独立判定、与 SCN-002 不联动 | 事实面 C2／规则面 C1（同上） | 同 #1 张力；owner 指示「独立于 SCN-002 判定」→ 两次独立裁决而非批量（边界判例见案例池）；S8 安全清审随本 change 由 owner 复核 | 批准关闭（「owner ruling this turn authorizes closure」） | 否 | 未记录（SCN-002 后同夜下一轮）〔**= 第 30 个事件，≥30 出口阈值在此越过**〕 |
| 31 | 2026-09-24〔补录 2026-09-26〕 | RUN-004 closure（7a801e67） | 多轮咨询协议（十完成条件 + 11 acceptance，16ce8b44 复验 691/0/0）是否关闭；G4 Elements XML 增强 DEFER | 事实面 C2／规则面 C1（P-D′） | 同 #1 张力；协议冻结 + DEFER 触发条件登记（spec §12.2）先行 | 批准关闭（与建议一致） | 否 | 未记录 |
| 32 | 2026-09-24〔补录 2026-09-26〕 | AGT-001 立项 adversarial grill（a23511c0） | UniAgent runtime 架构八面 grill + GQ1–GQ4 裁决（AbortCurrentTurn 通道 / D1 范围 / 预算语义 / schema 权威） | C1（新谱系架构方向，触发器①；GQ3/GQ4 语义判断） | 对抗 grill PASS_WITH_FINDINGS（F1–F7）；裁决 GQ1=A（带外中止、authority-bounded）/ GQ2=B-DEFER（D1 收窄运行时生命周期）/ GQ3=YES（语义尝试耗预算、传输重试不耗）/ GQ4=YES（.NET records 单一 schema 权威 + schemaHash 握手） | 四裁决落定，v0.2 修订（CONTEXT.md +2 词条） | 否 | 未记录（同会话） |
| 33 | 2026-09-24〔补录 2026-09-26〕 | AGT-001 closure（4022ce40） | focused re-grill PASS（F1–F6 CLOSED、F7+Trace/UI CONFIRMED、remaining=0）后是否 FROZEN 并关闭 | 规则面 C1（P-D′）；事实面 C2 | 同 #1 张力；设计稿升 FROZEN、DEFER 表在档、零 baseline 重开 | owner 接受裁决并授权 closure | 否 | 未记录（同会话） |
| 34 | 2026-09-24〔补录 2026-09-26〕 | RUN-005 设计 owner 预审（01a518ab） | L2 Policy 设计 v0.1 是否放行（owner 预审 × 零泄漏盲审双轨） | C1（产品语义：预算域/认知三态/FailClosed 基线/scope-lease 两难） | 预审 PASS_WITH_FINDINGS 与盲审 REOPEN 四处双命中根因互补；v0.2 删 MaxRounds/PolicyFallback/PolicyScope/ClaimNotEquals/ElementMissing/TextContains，加 PolicyTruth 三态表 + ClaimInSet + 良基 rank | 接受双审合并修订（真实高价值修正） | **是**（v0.1 六特性被删——双审双命中根因） | 异步（未记录具体耗时） |
| 35 | 2026-09-24〔补录 2026-09-26〕 | RUN-005 设计终裁（3c207d33） | v0.3 是否 FROZEN 放行实现 | C1（设计冻结 + 两项 Owner 决策） | PASS_WITH_ONE_NARROW_AMENDMENT：semantic lease 规则必补（adoption 绑 active execution lease，失效→PolicyInvalidated(LeaseInvalidated)→NeedDecision→零新 Effect）；Owner 决策 1 = PolicyInvalidated 统一 typed cause（不用 PolicyGuardTripped）；决策 2 = 独立最小 PolicyEvaluationView；GuardCursor warm-up≠Unknown | 采纳窄修 + 两决策落形；v0.3 FROZEN，Slice A→B→C | **是**（窄修实质修正：lease 缺口 + 命名裁决） | 未记录（同会话） |
| 36 | 2026-09-24〔补录 2026-09-26〕 | RUN-005 Slice A 终裁（v0.3.1 窄修） | Slice A 是否通过；PolicyPredicate.ElementExists 去留 | C1（v1 词汇语义：not-observed 不得绕 Termination Unknown 出口） | 终裁 PASS + 唯一修正项：删除 ElementExists（「看见→Satisfied / 没看见→Unknown」与终止 Unknown→PolicyInvalidated 出口矛盾；v1 无 coverage/completeness 语义；禁把 not-observed 改判 Violated 绕过）；owner 授权不重开设计、不再 grill | 采纳（PASS + 单项删除→DEFER） | **是**（删除一项已冻结词汇成员） | 未记录 |
| 37 | 2026-09-24〔补录 2026-09-26〕 | RUN-005 Slice B owner 裁决 | PolicyExpand / semantic lease / PolicyState 边界 / RUN-004 复用 / 预算失效五面是否过 | C1（step gate 冻结面执行核对）；机械证据面 C3 | 六项判定：PolicyExpand PASS · Semantic lease PASS · PolicyState boundary PASS · RUN-004 execution reuse PASS · Budget/invalidation PASS · Architecture deviation NONE | PASS，进 Slice C（禁令清单重申：不接 DSH/不扩词汇/不复活 ElementExists） | 否 | 未记录 |
| 38 | 2026-09-25〔补录 2026-09-26〕 | AGT-002 canonical 3080 验收（2f633b04） | 是否重启 owner 现役 3080 GUI 实例装载插件并以 canonical 端口执行正式 E2E | C1（owner 边界 + 现役服务外部影响，触发器②/④） | 唯一不可代理动作：重启 owner 实例须 owner 授权；3081 旁证实例已绿但 canonical 3080 = 现役面 | Owner 授权重启；live frozen-stamp handshake accepted（session-0124078e…）；正式 E2E 4/4 PASS | 否 | 未记录 |
| 39 | 2026-09-26〔补录 2026-09-26〕 | PER-010/011 Q1–Q11 设计裁决（05267a0e / 91a4af94） | typed hierarchy + fusion 设计树 11 问（checked 粒度 / Unknown 位置 / lineage owner / 有限覆盖 / Aligned 分歧 / derived 入证 / 视觉分轴 / 直冲销案 / exact 证明 / lineage 异常 / 迁移缺口） | C1（产品语义多项取舍，触发器②）；Q11 兼产品范围（PER-009 不重开） | `evidence/2026-09-26-per-010-per-011-grill-alignment.md` 原文：「Q1–Q11 已由**用户**选择推荐方案并完成裁决」 | 全按推荐（11/11）；Q11：前向设计 FROZEN、PER-009 不重开、dedicated migration decision 设为 PER-011 implementation 前置 | 否 | 未记录（同会话） |
| 40 | 2026-09-26〔补录 2026-09-26〕 | PER-012 Human Gate（400d7b53） | semantic migration contract：typed-only 前向语义 / egress-only legacy 面 / 无损投影 / 逐 consumer cutover / routing rollback / legacy 删除条件 / PER-011 实现门 | C1（迁移边界 = 权威切换语义，触发器②/④） | contract 全条目 + fixture matrix（plans/2026-09-26-per-012-…）；state 原文「Human Gate 裁决 typed-only forward semantics…；PER-009 不重开」 | Human Gate accepted；contract 与 fixture matrix FROZEN | 否 | 未记录 |
| 41 | 2026-09-27〔仓历；补录 2026-09-26〕 | PER-013 立项 owner 指令 | typed observation 迁移执行范围：PER-010 adversarial grill 17 findings 全量随 change 携带；slice 序「Gate 0→Slice A→Gate A，PASS 后汇报，不跨 Slice B」 | C1（方针级，owner 主动指令——执行范围与节奏） | PER-013 state/plan 原文「owner 指令 findings 全量随本 change 携带；spec/plan 按 owner 最终版 slice 指令落档」 | 采纳（按指令落 spec/plan 后开工） | —（主动指令） | 未记录 |
| 42 | 2026-09-27〔仓历；补录 2026-09-26〕 | CSC-001 OWNER_GATE 主裁决（e8250650 / 3dca53b4） | 十项汇报终裁：方向/权威/scope；跨 dispatch wm-size 缓存去留；Host 1080×2400 magic fallback 去留；closure | C1（产品语义冻结 + owner 边界） | Owner 裁决：Architecture direction PASS · Authority PASS · Scope NONE；**跨 dispatch 缓存：拒绝并令移除**（必改五项之首）；fallback：ACCEPT REMOVAL AND FREEZE（viewport 只能来自 实测 > 显式已验证配置，无 fallback 常量）；Final closure HOLD 待五项 | 五项必改全部执行（缓存移除 / 动态 viewport 回归 / 词汇修正 / 四元组 / 复跑） | **是**（推翻 agent 已实现的跨 dispatch 缓存设计） | 未记录（HOLD→终裁跨五项执行期） |
| 43 | 2026-09-27〔仓历；补录 2026-09-26〕 | CSC-001 终裁 CLOSED（3dca53b4） | 必改五项验收通过后是否 CLOSED | 规则面 C1（P-D′）；事实面 C2（五项机械证据齐 + 1015/1015 + 再认证） | 终裁依据 = owner 对后继 change 的显式 base 指令（「Base: CSC-001 CLOSED 后最新 HEAD」） | CLOSED（终裁留痕、提交固化） | 否 | 未记录 |
| 44 | 2026-09-27〔仓历；补录 2026-09-26〕 | CSC-002 Owner 终裁（b9fdf2b8） | A session cache + evidence-driven invalidation / B explicit config validity boundary / C single normalized resolver 三问 + 全量/认证/真机 | 事实面 C2／规则面 C1（P-D′） | A/B/C 全 PASS + 1027/1027 + certification 29/0 + live 三件套 PASS；non-blocking gaps 两条按 Owner 原文记录 | FINAL VERDICT: CLOSE | 否 | 未记录 |
| 45 | 2026-09-27〔仓历；补录 2026-09-26〕 | CORE-004/005/006 联合 closure（f37fb994） | 三个 DECISION-HEAVY 对齐 change（acceptance 6/6、7/7、8/8 机械证据 + 交叉权威审计）联合 OWNER_GATE 审阅是否关闭 | 事实面 C2／规则面 C1（P-D′） | 同 #1 批量张力：一次联合审阅出三个独立 verdict；冻结边界已被 PER-013（HierarchyCaptureDescriptor 经 CORE-005 缝）与 CSC-001/002（Space 字段同通道）事实上当 upstream authority 消费 | 三个 verdict 全 PASS，全部关闭（**计 1 事件**——批量判例见案例池） | 否 | 未记录 |
| 46 | 2026-09-27〔仓历；补录 2026-09-26〕 | SIM-001 Owner final closure（8ad805ec） | SeamOverrides 六缝（Review Gate R1–R7 全 PASS + 1029/1029 + 零产品代码改动）是否关闭 | 事实面 C2／规则面 C1（P-D′） | 行为级（InjectedFreshness WasCalled / Driver 计数）+ 组合级（六默认组合证明）证据 | CLOSE（闭门前两处文档措辞更正一并执行：Continuity null 透传语义、注释 1→2 consultations） | 否 | 未记录 |
| 47 | 2026-09-27〔仓历；补录 2026-09-26〕 | SCN-001 superseded closure（d3ac93da） | 场景库 Phase 1 原计划是否仍实施（duplicate-build 风险）；如何关闭 | C1（方向裁决：禁止按原计划实施）+ closure | supersession 表逐条映射：四条 scope 已由 SIM-002/003 + SCN-002 + SIM-001（均 closed）以更强形态交付；A2 按 D2 措辞过时显式处置（SUPERSEDED WITH STALE WORDING，不宣称旧字面 acceptance 通过） | READINESS PASS · **禁止按原计划实施**（duplicate-build risk HIGH）· DOCS-ONLY CLOSE（superseded） | 否 | 未记录 |

## 边界案例池（「单一显然步骤」判例积累）

- **2026-09-23 · 事件 #25**：D1 委托裁定形态——用户以「哪种符合架构正确性」
  把选项裁决转换为架构推导题（非弃权、非默认采纳）：Leader 依工件级推导链
  （baseline §4/§5 + ADR-0022 D4 + 研究 §3.4/§8.5）裁定并以 veto 窗口
  兜底。与事件 #7（推导链已硬→C2 直行）同族但方向相反：#7 是推导链足够
  硬以致无需上报，#25 是上报后人工选择「用架构正确性替代人工偏好」。

- **2026-09-20 · 事件 #1**：DECISION-HEAVY closure 在事实面可推出（后继全 closed、证据绿、残余无），但 P-D′ 规则将其永久保留给人工——出现「规则面 C1 / 事实面 C2」的张力。人裁决与事实面建议一致（零 override、零等待成本）。校准报告需决定：规则保留是否过宽，或是否正是「闭门类决策」本就该从 C1 排除的信号。

- **2026-09-26 · 补录 #45（CORE-004/005/006）**：一次联合 OWNER_GATE 审阅
  出三个独立 verdict，按 #1/#8/#12 批量判例计 1 事件。commit 自述
  「three independent verdicts」与「joint review」并存——联合审阅到底
  计 1 还是 N，留给校准报告定口径。

- **2026-09-26 · 补录 #29/#30（SIM-002 / SCN-002）**：同夜相邻两轮 closure
  未按批量计 1，因为 owner 自己的指令（「独立于 SCN-002 判定、不联动」+
  commit「pending separate owner ruling per P-D′」）把它们定义为两次独立
  裁决——批量判例的适用前提（一次问答覆盖多选）被 owner 显式排除。

- **2026-09-26 · 排除项 10（PER-013 owner 反馈）**：owner 在 closure 后
  报缺陷（AVD FATAL / HostLiveFull 失败），agent 自主定位根因并修复。
  「owner 反馈触发修正」与「需要人裁决的决策点」的边界：反馈未要求
  裁决、修复可由证据推导（C2 类）→ 不计。若反馈中含有方向取舍则应计
  （对照 #9/#11）。此边界供校准报告细化。

## 里程碑

- 2026-09-20 · 实验启动（GATE-001）；首批批量 closure 候选：CORE-008 / CORE-010 / CORE-012。
- 2026-09-20 · 事件 #2：CORE-016 立项 grill 5/5 按建议、零 override。校准注意：Q1 是有真实备选的方向题仍一轮过——前置分析充分时「一轮过」未必是橡皮章，但也提示 Q2–Q5 类问题（推导链在案）在正式期本可不自决而未自决；报告期统计「C2 类槽占比」。
- 2026-09-20 · 事件 #3：spec 评审（汇聚点类触点）产出**真实修正**（override=是）——对 09-19「汇聚点=橡皮章」假设的反例。关键区别变量可能是「提交前是否做过机器事实核对」：S6 缺陷属 spec-claim-vs-code-fact 一致性，确定性检查（引用的 API 语义 vs 源码）可在提交前拦截。校准报告考虑：spec/docket 提交前置一道确定性事实核对，可再降 C1 触点率。

- **2026-09-23/24 · 事件 #28–#31 期间**：台账自 #27 后中断维护；2026-09-26 对账补录（见下节）。≥30 出口阈值实际在 #30（SIM-002 closure，会话 2026-09-23 / commit 2026-09-24 00:06 +0800）越过。
- 2026-09-26 · 校准报告产出：`evidence/2026-09-26-gate-001-calibration-report.md`（Owner 裁决 PASS 后按指令撰写；主队列 #1–#30 / 出口后 #31–#47 不合并）。

## 对账补录记录（2026-09-26 reconciliation）

### 根因与范围

台账在 #27（2026-09-23）后停止追加；此后至 HEAD（d3ac93da）期间的
人工门照常执行（各 change state.md / evidence 留痕），仅并行记录中断。
本节按 Owner 指令（GATE-001 ledger reconciliation）补录：逐事件回答
「若发生在 #1–#27 阶段，按当时规则是否必然入账」，入账者补 #28–#47，
事件日期一律保留**原始记录日期**，不以补录日期冒充。

### 判定规则（实验开始时已存在，本轮零修改）

- 事件纳入：v0.2 §3.3 + 台账头注——「本会需要人裁决」的决策点，一决策一行；
  docket / grill / closure / step gate 触点照记；同一决策的重复汇报、
  closure-only commit、INDEX 再生、verified→closed 机械转移不记。
- 分类 / override / 等待：v0.2 §2 与 §6 口径不变。
- 出口：2026-10-04 或 ≥30 事件（先到者），不变。

### 候选核查表（#27 → HEAD，b60cbc77..d3ac93da 全量扫描）

**入账 20 件**：#28 AGENTS.md §1.6（23bca220）· #29 SCN-002 closure
（1bcc510e）· #30 SIM-002 closure（5b8860ce）· #31 RUN-004 closure
（7a801e67）· #32 AGT-001 grill GQ1–GQ4（a23511c0）· #33 AGT-001
closure（4022ce40）· #34 RUN-005 owner 预审（01a518ab）· #35 RUN-005
设计终裁（3c207d33）· #36 RUN-005 Slice A 终裁 v0.3.1 · #37 RUN-005
Slice B owner 裁决 · #38 AGT-002 canonical 3080 验收（2f633b04）·
#39 PER-010/011 Q1–Q11 用户裁决（05267a0e/91a4af94）· #40 PER-012
Human Gate（400d7b53）· #41 PER-013 立项 owner 指令 · #42 CSC-001
OWNER_GATE 主裁决（e8250650/3dca53b4）· #43 CSC-001 终裁 CLOSED ·
#44 CSC-002 Owner 终裁（b9fdf2b8）· #45 CORE-004/005/006 联合 closure
（f37fb994）· #46 SIM-001 closure（8ad805ec）· #47 SCN-001 superseded
closure（d3ac93da）。

**排除 14 类（exclusion reasons）**：

1. SIM-003 closure（83fa8c0e）——review PASS→closed，无人工裁决记录；
   DECISION-HEAVY，P-D′ 一期违规（见下「流程偏差」）。
2. SIM-004 closure（04dd6457）——同上（Step 0 裁决 A 与 final narrow
   review 均代理侧执行）。
3. RUN-005 final closure（e0c65161）——前一 entry 明示「下一步（owner
   面）」，closure entry 本身无 owner 裁决记录；goal 自动续跑期 leader
   自任 review（见 PER-013 执行反馈 §四）。
4. PER-009 closure（6b3c8e92）——closure audit 由 Leader 执行；「docs-only
   Owner ratification（leader 5.3）」= leader 代理批准，非人工。
5. PER-010/011 verified→closed——Q11 裁决（已记 #39）的机械后果，无第二
   个人工触点。
6. PER-013 closure 与 Gates A–F / final grill——5.3 自审模式（leader 自任
   并留痕），无人工 closure 裁决。
7. AGT-002 Q1–Q23 disposition freeze（250603b5）——agent 内部 grill
   （「reached shared understanding」）；DSH realization 方向已在 #33
   （AGT-001 closure）预授权，不构成新人工触点。
8. AGT-002 F1–F5 修复 / 各 REVIEW / 3081 E2E——agent 评审与实现；
   canonical 端口人工触点已记 #38。
9. AGT-002 B1 restricted-session 修复（2eedeacd/8763e2ed）——agent 发现
   的泄漏；修复指令为 leader 起草的 CANDIDATE 草案，无人工裁决。
10. PER-013 owner closure 后反馈（AVD FATAL / HostLiveFull 失败，
    5bd8432e 等）——缺陷反馈而非裁决请求；根因定位与修复自推导
    （C2 类执行），不构成决策点（边界判例见案例池）。
11. c9aafa18 build(dsh) 部署工具——机械 harness 卫生；commit 自述
    「restarting the owner's instance stays a human decision」。
12. 23bca220 中 UAR-003 state 落档——#26 已计，重复汇报不重记。
13. changes/INDEX.md 再生——机械。
14. 全部 AUTO_GATE / 机械切片（SIM-002 G1–G4+S5–S8、SCN-002 Phase B、
    RUN-004 Gate 3–7 + finalization、RUN-005 Slice A–C 实现、SIM-003
    G1–G10、CSC-001 Gate 0/Slice A–D、CSC-002 Gate 0/Slice A–E、PER-013
    Slice A–F）——机械判定，不进人视野。

### 计数与出口（可复算）

```text
N = 27（#1–#27 原始） + 20（#28–#47 补录） = 47
出口规则（不变）：N ≥ 30 OR date ≥ 2026-10-04
47 ≥ 30 → EXIT_TRIGGERED_BY_SAMPLE_COUNT
阈值越点：#30（SIM-002 closure，会话 2026-09-23，commit 2026-09-24 00:06 +0800）
#31–#47 为越点后、台账失管期内的事实，补录作对账事实，不开新实验轮次
```

去重核验：每个 owner 决策恰计一次（closure 决策计裁决行、机械转移不计、
同一决策的重复汇报不计）；#28–#47 与 #1–#27 无重叠。PASS。

### LEDGER_DATA_ISSUE（只记录，不改写历史）

- **LDI-1（仓历/系统时钟偏移）**：2026-09-27（仓历）标注的 state/evidence
  事件实际提交于系统时间 2026-09-26 18:00–20:57 +0800（系统现刻仍为
  09-26）；偏移系执行侧有意「按仓库时间线对齐」
  （evidence/2026-09-27-per-013-execution-feedback.md §四自述）。补录行
  日期保留仓历原值并以〔仓历〕标注，commit 引用保留系统时间戳。
- **LDI-2（跨午夜会话日期）**：SIM-002/SCN-002 closure 在 state log 记
  2026-09-23，落档 commit 为 2026-09-24 00:00/00:06 +0800——沿用台账
  #12 已有的「裁决日 ≠ 落档日」记法，补录行取裁决日（09-23）。

### 流程偏差（P-D′ 一期「零自动关闭」被破坏——只报告，不计事件）

以下 closure 在无人工裁决记录的情况下由 agent 侧执行（多发生于 goal
自动续跑、leader 自任 review 模式）：SIM-003、SIM-004、RUN-005、
PER-009、PER-013。PER-010/011 的 closure 为 #39 裁决的文书收口（实质
不缺人工触点）。AGT-002 保持 verified 未关。此为实验规则执行面的真实
偏差，交 Owner 裁处；本对账不因偏差虚增事件，也不静默修正。

### 等待耗时口径声明

#28–#47 的等待耗时在当时未被记录，补录时不可复考（各行记「未记录」）。
校准报告的等待耗时对比中，post-#27 样本按 censored（删失）处理。
