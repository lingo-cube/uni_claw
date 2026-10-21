# SETTINGS-COVERAGE-007 — 同截图身份补证后的覆盖路由归属
lifecycle_state: closed · disposition: none · depth: standard · base: bce0ec3a

## Intent
有序推进真实验收时，已验证的Network & internet/Internet进入在覆盖表中仍为0，步骤RouteBefore为空。当前正式LiveFeed在同capture身份补证时只返回identity proposal，不重发screen.route；覆盖包装器把它当新观察周期并清空路由。需通过公开Runner确定性复现，保留接纳后事实与实际receipt归属。

## Scope / Out of Scope
仅SettingsCoverageRunner内部记账与既有公开Runner场景回归；不改World/Kernel身份/动作验证，不改Effect权威、模型/预算或安全策略；不因修统计放行Fix connectivity，不把本修复当全树完成。共享dirty树先保存本轮前快照。入口时未授权提交；所有者随后明确“完成后提交推送”，待并行任务1/2交付、核对归属和整合验收后执行，未完成/无关修改不混入。

## Decisions / Diagnosis
现有红反馈环：evidence/ordered-closeout-20261022/live-runner可运行正式Runner；bounded-live-01前两步verified=true但RouteBefore缺失、entered0/7，第三步page-identity-observation-exhausted诚实停止。当前场景只用于确定性重放归属；不重复点击第三个未知用途控件。
候选根因按顺序验证：1）同capture补证无route使_lastCycleRoute置null；2）World尚未确认时就记录候选观察；3）RouteKey与Root配置格式不一致。每次用测试区分，不靠重跑模型猜。

## Acceptance
1. 同capture身份补证经正式Runner后，已验证进入有准确RouteBefore/After、receipt与descriptor，覆盖计数不丢失。
2. 未确认身份不能产生带旧页occurrences的覆盖事实；新capture/补证失败不借用旧身份；不重发Effect。
3. 原普通21步骤完整场景、身份恢复/覆盖相关回归通过；原Owner/权限及最终缺口保持。

## Verification
level: DETERMINISTIC
method: 公开SettingsCoverageRunner+真实形状capture/identity补证的反应式仿真，RED→GREEN；原相关Host回归；双轴Review
expected: 同capture补证的最小enter/back闭环保留前后route和完整覆盖；原普通21步保持；未知阶段不产生假覆盖
actual: 最小enter/back、未确认负例、原普通21步场景3/3；新capture负例1/1；完整8项目2026/2026，新增负例后Host411/411；双轴Review及补审最终0阻塞，场景/协议检查通过。真实全树/H7和动作分类另保留，不宣称修复后已重新live通过
evidence: evidence/ordered-closeout-20261022/route-attribution-verification.json；route-attribution-review.md；route-attribution.patch；bounded-live-01/

## Status log
- 2026-10-22 UNDERSTAND→RESOLVE→PERSIST→PLAN：保留真实失败，Owner已有顺序推进授权；计划先补同capture补证回归再最小修记账；动作分类风险另记，不扩大本修复。

- 2026-10-22 PLAN→IMPLEMENT：公开Runner同capture补证回归RED复现entered0/8；保留缺route补证的原capture周期，观察记账等待World确认，未改动作许可。

- 2026-10-22 诊断最小化：首次扩到全部页面的confirmed fixture在第三步另遇身份耗尽，已保留该失败；归属缺陷已由entered0→1及back1证实。回归收敛为单入口enter/back，另补未确认capture不能算root覆盖的负例；不将此最小修复宣称为所有页面身份均已稳定。

- 2026-10-22 IMPLEMENT→REVIEW→IMPLEMENT→REVIEW：Spec发现A2新capture负例缺失，已补真实形状新capture及零咨询/步骤/Effect断言1/1；Standards/Spec补审最终0阻塞。
- 2026-10-22 REVIEW→VERIFY→CLOSED：8项目2026/2026与新增负例后的Host411/411，场景28认证0违规、协议生成检查通过；仅归属修复完成，不关闭H7/全树/动作分类风险。
