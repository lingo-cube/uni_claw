# RUNTIME-PLAN-001 组合实现：规范轴审查

范围为 `standards-implementation.patch` 的 33 个增量文件；未构建、未运行测试或设备。

## 文档标准违反

未发现阻塞性违反。

- Kernel 增量只增加通用 `IfItem`/`RepeatItem`、块帧与 Owner 回调（`src/UniClaw.Kernel/Runtime/AgentDecision.cs`、`PlanExecution.cs`、`RunDriverInputs.cs` hunk）；`KernelRunDriver.cs:1374-1408` 只消费声明条件、记录位置并机械转移。未含 Settings/UI 名称、页面解释或事实权威，符合架构基线 §3.5、§7 的 Kernel 边界。
- 条件名称在采纳前由 `ValidatePlanProposal` 拒绝未声明值（`KernelRunDriver.cs:2926-2934`）；条件结果缺 scope/revision/evidence 或为 Unknown 即退出计划（`1374-1403`）。这满足 P25 的有界 advisory proposal、失败零新 Effect（协议基线 P25:898-925），并未给条件投影 Effect 权力。
- Host 接线（`SettingsCoverageRunner.cs:260-293`）只同步账本后调用 `SettingsPlanFacts`，随后仍经既有 `GuardStep`；未新增 Host 执行循环或 dispatch/授权捷径。`SettingsPlanFacts.cs:5-6,69-85` 也明确只是事实投影，不把候选升格为 locator、许可或完成声明。
- 正式 schema 与插件副本、产品 prompt 与插件副本分别逐字节相同；README 所称 canonical/副本同步规则（`dsh/.../README.md:42-47`）没有被破坏。生成 schema 单行未全文展开审阅；其一致性由此字节比对及现有执法面覆盖。

## 启发式气味

未见需阻塞的气味。`KernelRunDriver.cs:1324-1630` 因封闭 PlanItem union 集中处理控制块；这是本次唯一执行器、避免重复 Driver/重复 switch 的必要聚合。`PlanConditionResult` 的 `ScopeKey`/`Revision` 为字符串身份值（`PlanExecution.cs:73-85`）可算轻微 Primitive Obsession，但当前仅作 Owner 证据关联，未形成跨模块数据团或错误授权；没有真实 buyer 前不建议预造域类型。

## 现场后路由与嵌套额度复核

范围限 `SettingsTraversalLiveFeed`、`SettingsCoverageLedger` 及三组 Host 测试增量；未构建或运行任何测试、设备或模型。未发现新的阻塞性规范违反。

- `DeriveRouteKey` 仍是 Host 感知侧的页面身份投影：先 `homepage_title`，再 `collapsing_toolbar` 的 `content-desc`，最后才回退 `android:id/title`（`SettingsTraversalLiveFeed.cs:304-375`）。输出仍仅为 `title/src/up` 指纹，且注释明确不参与 grounding、不产生 locator；因此符合基线对 World/Host 事实与 Effect Boundary 分离的要求，未把 UI 解释移入 Kernel。
- `DynamicNestedCandidates` 将额度计数收敛到同一路由的全 run 局部投影（`SettingsCoverageLedger.cs:931-955`），只影响候选发现；`includeSkipped` 仍走原审计投影，不改变 ledger 的 canonical 事实、授权或 dispatch。它没有新 Capability、平行 Owner 或 Host 驱动循环。
- 两处修复均有对应离线回归定位：真实/历史 XML 的页头优先断言（`SettingsRouteKeyTests.cs:52-75`、`SettingsTraversalOutcomeTests.cs:14-30`）及重复观察不重置页内上限（`SettingsCoverageLedgerTests.cs:265-276`）。`pageCounts` 是必要的局部状态，不构成跨组件 Data Clump；无须为计数器另造抽象。

审查未采信旧 trial 为新结果，也未因注册计量或 fingerprint 预检变更扩大本报告结论；所述现场 `346/346` 仅作为委派提供的验证状态，未由本审查复跑。
