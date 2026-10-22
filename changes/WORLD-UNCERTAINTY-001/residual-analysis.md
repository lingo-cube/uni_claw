# 遗留问题：业界思路与当前调用链分析

Status: DRAFT / EVIDENCE-BOUNDED
Authority: NONE
日期：2026-10-22。范围：研究与设计访谈，不实施产品、启动设备/模型或向其他会话派发任务。Q1-Q12和现有ADR继续有效。最新收敛：Q13接受延期方向（B），先聚焦World模型及必要消费；下文R1/R2/R3与完整遍历/H7改造保留为后续背景，不作为本期World完成前置。四类缺口的实际Agent输入证明仍保留。

## 结论

现有系统已有停滞复核、目标推进账目、完成前任务证据屏障和任务导航范围；不能把遗留问题重新描述成“这些机制完全没有”。当前需要补的是入口用途的正向依据、真实任务要求的端到端对应、进展信号的语义精度，以及真实运行的验收。外部框架提供分工和验证思路，不构成UI用途识别可靠性或本产品通过的证明。

来源综述见`residual-primary-sources.md`。以下“当前事实”来自源码；“迁移建议”与反例均为Leader推论，未运行复现。

## 1. 可以点，不等于该当导航去点

### 当前事实
- `SettingsActionPolicy.cs:328-346`拒绝无desiredState的switch，但其后普通tap/click仍直接映射navigate；禁词排除不能证明剩余所有按钮均是导航。
- 同文件`EvaluateCurrentStep`另核对目标唯一、enabled等，再可选调用`SettingsNavigationScope`；后者按当前route和任务正向条目约束，检查弹窗、目标和返回/根页滚动。它是任务范围约束，并非一般控件业务用途识别器。`SettingsCoverageConfig`末尾NavigationScope默认null，当前Load构造未把它变成所有装配必有项，不能仅因类型存在就声称普遍启用。
- 真实Fix connectivity输入/receipt/未验证到达见`evidence/ordered-closeout-20261022/navigation-classification-gap.md`。未证明它只导航、实际发生网络重置，或没有其他副作用；不能重新点击来碰运气验证。

### 一手思路及迁移
[Playwright动作检查](https://playwright.dev/docs/actionability)检查可见、稳定、可接收事件、enabled，并另提供后置断言；[W3C WebDriver点击算法](https://www.w3.org/TR/webdriver2/#element-click)定义交互过程。这些不决定业务用途，也不授予修改设置的权限。推论：至少区分①当前结构/可交互事实，②用途判断及依据，③任务允许的语义动作。事后Assurance验证继续保留，不能替代执行前用途与授权判断。

可用两条路线，均不以模型一句navigate直接授予许可：
1. 首期沿既有明确任务范围与正向用途依据闭合真实反例；范围外未知保留，不自动点击。实现面较小，动态覆盖范围受限，但可留下清晰的拒绝与未覆盖报告。
2. 首期一并设计并交付对新入口的用途补证能力：仍经原观察→Ledger/World→任务guard链，须有可判定的用途、相关性、冲突/未知与校准正反例。可支持更多动态探索，代价是新增生产/消费与可靠性验证，当前语义页识别能力不能直接冒充该能力。

Q12已接受路线1（A，用户2026-10-22“按推荐”）。首期先闭合明确范围，范围外保留未知；新入口用途补证另切片设计，最终动态遍历目标保留。具体协议若需新能力或公共接口，后续按真实buyer细化、审阅，不在本报告预造类型。允许未来扩展不等于现在承诺所有未知入口都能可靠识别。

拟验收：原始Fix connectivity反例零新Effect；已知导航/返回/允许滚动正例保持；模型伪报role、普通Button、用途证据冲突、失效/错capture和越任务范围均不点击。当前actual：未新增运行。

## 2. 停滞机制已有，仍需检验它“认为什么是进展”

### 当前事实与对旧叙述的修正
- `SettingsCoverageRunner.cs:32-44,734-845`已有Host停滞判断；比较去重进入/到达数与frontier数量，明确不看StepsVerified，也不要求画面/证据指纹不变；冷却后可以再次提醒。旧“弹窗每步verified导致永不触发”的描述不能直接当当前未修实现。
- `KernelRunDriver.cs:500-680,2831-2855`已消费稳定ProgressFactKeys、当前ActionableItems及事项级已资助事实；ADR-0052明确旧成果/换措辞不能支持同路线无依据续额，局部受阻可继续其他分支。不能再问是否允许无依据自动续额或重建第三套watchdog。
- `SettingsCoverageOverviewProjector.cs`已把发现、验证、排除、失败及要求状态带来源投影；这些状态类型不同。获得一份新诊断材料可能有探索价值，但不等于必需目标已经完成。

### 一手思路及迁移
[Kubernetes Deployment](https://kubernetes.io/docs/concepts/workloads/controllers/deployment/#failed-deployment)在期限内没有进展时报告ProgressDeadlineExceeded，状态报告本身不自动回滚。[Nav2 PoseProgressChecker](https://docs.nav2.org/rolling/configuration_and_development/configuration_guide/core_servers/controller_server/controller_server_plugins/pose_progress_checker/)配置最小位移/转角和movement_time_allowance；它测相对基准的运动，不保证离业务Goal更近。推论：复用现有复核出口，把目标相关、来源可查的进展作为输入；计数/时间只触发调查，仍不直接判任务失败或放行/续额。

需要验证的反例，而非本轮已复现bug：
- frontier数量相同而具体对象换成了有依据的新对象：数量相等是否误报停滞？
- 旧候选反复出现/消失导致数量振荡，但没有新目标成果：是否不断清空停滞窗口？
- 深页有效推进或新的必要检查完成，而根页进入数不变：现有摘要是否表达充分？
- 为已知阻碍重复写相同结果、换提示措辞或重放证据：不得再次当新成果；新的真实补证仍失败可按Q10再次复核。

这些性质可先用既有公开调用链确定性验证，再用预先声明的语料标定窗口；不在讨论时猜一个N或借固定累计硬停宣称全局必然终止。承认机制存在与证明其对全部目标有效是两件事。

## 3. Goal要求不是没传，而是缺少完整对应和范围语义

### 当前事实
- `SettingsCoverageRunner.cs:429-463`将配置目标、滚动/二级页/返回/重复访问要求和有界动态探索文字加入Contract.Objective，Agent不是完全看不到这些要求；Scope/预算另有既有字段。
- 同一Contract的显式mandatory义务主要是obj-end-on-root；但**不能据此推断只要回首页就完成**：goal-directed装配在`Runner.cs:343-352`提供CheckTaskEvidenceBarrier，Driver在TerminalEvaluation（`KernelRunDriver.cs:1788-1796`）先查覆盖报告未满足项，缺项返回TerminalNotProven，再走原终局评估。默认Settings配置已开启该模式。
- CoverageLedger.Report按当前配置要求给出Covered/Uncovered，不等于“设备全部可达Settings的发现已穷尽”。`UniAgent.Evaluate`仍消费RuntimeOutcome的既有稳定语义/义务，缺可验证criterion为Undetermined，不自己回读原始Evidence另造判断。

### 一手思路及迁移
[Cucumber Gherkin的Then](https://cucumber.io/docs/gherkin/reference/#then)要求以可观察结果核验预期，而不是用执行步骤代替结果。推论：先建立逐条对应表：用户要求→任务声明范围→由谁提供何种证据→现有完成屏障/正式义务如何检查→Outcome怎样表达→Goal评价能否判定。这里是设计校核产物，不要求引入Gherkin、另造任务系统或让Host报告授予完成权威。

当前能确认的最小表：

| 要求 | 现有供货/检查 | 仍需补齐的对应 |
|---|---|---|
| 回到Settings根页 | 当前世界route与mandatory obj-end-on-root | 保留身份/冲突/新鲜度原执法 |
| 配置一级页、滚动发现、二级页、返回和重复进入 | CoverageRequirements→Ledger.Report→Host缺项屏障 | 逐项核对要求范围、证据是否够用，以及Outcome/Goal是否保留同一语义，不能只靠英文Objective |
| 动态入口及子页发现 | 当前frontier/导航图/Traversal发现缺口 | 明确有限配置完成、已观察部分与发现穷尽的区别；不能将动态分支未进入的要求静默遗漏 |
| 额外能力检查/语言或状态任务 | 现有Task binding、handler结果、Assurance等原链 | 本报告未证明所有Task选项都进入相同完成条件，需逐项映射，不能把handler返回当检查通过 |

首要工作是补出具体映射与缺项，而非先把所有统计数字塞进World claim或替现有Owner造一个“TaskSuccess”。是否需要扩正式协议由映射暴露的真实缺口决定；新增公共模型/接口另列可审阅文档后再冻结。

Q12后补齐的限定映射见goal-completion-mapping.md：这条Settings CLI路径返回独立CoverageReport及分类字符串，未见接入正式UniAgent.Evaluate；RuntimeOutcome本身有义务/分类/依据，但不含逐项CoverageReport。这不否认Host缺项屏障。原D4已接受taskCoverage=reached+skipped和verifiedReachRate=reached/total，不把显式Skip计数当成本轮新发现的越权；Q12未知用途暂不点也不能被自动铸成Agent Skip。Q13已接受延期方向，正式Goal接通由后续独立切片处理。

## 4. 观察覆盖、发现穷尽和任务完成分别留证

[GraphWalker stop conditions](https://graphwalker.github.io/graphwalker-rs/stop-conditions.html)的edge/vertex coverage分母是给定模型的边/点，模型100%不证明真实应用模型完整。推论：报告必须说明当前设备/账号/权限/系统状态和任务允许范围；已观察、已执行/验证、被明确排除、未决和未证明发现范围分别列出。

当前`SettingsTraversalProjection.cs:75-77`仍诚实提供DiscoveryComplete=false；`SettingsCoverageLedger`依据有限配置计数得到CoverageComplete，goal-directed达到滚动配额时仅停止继续swipe、保留未满足事实并继续其他事项。不能把这个字段重命名成全树发现通过。未知用途不点击也不能变成“该页已覆盖”。滚动发现/弹窗关系生产需要单独真实buyer和来源依据，本稿未据类比证明其生产链已完整。

此处的后续设计目标是“声明什么范围，就证明什么范围”；现有完整Settings目标不能以降低任务要求追绿。若选择分阶段交付，要同时保留最终目标、当前阶段范围和未证部分，不能倒改旧失败或验收。

## 5. H7是真实验收缺口，不是再设计一个事件系统

[OpenTelemetry日志模型](https://opentelemetry.io/docs/specs/otel/logs/data-model/)区分Timestamp/ObservedTimestamp并可关联TraceId/SpanId；这些字段不证明端到端送达或exactly-once。推论：继续使用原事件seq/cutoff和决策相关性，验证“Owner生成→正式请求→真实DSH接收→模型可见输入”，分别留下原始工件。重复输入不成为新执行事实。

TRAJECTORY-001当前状态和bounded-live-01记录说明：已握手且有真实咨询/字段送达，但没有自然Heartbeat/CasePacket事件，不能宣称同cutoff的三组状态H7已通过。确定性故障注入用于证明逻辑，不冒充自然事件/设备验收；双方都应有自己的method/expected/actual/evidence。

## 6. 研究后的设计树与当前前沿

- R1用途/授权：原已决安全边界→既有范围与分类事实（已核实）→首期明确范围先闭合（Q12 Accepted A）→新入口用途补证另切片，再按buyer讨论证据来源、能力边界/接纳与校准；不先造字段。
- R2零推进：继承ADR-0052/Q4/Q7/Q10→核实现有M5/稳定事项事实（已完成）→前述反例与分支语义的工程设计→有界标定及真实送达；窗口值不是用户猜测题。
- R3Goal对应：已有Objective和任务屏障（已核实）→逐项要求/证据/Outcome映射（限定核验完成）→正式Goal接通延期（Q13 Accepted deferral/B）→后续独立切片按真实缺口派生模型/接口、沿原Owner验证；不让Goal回读Host原始报告另判Proof。
- R4覆盖：继承World观察覆盖/Control发现/Assurance完成分权→声明范围与未证范围→滚动/动态关系的具体buyer及性质→仿真/真实分开验收；不降低完整遍历的最终目标。
- R5H7：复用现有事件/实际决策传输→确定性与自然事件各自留证→同cutoff及实际可读负向验收；本轮不运行。

Q12已接受A；R3限定映射已完成，Q13按用户World聚焦方向接受延期。当前前沿回到World四类缺口、版本/证据与最小真实消费，不继续扩展Goal/Runtime/遍历设计。不重复问原Owner、Skip既有统计或让用户猜阈值。新研究未获得产品实施、提交推送或其他会话消息授权。

## 7. Q12之后的跨线阶段行为与进展拟验收（后续背景）

首期采用既有正向任务范围，且所有声明采用该阶段策略的正式入口必须实际接上约束；不能仅存在可选类型就声称已启用。缺必要范围/用途依据时不退回普通tap即navigate。未知记录与明确Skip分开，未知未访问仍保留，不能为使报告好看而自动排除。返回、滚动和明确授权状态操作按各自原规则处理，不用Fix connectivity关键词特判。

下表是应验证的输入输出，不是本轮运行结果，也不另设进展Owner、全局共享计数或固定停机值。

| 情形 | 应有进展/结果解释 | 提醒与完成约束 |
|---|---|---|
| 首次以有效当前证据完成某项要求 | 对应事项的有来源成果，只记一次 | 可供原复核消费，是否续额按原授权；不等于其他要求同时满足 |
| 任务要求重复访问一次，且该要求首次取得独立验证 | 可使这条明确的重复访问要求满足 | 不能因“页面来过”一律判无进展；要求已满足后的重复不反复制造成果 |
| 相同旧页/旧结果往返，没有任何未完成要求新证据 | 动作/验证记录保留，目标成果不增加 | 不用StepsVerified上涨重置停滞；不靠旧成果反复授额 |
| 有效新深页/检查成果，但根页计数或frontier数量不变 | 消费原Owner的具体事项/成果依据 | 数量相同不能掩盖真实目标推进；对象身份不由计数器铸造 |
| 旧候选反复显隐使数量变化，但未增加有依据成果 | 数量变化保留为观察，是否新发现由原Owner判断 | 不因数字波动无条件清空无进展历史；具体反例待验证，不声称已复现 |
| 新补证尝试超时或返回仍不足 | 新执行结果，不是目标要求已完成 | 按Q8/Q10说明结果并允许再次复核；不自动续额/解锁，未采集不填0 |
| Agent显式Skip且通过原协议 | 旧D4的处置指标可变化，实际到达率不因此上涨 | 与结构排除分账；未知暂缓不是自动Skip，完整访问目标不改称已满足 |
| 某任务要求证据仍缺 | 原任务屏障/Assurance/Goal各按对应语义留痕 | 新旧报告不能把“无法确认完成”说成成功；同一结果不在Goal另造Proof |

R4交付仍区分当前声明范围的成果与最终完整动态目标；阶段性安全拒绝不是最终目标豁免。R5继续要求正式接收端及模型输入证明同cutoff，确定性注入与自然事件各自验收。尚未运行的反例/标定保持待证，不把文档表格当执行证据。

## 设计研究验证四元组

method：一名Worker检索并记录官方规范/文档；Leader核实关键原文和限定源码/配置调用链，分析当前机制与可证反例，记录变更保护快照。

expected：不把旧问题当当前仍缺机制、不把行业框架当本产品安全保证；将证据事实、类比推论与用户取舍分开。

actual：完成研究、当前事实及限定Goal映射，Q12已接受A，Q13接受延期方向（B）；上述负向场景、真实标定、H7和完整发现未在本轮运行。已明确现有屏障与正式Goal接线缺口的区别，既有Skip双指标不作为新缺陷重裁。

evidence：`residual-primary-sources.md`、本文链接的官方资料与源码锚点，`evidence/residual-entry.json`及`evidence/residual-verification.json`。
