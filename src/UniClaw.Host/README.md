# UniClaw.Host 代码索引 — 组合根（Composition Root）

RUNTIME-STORAGE-001 C组：`Runtime/FileEvidenceJournal.cs`和`Runtime/FileWorldJournal.cs`通过CreateAsync等待原seq0 marker/head；World Owner异步初始化后才装配Kernel。`Runtime/FileRunRecoveryArchive.cs`经原身份接口异步写/flush确认，`Runtime/FileRunResourceJournal.cs`新文件不再同步读取空流。`HostRunner.cs`和`SettingsCoverage/SettingsCoverageRunner.cs`等待原输出文件及诊断观察回调；初始化异常也进入原失败记录/关闭路径。`Runtime/FileCommittedHistory.cs`内HistoryWriteMeter仅统计现有5个文件adapter实际managed调用，和原Evidence/Control/Plan/执行计数共同验收九路径，不是新Owner或存储平台。原同步读取/构造兼容及各自产物等级保持。


RUNTIME-STORAGE-001 B组（Slice 5）：`Runtime/FileRunResourceJournal.cs`原生异步frame/head及P25/exec源前缀hash；`Runtime/FileConsultationJournal.cs`原JSONL写入/flush确认后才发布条目，保留多态codec。两正式Runner等待这些来源及FileExecutionJournal异步关闭；各来源单writer、默认16MiB字节上限、接受后取消先结算，不创建专用线程/队列。执行原格式与P25/资源不同，沿各自adapter实现，不套用Control/Plan同格式模块。


RUNTIME-STORAGE-001 Group A（Slice 4）：`FileControlTraversalJournal`与`FileRuntimePlanJournal`分别实现原领域async接口；共有的frame/hash/head、真实异步提交/关闭实现在Host内部`FileCommittedHistory.cs`，仅服务这两个已有buyer。保留各自schema/错误前缀、payload严格度（Plan额外required constructor）、GUID/序号/hash/head/EOF规则；Asynchronous＋WriteThrough打开frame/临时head，WriteAsync/FlushAsync后原子替换head再确认。默认16MiB可配编码frame上限，单条在途、零收集/准入窗口；重叠写入在接受前拒绝，过大写前失败锁存，关闭等待已接受写入。旧文件读取不变；合法双记录批次及计数见下文v2，不新增掉电保证或每类型线程。

两正式Runner await Control初始化、Driver的选择/计划采纳与接替，并await using关闭两个来源。开发与验证分组，不合并Owner或提交事务；真实Control select和Plan adopt文件提交后Owner ack前各Kill一次、各两个新reader证据见Slice 4。资源/P25/执行/归档及统一收尾仍在组B、C。

Group A v2：`FileControlTraversalJournal.CanAppendBatch`支持Control已具备的配置规则＋启动双行一起确认；仍保存两条原frame，整批最多2条且合计编码字节受同16MiB上限约束，无收集等待/队列。`FileCommittedHistory.cs`统一一次WriteAsync/FlushAsync和末行head提交，失败锁存、确认前不发布；旧单条和不支持批次的journal行为保持。`HistoryWriteCounts`及两个adapter的WriteStatistics只投影实际managed调用/提交批次数/峰值，非物理syscall或记录权威。同一实际Driver场景10条Control记录保持、frame/head写入及flush各10→9；Plan仍为两条独立提交、未假称有额外合批收益。

Product Host 的组合根：装配 Kernel 六个 Product T2 责任域与 Capability Plane、
注册能力实例（ADR-0035 三域组合根分立）、接线观察 feed 与覆盖遍历。
**不含业务语义**——判断/权威/循环归 Kernel（HOST-001 spec 先例）。

## 代码归属

| 文件 | 职责 |
|---|---|
| `PageIdentityRecovery.cs` | 同capture观察材料与语义标题选择的补证adapter；只产普通proposal，身份/到达不在此裁决 |
| `HostRunner.cs` | Product Host 最小 composition root（HOST-001 spec v0.3） |
| `Runtime/ActivationRecordStore.cs` | P24 activation key 的 reserved/finalized/failed/blocked 持久记录 |
| `Runtime/FileConsultationJournal.cs` | Kernel P25 consultation journal 的文件 adapter |
| `Runtime/AuxiliaryArtifacts.cs` | 两正式Host共用的可选产物字节/等待限额，支持原子镜像与有界逐行追加；不写关键结果或必交付来源 |
| `Runtime/ConsultationArtifactLog.cs` | 普通Host咨询诊断的有界待写队列；复用Driver诊断消费者，逐条追加consultations.ndjson，关闭后只尝试一次兼容数组；facts暴露缺口，报告优先读增量 |
| `Runtime/RequiredReportDelivery.cs` | 必交付报告要求、固定来源/生成器、独立交付状态与可读性核验；报告重试不重跑任务 |
| `Program.cs` | 进程入口（装配序） |
| `LivePerception.cs` | 感知服务会话生命周期（UDS 服务 + 客户端启停/复用） |
| `UniPerceptionPipeline.cs` | UniPerception 组合管线（SemanticDisposition 只进诊断面） |
| `UiAutomatorDump.cs` | uiautomator XML → Observation claims（PER-009，producer=platform.uiautomator） |
| `FrameOccurrenceStrategy.cs` / `ScreenFrameOccurrenceStrategy.cs` / `UiHierarchyOccurrenceStrategy.cs` | World Model 观察推导策略（SIM-002 G1：产品能力独立成件） |
| `SettingsTraversalLiveFeed.cs` | Settings 遍历 buyer 的真机观察 feed |
| `SettingsPopupClassifier.cs` | LiveFeed 与离线 Host 测试共用的 XML 弹窗信号提取；保持现有规则，不提供窗口事实或动作授权 |
| `SettingsPopupClassifier.Windows.cs` | API35窗口前后投影、焦点/同Activity关系及XML配对的保守分类候选；不足为unknown |
| `SettingsWindowAcquisition.cs` | 当前ADB Host的有界只读系统窗口采集；不进入Capability Registry |
| `CurrentPageAnchor.cs` | 读取World当前交互锚点，历史页面不参与当前控件归属 |
| `SettingsPageIdentity.cs` | 从观察及进入上下文生成稳定页面签名提示，canonical身份仍由World铸造 |
| `SettingsPageScene.cs` | 当前页面捕获及覆盖层的观察投影；不签发身份或动作权限 |
| `HostUtilities.cs` | 组合根共享工具（devtest feed 提取的产品化） |

### Capability/ — 能力组合根

| 文件 | 职责 |
|---|---|
| `ModelManagementCapabilityComposition.cs` | uni.model.management 实例注册（CAP-006/008，DSH 缺省 realization） |
| `RuntimeIntegrationCapabilityComposition.cs` | Runtime Integration 域能力组合根（CAP-012，ADR-0035 三域独立） |
| `LanguageInspectionAllowlist.cs` | 全局术语白名单加载器（CAP-012 D2） |
| `LanguageInspectionBinding.cs` | 语言检查的任务要求绑定（CAP-013） |
| `LanguageInspectionPostCommit.cs` | observation 批处理后向绑定 inspector 提交（CAP-013） |

### Runtime/ — Host 自有持久化缝

`Perception/JevSemanticPerceptionAdapter.cs`是归一化布局到Jev选择题的L3供货适配：
显式注入ModelManagement/endpoint/key/HttpClient，创建可执行的同一`uni.perception`
实例；不实现L1，不写Ledger/World。Host持有并释放网络资源，健康只报告配置与
最近传输/响应schema，不宣称语义准确率。

| 文件 | 职责 |
|---|---|
| `RuntimeRunStore.cs` | RuntimeRun 快照与 append-only 事件的持久化缝（ADR-0036 投影消费） |
| `Runtime/FileRunResourceJournal.Versioned.cs` | 资源v4增量、周期基线和原子head；只读最新/正反版本查询，旧严格恢复仍全历史核验 |
| `Runtime/ResourceSnapshotDelta.cs` | 仅资源快照的可逆结构编码：旧值/新值、列表splice、属性存在性和顺序核验 |
| `TaskInstanceStore.cs` | taskInstanceId 铸造与 append-only 持久化（RUNTIME-IDENTITY-001，ADR-0044/0045） |
| `ProductSessionStore.cs` | productSessionId 铸造与 append-only 持久化（RUNTIME-IDENTITY-001，ADR-0044/0045） |

### SettingsCoverage/ — 覆盖遍历总成

| 文件 | 职责 |
|---|---|
| `PerceptionCapabilityComposition.cs` | 感知能力声明组装（uni.perception 实例 + 锚点，CAP-009） |
| `SettingsCoverageDirector.cs` | 覆盖遍历 director：ConsultAgent 缝上的计划执法者（AGT-005） |
| `SettingsCoverageRunner.cs` | 可配置、有界、可追溯的 Settings 菜单覆盖遍历 runner（AGT-005） |
| `CoverageEvent.cs` | Owner-owned 覆盖事件流、TargetCoverageMarked 与回放游标 |
| `SettingsPlanFacts.cs` | 由已有观察/覆盖账本投影当前根页候选、范围及来源；不驱动动作、不授予许可、不证明范围穷尽（ADR-0053） |
| `SettingsCoverageOverviewProjector.cs` | 由既有账本投影全局待办、验证、未知及稳定事实键；configured入口同时消费Owner已验证根进入事实，入口到达不代表子树完成；无新增可变权威 |
| `Geometry.cs` | TRAJECTORY-001 D5 几何工具箱：Rect / 遮挡判据（中心点）/ uiAutomator bounds 解析（只做信号输入，不改 Ledger） |
| `CoverageDirective.cs` | 覆盖指令的种类/目标事实与历史文本兼容读取；不授予执行权 |
| `SettingsCoverageLedger.Directives.cs` | 同一覆盖账本内的指令选择；提示文字不参与新执行链的语义判断 |
| `SettingsNavigationScope.cs` | 将任务声明的路由范围投影为导航约束；不替代 Kernel 授权 |
| `SettingsCoverageConfig.cs` / `SettingsCoverageLedger.cs` / `SettingsActionPolicy.cs` | 遍历配置 / 台账（含可选 dynamicRootEntries/all-visible frontier、visited、结构性 skip reason）/ 动作策略；已安装逐步Guard时Act与Plan延后至当前事实检查，Settings清障禁手和未知路由恢复限制也在逐步路径执法 |

## 变更规则

装配、注册、接线进本目录；**业务语义回 Kernel 对应维度**；宿主特有
transport/模型解析进 `../UniClaw.Host.Dsh/`；新能力注册 = 既有组合根
文件内扩展，不新建平行注册面。

## 指向

- HOST-001 spec；ADR-0031（host-neutral query core）、ADR-0035/0036；
- 被装配方：`../UniClaw.Kernel/README.md` 全维度导览；
- DSH realization：`../UniClaw.Host.Dsh/README.md`。

## RUNTIME-PLAN-002 菜单买方接线

`SettingsCoverage/SettingsTraversalProjection.cs`从当前World图/occurrence形成引用视图，只把任务明确分类的入口标为navigation，泛化clickable保持Unknown；live发现穷尽不作无依据声明。`Capability/TraversalLanguageInspection.cs`通过既有task binding调用注册的ILanguageInspector，输入仅为当前目标文字标签，原目标缺失给出typed Unknown，不替换目标；运行态生命周期失效使binding不可用。SettingsCoverageRunner把两条缝接入同一KernelRunDriver，并保存traversal-execution/traversal-recovery事实。

## WORLD-UNCERTAINTY-001 实现

- `PageSemanticReadSupply`：Host读取World已确认当前语义，否则调用实际语义供应方。

## RUNTIME-RECOVERY-002 资源记录与只读重开

HostRunner与SettingsCoverageRunner在新run目录排他创建`resource.journal.jsonl`，通过RunDriverInputs.ResourceJournal接入同一生产driver。FileRunResourceJournal负责同步写入/Flush、连续帧及摘要链；每帧把实际P25/exec文件前缀长度和SHA256一起封存，初始化要求两源为空；Runtime解释原计费、授拒和已资助依据。待核对期间要求原Owner写入者已停止；不提供活跃并发一致性或原binding自动恢复入口。已有文件只以`readOnly:true`打开或通过`FileRunResourceJournal.Inspect(path)`查看未获来源资格的历史分项，不能追加或据余额继续执行。完整只读账目使用`Inspect(path, originalBinding, consultationPath, executionPath)`，originalBinding仅由原KernelRunDriver在已接受Run/View且成功初始化资源记录后签发，构造器不公开；两个Runner结果通过不可序列化的ResourceBinding属性交回。文件中公开identity仅供审计，不能构造/反序列化这个对象，也不能让新driver为旧文件签发新绑定。跨进程原binding恢复尚未实现；丢失原绑定时只可读分项，不能认证完整余额。两源必须与封存的完整前缀完全一致；文件缺失、损坏或增加了后缀，以及旧资源前缀回退，均不返回完整余额。读操作不调用P25/exec的可写重放构造器，不受旧loader静默跳行/截尾影响。HostRunner对所有新run统一持久P25。缺初始化、半写/损坏、未确认操作及未决Effect均不返回可使用余额；记录可读不代表Run/World/Control恢复。故障范围为本机进程故障后的已Flush记录重开，未证明真实子进程kill、OS崩溃/断电或对抗性文件替换。该文件不是Runtime Outcome、World或delivery的新权威。

## 原身份跨进程只读核对（RUNTIME-RECOVERY-003）

两正式Runner现在把原Owner身份及Runtime资源策略同步写成同run目录的run-identity.json。新资源帧格式v3把唯一RecordingId纳入摘要并与原归档关联；v2只保留历史读取，不自动补造原归档。原writer已停止后，FileRunRecoveryArchive(runDirectory, consultationFile).Inspect(expectedOriginalRunId)严格读取归档，经Kernel原Owner身份规则校验，再核对该目录原resource/P25/exec前缀，读不写源。HostRunner咨询文件名为p25-consultation.journal.ndjson，SettingsCoverageRunner为缺省consultations.journal.jsonl。

跨进程恢复的是历史身份与确认账目，ContinuationSupported仍false；不构造Kernel或恢复原Run/World/Control现场，Pending/未知Effect/后缀缺口继续阻断。可信本地归档需在原运行时生成，不能从裸resource.Identity创建；故障范围不含恶意文件替换、OS崩溃/断电或活跃并发一致性。确定性子进程carrier通过真实Kill验证非terminal settled与Pending两种读取，无设备/模型/外部服务证据。

## 原Run状态历史（RUNTIME-RECOVERY-004）

两正式Runner向RunModel注入FileRunStateJournal，在run-state.journal.jsonl记录原typed状态历史，原身份归档关联该源RecordingId。每帧同步Flush后，通过同目录临时文件Flush+原子替换提交末位文件（.head）；之后才向Owner确认成功。读取要求EOF、最后序号/摘要、源ID与head一致。缺head、整帧尾部丢失或额外未提交后缀均拒绝，不回退旧非terminal状态。故障范围仍为本机单进程，非OS崩溃/断电或恶意联合替换文件。

原writer静止后，FileRunRecoveryArchive.RestoreRunState(expectedOriginalRunId)严格核对原archive及历史，返回只读RunModel；缺失、旧无历史关联、损坏、错源或非法terminal后缀均失败。恢复本身不Admit/Activate/Emit；拿恢复Owner组装Kernel后激活、Drive和终态重评仍关闭。真实子进程Kill分别验证原非terminal和实际Assurance判断产生的terminal；只读不改变六个原文件。World/Control/计划位置、当前证据及Outcome跨进程exactly-once emission均未恢复或证明。

共同只读核对入口为`FileRunRecoveryArchive.InspectRecovery(expectedRunId)`。它编排原Owner只读恢复、资源source/P25/exec严格读取，最后交给Runtime比较版本，不拥有状态或执行权威。原writer必须已停止；没有活跃并发一致性承诺。

新资源writer同步写`resource.journal.jsonl.head`：frame Flush→临时head Flush/原子替换→ack。共同入口要求head的source/seq/hash与整个EOF一致；缺head、整操作尾丢失或未提交后缀均不认证较旧前缀。旧`Inspect`保留独立历史账目读取兼容，但旧无版本关联/head的源不能获得共同恢复点。共同核对失败时可能仍显示独立确认账目，其余额不代表续跑许可；读取不补记或修改任何原文件。

进程故障范围仍是本机可信单writer；七个原source（身份、Run历史及head、资源历史及head、P25、exec）只读核对不覆盖OS/断电、恶意联合替换、多View、执行续跑或跨进程Outcome exactly-once。

原Evidence台账同步保存于`evidence.journal.jsonl`与`.head`；初始化帧seq0认证原空历史，后续每次admission seq1..N。原archive关联独立EvidenceRecordingId，`FileRunRecoveryArchive.RestoreEvidence(expectedRunId)`只读恢复原Ledger。缺源/缺绑定/错源/缺head/整帧回退/未提交后缀均拒绝，不fallback空台账或从trace补造事实。读取不新建Kernel、激活或Reconcile。新证据源不改变Run/Runtime共同点的两Owner范围。

读取保留原Origin字段而不依据Context重判；当前原模型没有ADR-0048要求的精确attempt correlation字段，其派生合法性未由本片认证。原字段读回不是新入证或当前效果证明，不能绕过恢复Ledger的只读与Kernel执行门。

写入范围与既有journals相同：frame Flush→临时head Flush/原子替换→ack，本机可信单writer的进程故障；不承诺OS/断电、活跃并发或恶意联合替换。Evidence历史只保留原资格与血缘，不取得当前现实或执行许可；尚未存在的失效/替代接口也未由本文件adapter创建。

ORIGIN-CORRELATION-001：SettingsTraversalLiveFeed的本次截图/层级及同capture同步identity补充、LiveFrameFeed的本次同步采集完成后，显式回填directive原link；Runner的interleave wrapper原样保留。LandSlow异步/迟到结果不继承当前link，仍External。正式Host接线经代码评审与可替换受控source验证；没有新增设备、模型或live服务运行证据。Evidence新历史版本2保存原Receipt/Intent/Binding/可靠AttemptId；旧v1文件仍原ID只读，不能据旧Origin恢复执行或补造当前关联。

RUNTIME-RECOVERY-007：FileRunRecoveryArchive.InspectEvidenceRecovery沿原严格Run/Resource/Evidence读源及archive身份，交Runtime比较精确原Owner位置，成功返回三份原历史共同点；原InspectRecovery两方语义不变。Evidence坏/错/缺源会保留可独立读取的Run/资源结果，Run坏源也可保留原历史Evidence。旧没有Evidence位置的资源文件仍可分项/两方读取，但不能认证三方；不会自动升级或写入旧文件。三方成功只是historical read，ContinuationSupported恒false，没有World/current或完整执行恢复。

RUNTIME-RECOVERY-009：两正式Runner从初始化起注入`FileWorldJournal`，保存原World完成操作于`world.journal.jsonl`及独立`.head`。frame Flush→临时head Flush/原子替换→ack，完整EOF/末位/原source严格匹配；无重启追加、缺源不默认原空World。原Run/Evidence recording、配置身份及原决定由World记录，Host不重判。原writer停止后，使用readonly FileWorldJournal与原WorldRecordingId、原RunRecordingId、原readonly Ledger调用`WorldHistoryRead.ReadOriginal`，得到独立历史数据。该文件来源尚未纳入Run/Runtime/Evidence共同位置，不能据此宣称四方一致、当前World恢复或续跑。故障范围仍是可信本机单writer进程终止，不覆盖OS/掉电、活跃并发或恶意联合重写全部源/head。

WORLD-HISTORY-002：`Runtime/WorldRevisionDeltaCodec.cs`由文件adapter私有持有，负责新v2修订差异编码与冻结JSON还原。`FileWorldJournal`默认新写v2，仍可只读v1；一份文件禁止混用frame版本。typed写入只序列化变化的值：WorldState按键set/remove并保存键顺序编辑，数组保存前后缀与变化中段，其余字段仅变化时替换；初始空状态后全增量，无周期完整快照。base计数与编辑区间严格校验，编码/文件提交失败锁存，不改变原frame/head确认。读取仍返回完整原操作及修订，保留原输入/日志/侧状态；没有重跑World策略、续跑或旧文件迁移。集合比较仍有扫描成本，完整历史读取仍会展开，异步等待由RUNTIME-STORAGE-001后续处理。

RUNTIME-RECOVERY-010：`FileRunRecoveryArchive.InspectWorldRecovery(expectedOriginalRunId)`沿原archive v3的World/Run/Evidence录制身份打开严格readonly原源，交Runtime认证四方精确末位。World坏源保留原三方结果；Run/资源坏源时仍保留可认证的原World/Evidence历史；必要Evidence缺失则不把World冒充已认证。旧无World关联仍只供原两方/三方读取，不从目录里的World文件补造关联。成功仍只读历史，ContinuationSupported=false，不构造Kernel或恢复Current/执行。真实非terminal Kill分别覆盖非空四方、原空World及未关联World后缀，源文件只读SHA核验独立保留。

## Run遍历历史格式（RUNTIME-RECOVERY-012）

FileRunStateJournal新写入`uniclaw.run.history.v2`与对应head v2，串联Run核心与原Traversal执行事实，frame及head落盘后才确认。读取要求单一原RecordingId、完整hash链/序列/EOF及原末位head；v2记录载荷拒绝无法识别的字段。旧v1只恢复其确有的核心Run历史，执行事实来源不可用；不把旧缺源提升为空历史。FileRunRecoveryArchive.RestoreRunState沿原接口只读恢复，仍不创建Driver、重跑handler或激活Run。

## 原Control遍历历史（RUNTIME-RECOVERY-013）

两个正式Runner均注入FileControlTraversalJournal，写control-traversal.journal.jsonl及独立原末位head；先帧和head确认再发布Control状态。运行原身份归档v4保留Control源GUID。FileRunRecoveryArchive.ReadControlTraversalHistory沿原Run身份/Run源/Control源读取独立原历史，旧无源归档明确不可用；读取不追加、激活或重放队列，既有四方历史共同点不自动升级为五方执行认证。帧hash/head/EOF与记录载荷/完整状态结构均需有效，文件内容不是签名认证的外部信任来源。

## 原历史读取适配（RUNTIME-RECOVERY-015）

FileRunRecoveryArchive的七个既有查询API保留，实际身份/Owner/共同点读取编排已移入Kernel.Runtime.RunRecoveryReader。每次文件查询由Host创建作用域内IRunRecoverySources，按需打开原只读journal并在查询结束（含失败）释放；原文件名、格式、hash链、head/EOF和P25/exec前缀核验仍由文件适配器负责。文件写入及归档v1至v4兼容规则不变，Host不重判Owner结果。

其他介质可实现IRunRecoveryArchive与IRunRecoverySources后直接组合相同Reader，不需要复制恢复编排或伪造文件路径；各领域写入仍沿原journal接口。新介质须证明其原来源关联及准确提交末位，并通过同一读取和拒绝契约测试。当前仅文件与测试内存来源被验证，没有数据库实现或配置切换承诺。六方共同点及自动续跑未实现。

## 原Plan采纳文件来源（RUNTIME-RECOVERY-016）

两个正式Runner注入`FileRuntimePlanJournal`，以独立GUID写`runtime-plan.journal.jsonl`及末位head，沿`IRuntimePlanJournal`确认后才允许Driver发布。新归档v5增加RuntimePlanRecordingId，v1–v4旧核心读取及v4/v5 Control读取兼容。FileRunRecoveryArchive新增第八个兼容入口`ReadRuntimePlanHistory`，由同一Kernel Reader解释原采纳图/版本/接替边界，Host只核文件连续序列/hash/head/EOF。

文件帧与head落盘后才确认；两者之间崩溃的缺口拒绝读取，不能截短到旧前缀。只读adapter不追加、不激活、不续跑。真实非终局进程在原adopt提交后、Runtime收到ack前被Kill，两个新进程查询仍读到完整八类原节点，而Runtime发布位置仍在初始化；这证明原提交可追溯，不能证明新Plan曾执行。文件与测试内存通过同一公开接口读取，尚无数据库耐久性证据；014中的完整游标/Control接收仍是后续工作。

## Evidence batch storage (RUNTIME-STORAGE-001, slice 1)

FileEvidenceJournal implements IAsyncEvidenceJournal with asynchronous frame/head writes and flushes, preserving the original v1 format and managed-flush process-failure scope. Each encoded chunk defaults to at most 128 records and 1 MiB; an oversized single frame fails and latches, and earlier committed chunks remain original history. One logical observation is published only after all chunks acknowledge. WriteStatistics counts managed FileStream calls, not physical disk syscalls or power-loss durability.

This slice has one Owner and no collection/admission queue: both waiting windows are zero. Concurrent native writes are rejected immediately before acceptance; an accepted write is awaited to its actual commit boundary, not cancelled by a fictitious success timeout. Shared queued backpressure remains future all-domain work.

HostRunner.RunOnceAsync and SettingsCoverageRunner.RunAsync await native observation storage, with synchronous compatibility wrappers retained. Production CLI/HTTP callers await their async entries. Initialization and other domain/snapshot writes are not yet fully async.

## World async storage (RUNTIME-STORAGE-001, slice 2)

本节为Slice 2范围记录；Run后续进度见下方Slice 3。

`FileWorldJournal`实现`IAsyncWorldJournal`与`IAsyncDisposable`，继续写v2修订增量、只读兼容v1。运行期每个完整外层操作用WriteAsync/FlushAsync写frame与临时head，再原子替换head后确认；一条在途、零收集/准入窗口，重叠写入在接受前拒绝。编码frame默认上限16MiB（构造参数可配置），超过上限在写前失败并锁存，不能丢记录或盲重试。原managed flush及单writer进程终止故障范围不变，没有物理磁盘syscall或掉电保证。

两个正式Runner await contract的World demand登记、Driver观察/权限冲突/动作后World记录，并await using关闭World源。World本次逻辑操作已经是一条frame，不将它算作额外批量收益；没有用跨屏障合批延后确认。构造期file header/World initialize仍走同步初始化或兼容桥，Run/Control/Plan/资源/P25/Effect和终局报告仍有同步路径；全域迁移未完成。真实Driver在World原文件提交后、Owner获ack前Kill与两个新reader证据见slice2。

## Run async storage (RUNTIME-STORAGE-001, slice 3)
`FileRunStateJournal`实现`IAsyncRunStateJournal`与`IAsyncDisposable`。frame和临时head以Asynchronous＋WriteThrough打开，真实WriteAsync/FlushAsync完成后原子替换head，再确认原序列；保留原刷盘请求（[官方FileOptions](https://learn.microsoft.com/en-us/dotnet/api/system.io.fileoptions?view=net-10.0)、[.NET 10 Unix映射O_SYNC](https://github.com/dotnet/runtime/blob/v10.0.0/src/libraries/System.Private.CoreLib/src/Microsoft/Win32/SafeHandles/SafeFileHandle.Unix.cs)），不是普通FlushAsync替代Flush(true)。原v2/v1读取、hash链/head/EOF及单writer进程终止故障范围不变，不新增掉电/目录元数据保证。单条编码frame默认16MiB可配，一条在途、零收集/准入窗口，非法重叠立即拒绝，过大写前失败锁存；正常关闭等待已接受写入。

两个正式Runner await Run admission和运行期状态/遍历保存，并await using关闭原源；attempt reflux同走已存在的Evidence异步批次。单逻辑记录一条frame不报额外合批收益；构造/历史读取、Control/Plan/资源/P25/执行/归档及若干Host产物仍同步。真实Driver在Running原记录提交后Owner ack前Kill时，内存位5/文件6条/处理器未调用；两个新reader保留原6条及Running历史，不能据此续跑。证据见slice3。

RUNTIME-STORAGE-002第三片：新资源文件使用v4完整Initialize事实＋确认快照正反增量，按`ResourceVersionOptions`的记录/字节阈值创建读取基线，原子head同时绑定log长度、末位摘要和已确认基线。`ReadLatestVersion`及`ReadVersion(sequence,Forward/Reverse)`只读数据，不认证整个未展开历史、不返回执行许可；最新查询必须有精确head/可核验基线，不能静默回退旧版本。原`ReadRecords`/Inspect/InspectRecovery仍展开完整资源事实并按Runtime原规则核对来源，恢复共同点不借快查询捷径。原v2/v3只读规则不变；v4不支持恢复追加、不自动清理log或基线，查询要求原writer已停止。`WriteStatistics`只统计frame/head；基线文件的新增I/O与空间另列，不能据frame字节下降宣称全部指标更快。
