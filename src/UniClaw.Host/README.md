# UniClaw.Host 代码索引 — 组合根（Composition Root）

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
| `Program.cs` | 进程入口（装配序） |
| `LivePerception.cs` | 感知服务会话生命周期（UDS 服务 + 客户端启停/复用） |
| `UniPerceptionPipeline.cs` | UniPerception 组合管线（SemanticDisposition 只进诊断面） |
| `UiAutomatorDump.cs` | uiautomator XML → Observation claims（PER-009，producer=platform.uiautomator） |
| `FrameOccurrenceStrategy.cs` / `ScreenFrameOccurrenceStrategy.cs` / `UiHierarchyOccurrenceStrategy.cs` | World Model 观察推导策略（SIM-002 G1：产品能力独立成件） |
| `SettingsTraversalLiveFeed.cs` | Settings 遍历 buyer 的真机观察 feed |
| `SettingsPopupClassifier.cs` | LiveFeed 与离线 Host 测试共用的 XML 弹窗信号提取；保持现有规则，不提供窗口事实或动作授权 |
| `CurrentPageAnchor.cs` | 读取World当前交互锚点，历史页面不参与当前控件归属 |
| `SettingsPageIdentity.cs` | 从观察及进入上下文生成稳定页面签名提示，canonical身份仍由World铸造 |
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
| `SettingsCoverageOverviewProjector.cs` | 由既有账本投影全局待办、验证、未知及稳定事实键；无新增可变权威 |
| `Geometry.cs` | TRAJECTORY-001 D5 几何工具箱：Rect / 遮挡判据（中心点）/ uiAutomator bounds 解析（只做信号输入，不改 Ledger） |
| `CoverageDirective.cs` | 覆盖指令的种类/目标事实与历史文本兼容读取；不授予执行权 |
| `SettingsCoverageLedger.Directives.cs` | 同一覆盖账本内的指令选择；提示文字不参与新执行链的语义判断 |
| `SettingsNavigationScope.cs` | 将任务声明的路由范围投影为导航约束；不替代 Kernel 授权 |
| `SettingsCoverageConfig.cs` / `SettingsCoverageLedger.cs` / `SettingsActionPolicy.cs` | 遍历配置 / 台账（含可选 dynamicRootEntries/all-visible frontier、visited、结构性 skip reason）/ 动作策略 |

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
