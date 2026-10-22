# SIM-015 — 组件隔离场景的Agent未参与标注
lifecycle_state: closed · disposition: none · depth: standard · base: 9bc15b8974f5fb5746074e373e6c132bc4df0089
## Intent / Owner decision
SIM-014两个真实组件没有装配Agent；既有场景强制decision=real/double会造假。用户2026-10-22明确选择“扩展组件场景标注，严格限定范围”。此为仿真C7 Human Gate裁决，仅扩组件隔离元数据，不改产品协议与Owner。
## Scope / Boundaries
新增可选testScope=component；仅明确component-test、execution.kind=none、无Agent参与的条目可使用agentDecisionRealization=not-applicable，GoalEvaluation也为not-applicable，咨询0、Goal/分类null、Runtime status描述为not-applicable，components非空且不含agent/uni-agent。已有35条元数据不改，整装默认/约束保留。禁止把N/A当double或产品通过，不新增产品Runtime分支。
写入scenarios/schema.json、ScenarioRealizationAnnotationTests.cs、测试侧ComponentScenarioScope.cs/ComponentScenarioScopeTests.cs、tools/scenario-coverage.py、tools/tests/test_component_scenario_scope.py、共享annotation-cases.json、本Change/evidence与C7局部delta。
## Acceptance / Verification
共享固定正反例由JSON Schema/Python和C#均消费；非组件、缺scope、golden、声明Agent或Goal完成、scope与realization冲突均拒绝。旧条目零改动且完整场景真值链通过。父SIM-014在此完成并接受后才能收口。
level: CONTRACT
method: 固定跨语言矩阵RED/GREEN、旧场景校验、父线隔离full与场景覆盖
expected: 严格限制N/A而保留旧整装行为
actual: PASS — 共享19例（三正十六反）三侧一致；旧35条不改，原构成锚点通过，父full2101/2101、37场景，边界评审无遗留问题。
evidence: evidence/sim-015/
## Status log
- 2026-10-22 UNDERSTAND→RESOLVE→PERSIST→PLAN：用户已完成C7范围裁决；按最小组件隔离标注实施并保留跨语言同源用例。
- 2026-10-22 IMPLEMENT→REVIEW：共享19行矩阵（三正十六反）；旧schema拒绝2个新正例，旧C#场景锚点拒绝新增两条N/A，修订后两侧专项通过。Python工具10/10；最终full与两轴评审随父SIM-014收口。
- 2026-10-22 REVIEW→VERIFY：独立Spec与Standards完成；Standards首次将SIM-007既有Goal N/A误报为新例外，按原Owner修订与固定正例复核后撤销。最终候选进入当前源码隔离full，旧条目与product源码保护检查继续。
- 2026-10-22 VERIFY有界修复：首次full仅DocsMetadataTests.Architecture_DeclaresFrozenAuthority失败（新C7局部修订写ACCEPTED，仓库要求FROZEN/CLOSED/COMPONENT-BASELINE）；修订按既有Owner批准记CLOSED。保留attempt1日志；产品/组件行为、37场景与旧认证均无失败，未修改预期或源码。
- 2026-10-23 VERIFY→CLOSED：最终full PASS（8工程2101/2101；Simulation314；37场景=20 certified+17 descriptive）；两新场景精确关联14+8个实际通过用例。原35条、产品源码、录制/导入/旧fixture/认证未改；4项既有元数据修改均属已授权SIM-015。边界、接口与生命周期清单见changes/SIM-014/component-contracts.md；真实Host/设备/模型和大模型候选生成不计作已完成。
