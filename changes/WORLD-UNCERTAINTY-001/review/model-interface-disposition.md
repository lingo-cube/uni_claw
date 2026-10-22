# 核心模型与Interface独立处置稿

2026-10-23。接续../model-interface-review.md，按最终Q1–Q20/spec审阅；工程行为契约可交实施，公共类型名/字段编码由实现Leader依据caller最终固定并单列真实diff复审。没有新增Owner或Registry能力。本文不是几组新Ixxx接口的批准，也不以类型就绪代替producer/consumer交付。

## 按实际读写需求收敛模型

| 信息/现有模型 | 必需最小增量及Owner | producer→buyer；复用/候选理由 |
|---|---|---|
| ContainerIdentity/ContainerBelief | 保持身份，不加导航generation充当身份；访问发生关联只保存足以区分的合法来源，未知可表达 | 已接纳观察+已验证导航/交互→World→局部读取/多轮解释。ObservedNavigation拒绝同端点，不能强改为访问计数；同C真实重入如无现有来源须在实际验证出口提供最小发生关联 |
| PageCaptureObservation/Provenance/EvidenceRecord | 复用capture/cycle/session/time/frame、derived parents/leaves、FusionRule/Version；处理来源补可核验规则/配置引用，保留原采集时点，缺失明确unknown | 感知执行Owner→P2 Ledger→World→回放/Agent。SemanticObservationProjector当前只含semantic.page:1、capture、request；不足以证明模型/提示/预处理都钉定。不要把新版模型名转成优先级 |
| 当前页与上层关联/ContainerRelation | 只为当前页上下文buyer提供被观察且有依据的最小typed关联；端点、语义kind、依据及适用/失效信息 | Hierarchy/上下文合法观察→World→局部解释和只读展示。ProductAssociationStrategy的Relations为空；导航边不证明Contains。不能从URL/任务目标造父关系；来源未证保留关系gap，不建全关系生命周期 |
| 局部scope与Slice | 复用DeriveSlice的Container集合，补buyer实际需要的页内区域/成员界定和来源frame；同源不同范围Id可分，scope集合ordinal去重排序。固定S带W；局部内容依赖标记独立 | World内部范围判断→只读Slice→Core/页面表达/Control各取需要的信息。已支持局部多Container不强制子树；页内不铸RegionIdentity。不让调用者传任意predicate从aggregate自行投影 |
| 跨观察累计 | World内部按对象/字段保存旧观察支持、当前对应/适用依据、时序/空间兼容及覆盖限制；当前occurrence与历史内容事实分开 | accepted O1/O2及有据viewport→World→已知页面表达/当前字段消费。多Slice从一个选定W读取，不能由renderer拼旧S建立真值。历史内容可无当前locator |
| 解释/归属纠错 | 目标原O/E/解释及原访问/范围、当前新依据、接纳W、被取代解释、受影响依据及未能界定范围；原记录不改 | 新的合法观察/派生证据→Ledger→World的窄纠错reconciliation→新Slice/摘要及原消费Owner。此为已接受Q15/20具体buyer，不购买全局admission withdraw/supersede。处理当前页以外旧O不能伪装当前PageIdentityObservation |
| 历史参考/当前沿用 | 每字段旧验证及本轮对应/适用依据、反证、仍未知字段；可读参考与当前采信分开 | World→Agent/页面表达先参考；本轮accepted对应充分后World派生当前表达。不是通用cache服务；用途/语义并非永远静态，状态/位置不随之继承 |
| 四类知识gap | 类别与reason/Conflict分轴；具体对象/命题/范围/依据、连续性、未解/解决/失效/相关性变化。可同根多类，不能互斥enum代表一切 | World派生belief gap→Control补证；咨询摘要联结原Owner的事实。必须每类生产/保持/解决路径，类型扩展不能静默吞掉 |
| 实际尝试/提醒 | 执行来源的真实attempt correlation、开始/结果/时间/完整性；Control维护有界补证及主动提醒去重，Runtime提供余量/deadline | capability/执行Owner的不可变结果→Control只读消费→Agent摘要。RequestsUsed仅调度额度，NextInput null不等失败；缺来源明示not_collected。提醒不是World claim，不能反过来投票销gap |
| 实际消费/影响 | Owner内部最小依赖与被消费快照/语义引用、关联I/B/R/J及可界定边界；World只输出被推翻事实和已知影响 | World受影响依据→Control重判计划、Assurance重判proof、Effect查可靠提交/receipt、Run保留Outcome。记录消费时捕获关联，事后不从时间相近猜因果；依赖不足显式未决 |
| 正式Agent载荷 | 充分中文/结构摘要、当前/历史/未知区分、各Owner来源和本轮变化、真实尝试完整性与资源；保留可核对输入 | Owner即时投影→ExecutionOverview/AgentDecisionContext→严格codec→DshAgentAdapter/DshOpenedHttpPeer→consultationPrompt。SourceFactRefs可附但不能代替正文；动态字段每次相关咨询更新 |
| 页面表达/展示 | 固定W/scope、组成Slice、已知语义/状态/布局、覆盖/缺口及来源；事实只读，非权威 | World→纯组合→复用Replay表达/diagnostic。当前实现位于Simulation tests，产品需要复用时抽纯表达代码到合理既有生产位置并保持依赖单向；不能直接引用tests。完整可视化交WORLD-VISUALIZATION-001顺序实施 |

以上是一组信息责任，可合并到现有Owner的领域记录及consumer-specific projection，不能逐行照造公开record/interface。World aggregate、全量EvidenceBasis和所有owner logs不跨缝倾倒。Core.ProjectSlice当前把source全部EvidenceBasis作观察依据，尚不能作为每字段精确依赖；保持现有兼容解释，同时新增buyer所需窄投影，不能谎称已有精确闭包。

## 五个操作的完整Interface语义

| 操作（职责名，非冻结签名） | 顺序/幂等/失败/retry/partial failure | 兼容与替代/删除测试 |
|---|---|---|
| 接纳后累计/纠错 | 先Ledger admission，再World核目标、来源、对象、范围、适用与冲突；构造新状态后一次publish。重复同canonical evidence/同纠错处理身份返回已有语义，不重建访问/尝试；目标不存在/来源失配拒绝，依据不足Unknown或Conflict。失败保留旧W，无半边归属；重复不同payload是冲突，不能当幂等 | 不放宽current-only capture gate；窄历史解释入口/操作模式须语义显式，不能bool开关绕gate。替代“最新覆盖”会丢历史且污染C2，拒绝；删除该Module会把一致纠错逻辑散至renderer/Agent，故应留World内部 |
| 派生当前/固定历史局部读取 | 当前消费从current即时派生，历史诊断显式指定W；无W/未知root/非法范围按既有fail-closed，空合法投影不代表世界Absent。相同q确定；纯计算可复用，view每次封装新W来源。读取无外部timeout；非法frame不悄悄换frame | 保留现有DeriveSlice(root,scope)调用意义；新增页内范围只为真实caller。旧Slice引用保留为legacy解释，不重算历史Id；新Id带编码版本并避免同一材料混用两个算法。新写统一，读者按材料/版本解释；不得扫描改历史BasisSliceId。替代“Id仍旧只改文件名”不能修Core依据碰撞 |
| 历史参考与当前沿用读取 | 参考查询无慢模型等待；当前采信以World的合法对应/适用/反证判定为前置，各字段独立。慢任务timeout/cancel不抹参考，不自动确认current；晚到矛盾按原来源入证据/纠错规则。重试仅调所缺字段，不强制每轮再跑同模型 | 不是consumer跨operation缓存view。现有Fast计算cache继续用；用通用LogicalItem承载所有标签会违反ADR-0015，拒绝。不存在两套稳定语义authority |
| gap/尝试/咨询组合 | World给belief gap，Control给相关/补证/提醒事实，Runtime给资源；在安全咨询边界取同次correlation快照。分页/截断必须报告省略数/未定完整性并保留影响动作的阻塞信息，不能静默少报。重复输入不主动新咨询，新真实失败/影响变化可再次触发；不能越过StepVerify | ExecutionOverview现有Host projector不取得World权威；在UniKernel/Owner命名派生缝组合，Host只传送/添自身事实。严格schema与生成物、prompt清单/哈希、surface allowlist、Context fingerprint/journal如覆盖载荷均同步。旧输入缺字段=not_collected，不是[]/0/已解决；不需要新查询工具前先给足摘要 |
| 影响交接与效果复核 | World提交新认识后，原Owner在下一合法动作边界对照新basis；不能靠异步诊断消息保证阻断。同步判断时应核当前basis，已跨dispatch边界的按可靠源处理；协作失败/结果不明保留未决。相关J可重判，但旧terminal不可修改；必要后续动作经合法流程 | 复用原Control/Assurance/Effect/Run seam；必要最小引用可扩展既有协议，不造补偿scheduler。原记录仅留历史无需动作。删除单独通用RepairManager应不影响功能，说明该新层无购买理由 |

## 可替换性、隐藏复杂度与迁移约束

现有IAssociationStrategy及感知executor已有真/受控供应替换，CW/CS明确两个Adapter穿过同一个真实Owner；本期新增纯Owner派生行为无第二实现时，不额外建IHistoryStore/IWorldQuery/ITrustCache。若确有新增外部Registry能力才走capability-component新能力裁决；当前设计不需要新Registry capability，故不引入新一轮产品问题。

隐藏复杂度集中在World内的支持/反证、累计适用性和新revision一致性，在Control内的执行事实关联/提醒去重，在Assurance/Effect内的效果/投递判定。可用内部索引/纯函数分割WorldModel partial，但不把它们变第二Owner。跨边payload仅带真实buyer需要的事实，不能带BindingAllowed/ShouldRetry/GoalComplete等消费判定。

实现前需固定三项具体工程数据，不向用户猜字段：①scope规范编码及legacy材料解释；②纠错目标/依赖/处理规则版本及各Owner捕获消费引用的位置；③四类与尝试摘要通过严格codec/生成物及最终prompt的形状。实现Leader将真实字段diff单列审阅，新增来源字段若改变Evidence canonical内容须同步id/golden兼容，不能保留旧hash冒充同内容；只有来源关联可复用时不重复复制原资料。

## 后续可视化buyer的数据保留

WORLD-VISUALIZATION-001需要的选定W、C/访问关联、O发生/解释来源、scope/S、前后归属/关系/缺口变化、旧参考/当前采信、已知依赖与未知影响边界均由本期核心需求本身购买。须以不可变可关联记录保留，不能只保留最新字符串覆盖原状态，亦不要求为展示先增四个counter或一套event平台。effects/Outcome从原Owner只读读取，缺则明确unknown。原时点解释和目前纠正后的历史解释可由旧W+新纠错关联分开；界面选择/布局/交互不是核心完成前置，按已授权顺序后做。
