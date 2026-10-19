# SETTINGS-COVERAGE-005 — 弹窗到达事实接入遍历待办与跳过后继续
lifecycle_state: implemented · disposition: none · depth: standard · base: 2225c642

## Intent（WHAT/WHY）
真实 RUN-006 已记录 SIM name & color 的 ReachedOverlay，但动态子页 frontier 仍派发该目标。随后 auto-skip 因首次标记不可改写失败，Director 却记录 skip 并返回 Defer，最终 defer-exhausted。本次修复让既有到达事实影响候选选择，且跳过后的继续不伪装等待。

## Scope / Out of Scope
- 修改 Host SettingsCoverageLedger 的目标状态消费、Director 跳过后的继续与真实结果记录；补现有公开接口回归。
- 接续入场时 Director 与 PlanTests 的未提交修改；完整补丁已在仓库外保存恢复快照。
- 不改 Kernel Defer 预算语义、协议、首次标记不可改写原则、Effect 权限、弹窗确认禁手策略；不把有界停止写成完整 Settings 树覆盖。
- Owner 后续授权适当增加任务预算；本次验证 override 采用 maxSteps=256/maxConsultRounds=320，其他保护值沿用，原 product 默认文件不改。

## Decisions
- ReachedOverlay/RouteEntered 的已到达事实须从普通 pending frontier 排除；显式重复进入验收仍保留。
- 标记写入结果决定日志内容，不把失败写入宣称为 skip。
- Host 本地跳过后的选择在同次咨询中重新计算；无有效动作时诚实有界停止，不生成空指令或用 Defer 代替调度。
- 沿用既有目标描述符标记语义，本次不引入新的身份契约或放宽未知路由事实。
- 首轮真实复验发现 Roaming 的 Checkable=true 却被当导航派发；派发前排除范围补齐为策略声明开关或本次 context.Elements 已观测的 role=switch / Checkable=true，与既有 guard 判据一致，零开关 Effect。

## Acceptance
1. 动态子页已标 ReachedOverlay/RouteEntered 的目标不再位于待办；无关 pending 目标仍保留；首次标记保持不可改写。
2. 按真实形态的进入→弹窗到达→CANCEL→下一咨询回放，不重新进入已到达目标，不把已到达改写为跳过。
3. 自动跳过/策略排除后发出下一个有效导航或诚实 NoAction；不返回调度用 Defer、不生成空 directive、日志与账本实际一致。
4. 定向回归、全量 solution、场景认证与覆盖、协议生成检查通过；真实专线/注册模拟器运行留证，诚实区分本缺陷修复与完整树覆盖。

## Verification
```yaml
verification:
  level: ENVIRONMENT
  method: "public Ledger/Director regression RED→GREEN; dotnet test UniClaw.Kernel.slnx --no-restore; scenario_certify.py --check; scenario-coverage.py --run; gen-protocol-machine.py --check; real DSH/registered emulator coverage run"
  expected: "reached overlay is never re-dispatched as pending; local skips continue without Defer; existing safety/immutability preserved"
  actual: "离线 1637 项全绿；live-1 已越过 SIM 弹窗循环；live-2 96 步/112 咨询用满，覆盖要求 50%，另有 Quickstep 动作后唯一性失败；整体验收未闭合，Owner 暂停后续执行。"
  evidence: "evidence/settings-coverage-005/ (to be collected); evidence/run-006/full-traversal/run-20261017-104646-383/"
```

## Status log
- 2026-10-17 · UNDERSTAND → RESOLVE → PERSIST → PLAN → IMPLEMENT · 已有真实帧/trace/bridge 与当前编译产物最小复现：ReachedOverlay 写入 true、后续 skip false、frontier 仍选同目标。Owner 授权直接修复。Codex Worker binding 缺席（ROUTING_UNAVAILABLE），Leader 直接执行。

- 2026-10-17 · VERIFY → IMPLEMENT · live-1 已证明 SIM 弹窗到达后转到 Roaming（只进入一次），后者被既有 guard 拒绝（switch target requires typed desiredState）。补齐当前已观测开关的导航排除，保留失败轮，不改 guard/权限。

- 2026-10-17 · VERIFY · Owner 指令“可以适当增加预算才对 按预期”：据已走过网络/连接设备/Apps 的实况安排 256 步/320 咨询的下一轮有界验证，保留 96 步轮证据，零权限扩张。见 evidence/settings-coverage-005/budget-plan.json。

- 2026-10-17 · IMPLEMENT / VERIFY 暂停 · live-2 自然结束：budget-exceeded:steps，96/95 步执行/验证、112 咨询、100 观测，覆盖要求 50%；一级 3/7、二级 7/8、0 滚动。43 次返回与 unknown-page 分支 30 次需整体诊断；step 91 Quickstep 验证失败待定位。Owner 指令“先总结整体运行情况…等我指令再开始”：已生成事实报告和整体小结，256/320 未启动，等待指令；不宣称 CLOSED。

- 2026-10-17 · 只读报告补充 · 注册 run-report 生成成功，发现摘要 consultations/deliveredEffects/completedSteps=0 与原始 112 交换/96 步不一致；trace Quarantined、terminal=false 两条告警。记录报告适配/来源核对缺口，未启动 model-procedure 诊断或修复；见整体小结。

- 2026-10-18 · VERIFY 恢复 · Owner 指令“再次启动全遍历测试”：RUNTIME-CONSULTATION-001 修复后正式配置运行，初始96步/112咨询、规则续额最终256步/320咨询，阈值20/24；不使用旧256/320初始override。真实注册设备和3081部署复验通过，源摘要无漂移，已有修改保持。运行记录：evidence/settings-coverage-005/full-traversal-20261018-103122；由独立后台 launcher 托管，等待实际结果，不提前声明完整覆盖。
- 2026-10-18 · VERIFY 结果 · 正式配置全遍历10:31→10:48自然结束，exit=3，TerminalNotProven/evidence-insufficient；92步执行、91验证、109咨询，覆盖要求62.5%，一级3/7、二级8/8、滚动0。step91（零基）Quickstep动作后唯一性验证失败；末次恢复提出Navigate up，被Director按Quickstep指令匹配拒绝，3次后无有效决定。续额窗口21次开启但budgetRenewals为空，最终尚余4步/3咨询；非预算耗尽终止。空账本不足以区分模型未申请与上游过滤，需进一步诊断。证据：evidence/settings-coverage-005/full-traversal-20261018-103122/result-summary.json；未自动重跑或修改实现，不宣称CLOSED。

- 2026-10-18 · 后续修复 · SETTINGS-COVERAGE-006 已修复失败恢复被旧目标阻断、续额错层反馈不明与咨询上限误报；真实定向Quickstep失败→Defer→返回成功，并获批续额。该证据不是全树复验；本Change继续保持原状态。
