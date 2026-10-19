# RUNTIME-PLAN-001 独立增量评审

评审基点：`evidence/runtime-plan-001/entry/snapshot.json` 对应的 dirty 入口；逐文件增量以 `review/implementation.patch` 与 `review/inventory.json` 为准，不以 HEAD 差异代替。检查了 71 个清单项，当前字节 SHA-256 均与 inventory.after 一致；20 个场景文件的补丁只改 `runtimeSourceHash`、`certifiedByChange`、`certifiedAt`，`expectationsDigest` 与 `executionDigest` 保持原值。静态评审不运行重型测试、设备或真实模型。

## 规格轴

**阻塞 Bug：未发现静态证据。** ADR-0053 §2、实施契约 §6-18 与实施计划 §3-7 要求计划逐动作复用共同Kernel链、候选从Owner投影动态重读、未知安全停止、受阻仅在有证据且显式允许时略过、精确位置续行并保留尝试与receipt。`KernelRunDriver.cs:1290-1468` 在每个候选处理完后回到PlanExpand重新调用 `ProjectPlanCandidates`，按稳定Key去重，Unknown退出，Blocked须有原因/证据且 `ContinueOnBlocked` 才记录并跳过；每个Act进入既有StepAct/StepVerify。`KernelRunDriver.cs:830-848,2443-2453` 要求已暂停、逐字段位置相等、Plan身份/版本一致，且续行前仍有步预算。`KernelRunDriver.cs:1821-1889` 保留attempt身份、关联receipt及证据。具体反例测试覆盖E4在ForEach body中已满足后仍推进body、重复迭代旧step锚不能误证、聚焦重试/gate拒绝更新原Started attempt、普通Act guard拒绝不污染Plan尝试（`KernelRunDriverPlanTests.cs:1290-1515`）。这支持已知边界修复，但不能代替现场行为证据。

**未完成验收：现场对照。** 计划 §3-7、proposal §10要求先Step基线，再ShortPlan与RulePlan同范围对照；当前证据尚未开始，故主要泛化能力及延迟/成本收益未证明。新增 `RuntimePlanComparisonLiveTests.cs:21-149` 记录实际决策、执行状态与验证步数；`ConformsToMode`（151-163）分别约束Step不含Plan、ShortPlan含Plan但不含ForEach、RulePlan实际含ForEach且所有模式排除Policy。收集器 `collect_metrics.py:60-62` 仅将已完成、覆盖完成、模式匹配且验证数达预期的trial标为valid。故注册环境和逐组试验仍是关闭该验收项的前提，仿真/既有单测不能冒充现场结论。

## 规范轴

**硬性规范违规：未发现。** 增量符合AGENTS.md §1.5的单一职责/显式缝约束和ADR-0053的权限边界：Kernel只持有运行位置与执行记录，候选/动作含义由Host Owner投影及逐步guard提供；Settings事实源在 `SettingsPlanFacts.cs:16-56` 从CoverageLedger当前接受观察构造语义候选，不提供坐标或完成结论；`SettingsCoverageRunner.cs:259-285` 先同步Owner事实、对物化动作做guard，并把获准步骤交给既有receipt归属账本。正式协议路径也有闭合实现：`DecisionJsonConverters.cs:55-82` 对plan/continuePlan严格检查顶层字段和位置非负值，`ProtocolSchema.cs:292-360` 从共享类型生成Plan联合结构，产品/DSH Prompt副本说明相同Plan与续行字段；场景重认证没有改期待或执行输入。以上对应实现契约 §6-18及协议基线P25。

**非阻塞建议：** 本次未记录需修改的代码气味。标准检查只基于增量，未把已由测试/生成校验执法的问题重复报为人工缺陷。协议、schema/hash、Prompt双副本及20个场景认证的静态一致性可审查；但静态一致性不证明真实模型会生成合规载荷，也不证明Settings设备动作适用或提效。Owner已确认的Q1-Q9/D10与ADR-0052不在本评审重开范围。

## 结论

规格轴：0项阻塞Bug；1项未完成验收（真实三组对照）。规范轴：0项硬违规；0项非阻塞建议。关闭前仍需按预注册顺序取得有效现场trial，并保留失败/无效试验消耗与原因。
