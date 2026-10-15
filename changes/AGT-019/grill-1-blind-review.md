# AGT-019 Grill 记录 1 — 盲审（fresh adversarial review）

> Provenance：fresh subagent（无本会话上下文），zai-coding-cn / glm-5.3-flash（owner 指令），
> 2026-10-17 派发，原文未改动。派发 prompt 攻击面 = changes/AGT-019/plan.md 步骤 1 全清单。

盲审范围：设计稿 + AGT-019 spec/plan/state + 四个冻结面原文 + baseline §24 + 证据目录抽查。未改任何文件。

---

## 1. 发现清单

**F1 · M1「back 选项」的协议前提不成立（Q1 前提错误）· high · 攻击点 1**
场景直白话：设计稿 Q1 说「safe 动作类已含 back」。查证：`product/policy/android-settings-forbidden-actions.json` 的 `safeActionClasses` 确有 `"back"`，咨询上下文里也有 `semanticMapping={back:tap}`（见 evidence/settings-coverage-003/leader-pass1/inbox/request-decision-…-1.json）。但那是 **Host policy 工件的 effect-class 分级**，不是咨询协议的 Act 形状。协议 §2.2（consultation-protocol-v0.1.md:81-91）的封闭 union 只有 Act/Policy/NoAction/Defer，Act 必须携带 Proposal（Steps + 可 grounding 目标）。P1 死局恰恰发生在 identity-only 页：元素列表为空、没有 back 目标可 grounding——**back 最被需要的时刻，恰恰是 Act(Proposal) 无处安放目标描述符的时刻**（任务 yaml 里有 `backDescriptor: Navigate up` 可兜底，但设计稿没说 back 步用哪个描述符源、也不在 M1 文本里）。「无目标安全步的 Act」在现协议里不存在，是发明新形状，属于协议 amendment 而非 Host change。
建议处置：**改设计**——M1 的 back 选项必须明确三种出路之一（带 backDescriptor 的 Act / union 新成员 / 协议 amendment），并写明 P1 死局页上 back 的目标来源；不能停在「safe 类已含」。

**F2 · M1「标记 blocked+原因」无法塞进现协议，却宣称「不动 Kernel 协议」· high · 攻击点 1/6**
场景直白话：Agent 被卡死后回答「blocked，原因是 hierarchy 不可用」。现协议回答 union 里没有 blocked：Defer 是等待、NoAction 必须带 CompletionEvidence（否则 V4 hollow-completion 拒收，protocol §2.3:99）。blocked+原因只能：(a) 加 union 成员 = 协议 amendment；(b) 伪装成 NoAction = 破坏 V4；(c) 伪装成 Defer = 回到原点。设计稿 §4 第一行却写「Host change（约束住 Director 代码，不动 Kernel 协议）」——**M1 至少一项悄悄需要协议通道，通道声明自相矛盾**。
建议处置：**改设计**——把 M1 拆成「Host 部分（Director 放宽 Defer-only）+ 协议部分（blocked 表达形状）」，或给出 blocked 在现 union 内的合法落形。

**F3 · M1 翻案未携带 004 原保护条款 · med**
SETTINGS-COVERAGE-004 的 A2 是「Agent 非 Defer 响应不进入 Kernel Effect，仍返回安全 Defer」（004/state.md:15）——这是防「本地自答绕过 Agent」（P3 三宗罪第一宗）的补丁。M1 放宽为选项集后，spec 的 Acceptance 1 只说「有机械可证的咨询记录与选项集执法记录」，**没有逐条继承 A2 的 fail-closed 等价物**（非白名单响应仍零 Effect）。翻案证据（pass5 96 次同路由、pass6-8 bounded-stop:unknown-page，analysis-v2.md:51-52）充分，但翻案边界条件（哪些卡型、预算多少、如何防漂移）设计稿完全没写。
建议处置：**补叙事**——M1 写明翻案边界条件 + 新 Acceptance 显式覆盖 004-A2 等价执法。

**F4 · M3 触发词「Fast 产不出任何东西」救不了 P1 · high**
场景直白话：identity-only 死局里，Fast **有产出**——它产出了路由身份（trace 里 `fastAvailable: true`、screenIdentity 有值，pass1 trace cycle 1 可查）。P2 说 Slow 结果迟到被错屏丢弃、预算错配；但 P1 的死因是「Fast+XML 只给出 identity、无 grounding 材料」。M3 的触发词按字面（Fast 零产出）在 P1 现场**永不触发**——同步等待 Slow 只覆盖「Fast 彻底失败」这一窄类。设计稿自己把 P1 列为头号死因，M3 却对它无效。
建议处置：**改设计**——触发词重新定义为机械可判谓词（建议：`grounding 材料不足`类，用 trace 已有的 fastAvailable/hierarchyAvailable/proposalCount 组合表达），并给出与 pass1-8 trace 字段的对照表；否则 M3 的 PER-019 amendment 证据链断裂。

**F5 · M2-③「判断级不入账」只有承诺没有执法机制 · high**
场景直白话：截图给视觉模型看，模型说「这是确认弹窗」——这句话去哪了？设计稿只写「判断级不入账」四个字。机械证明需要至少三件套：截图派生内容带 judgment-level 标记、入账提案拒绝该来源、trace 可审计该标记。一样都没有。这条同时是 spec 自己列的红线（「判断级/事实级分界」）——**红线被候选机制正面踩到，却只用一句口号设防**。另外 §24.7（baseline:1196-1203）：页面内容是 untrusted Evidence，系统权限弹窗只能执行不宽于既有 Grant 的选择——M2-③ 给 Agent 的截图判断若驱动弹窗处置，与不变量 47 的交互必须显式论证。
建议处置：**改设计**——补判断级标记的机械执法链（谁打标、谁拒收、trace 何处可验）；Q3 不可只裁决「附着形状」。

**F6 · 能力门控三层分链不闭合（层间冲突无裁决）· med**
spec 修正面写了「profile 声明有没有眼睛 → 宿主面定用哪双 → 协议面定何时允许看」。反向不闭合：profile 声明了 image-input 而宿主未绑定 → 现语义是 `required=false 诚实 NotConfigured`（product/profiles/uniagent-prod.yaml:17 注释）尚可；**宿主绑了模型而协议触发词未放开怎么办？协议触发词放开了而 profile 未声明怎么办？**AGT-001 F5 的 handshake 只钉 protocolVersion/schemaVersion/schemaHash，M2 增加的触发词字段是否入 handshake 钉扎，spec 未说。
建议处置：**补叙事**——为六种层间组合各写一句 fail-closed 落点；M2 的协议新字段进 F5 钉扎清单。

**F7 · M4 语义判读兜底重新打开 P4，且撞不变量 47 · high**
场景直白话：已核实词表缺陷为真——`SettingsCoverageConfig.cs:86` `DefaultObstacleTargets = { "CANCEL", "button2", "DISMISS", "button1" }`，注释自认「button1 次之」；弹窗只有 button1 时真的会点确认。M4 的 {cancel, dismiss, back} 白名单机制可判、方向正确。但 M4 又写「处置权交 Agent（判断级读弹窗内容选择）」+「超界前一次咨询」——语义判读一旦兜底，P4 的橡皮图章问题换了个角色重演；且 §24.7 要求系统弹窗出现**更宽选项时必须暂停并重新授权**，不是「咨询一次再自己选」。
建议处置：**需 owner 裁决**——弹窗 policy 的最终形态必须在「纯 resource-id/结构机械分类（可判但可能判不出）」与「语义判读（灵活但撞 47 与 P4）」之间显式取舍；白名单外 confirm 的唯一出路应是暂停+重授权而非 Agent 处置。

**F8 · 修订通道张冠李戴：consultation-protocol 不是可 amendment 的冻结面 · high**
场景直白话：spec 冻结面表第三行写「consultation-protocol §2.1/D3 → 窄扩展 amendment」。但 protocol 文档自 header 就是「Status: DRAFT / CANDIDATE · Authority: NONE」。真正被冻结的是 **RUN-004 D3**（RUN-004/state.md:27「上下文 v2 与回答 v2 字段集 = consultation-protocol §2 全文」）——RUN-004 已 CLOSED。对一份无权威的草稿「amendment」是伪通道；M2/M5 真正要动的是 RUN-004 的冻结字段集（AgentDecisionContext/AgentDecision），这条冻结面**根本没出现在 spec 的冻结面表里**。同理 M5 动 Progress 字段（RUN-004 D3 / ConsultationProgress），通道也该指向 RUN-004。
建议处置：**改设计**——冻结面表重写：把「consultation-protocol §2.1/D3」替换/补充为「RUN-004 CLOSED 的 D3 字段集冻结」，通道按 RUN-004 的重开/assessment 规则走；RUN-005 是否被参数化 Defer 波及也需一句话声明。

**F9 · AGT-001「单工具 allowlist」是 grill 闭合决定，非 §6.2 可自行登记 · med**
spec 冻结面表第四行走「AGT-001 §17 revision rule 登记」。但 AGT-001 F3 闭合条款明写「static allowlist（**submit_decision 单工具**）」（AGT-001/state.md:31-33）——M2-③ 若走请求形态（工具），不是在既有 allowlist 里登记，而是**推翻一条 grill 闭合的结构性决定**。这需要 focused re-grill 级别的 owner 处置，不能按常规 revision rule 滑过去。另外走「context 附着」形态则 allowlist 不沾，但那又回到 F5 的判断级执法问题。
建议处置：**需 owner 裁决**——先裁 M2-③ 走附着还是工具；若走工具，明示这是 F3 闭合条款的翻案。

**F10 · PER-019 amendment 的证据引用有两处无锚 · med**
spec 引「三次派发零落地 + 15s/2轮预算错配」。抽查：boundedWaitMs=15000 / popupConsecutiveCycles=2 确在 `product/tasks/settings-coverage.yaml:53-58`（预算错配锚点成立，且「周期不阻塞超此界」注释与 PER-019 原则自洽）；但「三次派发零落地」与「slow 令牌零耗时字段」（P6）在 evidence/settings-coverage-003 pass trace 与 004 live-fix 里我未能定位到对应记录（trace 含 fastLatency/hierarchyLatency，slow 派发计数的零落地记录未见）。15s 与「Defer 2 轮」的关系也含混：2 轮是 popupConsecutiveCycles 还是 ObserveSpec.MaxRounds？
建议处置：**证据不足需补**——给出三次派发的具体 trace 文件+行，或改写证据表述。

**F11 · M5 误报与「职责越位」双风险 · med**
场景直白话：合法长遍历里 frontier 停滞是常态——在一个 30 项的二级页逐项验证、或 backNavigation+repeatedEntries=1 的合法重入（task yaml:19-21）都会被「同 route 重复计数」误伤；pass5 的 90/96 同路由是病态，但阈值怎么区分「病态 90 次」与「合法第 3 次重入」没有数据支撑。另外 protocol §3:112 明写「no-progress 判定是 **Agent 职责**」——M5 让 Host 计算停滞摘要进上下文，边界必须钉死为**纯事实计数**（设计稿已用「摘要」一词，但未写禁令），否则 Host 观点进了决策上下文，滑向执法面代行判断。
建议处置：**补叙事**——M5 写明「只进事实计数、禁 Host 判语」；阈值用 pass5 数据 + 合法重入频次做标定后才可冻结（Q6 需数据）。frontier 停滞建议按「新 discovery 计数」而非「周期数」定义，避开验证期假阳性。

**F12 · 修正面总览列了声明面却无 revision/hash 路径 · med（攻击点 8）**
spec 修正面列了 profiles、prompt/payload-shapes、skills、tasks/settings-coverage.yaml、policy 工件。`product/AGENTS.md` 变更纪律要求：内容变更须经 change 并**递增 profileRevision**、引用工件**以 hash 钉扎**（sha256 fail-closed，PRF-005）、Skill 变更须更新 revision+hash。spec/plan 全文没有任何一处写「本次变更递增哪些 revision、重算哪些 hash、skills-lock 是否随动」。policy 新工件（M4）按 PRF-004 是新 face，也无 owner 指派。
建议处置：**补叙事**——修正面总览每行加「revision 动作」列（profileRevision +N / policy hash 重钉 / skills-lock 随动）。

**F13 · M2-② 的 Owner 归属自相矛盾 · low**
M2-② 说「**Owner** 派生只读视图（ADR-0011 纪律）」，spec 修正面却把派生放在 **Kernel**（「Agent 局部视图派生（PolicyEvaluationView 兄弟）」）。ADR-0011 的纪律是 view 必须由 canonical 数据 owner 派生、每个字段有真实读取证据。世界面的 owner 是 World Model（Kernel 只是执法），Kernel 侧派生局部视图若顺手加了加工（趋势、聚合），就成了第二 truth。诊断族字段我核了 trace：`fastAvailable/hierarchyAvailable/proposalCount/fastLatency/hierarchyLatency` 确实逐周期已产出（pass1 trace cycle 1-6 可查）——这族的真实读取证据成立；但「近 N 周期趋势」是新造聚合，无 buyer 证据。
建议处置：**改设计（措辞）**——明确派生 owner 与落点；趋势族按 ADR-0011 先证明 buyer 再入 view（Q2 可据此收窄）。

**F14 · M3 同步等待与「错屏丢弃门」的相容性未声明 · low**
PER-019 的流水里有「路由变更诚实丢弃 Unaligned」；M3 同步等待把 Slow 结果留在本周期——但等待期间若 route 变了（用户/系统弹窗导致），丢弃门必须照常生效。设计稿红线声明里列了「错屏丢弃门不动」，但 M3 机制文本未写同步等待不豁免 Unaligned 丢弃。
建议处置：**补叙事**——一句话钉死。

---

## 2. 修订通道核对表

| 冻结面 | 声明通道 | 裁决 | 一行理由 |
|---|---|---|---|
| SETTINGS-COVERAGE-004「只准同号 Defer」 | Host change 翻案 | **正确（带条件）** | 004 已 verified，其约束本就是 Host seam Scope；翻案证据（pass5/pass6-8）真实；但须显式继承 004-A2 的零 Effect 保护（见 F3） |
| PER-019「slow 不阻塞周期」 | 定向 amendment | **证据不足** | 通道形态可（closed change 以新 change 定向修订，有 §24 narrow amendment 先例）；但触发词救不了 P1（F4）、三次派发零落地证据无锚（F10） |
| consultation-protocol §2.1/D3 | 窄扩展 amendment | **错误** | 协议文档自身 Authority: NONE / DRAFT；真冻结面是 RUN-004 CLOSED 的 D3 字段集，未列入冻结面表（F8） |
| AGT-001 §6.2 allowlist | 登记通道 | **需 owner 裁决** | 「submit_decision 单工具」是 F3 grill 闭合条款；加观察能力是翻案非登记；且 M2-③ 附着 vs 工具形态未裁（F9） |

遗漏面：AGT-009 词表（button1）在 M4 被翻，但 AGT-009 未出现在 spec 冻结面表——词表本体是 Host config 默认值 + policy 工件，通道应为「policy 工件新面 + Host 默认值变更」，建议补列。RUN-005（M2 参数化 Defer 波及 ObserveSpec/Policy 形状）亦未列，仅 Out-of-Scope 一句「复用其冻结形状」——参数化 ObserveSpec **修改** V5 管辖的形状，这句话与 M2 自相冲突，需一句话澄清「扩展不改 V5 上限」是否被 RUN-005 冻结条款允许。

## 3. 红线核对表

| 红线 | M1 | M2 | M3 | M4 | M5 | M6 | 裁决理由 |
|---|---|---|---|---|---|---|---|
| 错屏丢弃门 | 未触 | 未触 | **边界擦过** | 未触 | 未触 | 未触 | 同步等待期间 Unaligned 丢弃是否照常未声明（F14） |
| 判断级/事实级分界 | 未触 | **触碰风险** | 未触 | **边界擦过** | 未触 | 未触 | ② 判断级标记无执法机制（F5）；④ Agent 判断级读弹窗选处置（F7） |
| 幻影目标防线（Act/Policy 需可 grounding） | 边界擦过 | 未触 | 未触 | 未触 | 未触 | 未触 | back「无目标安全步」若成立即绕开可 grounding 要求（F1） |
| UnknownOutcome 强制重观察 | 未触 | 未触 | 未触 | 未触 | 未触 | 未触 | M1 文本显式保留 |
| 失败咨询计轮（GQ3） | 未触 | 边界擦过 | 未触 | 未触 | 未触 | 未触 | 参数化 Defer 轮数与 D3 预算合同的叠加语义未写 |
| 三判据分离 | 未触 | 未触 | 未触 | 未触 | 未触 | 未触 | M5 只给停滞事实不给 coverage 判语；注意后续勿升级 |
| Kernel never invents/repairs Policy | 未触 | 未触 | 未触 | 边界擦过 | 未触 | 未触 | confirm 白名单的 author 未指派；若 Agent 参与改 policy 即触 |
| baseline §24 全节（不变量 43-47） | 未触 | **边界擦过（47）** | 未触 | **边界擦过（47）** | 未触 | 未触 | 47：截图判断与弹窗更宽选项须暂停重授权，两机制均未对齐 §24.7 |

## 4. Q1-Q8 可裁决性

| # | 可否仅凭仓库事实裁决 | 结论 / 精确权衡 |
|---|---|---|
| Q1 | **部分可裁** | 事实：policy safe 类含 back（真），但协议 Act 必须带可 grounding 目标，「无目标安全步」不存在（F1）；backDescriptor「Navigate up」在 task yaml 有现成锚。**仍需 owner**：back 走 Act+backDescriptor 还是 union 新成员；权衡 = 协议膨胀 vs P1 死局页可用性 |
| Q2 | **大半可裁** | 诊断族六字段中 fastAvailable/hierarchyAvailable/proposalCount/fastLatency/hierarchyLatency 在 pass1 trace 已逐周期产出，真实读取证据成立；「近 N 周期趋势」是新聚合、无 buyer 证据，按 ADR-0011 应先删或试证。其余字段族可对照 pass trace 逐字段核证——建议 grill 就做 |
| Q3 | **需 owner** | 附着 vs 工具形态决定通道（F9）：附着则 allowlist 不沾但 F5 执法问题加重；工具则翻 AGT-001 F3。触发词集可参照 Q4 谓词先裁 |
| Q4 | **可半裁** | 事实：boundedWaitMs=15000 已是机械 fetch 界，等待预算直接定义「≥ boundedWaitMs 且 ≤ 本周期剩余预算」即可机械定义；ObserveSpec MaxRounds≤4（V5）不动。**需 owner**：M3 触发谓词终形（现词救不了 P1，F4） |
| Q5 | **需 owner** | 事实已裁一半：词表机械分类被证不安全（button1 实锤，Config.cs:86）。权衡：纯 resource-id 分类（可判、可能漏）vs 语义判读（撞不变量 47 与 P4）——46 的「更宽选项必须暂停重授权」应作为硬边界先钉死，再谈兜底 |
| Q6 | **需数据后可裁** | pass5 提供 90/96 同路由病态样本；合法重入频次（repeatedEntries=1、backNavigation）可作为假阳性下界。建议按「新 discovery 计数停滞」而非周期数定义；阈值 owner 裁 |
| Q7 | **需 owner** | 纯分期取舍，仓库无可推事实 |
| Q8 | **需 owner** | E4b 反射分界在仓库无既有定义；且 F13 已示 Kernel 侧派生 vs Owner 派生的归属矛盾——Q8 应与「视图 owner 归属」合并裁决 |

---

**总评**：问题清单 P1-P7 证据锚点大体真实（96 步空转、button1 词表、15s/2 轮错配均核实）；机制方向（异常时真咨询、观察阶梯、dismiss-only）与 §24.2 的职责分工同向。但**通道层有三处硬伤**：M1 悄悄需要协议通道（F2）、真冻结面 RUN-004 缺席（F8）、M2-③ 形态未裁先写登记（F9）；**机制层有两组口号式设防**：判断级不入账（F5）与弹窗语义兜底（F7）。先修 F2/F4/F5/F7/F8 再谈字段与阈值。
