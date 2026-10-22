# REPLAY-STATE-EVIDENCE-001：checked 状态补采与证明设计（待评审）

2026-10-22；核验基线 `uni-harness@9bc15b8974f5fb5746074e373e6c132bc4df0089`，共享工作区存在并行修改。本文件是设计评审输入，不批准新产品契约、不启动实现、设备或模型调用。事实以 evidence/source-manifest.json 与对应带行号摘录为准；建议均标为候选。

## 1. 结论与原问题

当前旧 Stay awake 录制不足以证明 Unchecked。原始 XML 目标节点 `checkable=true`，before `checked=true`，after `checked=false`；这是布尔来源事实，不等于 after 的三态语义已被证明。初始审计快照的调用链是 `RecordedOperationReplayTests.Observe → UiAutomatorDump.ParseHierarchyObservation(LegacyXmlDefaultCapabilities) → CheckedSemantics → TypedHierarchyProposalProjector → Ledger → World → SemanticCheckedResolver → RuntimeAssurance`。默认 Collapsed 的 false 输出 Unknown(partial-unrepresentable)，projector 不发 checked claim，consumer 返回 `typed-route: insufficient-evidence (no-checked-claim)`。已有 diagnostic 的 actual 验证失败，不能用它的 expected/sequence note/GT 中 OFF 覆盖 actual。

旧 XML、sequence、GT、`evidence/sim-010/output/recorded-stay-awake-direct-diagnostic.json` 与 `recorded-diagnostic.trx` 原样保留；新补采必须独立命名、独立 hash、独立 provenance。当前设计只读核验了既有失败材料，没有重新执行该运行。

## 2. 契约与现有执法：可以复用什么，还缺什么

| 位置/权威来源 | 当前已核实行为 | 可复用/限制 |
|---|---|---|
| PER-010 spec §Checked/proof rule；CheckedSemantics.cs | 三项 proof 非空 + Exact 才允许 boolean false→Unchecked；true→Checked；TriState 走 MapTriState；Unknown 不属于 CheckedState enum | 直接复用值域和纯映射；proof.IsComplete 只验字符串非空，不验证文件存在、哈希、语义、签发者或适用范围 |
| HierarchyCapabilities.cs | 优先级 TriState > Exact > Collapsed；无 checked 能力为 Unsupported | 复用 flags；优先级不是冲突声明合法性的证明 |
| UiAutomatorDump.cs:280–377,463–490 | parse context 可携 ExactProof；默认 Collapsed；missing→Unknown(attribute-missing)，非法字段→Unknown(malformed-field)，无 checked capability→Unsupported | 复用受证明的二态路径；proof 是整个 capture 参数，无法表达逐节点 domain。不能把单个开关证明扩散到整页 |
| 同上 CheckedField + CheckedSemantics.MapBoolean | XML checked=partial 且 TriState→Partial；XML true/false 且 TriState 会进入 MapBoolean 并抛 ArgumentOutOfRangeException | 不能声称既有 XML parser 已完整支持三态；三态真件应输出 typed observation，或另行评审最小适配修正 |
| UiAutomatorDump.TryDumpToDevice / ProbeStateMachine | 现有同步 dump 有 timeoutMs、超时杀进程及结构/瞬时失败分类，Probe 有 Run 级退避；无 cancellation 参数，定点设备文件只在成功 shell 链清理 | 复用现有有界执行思路；不能声称完整满足 PER-010 的 cancellation/失败清理契约。新采集切片须覆盖取消、超时、失败路径清理与独立 run 文件，避免并行采集碰撞 |
| CaptureMetadata / UiHierarchyObservation / UiHierarchyCaptureResult | capture-local identity、版本、设备/会话/周期、完成时刻、duration、能力、coverage、space；结果 Complete/Empty/Partial/SourceUnavailable/Malformed；Validate 是显式调用 | 可复用既有记录与验证，无需先造新 IAcquirer；当前仓库未发现专用 IUiHierarchyAcquisition 接口。枚举丰富来源不等于已有对应真件 adapter |
| HierarchyCoverage / result.Validate | Partial coverage 必须有 limitation；Malformed/SourceUnavailable 不携 observation；Empty 不等于 absence | capture Partial、字段 Unknown、checked Partial 是三条不同轴。XML parser 目前固定 CompleteWithinDeclaredSurface，不能据此声称覆盖全部窗口/屏幕 |
| TypedHierarchyProposalProjector | 仅 Observed 产 occurrence-qualified claims，保留 HierarchyCaptureDescriptor；Unknown/Unsupported 不产 claim | 直接复用；descriptor 不携 CheckedExactProof 三项内容/校准引用，须在采集材料中可追溯，不假称 Ledger 校验了 proof |
| UiHierarchyOccurrenceStrategy | 按 capture/node 组装，校验 page/cycle/session/time；有过期 capture 防护；开关判定是 checkable=true **或** class endsWith Switch | 关联与时序可复用；Switch 类名路径仍可携 checked state，不能证明 validity guard 闭合 |
| SemanticCheckedResolver | role/descriptor/container 匹配恰一；resource-id 优先、证据/bounds 关联；多 capture 可选唯一最新；多/零 claim 拒绝；提取 canonical timestamp | 复用只读缝；没有读取 checkable 字段判真。不能以唯一 role 或 class 名替代该 guard；resource-id 也不是跨 revision 永久身份 |
| RuntimeAssurance.PostAction | 有 DesiredState 才走 typed；检查唯一解析/Observed/captureTime>dispatch；Partial 一律 insufficient；Checked/Unchecked 比较期望；当前 typed Unknown 不回视觉通过 | 复用判断 owner；注释将 Observed 视为属性有效证明，实际上仍有上述 proof/guard 缺口。PER-014 旧状态说明写“回视觉”，当前代码更严格，以当前代码为核验事实 |
| SettingsTraversalLiveFeed.cs:24–37,314–328 + wifi-toggle-contract.json | Wi-Fi 专用二态 contract/metadata/evidence 三字符串样例；调用处给整个 XML 使用这份 proof | 仅复用声明形式。它不是 Stay awake 的证明；现有 fixture 自述 Wi-Fi 域，未证明整页、OEM 或任意 Settings 开关。全 capture 传播范围需后续最小切片约束 |
| RecordedOperationReplayTests + ScenarioStimulusFeed/ScenarioPerceptionAdapter | 初始快照回放经 parser/projector/runtime；收尾并行修改后入口改为反序列化 imported UiHierarchyObservation，再进 projector/runtime；另有 Human-reviewed state 输入路径 | 复用注入缝/context 纪律与消费顺序。人工 reviewed 输入可作独立仿真源，不能伪称来自原 XML 或新真件采集 |

架构依据：产品 baseline 的 Evidence/Belief Flow 与各 owner 边界；ADR-0026 要求真实 buyer 决定缝，故不预造通用证明服务/采集框架；ADR-0017 要求 toggle 在 dispatch 前根据状态控制，Unknown 不能通过盲 tap“补证”；ADR-0048 约束 observation origin/derivation；ADR-0049 将 expected 与观测证据分开。checked 三项证明及四门细则主要来自 PER-010/011/014 与 PER-009 D9/D12，**不是独立 checked ADR 已经批准的泛化新契约**。

## 3. 哪些路径可以有依据声明能力

### A. 候选首选：有限 claim domain 的 BooleanExact 新录制

先取得特定应用/控件域的明确二态契约，再校准当前采集器。优先可控 fixture 应用：状态模型和 accessibility 输出实现有版本固定的源码、断言与独立设置/读回接口；补采 Checked 与 Unchecked。fixture 只能证明该 fixture，不证明 Android Settings Stay awake。若目标必须 Stay awake，必须另外建立具体 Settings build/控件实现及语义范围的二态依据与实际版本对应；若无法取得，不允许 Exact 正例，应走 B 或保留 Unknown。

三项 proof 的生产来源分别为：

| 字段 | 谁产生/应包含什么 | 如何独立核验 |
|---|---|---|
| TwoStateContractId | 应用/fixture 语义 owner：限定 package/build、页面与控件、checked 属性域、允许状态集合恰为 Checked/Unchecked；源码/正式契约固定版本 | 审阅允许状态与 setter/readback/accessibility 实现，核对运行二进制与源码版本。API level、Switch 类名、OFF 标签、未见 Partial 样本不充当契约 |
| AdapterCapabilityDeclaration | 采集 adapter owner：声明 acquirer/version/format、适用 domain、Exact、映射规则与绑定的 contract/evidence hash | 对实际运行的采集程序版本、请求及同 capture 元数据检查一致；不能从期望结果倒填 Exact |
| TraceableEvidenceRef | 校准/语料 owner：指向固定内容的契约与校准材料，记录原始输入、独立期望、结果、差异、版本及哈希 | 消费材料前验证引用存在/哈希匹配/domain相同；分别测试 true/false、缺项、错域、旧版本和损坏引用；重复采样用于验证实现，不用于凭经验创造二态契约 |

每 capture 若包含不属该域的节点，不能给整个 XML 扩散 Exact。最小方案待评审：采集侧按已有明确 domain 校验后，仅构造该范围的 typed 字段（其余保持 Collapsed/Unknown），或在单一受控 fixture 表面校准；若需要逐节点 proof API 变化，先提交该真实 buyer 证据再评审，不在本设计中冻结字段形状。

### B. 候选：保留原始三态的 richer acquisition

Android 官方 getChecked() 从 API 36 提供 FALSE/TRUE/PARTIAL，且仅 isCheckable() 为真才有意义。这是可行性依据，**不等于某台设备、某个采集程序或旧录制已具备三态**。候选真件直接读取该方法，保存原始整数及常量映射/版本，输出 `MapTriState(Checked/Unchecked/Partial)` 后的 UiHierarchyObservation，再复用现有 projector。保留原始方法名、checkable、API/运行包/采集器版本、覆盖限制；声明 TriState 前必须三值贯通校准（自控 fixture 可独立制造 Partial）。API36 但仍调用 isChecked() / 旧 XML dump，继续 Collapsed。不能将 XML false 冒充三态 FALSE。

若实际设备只有布尔通道且无二态契约，正确结果仍是 Unknown；不得从截图/OCR 颜色或系统设置值直接产生 UI semantic.checked。独立设备设置读回可辅助 oracle，但它测的是设备/配置状态；若要与 UI 语义等同，仍须控件域契约。

### C. 立即可用的仿真正例来源

现有 reviewed fixture/显式 typed 输入可以证明 Runtime 消费三态、Exact 与拒绝路径，须标注 synthetic/reviewed、有独立作者预期与版本。其结果不计为补采或真件 capability 校准通过。详见 cases.json。

## 4. consumer 四门及有效性缺口的处理建议

1. 权威域：只验证 widget semantic.checked；rendered appearance、设备无线电态等不升级为同一权威。
2. 身份：dispatch 后重新唯一解析 occurrence，在该 capture 上关联唯一 typed node。双同名/复用 resource-id/相同 bounds/同龄多 capture 必须拒绝。不能注入 occurrence ID 或 WorldBelief 当外部输入。
3. 属性有效：同一 capture+node 的 checkable 必须 Observed(true)，能力足以表达该值；Exact 时三证明内容/范围/版本验证完成。raw checked 即使 checkable=false 仍可保留，权威消费必须拒绝。这是当前需要补齐/证实的最小切片，不修改原录制来掩盖。
4. 时序：两项都核对——当前页面 identity/capture 已确认，且 checked 的 canonical capture timestamp 严格晚于实际 dispatch。完成时刻由同一时钟基准记录；duration/开始时刻用于发现跨 dispatch 的撕裂窗口。仅完成时刻晚于 dispatch 不能独自排除窗口跨界，补采要求 post capture 请求在 dispatch 完成后启动。时间相邻不能证明 PostActionSelf：按 ADR-0048 保存精确 attempt correlation，Context 与 Origin 分开，禁止由 producer 自报或时间戳伪造因果。

四门通过后仍要比较值；Partial 保留原值但当前 Assurance 不判 verified，即使未来有人 author Partial 期望也不擅自改变此规则。Unknown/Unsupported 不满足；Checked 与 Unchecked 不相等是 mismatch。DeliveryCompleted 只证明交付，不能证明 checked 已改变。没有 DesiredState 的导航/视觉行为走其既有规则，不借此证明状态动作成功。

## 5. 最小补采包与独立 oracle

新包建议独立目录名 `checked-state-calibration-<run-id>`（本任务不创建包）。最少包含：

- manifest：bundle版本、schema/producer/parser/projector版本、产品 commit、输入 sha256、contract/声明/校准版本及 hash；文件引用完整；raw/reviewed oracle/expected/runtime actual 分区。manifest 是研究/测试材料，不是新增产品 schema。
- 环境与权限：device/session/run、API、build fingerprint、应用包/版本与二进制 hash、采集器实际版本/方法、格式/能力、权限/服务状态。域不匹配不能沿用 proof。
- 每帧原始 hierarchy 或 richer dump、独立截图（独立 capture ID，不强称原子同步）、起止时间/时钟基准/observation cycle、坐标系/尺寸/旋转、window/coverage/limitation；raw checkable/checked 与节点 capture-local 索引。
- 每操作 intent/receipt/精确 attempt correlation、dispatch start/end、post 请求起点、post capture 完成；至少两个**独立采集会话**重复 Checked→Unchecked→Checked，保存恢复结果。重复恢复本身需要后续设备授权；预算建议最多两会话、每会话一往返，不自动重试到成功。
- oracle：可控 fixture setter 的预先指定 state + 独立只读模型读回/log + accessibility raw，分别保存，不从 Runtime 导出再当期望。Settings 目标采用绑定版本的源码/契约审阅 + 独立读回/人工语义审阅；截图只作外观佐证。失败时保留分歧，不投票造 truth。
- negative 材料：复制新包后有标识地缺 proof 各项、坏 hash、错 domain、错版本、checkable=false/missing、陈旧/相等时间、多候选、不支持能力、非法/缺字段；变异标明 synthetic，保留父 hash，不混入真实 raw。

Unknown 无需“制造成第四种 checked 值”：从缺字段、能力不足或独立故障注入检验 FieldState.Unknown。Partial 用可控三态 fixture 的模型状态独立指定，再核验 getChecked 原始 PARTIAL 与 typed Partial；BooleanExact fixture 不能靠 false 制造 Partial。capture coverage Partial 另做截断窗口用例，不与 checked Partial 混用。

## 6. 最小前置切片及 owner（本次均不实现）

| 顺序/行为 | 复用位置 | 最小新增工作与责任 |
|---|---|---|
| P1 拒绝假 Exact，先测错域/错版/坏引用 | CheckedExactProof、parse context、既有 fixture manifest | Host 采集/语料 owner 先做本 buyer 的 proof 内容与 scope 校验，保留校验记录；不造通用 trust service，不把三字段非空当已核验 |
| P2 raw checked 可保留但 invalid guard 不可用于控制/完成 | occurrence strategy、typed resolver、Assurance | 产品 World/消费投影 owner 决定并补齐同节点 checkable=true 的消费执法；先证明 false/missing/class-only 拒绝，不能仅改测试期望 |
| P3 采真实新二态正例 | UiAutomatorDump parser + projector + runtime feed | 采集 owner 按 A 的受限域新录制；语料 owner 独立审阅期望，Simulation owner 注入真实外部缝；产品 core 保持同一运行链 |
| P4 三态校准（若选择 B） | MapTriState、typed observation、capture result | Host richer adapter owner 实现有界原始读取并在 true/false/partial 全链测试；现有 XML triState true/false 缺口需明确修复或避开该不支持通道，不能靠 capability flag 解决 |
| P5 两会话复采/重放对照 | RecordedOperationReplayTests 外部观察/Agent/delivery 替换思路 | Simulation owner 固定 reviewed oracle 独立比对 first divergence；真件 owner 控制设备授权、恢复与预算。不要接管正在修改的 Simulation 测试 |

生产/消费权威：adapter 负责原始采集、归一化与能力声明；语料/校准 owner 负责可追溯材料与独立期望；Ledger 入证不因本设计获得新 proof 裁决权；World 负责 belief/occurrence；consumer resolver 只读派生；Assurance 判断 freshness/verification；Control 决定是否签发动作；Effect 只负责绑定交付。仿真线替换外部输入与交付，不写 canonical Evidence/Belief/Outcome，不让 expected 注入生产 truth。

## 7. 后续采集/实现验收四元组（全部待执行）

| ID | method | expected | actual | evidence（计划产物，不是已存在的通过证据） |
|---|---|---|---|---|
| C1 | 审阅 domain 契约/源码与实际版本，逐项剥离 proof 并变异引用/版本 | 仅同域、同版、三项可追溯者获 Exact；其余不得 false→Unchecked | NOT RUN | 新包 contract-review、proof-validation、mutation-results |
| C2 | 独立设定二态 fixture 状态，采集→parser→projector→真实 Runtime | Checked/Unchecked 与 oracle 一致；无期望注入；post 时序合法时状态验证匹配 | NOT RUN | raw、oracle、ingress、typed、actual、first-divergence |
| C3 | 独立设置三态 fixture 三值，读取实际 richer API，三值全链及未知值故障注入 | 逐值无损；Partial 非 verified；未知原始值不猜 false | NOT RUN | API raw 与版本、mapping calibration、typed/runtime actual |
| C4 | raw checked 为真/假但 checkable=false/missing，另设 Switch 类名 | raw 不抹除；Control/Assurance 不得据其满足状态义务 | NOT RUN；静态发现当前缺口 | guard regression 和调用链证据 |
| C5 | post<=dispatch、capture 跨 dispatch、同名多节点、坏 association、旧 capture/context | 不判状态动作成功，first divergence 可定位门 | NOT RUN | receipts、timestamps、context/association negatives |
| C6 | Empty/Partial coverage/不可用/结构错/字段错分别输入 | 不混同 absence、Unsupported、Unknown、checked Partial；失败保留诊断 | NOT RUN | result validation 与负例输出 |
| C7 | 两独立会话各一往返并恢复；同新包重复确定性回放两次 | 语义结果可重复、无未消费帧/跨域 proof；恢复证据完整；差异如实报告 | NOT RUN | 两 run manifests、restore、replay digests、差异报告 |
| C8 | 重放旧 XML 的原默认能力（独立拒绝基线） | after Unknown；无 unchecked claim；verifiedEffects=0，旧失败证据保留 | 本次只核验已有 diagnostic，未重跑 | 本目录 legacy-observation.json + 原 diagnostic/trx |

## 8. 实质待裁决与未验证项

需要后续用户裁决：首个正例 buyer 是受控 fixture 还是必须 Stay awake；允许使用哪台设备/可安装采集服务及 fixture 与否；是否具备 API36 richer 采集条件；两会话往返与恢复的运行预算/时间窗口。若选 fixture，不能把其 PASS 宣称为 Stay awake 精确状态通过；若选 Stay awake，P1 需要该真实 build 的证据。当前不请求立即执行设备动作，先评审本设计与这些取舍。

尚未验证：真实设备/采集服务/API路径、三态真件传输、proof语义与版本校验、checkable guard 端到端、任何新正例/两会话复采。没有运行产品测试、模型或设备。当前可交付的是现状审计、可执行的验收要求与拒绝/正例期望，不是产品修复或新语料已完成。

## 9. 收尾并行漂移说明

初始 source-manifest 与 source-excerpts 保留审计时内容。收尾时 `RecordedOperationReplayTests.Observe` 已由并行线改为导入 typed observation（先核验 raw 与 imported 文件 hash，检查 captureId/time，再走 projector/runtime），不再在该入口现场调用 XML parser。本设计对旧 XML 的语义判断来自未变更的产品 parser/mapper 及原 diagnostic 快照，不以这处测试入口变化获得新的 exact proof。当前入口见 evidence/concurrent-replay-entry.txt。未评审 imported 资产的来源或真实性，也未验收并行实现。

## 10. 官方来源（2026-10-22 查询）

- [Android AccessibilityNodeInfo.getChecked](https://developer.android.com/reference/android/view/accessibility/AccessibilityNodeInfo#getChecked())：API36、三态返回与 checkable guard；isChecked 是布尔且 API36 弃用。只支持候选采集路线的 API 事实，不能证明本条旧录制。
- [AOSP AccessibilityNodeInfoDumper.java，blob 59616c8ab655c39da0dad4c8b2120fce888b7cf2](https://android.googlesource.com/platform/frameworks/testing/+/refs/heads/main/uiautomator/library/core-src/com/android/uiautomator/core/AccessibilityNodeInfoDumper.java)：序列化 checked 使用 Boolean.toString(node.isChecked())。只说明所查源码实现，不认定旧设备必运行该 blob。
