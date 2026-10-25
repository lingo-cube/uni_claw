# Control Loop 代码索引（Product T2 · 基线 §14）

## 原生异步遍历记录（RUNTIME-STORAGE-001 Group A）
`ControlTraversalHistory.cs`的`IAsyncControlTraversalJournal`扩展原领域接口；`TraversalControl.History.cs`与`ControlLoop.cs`保持Control唯一游标/选择权威。初始化及全部外层操作共用候选→真实await确认→发布，旧同步入口是显式桥；嵌套Gap/Pause留在原完整外层操作。等待期间旧历史/状态可读，同Owner写入重入立即拒绝，失败含ack丢失锁存；select未确认不交出Work，drain未确认不交出progress。Kernel/Driver只await，取消在已接受确认后处理。

与Runtime Plan按一组改造和验证，各自保留原来源和提交末位，不把计划接替变为跨Owner原子事务；完整游标恢复与Runtime接收协议仍未实现。

配置规则→启动之间无外部调用，`ConfigureAndStartAsync`在支持批次的journal上保留两条原行，共同确认后才发布；不支持批次的原journal仍按原顺序逐条确认。原生File adapter最多两条/整批16MiB、收集窗口0；失败不发布任一候选或盲重试。实际同Driver场景10条原记录的frame/head写入及flush各10→9，配置＋启动局部2→1；Plan跨Owner接替阶段仍分别确认。

Sole **Control Intent Authority**：Control State、Tactical Hypothesis、
Observation Control、Traversal Control、Recovery/Reorientation。不拥有
WorldBelief、Run progress truth、Assurance Judgment 或 effect delivery
（权威表见基线 §10）。

## 代码归属

| 文件 | 职责 |
|---|---|
| `ObservationRecoveryControl.cs` | 当前执行位置的有界补证等待与run子配额，恢复不续预算 |
| `TraversalControl.cs` | Control所有的有界探索/返回/深度和义务游标；Run另记录执行事实，不投递Effect |
| `TraversalControl.History.cs` | 原完整Traversal操作的候选确认、完整状态冻结；原选择/分配不是Run完成证明 |
| `ControlTraversalHistory.cs` | 原来源协议、原Run绑定与纯只读历史查询；不创建可执行Control或签发Intent |
| `ControlLoop.cs` | control intent 权威本体（不变量 21） |
| `ControlIntent.cs` | intent 的 typed 形状（跨 Product T2 只传 immutable command） |
| `ControlPolicy.cs` | 控制策略词汇 |
| `TacticalHypothesis.cs` | 战术假设（reorientation/recovery 的输入） |
| `DescriptorTargetPolicy.cs` | 目标规格（CTL-001 D1）：authoring 侧「对什么 affordance 施加什么」的表达 |

## 变更规则

产生/记录 control intent 的语义进本目录；intent 的**执行**归 Effect
Boundary，intent 的**安全裁决**归 Assurance，进度真值归 Run Model——
三权分立是基线 §7 的硬约束，不得在 本目录合并。

## 指向

- 基线 §14；组件间协议：`docs/architecture/protocols/inter-component-protocol-baseline-l1-l3.md`；
- 决策缝消费（Agent 咨询）：`../Runtime/`（AgentDecision/PolicyRuntime）。

## WORLD-UNCERTAINTY-001 实现

- `ControlLoop.WorldConsumption`：Control记录实际决策供给来源并识别撤回的计划依据。
- `WorldRecoveryControl`：Control去重真实执行/结果版本并管理安全边界复核提醒。

## 原Traversal操作历史（RUNTIME-RECOVERY-013）

ControlLoop可注入IControlTraversalJournal；默认不录制时TraversalHistory为null，不能当作确知空历史。Kernel接受原Run后绑定其原身份及Run RecordingId。ConfigureRules/Start/Select/Navigated/Returned/ReadOnlyCompleted/Pause/Resume/Abandon/Fail/Gap/Drain以完整操作在候选副本上执行，原输入、结果、完整状态先确认再发布；嵌套Gap/Pause属于同一操作。失败锁阻断Kernel/Driver后续执行。原算法保持，Start也保持原invocationOrdinal不清零的语义。

完整状态包括路径Frame和activePath、候选观察/分配/Completed簿记、已展开导航深度、原规则、返回操作与预期父页、plan版本/调用序号、暂停/发现/结束状态、gap去重及待交Run队列。Completed集合表示调度预留；Drain表示交出Control队列，均不证明Run已确认操作完成。队列的未决/交接状态按原操作保存，不能重放它去补写旧Run事实。

ControlTraversalHistoryRead.ReadOriginal只核验原Run/Control源、身份、连续原记录与完整结构，并返回冻结JSON；不运行Select、创建ControlLoop、恢复旧Intent引用或Current World。原选择结果与后续work引用、调用ordinal保持原关联。Host身份归档v4保存Control录制GUID；旧v1/v2/v3的资源/Run读取继续工作，但缺Control源明确拒绝。此独立查询不认证五方共同执行点，不恢复其余Control子域、Runtime完整游标、Task/原期限或执行续跑。
