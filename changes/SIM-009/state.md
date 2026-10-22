# SIM-009 — 模块契约、场景与错误回放回归
lifecycle_state: closed · disposition: none · depth: standard · base: 9bc15b8974f5fb5746074e373e6c132bc4df0089

## Intent（WHAT/WHY）
用户要求同时覆盖场景库仿真、各模块测试与错误回放：先复现可观察分歧，再用相同输入/独立期望核验修复。现有 full 入口已覆盖八个 .NET 测试工程，但场景注册缺身份补证与操作关联空间反例；确定性双跑不能替代独立期望。

## Scope / Out of Scope
- 新增 Simulation 专有回放测试与 JSON 输入/期望；经实际 EvidenceLedger、WorldModel、RunModel、ControlLoop、RuntimeAssurance、EffectBoundary、UniKernel、KernelRunDriver 运行。
- 新增 SCN-REPLAY-001/002/003 专有载体登记，沿现有 Trait/coverage/schema 发现。使用 execution.kind=none，明确不声称 golden-bundle 认证；详细期望由专有 JSON/测试明确消费。
- 覆盖矩阵列出模块契约、既有测试、场景交叉验证和未验证项；独立 evidence 保留错误重现、补证恢复、oracle 检出与修复前后结论。
- 不改 src/、World 线负责测试、共享 INDEX、模型路由、协议或旧场景期望；不运行设备/真实模型，不提交推送。真实产品缺陷保留反例交负责线，不能改期望追绿。

## Decisions
- 沿用已批准产品缝和 WRC-003 平移/未知边界；不引入通用回放服务或第二个 World。
- 只替换外部输入、Agent 决策和投递；期望来自固定例子/既有契约，与实际 owner 输出分离。修正 fixture 或补证不是产品代码修复。
- 分开记录：历史缺陷回归、当前错误输入的拒绝/恢复、oracle mutation 检出；没有本轮产品修复则明确报告无。
- 回放产物包括输入来源、检查点、first divergence、产品关系/回执引用；JSON 导出受单独输出环境变量控制，不写回期望。

## Acceptance
1. 身份补证回放：缺身份暂停且不重复投递；错 capture 不授权；匹配补证经入账/World/Assurance 后恢复原遍历；历史 revision 不变。
2. 操作回放：导航/回执/已验证滚动进入产品图；充分重叠的固定例子位移与内容坐标正确；重排/嵌套/歧义保留关系但偏移未知，不假装已还原。
3. 独立期望可检出故意错误的修复结果/坐标/状态；报告给出可定位 first divergence；输入/期望不被 actual 覆盖，同输入双跑结果确定。
4. 新场景被既有 Trait/schema/coverage 发现；模块责任矩阵与实际运行范围一致，不将缺断言报成覆盖。
5. 专项与相关模块回归、Simulation 全量、认证与新鲜 TRX coverage、既有 full 入口执行并保存结果；环境/既有失败与本次缺陷分开归因，不要求更改无关产品追绿。
6. 独立 Review 与 Verify 分开；负责路径外保护 hash 对照；写前快照可恢复且不撤销并行更改。

## Plan
逐片执行：身份补证回放 → 操作/空间回放及独立 oracle 反例 → 场景映射与模块矩阵 → 专项/全量验证 → 独立评审及所需复验。
构建在本线独占 /private/tmp 源快照中串行运行，避免共享 bin/obj 和设备；保存快照输入指纹。

## Verification
```yaml
level: SCENARIO
method: 专项回放及 mutation；dotnet tests；tools/verify-change --scope full；scenario coverage；保护 hash
expected: Acceptance 1-6
actual: PASS within declared scope; eight projects 2038/2038; Simulation 263/263 including 11 new replay regressions; recorded/reconstruction Host 20/20; coverage 31/31; two-axis review 0 remaining blockers. Initial full failure retained and replaced only by justified affected reruns plus missing Host.Dsh execution.
evidence: evidence/sim-009/
```

## Status log
- 2026-10-22 · UNDERSTAND→RESOLVE→PERSIST→PLAN · 用户明确开干并接入错误复现/修复回放；核对既有完整链和专有载体机制，限定本线增量；保护快照见 evidence/sim-009/entry/。

- 2026-10-22 · PLAN→IMPLEMENT · 新增两类数据驱动真实产品链回放与三条专有场景。初始 oracle 四变异 RED；fixture 默认配额4不足以支持五帧身份，保留错误回放并明确配额8修正；身份结束响应改为有界 NoAction，保留 TerminalNotProven。
- 2026-10-22 · IMPLEMENT→REVIEW→IMPLEMENT · 独立 Standards 发现导航内容仅数边的漏检；三个错边反例3 RED后补独立端点/方向/类型检查，复核0阻塞。Spec 三项疑问经实文件/逐检查点/RED-GREEN证据澄清后撤回，最终0阻塞。
- 2026-10-22 · REVIEW→VERIFY · full 首轮因快照缺旧XML与新Theory展示名映射失败而FAIL；拷入25个原XML、改为无参数场景入口后Host411/411、Simulation263/263、coverage31/31。实码发现full漏Host.Dsh，显式补跑56/56；其他模块原有效执行保留，八工程合计2038/2038。
- 2026-10-22 · VERIFY→CLOSED · 本线验收完成；源码与测试快照hash一致。保护5258个既有文件；3个外部并行文档变更单列未回退。共享INDEX按入口限制未写；未提交/推送。产品/真机/模型质量与新语义边界不外推。
