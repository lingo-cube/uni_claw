# 设计缺陷文档与真实问题的覆盖核对

日期：2026-10-22；HEAD：9bc15b8974f5fb5746074e373e6c132bc4df0089；共享工作区有活动改动，状态是本轮读取时的快照。仅文档核查，未运行产品测试、设备或模型。

后续更新：用户已要求继续grill并研究上述遗留问题，新增residual-primary-sources.md及residual-analysis.md。本表保留覆盖核查时点的范围判断；后续源码核实确认M5已有停滞机制和目标推进账目、goal-directed装配有任务缺项屏障。Q12已接受明确范围先闭合；goal-completion-mapping.md补齐限定映射，Q13已按最新World聚焦方向接受延期（B），正式Goal接通及其他跨线改造另切片；本期仍保留四类缺口的最小真实消费与Agent可见要求。遗留不是机制全无，也未由本轮文档变成已验收。

## 结论

不能将Q1-Q11已接受解释为“下午所有设计缺陷和昨晚所有问题均已解决”。World未知表达、补证连续性、Agent可见性、提醒规则及B1报告边界已纳入设计；一些模块缺陷已由旧Change交付，应继承而非重做。导航用途分类、无World缺口时的任务零推进、Goal范围投影、完整发现/滚动及实际停滞送达验收，仍由其他Owner承担，当前World稿没有给出它们的完整解决方案。

这里分三件事：①是否有明确设计处置；②是否已有实现交付；③当前版本是否有相应真实验收。三者不互相替代。未在World稿重复展开不自动等于遗漏；只写一句“归原Owner”也不能算该问题已设计解决。

## 一、下午整理的文档

| 来源 | 本次覆盖及当前处置 | 不能声称的内容 |
|---|---|---|
| `docs/analysis/world-model-capability-gap-and-kernel-fallback-assessment-2026-10-22.md` | World知识缺口、返回读取可达性、重启账目及权威边界已逐项处置，见design.md“原分析与提案的逐项处置” | 普通claim迟到保护/新鲜窗遥测、自定义装配一致性已修；所有旧缺陷断言都仍成立 |
| `docs/analysis/world-model-uncertainty-and-verification-plan-2026-10-22.md` | S0-S4均有去向；四类同期开，替换通用M-of-N与独立Kernel看门狗提案；B1诊断及旧资料资格已裁决 | 场景已补、四类载荷已接线、实际Agent已收到新字段、可用旧轨迹精确重放或发布门槛已校准 |
| `docs/analysis/uniclaw-module-boundary-design-review-2026-10-21.md` | 先对照`changes/ENG-OPT-001/state.md`：Run/Ledger接纳快照、World只读封装、Assurance归位、去除Kernel对AdbEffectDriver分类依赖、Coverage显式Kind/Target/Text已在原范围交付 | 不能将旧审查中的这些条目重新当本次未修生产缺陷；也不能用原范围关闭推导所有typed guard或未来改动都正确 |
| `docs/analysis/uniclaw-component-engineering-closeout-2026-10-22.md` | 作为上述实施与组件职责的收口入口；继承原证据，不重开已完成优化。Driver状态簇、HTTP接入/启动职责及Host authoring仍是后续范围 | 本次World设计覆盖了全部组件重构或Host代造Contract的治理问题 |
| `docs/analysis/uniclaw-directory-organization-review-2026-10-21.md` | 导航/归属入口已有原工程交付；目录大迁移、Provider/schema历史命名、runs-d9等证据迁移保留后续候选，路径受保护 | 所有目录建议已实施，或World未知实现必须先搬全仓目录 |
| `docs/research/jev-page-identity-evaluation-20261021.md`、`jev-traversal-integration-assessment-20261021.md` | 语义观察与World身份/Assurance判断分开；同route不自动同对象，未知不猜、confidence不授予真值或许可；保留研究的误判/弃权/成本限制 | Jev已解决所有身份/控件用途识别，截图已真实消费，或提高安全/覆盖/速度已有因果证明。具体图像供货和新盲测不是本次未知分类方案已交付的能力 |

## 二、真实运行及历史问题

对照入口为`evidence/ordered-closeout-20261022/report.md`、`navigation-classification-gap.md`、AGT-019/RUN-006/TRAJECTORY-001及Settings原Change状态。下表按问题区分，不把旧RUN-006记录都当成昨晚新发生，也不以文件日期证明发现时间。

| 具体问题 | World设计覆盖到哪里 | 当前未完成/原负责面 |
|---|---|---|
| 页面身份补证耗尽、同route歧义、补证反复无效 | 已覆盖具体未知、跨capture存续、实际尝试结果、已有补证屏障及提醒；无法解决时诚实停下 | 未承诺新感知能力能识别所有页面；SIMs/App notifications等真实难例须按感知/World/Assurance原链逐例验证 |
| 已verified进入却RouteBefore为空、覆盖数仍0 | 继承`SETTINGS-COVERAGE-007`已关闭的Runner记账修复；不是重新改World | 原公开Runner确定性证据不冒充修复后完整设备复验，不等于动作分类已修 |
| Fix connectivity可点击→被当成导航→允许tap | **当前稿只覆盖未知不得授予许可的原则，未完整设计该分类修复** | Perception保留必要观察，Host候选/任务guard消费明确用途与授权范围，Assurance另验结果。须先修复或用既有明确范围约束，才继续完整动态树；receipt不证明导航成功或无其他副作用 |
| 每步动作verified，但任务一直不前进（含旧96步/局部覆盖案例） | 有World知识缺口时可触发复核；**无知识缺口的零推进循环不因此自动被识别** | 原Host/Control/Runtime的目标推进与停滞判据仍须单独设计/验证；不能把World未知计数当目标进展。旧latch等修复按原Change继承，M5标定仍未获本轮新证据 |
| Heartbeat/CasePacket是否真正进入Agent、是否同cutoff | 本次新四类信息已定位正式decision输入链，并要求实际可读；设计要求已覆盖 | `TRAJECTORY-001` H7的自然事件、三组字段同cutoff及真实送达验收仍未证明；旧握手障碍已变化，不能继续仅说端口未启动 |
| 全Settings/子页滚动穷尽、弹窗/系统模块、剩余项处置 | World明确观察覆盖未知，Control发现进度与Assurance完成分开；不假称全树 | **本稿没有完整滚动发现/弹窗关系生产/全覆盖算法方案**。Settings003/005及相关任务继续原验收，Contains/Overlays等需要明确buyer的独立切片 |
| Task/Goal范围未完整投影到Contract、Plan稳定生成及同条件收益 | 说明World不接管Goal/完成、恢复不重置资源，未改变已决Plan语义 | **未纳入本稿完整解决方案**；由Agent/Host authoring、Runtime及Plan线继续。`evidence/legacy-closeout-20261022/plan001-acceptance-mapping.md`已完成承接对账，RUNTIME-PLAN-001仍未整体验收 |
| high/Slow等待预算、补证deadline、执行恢复 | 已覆盖同次补证不重置期限/额度，未决Effect不重发，有限账目恢复 | 未重新设计全部等待策略或实现完整现场恢复；不同provider等待与真实长尾按Runtime/感知原责任处理，不能凭成功响应或当前默认值外推 |
| 旧回放输入/状态证据不足 | Q11纳入证据有界诊断，未证点不比较模型优劣、不进同输入统计分母 | checked状态ExactProof的补采资格及实际新语料由`REPLAY-STATE-EVIDENCE-001`/仿真线设计；原XML不升级为强证据 |

## 三、今天新增的仿真发现，另行标记

`SIM-011`当前记录11个受控例首跑9通过/2失败：checkable=false或缺失时unchecked被误判verified。`CHECKED-GUARD-001`正在独立实施，仅修既有guard消费。它是本日下午核对时看到的新发现，不能混称昨晚已列出的故障，也不能因ENG-OPT曾归位Assurance就视作已解决。本线不接管正在变化的两个产品文件；最终状态以该Change后续证据为准。

## 四、本轮补充及剩余范围

本次补充的是这张显式覆盖表，以及design.md和state.md中的范围限制。没有将导航分类、零推进或全树算法静默加入World实现，也没有把它们从系统待办中删掉。用户尚未确认整份设计达成共同理解；11项局部答案仍有效，覆盖核查不新增产品实施授权。

最需要继续补的两处设计是：①导航用途分类与执行前授权的真实反例闭合；②无World知识缺口时如何发现目标零推进。实际H7/停滞标定和全树属于尚待执行的验收；目录/Driver/HTTP大整理属于另行选择的工程候选。若期望由本会话统一解决这些事项，需要先明确跨线范围，不能仅以“已分配Owner”标为解决。

method：文档、原Change状态及最近会话记录的只读对照；一名Worker辅助核对旧模块审查，Leader以ENG-OPT实际收口修正旧断言。

expected：覆盖/已交付/待验证/未展开分开；不重开旧修复，不遗失活动问题。

actual：上述World范围已设计；已有工程交付有原Change证据；多项跨线设计和真实验收仍未完成。本轮未进行产品测试或真实运行。

evidence：本文列出的来源；`evidence/coverage-entry.json`、`evidence/coverage-verification.json`。当前外部并行文件漂移仅记录，不回滚。
