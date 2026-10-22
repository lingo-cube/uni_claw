# WORLD 残留不确定性：一手资料研究

Status: COMPLETE / EVIDENCE-BOUNDED
Authority: NONE
检索日期：2026-10-22。本文仅提供外部类比与待核实项，不改变已接受 Owner 判断、权限边界或 Q1-Q11 决策。

## 结论范围

已接受设计包含：四类有依据的知识缺口、实际尝试及结果、每次 Agent 咨询可见、新结果才主动复核、沿原 Owner 判断解决、预算不重置、旧输入不全仅诊断。以下业界资料提示需要把动作执行、语义进展、覆盖边界、时间相关性分开表达。不是建议另建 watchdog。

## 一手来源与映射

### 浏览器动作成功与业务后置条件

[W3C WebDriver：Element Click](https://www.w3.org/TR/webdriver2/#element-click)定义点击命令的交互步骤及可交互性检查；[Playwright：Assertions](https://playwright.dev/docs/test-assertions)将动作与自动重试的后置断言分开。原意是动作可被执行与期望状态成立是两类证据。可借鉴为 Fix connectivity 按钮可点击只证明交互入口执行，不证明它应导航或已达成目标；后者须看实际状态及任务断言。不能证明具体产品语义，也不能决定现有 Owner 归属。映射推论：将 action result 与目标状态证据分开记录；按钮意图需由现有 Owner 判定。

### Android 无障碍节点元数据

[Android AccessibilityNodeInfo](https://developer.android.com/reference/android/view/accessibility/AccessibilityNodeInfo)提供节点 action、scrollable、collection 与 collection-item 信息；[AccessibilityAction](https://developer.android.com/reference/android/view/accessibility/AccessibilityNodeInfo.AccessibilityAction)说明滚动动作及滚动事件。原意是辅助技术可发现和操作界面结构。可借鉴为滚动成功、节点集合信息可支持遍历证据；不能证明节点承载业务用途、实际设置语义、或 Goal 完成。映射推论：动态全树发现与观察/完成应有不同证据状态，不能将节点数或滚动动作直接提升为任务进展。

### Kubernetes rollout 停滞

[Kubernetes Deployment 文档](https://kubernetes.io/docs/concepts/workloads/controllers/deployment/)说明超过 `progressDeadlineSeconds` 会报告 `ProgressDeadlineExceeded`，控制器仍继续处理，且不会自动采取回滚等动作。原意是进展判断、告警、处置分层。可借鉴为停滞信号不等于任务取消或自动改策略。不能类推其 Deployment 指标就是 Settings 进展定义。映射推论：现有 runner 已按去重进入数和 frontier 数判断停滞，且有冷却再次提醒；应聚焦这些代理量是否能识别同数不同集合、数量波动及深页语义推进，并实测阈值，而非新建第二套 watchdog。

### Nav2 的目标相关进展检查

[Nav2 PoseProgressChecker](https://docs.nav2.org/rolling/configuration_and_development/configuration_guide/core_servers/controller_server/controller_server_plugins/pose_progress_checker/)以目标相关位移/转角阈值和时间 allowance 判定进展。原意是特定任务定义可量化进展并给出等待时限。可借鉴“进展指标应与目标相关，且有时间边界”。不能把物理位移等同 UI progress，也不能直接照搬距离阈值。映射推论：Settings 应用可考虑以目标语义里程碑作为核验对象，但字段、规则与裁决仍归既有 Owner。

### GraphWalker 覆盖停止条件

[GraphWalker Stop Conditions](https://graphwalker.github.io/graphwalker-rs/stop-conditions.html)明确 coverage 相对于给定模型的顶点/边/需求，支持与特定终点、时间或步数组合。原意是覆盖率的全集是模型，不是整个系统。可借鉴为 Goal scope 投影需声明其模型边界及完成条件。不能把模型 100% 覆盖说成全 Settings 覆盖，也不能证明模型本身完整。映射推论：滚动或动态全树只能证明观察到的范围；报告中需保留目标范围与已知未覆盖范围。

### OpenTelemetry 时间与关联

[OpenTelemetry Logs Data Model](https://opentelemetry.io/docs/specs/otel/logs/data-model/)区分事件发生时间 `Timestamp`、采集观察时间 `ObservedTimestamp`，并提供可选 TraceId/SpanId。原意是关联记录并保留事件与观测时序。可借鉴为 Heartbeat 与 CasePacket 应以明确身份/运行上下文关联，并区分生成与送达时间。不能证明消息真实送达、无丢失或 exactly-once。映射推论：Leader 需用同 cutoff 的端到端证据核实真实送达；仅有同 trace 或先后时间戳不足以证明。

### Goal要求与可观察结果

Leader补充核实[Cucumber Gherkin Reference的Then](https://cucumber.io/docs/gherkin/reference/#then)：预期结果应与实际可观察输出比较，执行步骤本身不是结果证明。可借鉴把每条用户要求对应到声明范围、证据和完成判断；不能据此推定现有Contract必须采用Gherkin，也不能让测试Oracle或Host报告成为产品完成权威。对应本项目的具体源码事实、已有任务屏障及待补映射见residual-analysis.md。

## 可行路线与取舍

路线 A：复用既有明确任务导航范围，以及现有进展与证据屏障；未知目标保持不点击，步数不作为完成条件。优点是沿现有机制验证，成本较低；限制是当前按 route 的正向范围检查未必覆盖所有 Goal 投影。路线 B：本轮同时扩展到可审核的通用用途证据与任务语义里程碑，未知目标仍不点击，也不以步数判完成。它可能覆盖动作意图与业务结果之间的缺口，但设计成本、运行成本与校准方式均待评估。两条路线都不改变既有 Owner 判断；选择需由 Leader 根据源码事实和代价核实。

## Leader 待核实事实

1. 当前 `SettingsActionPolicy` 将 tap/click 归为 navigate；Fix connectivity 的实际动作语义及其 Owner 判断是否已有证据。
2. `SettingsNavigationScope` 为可选项，按 route 的正向任务范围二次检查；哪些场景未配置、Goal 范围投影是否完整。
3. runner 的去重进入数/frontier 计量面对同数不同集合、数量波动、深页有效推进时的真实表现与校准依据；现有冷却提醒是否符合预算约束。
4. H7 的 Heartbeat/CasePacket 在同 cutoff 是否真实端到端送达，送达时间、接收方及关联身份证据何在。
5. 动态树扫描、滚动、节点更新分别被记作发现、观察还是完成的现行证据规则。

以上均为事实核验项，不构成 Owner 变更建议或产品裁决。
