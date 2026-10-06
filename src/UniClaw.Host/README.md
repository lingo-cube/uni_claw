# UniClaw.Host 代码索引 — 组合根（Composition Root）

Product Host 的组合根：装配 Kernel 六个 L2 责任域与 Capability Plane、
注册能力实例（ADR-0035 三域组合根分立）、接线观察 feed 与覆盖遍历。
**不含业务语义**——判断/权威/循环归 Kernel（HOST-001 spec 先例）。

## 代码归属

| 文件 | 职责 |
|---|---|
| `HostRunner.cs` | Product Host 最小 composition root（HOST-001 spec v0.3） |
| `Program.cs` | 进程入口（装配序） |
| `LivePerception.cs` | 感知服务会话生命周期（UDS 服务 + 客户端启停/复用） |
| `UniPerceptionPipeline.cs` | UniPerception 组合管线（SemanticDisposition 只进诊断面） |
| `UiAutomatorDump.cs` | uiautomator XML → Observation claims（PER-009，producer=platform.uiautomator） |
| `FrameOccurrenceStrategy.cs` / `ScreenFrameOccurrenceStrategy.cs` / `UiHierarchyOccurrenceStrategy.cs` | World Model 观察推导策略（SIM-002 G1：产品能力独立成件） |
| `SettingsTraversalLiveFeed.cs` | Settings 遍历 buyer 的真机观察 feed |
| `HostUtilities.cs` | 组合根共享工具（devtest feed 提取的产品化） |

### Capability/ — 能力组合根

| 文件 | 职责 |
|---|---|
| `ModelManagementCapabilityComposition.cs` | uni.model.management 实例注册（CAP-006/008，DSH 缺省 realization） |
| `RuntimeIntegrationCapabilityComposition.cs` | Runtime Integration 域能力组合根（CAP-012，ADR-0035 三域独立） |

### Runtime/ — Host 自有持久化缝

| 文件 | 职责 |
|---|---|
| `RuntimeRunStore.cs` | RuntimeRun 快照与 append-only 事件的持久化缝（ADR-0036 投影消费） |

### SettingsCoverage/ — 覆盖遍历总成

| 文件 | 职责 |
|---|---|
| `PerceptionCapabilityComposition.cs` | 感知能力声明组装（uni.perception 实例 + 锚点，CAP-009） |
| `SettingsCoverageDirector.cs` | 覆盖遍历 director：ConsultAgent 缝上的计划执法者（AGT-005） |
| `SettingsCoverageRunner.cs` | 可配置、有界、可追溯的 Settings 菜单覆盖遍历 runner（AGT-005） |
| `SettingsCoverageConfig.cs` / `SettingsCoverageLedger.cs` / `SettingsActionPolicy.cs` | 遍历配置 / 台账 / 动作策略 |

## 变更规则

装配、注册、接线进本目录；**业务语义回 Kernel 对应维度**；宿主特有
transport/模型解析进 `../UniClaw.Host.Dsh/`；新能力注册 = 既有组合根
文件内扩展，不新建平行注册面。

## 指向

- HOST-001 spec；ADR-0031（host-neutral query core）、ADR-0035/0036；
- 被装配方：`../UniClaw.Kernel/README.md` 全维度导览；
- DSH realization：`../UniClaw.Host.Dsh/README.md`。
