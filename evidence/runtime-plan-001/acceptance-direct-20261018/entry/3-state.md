# RUNTIME-PLAN-001 — 可组合产品Plan与有界本地展开实施
lifecycle_state: VERIFY · disposition: none · depth: decision-heavy · base: 2225c642

## Intent（WHAT/WHY）

Owner发现正式Settings遍历主要返回Act，要求先研究通用Harness、形成观点文档，再grill-with-docs。Q1明确引入Plan的首要目的为产品的泛化执行能力，并减少逐步请求Agent导致的延迟与成本；不能只做规划展示或给单步Act换外壳。Q2明确采用产品级规划机制，不同场景在共同机制上细化策略或规则。

## Scope / Out of Scope（上一阶段记录；当前授权见文末新增实施节）

- 本轮为研究、代码/协议审计、grill、设计定稿及实现准备。主文为proposal.md；两轮证据为docs/research/general-agent-harness-planning-evidence-20261018.md与runtime-plan-design-review-20261018.md。Q1-Q9及复审五项经代码核对/应用说明后由Owner“好继续”确认合入，架构选择记ADR-0053，实施顺序落plans/runtime-plan-001.md。
- 研究通用Agent Harness作为产品架构输入；仅文档及grill中已定型术语的CONTEXT增补。不修改开发Harness控制面、产品代码/测试/Prompt/Schema/配置或已接受ADR，不启动设备、不提交推送。
- ADR-0052/RUNTIME-OVERVIEW-001已关闭，作为本Change的已实现前提，不重开或冒充其包含正式Plan wire。SETTINGS-COVERAGE-005仍未完成，保持独立事实。
- grill共同理解与方案改进已确认，无重复确认问题；本次继续完成文档定稿及实施准备，产品实现尚未启动。ADR-0053接受设计选择，不表示正式协议或代码已经更新；旧ADR/架构及历史验收不改写，不在adapter绕行。
- 共享dirty checkout不清理。入口status与tracked patch：/var/folders/_3/lbphn3p95f3dj8ds3z1874q00000gn/T/uniclaw-runtime-plan-001-i7n7fos2/。研究Worker仅写唯一证据笔记，Leader负责本地审计和主文。

## Confirmed Inputs / Current Facts

- Owner授权的是“先研究形成文档，之后grill”，不是直接把所有决策强制改成Plan。
- AGT-009已建立进程内AgentDecision.Plan、有界有序Act/Observe/Control及逐项执行；DSH wire扩展明确留在当时范围外。
- 正式DecisionJsonConverters及Prompt尚无plan分支；最新真实模型小范围记录为6次Act、2次NoAction。内核支持不等于线上Agent可用。
- CONTEXT已定义Adaptive Planning和Agent Strategy State，产品android-automotive-ui-testing guidance要求方向→执行→精确；认知规划已有语义，不把缺少typed wire等同完全没有规划。
- PlanExpand逐项复检预算/冲突并经既有动作链；明确续额复核当前在NeedDecision建立。开放多Act Plan前须核对中途回决策边界、计划假设失效、步骤锚点与预算账本的组合。
- World Model图、任务进展、认知计划及Effect许可仍属各自Owner；外部Harness机制不转移Uni Kernel的Run推进权。

## Decisions / Alternatives

- D1（Q1 Owner纠正优先级）：Plan服务于泛化执行，以及减少每个动作都请求Agent所产生的延迟/成本。规划可追溯、预期和偏差分析为支撑要求，不取代该首要目标。泛化不要求预先知道所有具体动作；需讨论计划可表达的策略与Runtime可自行展开的范围。按该目标，仅增强阶段意图而仍逐步咨询，不足以单独证明本Change达标。
- D2（Q2已明确）：建设产品级规划机制，各场景可在共同机制上细化策略/规则；Settings是首个真实验收买方，不把核心写成Settings专用流程。通用表达、场景规则与执行权威的具体缝后续收敛，不直接扩大成任意代码执行或新调度平台。
- D3（Q3按推荐确认）：产品执行Plan同时支持具体步骤与可判定的有界规则，Runtime可根据当前事实选择、展开、执行和验证，不要求预编所有具体动作。筛选、动作与验证判据须落到已提供的能力/规则；需要新语义判断、规则无法覆盖或预算复核时再咨询Agent。每步观察、权限检查和验证不省略。已同步Product Execution Plan术语；术语不意味着新增第二套计划类型或另建执行器。
- D4（Q4按推荐确认）：产品提供有明确语义、可校验且可组合的基础操作与条件；Agent按目标选择、组合并参数化，各场景细化策略、判据和限制。观察、筛选、选择、执行、验证、条件转移及有界重复为讨论中的基础能力例子，具体目录由真实买方验证；不逐任务写死完整流程，也不直接执行模型任意生成的代码。
- D5（Q5按推荐确认）：再次咨询按事件和阶段检查点触发。新页面/新入口仍可由已有规则处理时继续本地执行；计划内补证/恢复仍无解、需要新语义判断或调整策略、阶段需评估或预算复核时回Agent。每步观察/验证继续，不因页面变化本身自动重问完整Agent。
- D6（Q6按推荐确认）：UniAgent提出计划内容与调整理由，Runtime记录实际采纳的版本、执行位置、每步结果及中断原因；Agent消费同源overview后提出修订，不直接改写执行事实。World/任务证明/预算仍由既有Owner管理，记录的物理形状或事件投影方式由后续工程设计确定，不因此增加独立事实权威。
- D7（Q7按推荐确认）：修改形成新版本，在当前动作完成或失败并对账后的合法边界接替未执行部分；旧计划、已完成工作、失败证据与消耗全部保留，不自动从头重做。首版不做任意执行节点热修改。仅复核/暂歇而未修改计划不必生成新版本；恢复前仍须核对当前事实与剩余计划适用性，这是D3/D5的既有约束。
- D8（Q8按推荐确认）：首版保证同一运行实例内暂停、咨询、预算复核及修订后的正确续行，保留可审计运行证据；不承诺进程崩溃/重启自动恢复Plan。完整Run/在途Effect恢复另行明确，不能把咨询journal当成自动恢复检查点。
- D9（Q9按推荐确认）：以真实Settings的已知多步路径及动态候选集合验证设备行为；另用结构不同的可替换仿真任务验证仅更换场景规则/能力而不改共同核心。与同条件逐步Agent基线比较，要求代表段减少完整决策调用，并统计全部模型/慢感知调用、token/费用及总时延；实际改善门槛由实测基线标定。仿真不是第二个真实设备适用证明。
- D10（复审及应用说明后“好继续”确认）：采纳R1-R5，合入同一方案：阶段预期与当前可执行段分开，首版收紧到实际买方需要的步骤/候选/有界迭代/检查点；明确前提、结果、在途及对账后续行。既有Policy未知出口保持；明确继续当前Plan与单纯获授资源不同，NoAction不隐式变更含义。overview沿用原Owner事实并补计划关联，不用待办净减少衡量推进。首个切片验证小范围动态候选/受阻/中途复核后续行，再做不同结构仿真与逐步Agent/短Plan/规则Plan三组真实对照。架构取舍见docs/adr/0053-composable-product-plan-with-bounded-local-execution.md。
- 原A（阶段意图＋Act循环）保留对照；原B固定短步骤为具体形态之一；采用具体步骤＋可组合有界规则，不要求先预知完整动作树。操作/条件行为、场景缝、版本/续行及验收设计见proposal.md §6-10；不冻结未经买方验证的新通用协议或另建执行器。

## Grill Frontier

- 第一轮G1/G2已回应，见D1/D2。Leader原“阶段可追溯优先”的推荐由Owner调整，不继续按旧优先级设计。
- G3已按推荐确认，见D3。
- G4/G5已按推荐确认，分别见D4/D5。
- 继承已接受边界，不重复裁决：条件满足/不满足/未知区分；当前候选为空不证明已穷尽；计划局部结束不等于全任务完成；本地补证/探索须在既有范围内，未知无法消除则回Agent。此为既有PolicyTruth、ADR-0052和此前范围/未知决定在Plan中的一致应用。
- G6/G7已按推荐确认，分别见D6/D7。
- G8/G9已按推荐确认，分别见D8/D9；无待答产品问题。
- Leader已按D1-D9形成整体方案：有限组合/三态条件、当前候选选择、逐步场景校验、同一执行链、局部展开界限、版本/迭代证据、同实例续行及实测验收。见proposal.md §6-10。
- Q1-Q9及复审前沿已收敛，D10记录最终共同理解。五项已合入主文，ADR-0053及plans/runtime-plan-001.md已保存；当前planned表示实现准备就绪，不表示已实现。只有新重大产品取舍/已接受架构冲突才回RESOLVE，不逐字段提问。

## Acceptance

1. 有可追溯的一手来源，比较实际规划表示、调用循环、反馈/重规划、历史状态与验证，不只罗列框架名称。
2. 区分通用规划、Todo/Plan Mode、typed执行Plan、Policy、Act；区分厂商事实、本地已核验事实与Leader推断。
3. 明确收益、代价、适用条件与动态UI反例；不默认强制Plan能降低成本或解决遍历未完成。
4. 给出选择及被拒方案、职责、组合语义、动态UI示例、验收设计和grill决定；整体汇总与复审应用已确认，必要ADR与实施切片可恢复，不提前冻结未经买方验证的字段/性能数值或新执行器。
5. 文档保存、链接与格式检查通过，状态和索引同步；报告设计/实现准备完成及尚未实测部分，不冒充产品实现验证。
6. 后续实现须以目标/权限/验证保持一致的代表性场景，证明可在不逐动作调用完整Agent决策的情况下执行有意义任务段；记录咨询次数、模型及慢感知调用、token、端到端时延与达成情况。未知范围不能预编具体动作，尚未提供策略的场景应明确返回Agent；仅显示计划文本不足以验收D1/D2。代表场景和可观察结果见proposal.md §10，样本及性能门槛须由后续基线实测标定。

## Owner / Authority / Residual Risks

UniAgent拥有语义规划与方向判断；Runtime拥有咨询及执行推进；World拥有图belief；Assurance/Effect维持验证与许可。风险包括把任务清单当执行器、把规划输出当事实、未来页面控件提前绑定、每步都重规划抵消收益、长Plan跳过续额复核、完成锚点与计划版本混配，以及开发/产品Harness概念混用。

## Implementation Readiness / Assumptions

- HOW与逐片可判定结果见[实施计划](../../plans/runtime-plan-001.md)，设计权威见[ADR-0053](../../docs/adr/0053-composable-product-plan-with-bounded-local-execution.md)；当前源码现状仍以proposal.md §4为准。
- 有界规则能否降低总费用/时延、正式模型能否稳定生成最小组合，仍为待实测假设；不预先宣称收益或固定改善百分比。具体字段由Settings与不同结构仿真买方共同验证。
- 定稿前保存本Change文件及工作区status：/var/folders/_3/lbphn3p95f3dj8ds3z1874q00000gn/T/uniclaw-runtime-plan-finalize-sjaf0xh7/。本轮只改本Change主文/状态、新ADR/实施计划及既有Plan术语，不动产品代码或清理共享修改。

## Verification（前一阶段文档定稿历史，非当前实施结论）

```yaml
verification:
  level: CONTRACT
  method: "primary-source research and current code comparison; Python pathlib/re local-link/code-fence/Markdown-whitespace/decision-presence checks over state/proposal/ADR/plan; git diff --check on scoped tracked files; python3 tools/gen-open-changes.py"
  expected: "confirmed design with R1-R5 integrated, explicit ADR boundary and executable implementation slices; consistent documents/index; no product implementation changes"
  actual: "D1-D10已确认，五项改进合入主干；ADR-0053与实施计划已保存，动态候选/续额后继续作为首个贯穿验收，结果/未知/在途及明确续行规则已写清。文档与索引检查通过；尚无产品实现、设备或性能验收"
  evidence: "changes/RUNTIME-PLAN-001/proposal.md; docs/adr/0053-composable-product-plan-with-bounded-local-execution.md; plans/runtime-plan-001.md; docs/research/runtime-plan-design-review-20261018.md; changes/INDEX.md"
```

## Status log

- 2026-10-18 · UNDERSTAND · Owner要求先搜寻通用Harness/业界方案、形成文档，再grill-with-docs；保存共享入口，复用旧研究，单个研究Worker负责外部一手资料，Leader核对现有Plan/Act/Adaptive Planning调用链。尚未开始设计问答或实现。
- 2026-10-18 · UNDERSTAND→RESOLVE · 单个研究Worker完成证据笔记，Leader对照一手页面与当前源码，补齐LangGraph Plan-and-Execute及Settings单步guard/预算复核阶段缺口。主文形成；当前开始第一轮grill（G1/G2），不预选实现方案、不修改产品或已接受ADR。
- 2026-10-18 · RESOLVE · Owner回应Q1：Plan首要目的是泛化执行并降低逐动作Agent咨询的延迟/成本；Q2：产品级规划、场景细化策略/规则。记录D1/D2并修订主文：阶段展示/固定短步骤不足以单独达标，当前前沿为G3计划表达及Runtime本地展开边界。
- 2026-10-18 · RESOLVE · Owner按推荐确认Q3，记录D3并增补产品执行Plan术语。核查PolicyProtocol现有封闭谓词与固定动作模板，明确现有Policy不是通用集合选择器；前沿推进到G4规则来源/组合、G5再次咨询时机，不实现或绕过冻结协议。
- 2026-10-18 · RESOLVE · Owner按推荐确认Q4/Q5，记录D4/D5并同步主文；将继续/结束/未知与既有范围和完成判断对齐，不把队列为空算全任务完成。下一前沿为G6内容与执行记录Owner、G7计划修订替换方式。
- 2026-10-18 · RESOLVE · Owner按推荐确认Q6/Q7，记录D6/D7并同步Product Execution Plan术语；核对现有Plan状态仅在内存、咨询journal不提供完整Run恢复，提出G8首版恢复范围和G9泛化/性能验收边界。仍为文档grill，不启动实现或测试。
- 2026-10-18 · RESOLVE · Owner按推荐确认Q8/Q9，记录D8/D9；Leader将主文收敛为整体方案，明确有界展开/三态条件、逐步场景guard、Plan中途复核、节点与迭代/实际尝试身份、同实例恢复及真实/仿真对照验收。文档检查后待最终共同理解确认，不启动实现。
- 2026-10-18 · RESOLVE · Owner要求再次检查业界方案与当前设计；研究Worker完成有限前瞻/SayCan/BT/Voyager一手笔记，Leader补核官方工具设计、Sequence语义、Agent评测及当前Policy未知出口。proposal.md §12提出R1-R5改进，保留D1-D9，不发布accepted ADR、不实现；纠正来源出版年并区分前瞻范围/工作配额，不引入未经标定的通用步数顶。
- 2026-10-18 · RESOLVE→PERSIST→PLAN · Owner在现有代码核对及人话应用说明后回复“好继续”，记录D10并将五项改进合入主文；接受ADR-0053、保存plans/runtime-plan-001.md，首片以动态候选/局部受阻/明确复核续行为主。完成文档检查与索引同步，仅文档定稿/实施准备，产品尚未实施。

## 本次实施授权（2026-10-18，追加，不改写历史）
Owner明确要求Leader实施ADR-0053并完成代码、正式协议/Prompt与生成物、独立评审及必要验收。上文仅文档/不启动产品的限制属于前一阶段，现由本次授权扩展；Q1-Q9/D10保持已决，不重复确认。沿用plans/runtime-plan-001.md的Driver/codec/Settings逐步guard/journal公共验收缝，TDD在这些已批准缝实施。ADR-0052为已完成前提。
共享checkout入口可恢复快照见evidence/runtime-plan-001/entry/snapshot.json（完整dirty归档+tracked patch+逐文件hash）；不重置、不清理、不提交推送。最多Leader+一个普通Worker，Worker不得再委派；模型按Owner指定，不静默替换。构建/测试仅一个重型进程，设备每次一组；入口没有运行中的重型构建/测试或模拟器，既有adb服务保留。新Plan操作不登记Capability、不引入第二Run驱动；若实际需要新Capability则返回对应语义门。
- 2026-10-18 · PLAN→IMPLEMENT · 核对新授权、ADR-0052已关闭证据与当前线性Plan缺口；保存4575个dirty文件的可恢复快照，按动态贯穿切片开始实施。

## 当前实施进展（持续更新，非关闭声明）
- 核心首片已TDD贯通：同一ForEach Plan处理A、Owner有证据受阻B、动态新C；阈值复核后ContinuePlan精确续行，2次独立验证且receipt/候选来源/attempt身份关联（evidence/runtime-plan-001/core/dynamic-red.log、dynamic-green-attempt11.log）。后续核心边界及全套回归尚在推进，不把此单例写成全验收。
- 正式codec新增plan/continuePlan并同步已有blocked语义出口，动态Plan实际往返1/1；Host候选投影、文件journal动态Plan与位置回放、现有动作策略初步21/21（protocol/wire-green-attempt1.txt；host/host-green-attempt2.txt）。旧调用方未安装逐步guard时仍拒绝多动作，不做全Plan旧页面预放行。
- 已安装SettingsCoverageRunner/HostRunner当前动作guard，候选由已有Owner账本投影，实际物化步骤用于receipt归属；追加原提案咨询journal及plan-execution/候选快照/逐动作guard证据。新增不同模式Host贯通测试、严格wire反例待运行。
- 真实已知路径/当前多候选三组对照入口、统计脚本及运行前procedure已保存；尚未启动本Change设备或真实模型对照。必须先Step基线并保存门槛，再运行Plan对照。所有失败/缺失usage/不可得费用单独报告。

- 核心首轮全量929/929、重点74/74通过（core/kernel-full-suite-final.log、kernel-focused-suite-attempt7.log）；Host两种Plan贯通及guard/journal/candidate回归24/24（host/host-integration-attempt3.txt），正式模型通道全量184/184（protocol/agent-dsh-full-attempt1.txt）。实际设备/模型对照尚未开始。
- 正式生成物哈希9e9aa4a…已同步，Prompt/Profile revision 14及双副本hash检查通过；workspace schema检查13组通过。Leader检查发现旧Control AlreadySatisfied分支尚按顶层游标推进，正在补反例；不同结构记录买方补强中，以上数字不表示最终验证完成。

- 2026-10-18 · IMPLEMENT→REVIEW · E4仅推进当前ForEach body并对账AlreadySatisfied；聚焦/grounding/gate/root失败收束同次attempt，普通Act不污染Plan历史；规则迭代不能使用旧step:N.M完成锚。记录买方通过Owner补证与独立结果义务核验，局部受阻不计完成。最终重点37/37、Kernel936/936（core/focused-green-final.log、kernel-full-suite-followup-final.log）；新Worker按dirty入口增量独立评审，真实三组对照尚待实测。

- 2026-10-18 · REVIEW→VERIFY · 独立新Worker按dirty入口71文件增量分别完成规格/规范审查，无静态阻塞Bug/硬违规；现场三组对照仍为未完成验收（review/independent-review.md）。20个执行场景仅认证元数据更新，Simulation188/188、最终Host339/339、Host.Dsh34/34通过。3081实际为独立uniclaw-product profile：误用默认web部署后按部署前备份恢复41文件，仅重载原空闲专线；前置错误单独留档，未开始现场试验。

- 2026-10-18 · VERIFY→IMPLEMENT · 首个真实Step基线进入后no-response，保留失败消耗不作为有效基线。E4定位咨询元素默认只兼容单容器，历史页保留后子页Elements=[]；公开两页反例RED→默认采用World当前anchor后43/43GREEN。另发现现有DSH retry pending漏decisionId，合法提案expected undefined拒绝，由单Worker限定修复并核对物理回合序列化；不改任务范围、权限、观察或模型/代理（protocol/baseline-failure-diagnosis.md）。

- 2026-10-18 · IMPLEMENT→REVIEW · 后续重试失败/超时物理屏障反例RED 0/2，修复为只清本次pending、保留activeTurn至turn/end/显式retire；最终相关5/5，独立只读复审结清（review/post-live-fix-review.md）。Kernel936/936、Host339/339、Simulation188/188、Host.Dsh34/34通过；已重载专线新代码。
- 2026-10-18 · REVIEW→VERIFY · 后续Step/2动作2/2、Owner CoverageComplete，但模型提交intent假dispatch锚，Runtime正确转人工完成裁决；Step/3、4模型原始及retry参数缺JSON闭合，provider MALFORMED_RESPONSE，保留全部成本且均不算有效基线。统一提示补完整JSON封装例，Prompt/Profile15、hash8829fab7…，不补齐坏JSON、不放宽校验、不增重试、不换模型。有效Step基线和Plan对照仍未完成。

- 2026-10-18 · VERIFY · revision15同编译产物下Step已知路径5号及候选集合1/2号均Completed/Owner CoverageComplete、验证2/4/4步、usage完整；baseline-metrics.json保存3有效+4历史无效样本。Plan结果前freeze_thresholds.py断言无任何Plan trial并写thresholds.json：已知路径≤2咨询/5模型调用、80167 token、21142.1629ms；候选≤4咨询/9模型调用、207364 token、34817.446ms。组内全部尝试消耗按有效完成数摊计；token/时延独立不回退，货币费用不可得。开始正式Plan对照，未预判通过。

- 2026-10-18 · VERIFY→IMPLEMENT→REVIEW · 首轮真实Short/Rule均功能完成但收益未达门槛（Short3咨询/7模型调用/1308713token；Rule4/12/已报告1942063token下界）。定位完整attempt原始eID反复送入咨询；公开Driver RED→紧凑引用GREEN1/1，Plan38/38，完整审计及Verified完成执法保留。Source文案明确返回属于每次body，Prompt/Profile16明确Act已有独立观察验证、欠证才补Observe/Defer。
- 2026-10-18 · REVIEW→VERIFY · Kernel937/937、Host339/339、Simulation188/188、Agent29/29、Agent.Dsh184/184、Host.Dsh34/34串行通过；20场景按最终源重认证。独立复审后准备compact新epoch。旧baseline/thresholds/trials不移动不覆盖；新Step6/候选3/4完成后才冻结thresholds-compact并运行新Plan组。Node全量上一轮39/44因fixture未清理仍在途retry；已补真实detach afterEach，待最终全量复跑，不把未复跑写通过。

- 2026-10-18 · VERIFY · compact epoch三份Step均有效且usage完整：known6为3咨询/6模型/80311token/20734.5604ms；候选3/4均5咨询/10模型，均值208962token/35876.5406ms。正式Plan前freeze_thresholds.py --name compact按完整条件确认没有本epoch Plan并保存thresholds-compact.json（门槛≤2/5与≤4/9，token/时延分别不回退）。开始预定六份Plan现场运行。

- 2026-10-18 · VERIFY→IMPLEMENT→REVIEW · compact首次Known短/规则均功能完成但3咨询未达门槛（短132808token/27973.9086ms）；候选短1号JSON无效0动作，batch中止，全部成本保留。发现完成根页仍发STAGNATION，公开Host扩展2反例RED，逐层定位到缺本地观察前receipt对账及entered仅取显式mark。修复为External前对账、真实已验证根页进入与显式到达去重、完成Root不新发停滞；10/10含原popup-loop反例通过。Prompt/Profile17明确检查点已完成可直接NoAction、无需Continue空尾或重复已有subject观察，hasha1574ef2…。
- 2026-10-18 · REVIEW→VERIFY · Host341/341、Agent29/29、Agent.Dsh184/184、Host.Dsh34/34串行通过，Kernel937/937与Simulation188/188未受后续Host私有接线/提示修改影响；场景认证检查通过。部署实际uniclaw-product3081 revision17，准备Step7及候选5/6，再冻结thresholds-final，未运行该epoch Plan。

- 2026-10-18 · VERIFY · 第三epoch三份Step功能通过，一份usage缺失；Plan前冻结保守下界门槛，独立复审确认完整Plan总量≤基线下界足以证不回退。该批次与补充样本均保留，仍有规划形态不遵从、额外失败调用/时延等未达标项，不关闭。
- 2026-10-18 · VERIFY→IMPLEMENT→REVIEW · 定位DSH Provider在已收到完整usage后先校验非法tool JSON而抛错，导致计量丢失；独立DSH checkout修改前源/测试/docs/bundle快照已保存。最小调整为settled响应先yield真实usage再拒绝非法输出，RED2/47→源stream/runtime/Loader组合209/209；发布产物plain Node实测usage17+MALFORMED_RESPONSE+无finish，定向完整headless会话回放1/1通过。没有修复坏JSON、增加retry或换模型；双语说明/pairhash同步。复审无产品阻塞，恢复快照与当前bundle身份分别记录。
- 2026-10-18 · REVIEW→VERIFY · 独立专线PID32738加载实际provider bundle3c05833a…，harness metadata新增实际provider哈希，首次规划形态指导精确化，Host.Dsh34/34通过。accounted epoch Step8虽失败但4模型调用usage完整；Step9及候选7/8有效且usage完整。Plan前冻结thresholds-accounted，基线失败进入成本分子，调用门槛严于有效Step；正在运行已知短5/规则4与候选短5/6、规则3/4六份对照，所有旧样本/门槛保持冻结。完整验证与性能仍未宣称通过。

- 2026-10-18 · VERIFY · accounted六份Plan均完成任务且usage完整，known-path短Plan3咨询/7模型未低于有效Step3/6，其余三个Plan组通过全部预注册门槛（comparison-result-accounted.json）。Owner明确授权“更换模型并重跑对照”，已备份正式binding并选择同官方provider catalog的deepseek-v4-pro；原Flash及所有样本保留，代理/凭据/Prompt17/权限/逐动作验证不变。开始Pro独立Step基线，完成后先冻结pro新门槛再运行六份Plan，不能混用Flash收益证据。

- 2026-10-18 · VERIFY · Pro Step known10、候选9/10均有效、usage完整。baseline-pro-metrics已在Plan前保存；freeze脚本因按完整RunDir字典序把9选在10之后，误选旧Flash条件并拒绝冻结。Leader编排漏判失败仍启动known Short6，随后停止batch，未启动其他Pro Plan、未读取其性能/决策结果。选择已改为运行时间戳，基线文件/hash不变，既定公式盲算数值保存model-change-pro/blind-gate-calculation.json，偏差保存registration-deviation.json；独立评审处置中，不冒称该trial正常预注册。

- 2026-10-18 · VERIFY · Pro门槛以结果盲法late lock锁定：原baseline哈希不变、原公式不变、显式pin Pro/provider bundle，锁定后才读已启动的两份known Plan结果。文件时间审计纠正暂停前batch已推进至Rule5，deviation完整列出Short6/Rule5，不伪称严格pre-Plan注册。独立复审允许保留普通功能有效性及完整成本分母/分子；known短2咨询/4调用/48715token但28942.2851ms高于固定27745.0817ms，其余待汇总。结果触发且在追加前固定两模式各补2次，四次全部完成再看数，不改基线/门槛、不提前停止、不无限追PASS（fixed-known-path-replication.json）。
- 2026-10-18 · VERIFY · Node完整44项得到43/44，实际失败为PRF-009直接挂载夹具未登记afterEach清理，使retry物理屏障残留到PRF-003；仅补该夹具activeHarness登记，产品src不变，待最终44复跑。evaluator按独立复审建议要求真实可读调用账本才允许actual-call子门槛通过，当前试验未触发缺失账本。


## 当前实施验证四元组（最终本批次，保留VERIFY）
```yaml
verification:
  level: LIVE
  method: "公开Driver/Host/正式wire回归；不同结构记录仿真；注册Settings真实Step/ShortPlan/RulePlan；同模型同条件、固定数值门槛与全部尝试成本；独立规格/规范及结果复审"
  expected: "逐动作权限/观察/独立验证、动态候选/局部受阻/明确续行不变；代表段减少咨询和实际模型调用，token与正式时延分别不回退"
  actual: "Kernel937/937、Host341/341、Simulation188/188、Agent29/29、Agent.Dsh184/184、Host.Dsh34/34、Node44/44全部通过；DSH Provider209/209及实际bundle/headless定向回放通过。Pro3份Step和10份Plan全部功能有效、13会话实际模型与正数usage核对通过；候选短/规则两组全性能门槛通过，known短调用与时间、known规则时间未过。两份已启动Plan在未解盲前late lock及结果触发固定追加四次明确披露；不把探索性证据写成严格无偏差预注册。性能验收未全部满足，保持VERIFY，不关闭。"
  evidence: "changes/RUNTIME-PLAN-001/verification.md; evidence/runtime-plan-001/comparison/comparison-result-pro.json; evidence/runtime-plan-001/comparison/model-change-pro/results.md; evidence/runtime-plan-001/review/post-live-fix-review.md; evidence/runtime-plan-001/integration/plugin-tests-final44-green.txt"
```
- 2026-10-18 · VERIFY · 固定追加四次全部完成，Pro汇总known短3样本平均2.33咨询/5.67模型/75613token/32.69s，known规则3样本2/4.33/51829/30.37s，未过全部门槛；候选短2样本2.5/5/74586.5/37.26s、规则2样本2/5/65437/39.14s均全门槛通过。所有Pro会话usage完整，真实模型选择核验通过，旧13次usage缺失仍未知；全历史49尝试40有效331模型，token下界7900624。Node最终44/44、exit0，夹具修复结清。仅停止本次注册emulator-5556；不提交/推送两个仓库，不放宽门槛、不追加追PASS，不关闭本Change。后续若继续解决2动作代表段收益，应先检查Control Reobserve/Observe提示语义及模型重复/缺ID提交，再以明确的新产物重建对照。

- 2026-10-18 · VERIFY · Owner确认暂不采用Pro；正式selected恢复deepseekFlash，仅保留Pro choice和全部历史对照，修改前binding另存、前后hash及授权见comparison/model-change-pro/returned-to-flash.json。未重启服务、未调用模型、不改Prompt/代理/凭据。读取本批真实journal turn/start→turn/end计时：Flash有效组模型会话包络占正式耗时约27%-45%，Pro约34%-53%；纯网络未单独测量，不能直接归因为网络瓶颈。已知Step样本Flash约22.0s中9.0s在模型会话内、其余13.0s；Pro约27.7s中14.5s在模型会话内、其余13.2s（latency-envelope-audit.json）。余时包括设备操作/观察验证/编排，不进一步冒称细分。性能状态仍VERIFY。
