# World整体架构审查

2026-10-23；审阅HEAD fddc69a6及当前工作文件。结论：沿既有Owner和seam实施可符合基线，没有发现需要新增Owner、重裁20项决定或改变ADR的阻塞。当前缺失的producer/消费链是本期实施要求，不能把“审查可实施”写成“架构执法已产品验证”。具体Interface后继稿见model-interface-disposition.md。

## 从实际调用链看边界

SemanticObservationProjector把合法语义观察经P2投到Ledger，故ExpectedMatch/Intent不能进World；WorldModel.Semantics核capture/cycle/session/time与derived basis后才关联当前Container。WorldModel.Associate校验accepted basis/反证，ProductAssociationStrategy只提案，不直接铸身份。RecordObservedNavigation有事后验证专用gate，association提案不能造导航边；WorldModel.Viewports只给有据配准，没有时保留明确原因。

World.DeriveSlice由current派生，CoreSemanticProjection为纯投影；Control消费后，Effect Boundary认定当前binding，Assurance做消费相对freshness/授权；Effect唯一越界投递，Driver的StepVerify保留串行后验义务。Driver目前TrySchedule后NextInput可返回等待，证明不能把driver调用次数当实际感知执行。新增gap/历史参考/纠错必须接在上述Owner处，不能放driver积累另一份“可信世界”。

## AGENTS §1.5五项逐条检查

| 原则 | 当前证据及风险 | 本期处置与可判定验收 |
|---|---|---|
| 架构优先 | 基线§11/12/14–16，P3/P4/P22/P23/P27；ADR-0010/11/12/14/15/17/23/26/49/52/54。旧ADR或文稿“无实现”状态不能压过当前源码/最新授权 | 保留currency/freshness、demand/prior与Evidence分轴；旧观测纠错新W，不放宽current gate；Q2/Goal等延期保留，未新增架构裁决 |
| 单一职责/显式seam | World自身关系/累计/纠错，Control补证，Assurance效果与充分性，Effect投递，Run终局。核心风险是renderer拼图变真值、Host字符串替World裁决、driver新计数变事实 | 使用model-interface稿的数据责任；K04/K07/K10/K12从正式端口验收；禁止viewer/Oracle写World/执行。跨Owner只读摘要不带授权判定 |
| 高内聚低耦合/替换 | 现有association/continuity内部策略、语义executor供应可替换；CW/CS两Adapter穿真实Owner。Replay纯表达现位于tests，不能直接成为生产依赖 | 深Module保持World一致提交，小Interface携带buyer事实；复用纯表达时抽必要纯逻辑，不复制World。无第二Adapter/无真实buyer不新造公共Interface或包；替换供应不改核心 |
| 验收先可判定 | 41场景元数据与实际断言不同；N/V/P/S/CW/CS/RC/CK/R已有局部保护，缺本期纵向链 | scenario-derivation逐项20+38+早期7组映射，K01–12写初态/输入/中间/终态/不变项；实现填四元组，代码阅读和有限推演不作产品PASS |
| 完备且可扩展由buyer验证 | 四类均有具体buyer，历史复用、归属纠错、页内scope和Agent正文已购入；全关系、通用重识别/补偿/Goal/全Settings未购入 | 每类有合法producer及保持/解决/消费，来源不够Unknown；扩新字段/能力须协议/白名单及真实读证据，不能预造图平台。未来可视化仅保留核心本来需要的来源信息 |

## 六类权威及相邻职责核对

| Owner | 本期应拥有/提供 | 不得因本期偷渡的职责 |
|---|---|---|
| Evidence Ledger | 原P2/P3 admission、immutable record/derived lineage/幂等；新纠错的支持观察仍经接纳 | 不因World标解释错误就删除/改旧record；admission≠truth；全局撤销基础设施仍非本期 |
| World Model | 事实/身份/归属/有据关系、四类gap、累计/纠错一致W、S及只读事实/来源 | 不记资源/任务progress/提醒调度，不判目标完成、动作许可/成功；不把模型分数当充分来源 |
| Control Loop | 观察需求调度、实际来源的尝试摘要/安全边界主动提醒、意图/计划重判 | 不铸World身份/关系，不按计数投票销gap；null等待不造实际尝试，纠错不续额度 |
| Assurance | sufficiency/freshness、反证后重新检查proof/实际effect、UnknownOutcome处理所需判断 | 不从当前画面正确推出整个实际影响闭合；同源标签自洽不证明目标含义；不回写World |
| Effect Boundary | 当前canonical binding、gate、可靠提交/receipt/未决发现及delivery闭合 | 不等待/选择感知模型，不反向点击自动补偿，不由World纠错改receipt或重开terminal delivery |
| Run Model/Runtime | Run/Contract/消耗与明确授权、terminal记录/一次发出；组合转送合法Owner信息 | World不接Run状态；Driver编排不成为第七Owner；不恢复ADR-0052取消的必需固定HardCap终止证明 |

## Findings与实施处置

严重度表示若遗漏对本期交付的影响，并非全部都是现网已复现bug。均为既定范围内工程要求，review blocker=0；实现阶段未解决则相应验收不能关闭。

| Finding | 具体事实/反例 | Owner/要求/验收 |
|---|---|---|
| F01 high：scope引用碰撞 | CoreSemanticProjection.SliceId(rev,root)遗漏InScope；同W/root不同合法scope同Id。DeriveSlice过滤去重但输出未规范 | World/Core投影：固定scope规范编码、局部范围、legacy解释与BasisSliceId一起改，K01；不回写历史绑定 |
| F02 high：历史纠错/累计没有正式链 | SemanticContainer只认current；occurrence按观察替换；关系追加不等正式旧观察归属纠正 | World：有目标的新accepted依据、字段适用与反证、一次一致提交、独立当前不倒退、旧S不变，K02–04；不可用放宽current gate替代 |
| F03 high：参考/当前采信混同风险 | F缓存只重用fast计算；没有证明挂起慢模型时历史先读、当前对应后免重复语义供应 | World+感知调度消费：旧验证/本轮对应双来源，字段独立，K05/06调用/可用顺序真实证据；收益未实测 |
| F04 high：四类producer与摘要未完整 | 身份need已实现；关系graph unknown、coverage声明有部分基础；ProductAssociationStrategy关系为空，PageCapture无覆盖声明 | World+观察producer：限定最小上下文/coverage来源，四类逐项产生/保持/解决与Agent消费，K07/12；不能SourceId存在即声称数据具备 |
| F05 high：计数/提醒事实来源不足 | TrySchedule增加RequestsUsed，后续NextInput可null；revision/retry非实际attempt | 执行Owner供开始/结果关联，Control去重/持续可见/新结果再提醒，K08。缺记录not_collected；现有deadline/额度不改 |
| F06 high：影响不完整却报无影响 | EvidenceRecord derived lineage不含全部语义→计划→binding→receipt→J；缺边反例；CI02共同污染选目标与验证 | World及各消费Owner保存最小实际依赖，有限闭包重判非全假，未知边界显式；原Owner效果核查/terminal不变，K09/10；不是补偿平台 |
| F07 high：字段可传不等Agent看见 | overview主要字符串；context→consultationPrompt链存在但新增内容未实证；能力目录只首次 | Kernel/Host/Agent adapter同步严格codec/生成物/最终正文，第二轮更新、可读足量摘要、输入资格，K11/12。离线传送证明与真实模型行为分层 |
| F08 medium：展示与产品依赖 | R有单Slice ContentOnly/Oracle，不是多SliceWorld供给；当前纯表达在tests | 核心只补正式供给及必要纯表达复用，产品不能引用tests；保留未来只读展示来源，界面WORLD-VISUALIZATION-001后做，K03/12 |

## 不升级与延期判断

S0 Returned当前容器冲突的旧报告不能直接当正式装配可达bug：本期输入没有新增相反运行证据；维持潜在自定义scope风险，后续若实际caller可达另立回归。全局FreshnessBasis最大时点不能证明普通claim迟到安全，但当前已关闭exact-capture不因此重开；新功能涉及的字段/路径必须有晚到断言，通用普通claim演进与新鲜窗标定另查，不做无buyer改造。

Q2确认账目恢复/未决屏障已由ADR-0054定语义，运行实现交原Runtime线；正式Goal、全树遍历、H7整体、通用导航用途、全生命周期/身份merge/跨Container continuity和通用补偿继续延期。这些不阻塞World设计ready，也不能借延期掩盖四类信息与Q19/Q20的必要消费缺失。

## 验证边界与放行含义

本轮仅CONTRACT源码/文档检查、只读场景JSON/录制pin核对、只读认证一致性检查和有界抽象推演；没有构建、运行现有产品测试、产品仿真、设备或真实产品模型。认证检查41 files/0 violations，录制及构成94条引用hash匹配/远端可用性以reviewed commit对象存在为界，均不证明场景行为。evidence/finite-derivation.json只证明其有限抽象的性质/反例。现有测试断言有源码证据，不继承旧PASS当本轮PASS。

UWM旧正文P-UW-18“外部ObservationNeed未冻结”和部分ADR历史“无当前实现”注记须结合当前P27、P23最新状态阅读；本审查按已实现current capture补证和进程内continuity检查，不恢复旧deferred描述、不宣称跨边physical path全闭合。

架构判断为ready_for_implementation_with_required_changes：F01–F08已给明确工程处置，不是需要再次许可的门；无未决语义/Owner/ADR阻塞。实现会话需独立复验输入，固定公共shape与兼容、完成PLAN/REVIEW/VERIFY和实际四元组；本审查不能替它签产品CLOSED。
