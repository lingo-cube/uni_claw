# 聚焦World后还剩什么缺口

日期：2026-10-22。状态：只读核验及设计输入，Authority NONE；不是新增实施授权或协议冻结。沿Q13的World聚焦范围，不将Goal、任务进展、执行许可或通用恢复归给World。

后续节点/边核对见node-edge-design.md：用户明确按节点/边维护、多个局部Slice共同呈现页面Container。增加最小结构边、页内局部范围、范围相关Slice标识、归属/关联纠正及跨观察时间一致性的实际缺口。CoreSemanticProjection.SliceId只取revision/root、忽略已有InScope差异，已确认源码级表达缺陷，尚未运行回归或证明现场错绑。图保留历史节点和导航历史revision不直接作为缺陷。Q14/Q15/Q16已接受A；有据累计、归属纠错及结构/语义/状态/布局表达纳入首期设计。Replay已有单Slice呈现，复用已有能力，主要补World供给与多Slice组合；不是新建一套绘图系统。详见page-expression-design.md。

## 当前优先收敛的四项

| 缺口 | 当前能确认的情况 | 还需完成什么 |
|---|---|---|
| 统一的缺口记录 | WorldBeliefRevision.cs:25-27的Uncertainty仍只计冲突数；PageObservation.cs:24-26的ObservationNeed当前为身份补证；图投影有范围/缺边字符串。不能据此说World完全不表达未知，也尚不能承载已选定四类的统一语义 | 四类各有对象/命题、scope、revision、来源、独立原因与消费需求；显式冲突保留独立含义。字段、版本兼容及不支持类别的处理仍需具体设计，不能只添枚举 |
| 观察覆盖的真实依据 | HierarchyCoverage.cs:7-35已有声明面内完整/部分/未知；TypedHierarchyProposalProjector.cs:69-70保留覆盖与限制。但PageCaptureObservation没有通用覆盖字段；当前World目录未检索到这些覆盖字段的消费 | 将来源限定的覆盖依据接到World的观察覆盖判断，说明声明面、已观察部分及不能证明的范围。XML导出完整不能扩成整页/虚拟列表/任务穷尽 |
| 缺口的延续、失效和解决 | Q6/Q9已确定同一问题须有对象与兼容范围依据；移出当前视图不等于解决，旧Slice不改写。这些是已接受规则，本Change尚未实施统一记录 | 将规则落实到可核对的记录与投影：新证据如何关联、何时仍未知/不再相关/失效/解决，同因多类别如何关联。已关闭的页面身份/归属链继续复用，不另造身份Owner |
| 从World到实际消费 | ConsultationTypes.cs:141-154有Unknowns/Blockers等既有投影；decision-channel index.js:628-629确实序列化当前Context。传输路径存在，但没有证据证明新增四类摘要已经实际到达模型 | World及其他Owner的只读摘要接到正式输入；每类有实际消费及保持未知/解决的正反例。引用不是详情读取能力；补证尝试/资源来自Control与Runtime，消费充分性与恢复动作仍由原Owner判断 |

前两项包含仍需细化的生产/语义问题；后两项已有行为选择，主要缺具体模型、接线及证据。不能把它们全部再变成需要用户重答的产品问题，也不能把设计已定写成实现已完成。

## 仍有限制，但按真实消费需要另开能力切片

| 能力差距 | 当前边界与证据 | 本期处置 |
|---|---|---|
| 同一路径下区分不同实体、跨变化重识别 | ProductAssociationStrategy.cs:60-109按签名匹配；route是普通证据，不参与关联。WorldModel.Semantics.cs:19-52仅把同次已接纳语义关联到已有当前容器，不凭页面类别创建或合并身份。WORLD-SEMANTICS-002/state.md:53明确不承诺同route语义实体区分 | 本期表达身份不足及相应消费限制；不声称四类缺口自动获得新的识别能力。若验收场景必须识别这些实体，另列真实buyer、所需观察与反例 |
| 包含/覆盖层关系及生命周期 | ContainerAssociation.cs:25-34有Contains/Overlays/NavigatesTo；当前ProductAssociationStrategy各分支均给空Relations，WorldModel.cs:243-245图投影仅选导航边。协议§33保留Container Ended等后续问题 | 本期能表达具体关系缺据；弹窗覆盖谁、关闭后的失效/撤销，须有来源、适用范围及当前消费者后独立设计。导航与已验证滚动关系已有实现，不泛称World没有关系图 |
| 需要时跨观察认定同一逻辑控件，以及跨容器/会话延续 | World已有ContinuityDemand/ResolveContinuity及LogicalItem语义；UniKernel.cs:613-628已有entity obligation的demand登记，不能说从未接线。本次src检索未见生产IContinuityStrategy实现或World外ResolveContinuity调用；协议§33仍保留Control引用编排、跨容器/会话等后续问题 | 不能把滚动对应锚、同坐标或同文本当同一逻辑对象。本期缺口延续不等于全部LogicalItem/跨会话身份能力已补齐；具体自动连续性能力仍按buyer另切片 |

旧协议deferred清单有已经被后续Change启用的条目，不能整段当当前欠账复制；上表结合当前源码和后续状态限定。

用户后续建议参数化`/{device}/{detail}`、detail只管局部。修正上表重识别范围：若设备上下文与当前局部身份信息已有充分证据，复用既有父级上下文签名规则即可区分，不应统称必须新增语义重识别能力。真正仍缺的是相关上下文未被观察/接纳时如何取得可靠判别依据，以及未承诺的复杂重识别；模板复用与实体实例身份分开。具体设计边界见design.md“参数化上下文与局部职责”，未冻结新协议或实现。

进一步收敛：用户明确完整上下文由层级表达、当前页Slice只呈现本页语义。当前页与上层上下文的最小有据关联及读取已具实际消费需求，列入本期“统一记录/真实消费”的设计核对；不再将这一最小关联笼统归入全套关系能力延期。Slice仍为World revision派生的只读局部投影；完整关系全集、生命周期及跨会话连续性仍按各自buyer处理。具体关系类型/公共载荷未冻结，不能从该需求直接推导Contains/Overlays生产已获实施授权。

## 需要继续核验的风险，不计为已证实bug

- 普通状态claim遇到迟到/相矛盾证据时，时间保护、权威裁决及消费是否一致，仍需具体反例与路径核验。WorldModel.cs:654-656的revision freshness时间单调，不能单独证明每条claim不会被错误覆盖；也不能据此推断已有漏洞。已关闭ui.screen/语义exact-capture修复不重开。
- 冲突新鲜窗补充核实：KernelRunDriver.cs:156固定3秒，act和终局路径:1072/:1795使用，ConflictResolver.cs:84按capture时间差检查。标定与完整可观测性仍未完成核验，不能由常量存在推出可靠，也不再说未找到当前机制。
- 自定义scope读取一致性是静态潜在风险；当前两个正式Host装配不能用合法输入生成原报告要求的当前容器claim冲突，未动态复现，不列作World首期的已证前置bug。

完整Claim Supersede/Withdraw、身份merge/alias等继续保留原设计的扩展条件；本次不因词汇未全部实现就升级成当前阻塞缺陷。

## 下一步与验证状态

下一步先补前四项的具体数据流与行为稿：从一个有界观察进入World，到四类缺口派生、下一次观察更新、消费者及Agent读到结果。对同capture/旧revision/错归属/冲突/重复证据/部分覆盖/页面重访逐项给出预期，先辨清实际生产输入，再审阅公共载荷；不先承诺全知或全图。

method：当前源码、架构协议与已关闭Change交叉核验；一名只读Worker限定核验三项遗留风险，Leader区分已证事实、潜在风险与未核实项。

expected：本期必需、已接受待实现、按buyer延期及未证风险分开；不重开旧身份修复，不混入Goal/Runtime职责。

actual：形成上述清单；当前源码仅静态读取，未运行构建、测试、设备或模型。各类新载荷、实际Agent输入及新增反例均没有本轮运行证据。

evidence：本文件源码锚点；q5-design-assessment.md、agent-information.md、design.md及已关闭WORLD-OBSERVATION-001/WORLD-SEMANTICS-002/WRC-003状态；evidence/world-gap-entry.json、evidence/world-gap-verification.json。
