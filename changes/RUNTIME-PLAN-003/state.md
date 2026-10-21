# RUNTIME-PLAN-003 — 执行协议一致性与有序异步 handler
lifecycle_state: closed · disposition: none · depth: decision-heavy · base: bce0ec3a

## Intent（WHAT/WHY）

Owner于2026-10-20确认分两个Change顺序推进，并授权当前Leader组织实施。先修复002正式协议的类型/文档不一致，再使操作级handler支持有序异步等待、状态查询和原位置续行，避免当前Task.Run后同步Wait阻塞协调路径。002保持原关闭范围与历史证据；004依赖本Change稳定协议。

## Scope / Out of Scope

范围：TypeScript生成器正确生成null；修正002文档示例；现有语言检查typed适配的异步执行、运行中记录、完成/超时/取消及迟到结果边界；真实Kernel链仿真、协议资产与Prompt闭包同步。实施前冻结工程契约，不把候选字段直接当已支持。

范围外：新领域Capability、任意字符串Execute、独立Handler Registry、默认并行、跨进程恢复、完整菜单发现证明、改变领域Finding/Goal/Assurance权威、真实模型生成评估（归004）。

## Decisions

- D1（Owner，2026-10-20）：两个Change，先执行协议，再UniAgent生成/Profile/Skill；Leader有序推进，沿用此前Worker模型授权。
- D2（沿用002及本轮已接受建议）：Plan声明目标、时机、顺序与能力绑定；同步/异步由执行适配负责，不要求模型添加async控制流。before全部满足继续条件后才能操作，独立动作验证后才能执行afterVerified。
- D3：World拥有事实、Run拥有执行记录、Control Loop拥有Traversal与Recovery、Capability拥有领域结果；driver协调，无新主权威。
- D4：运行中沿用只读查询面；最终报告只在执行结束时返回。等待不触发咨询、不重复提交调用、不重发已验证动作。超时/取消不等于底层已经物理停止，迟到结果不得污染终态。
- D5：已知根因的两处协议材料问题先修；异步工程形状由代码事实与现有typed缝论证后冻结，未冻结部分暂不派发实现。
- D6（Leader，既定范围内工程选择）：已核对Host/Simulation入口；不改ILanguageInspector业务协议，扩RunDriverInputs typed异步适配；保留Drive可恢复入口，新增DriveAsync持续等待接回，pending用WaitingForHandler，Run记录可选callId及Running/Cancelled。具体契约见[execution-contract.md](execution-contract.md)，无新增领域语义裁决。
- D7：本地typed结果交接完成时间决定调用截止，不承诺provider内部finished-at。Drive/DriveAsync共用串行入口，竞争执行返回drive-in-progress，不创建第二执行；只读查询不受此门闩阻塞。
- D8：并行WRC语义与源码不归本Change；干净基线叠加本次文件独立验收，同时验证共享目录完整装配。生成Schema按各自实际World类型同源生成，身份分别记录，不能将并行新增场景数归为本Change新增。

## Acceptance

- A1：ReadOnly.step在C# codec、JSON Schema、TypeScript一致为必填null；正式文档示例可通过Schema；生成资产同源且镜像/握手无漂移。
- A2：真实Kernel执行慢handler时可观察Running、查询不推进；执行协调可让出等待，不同步Wait堵塞整个等待期；完成后接回同一操作/handler位置。
- A3：before/afterVerified有序屏障不变；记录领域不符合与调用失败分离，required/requirePass语义保持；每次Effect仍经原安全与验证链。
- A4：调用关联和单一终态可验证；超时/取消/版本替换与迟到完成竞态不污染当前运行，不隐式重提；恢复保留已验证动作、结果、预算。
- A5：同步及真正延迟完成的typed适配使用同一Plan形状；仿真经真实Kernel链，正式producer/codec/consumer与生成协议校验通过。
- A6：独立Review、相关回归、场景再认证及文档同步；所有结论有method/expected/actual/evidence四元组。

## Constraints / Owner-Authority impact

共享脏工作区：入口恢复快照见evidence/runtime-plan-003/entry-20261020/manifest.json，不能清理/覆盖其他任务。显式文件所有权，Worker不提交推送、不改验收门槛或Owner决策。无新权威，无跨域直写，不将状态查询变成驱动操作。

## Assumptions / Alternatives / Residual risks

已有语言能力业务输入/期望/豁免和Finding含义沿用冻结协议，当前是既有能力集成执行机制的扩展，不注册新能力。若查证需改变领域语义或新能力则回RESOLVE。拒绝通过Task.Run+同步Wait冒充真正异步，也不预建通用任务平台。需核对Host/Simulation持续执行入口，不能只让driver返回等待而无人恢复。

## Verification

verification:
  level: SCENARIO
  method: "按plans/runtime-plan-003.md运行正式协议测试、真实Kernel/Simulation异步场景、Host适配与相关回归；scenario_certify --change RUNTIME-PLAN-003 --all/--check；独立Review"
  expected: "A1-A6全部满足；原Owner/安全/验证/预算语义保持；异步可观测且有序续行，晚到结果无写入"
  actual: "A1-A6 PASS。隔离基线C#1795/1795、同源Schema48eedf/prompt20的DSH Node45/45；共享目录C#1858/1858且测试期间Kernel/Agent源码零漂移。独立双轴最终各0确认未处置问题。新异步仿真4项包含于Simulation220，不重复加总；共享1858包含并行WRC场景，不算本Change新增。"
  evidence: "evidence/runtime-plan-003/verification.json; isolated-verification/{test-summary.json,test-results/,entry.json}; shared-final/{test-results/,source-drift.json}; protocol/node-tests.log; review-1/{standards-final.md,spec-final.md,disposition.md}"

## Status log

- 2026-10-20 · UNDERSTAND→PERSIST · Owner授权，已复验002交付与当前HEAD；已保存763个相关文件恢复快照。先派发协议修复，Leader并行核对异步缝及持续执行入口。
- 2026-10-20 · PERSIST→PLAN · 异步买方、typed适配与持续驱动已核对，工程契约冻结；现有语言领域协议不动，无新能力拷问前沿。测试缝沿Owner已确认协议与真实Kernel链仿真；派发Kernel异步垂直切片，Host/Simulation随后接线。
- 2026-10-20 · IMPLEMENT进展（仍在PLAN/IMPLEMENT阶段，未宣称implemented）· A1切片修复完成，生成回归RED/GREEN、Agent.Dsh全套199/199、文档正式Schema校验通过，见evidence/runtime-plan-003/protocol/；异步Kernel实现及Simulation接线进行中，Host新增适配测试已RED。
- 2026-10-20 · PLAN→IMPLEMENT→REVIEW · 有序异步、Host/Simulation接线完成；Leader发现三项回归先RED后修复，独立Standards发现并发入口缺口后亦RED/GREEN修复，最终双轴复核无确认未处置问题。
- 2026-10-20 · REVIEW→VERIFY→CLOSED · 隔离1795/1795、Node45/45与共享1858/1858均通过；隔离组装误纳两份并行World文件、路径别名构建问题及两处版本期望未随动均保留失败证据后纠正。A1-A6四元组见verification.json，交付见implementation-report.md。未提交推送；接续004，不将确定性结果表述为真实模型/真机验收。
