# AGT-005 — Configurable bounded Settings menu coverage traversal
lifecycle_state: closed · disposition: none · depth: decision-heavy · base: 7b49038d

## Intent（WHAT/WHY）

AGT-004 证明了 Settings→Internet→Wi-Fi 的两步闭环（导航 route 验证 + typed 状态）。
其 Gate disposition 明确：全菜单覆盖需另立覆盖边界清晰的任务。本 change 把
traversal 从「单目标短路径」升级为「可配置、有界、可追溯」的菜单覆盖测试：

- 覆盖面：Settings 根页、当前可见一级入口（全部）、至少一个仅滚动可发现的入口、
  至少两个二级页面；并验证返回导航与重复进入不产生错误步骤或重复 effect。
- 有界：遍历范围、最大步数、滚动次数、目标页面、终止条件全部进入配置文件，
  不得写死在测试逻辑中。
- 可追溯：每个已执行步骤记录 DecisionId、DSH session、动作前目标唯一性、
  ADB effect receipt、动作后 hierarchy、ui.screen.route 指纹、
  Grounding/Assurance/Verification checks、失败原因与证据文件。
- 判定规则（沿用 AGT-004 并扩展）：页面跳转仅在动作前目标唯一且动作后路由
  改变时通过；滚动步骤在路由未变且可见内容变化时通过；状态目标必须有
  typed observation。重复进入 = 新的已验证步骤 + 新回执，不得复用旧回执。

## Scope

- 新增覆盖遍历配置文件（bounds/coverage/targets/termination/session 命名）与
  fail-closed 加载校验（UniClaw.Host）。
- 新增 SettingsCoverageLedger（纯逻辑覆盖账本：发现/进入/滚动/返回/重复记账、
  终止条件评估、覆盖率报告、首个分歧点捕获）。
- 新增 SettingsCoverageDirector（ConsultAgent 包装缝：覆盖指令注入咨询上下文、
  计划符合性校验（有界纠正再咨询）、coverage-complete 终局、bounds 诚实停止）。
- 新增 SettingsCoverageRunner：单 Kernel run，contract（objective/AllowedEffects/
  MaxConsultations/MaxTotalSteps/obligation）由配置派生；逐步 trace 工件
  coverage-steps.json + coverage-report.json。
- Kernel 最小扩展：swipe-up effect（tap/swipe 同一支持集声明）、scrollable
  occurrence 角色（typed hierarchy 投影）、滚动步骤 post-action 验证
  （路由未变 ∧ 可见内容变化）。
- Host.Dsh `--settings-coverage` 入口（复用 DSH session、zai-coding-cn/glm-5.3-flash、
  autoCloseTurn=false 语义经专用 3081 服务 profile）。
- 确定性测试补齐：coverage/scroll/back/repeat/route-unchanged/target-gone/
  illegal-desiredState/capacity-retry/session-reuse；受影响场景重认证。
- 专用 3081 服务 + 专用模拟器上的真实覆盖遍历执行与人类可读报告。

## Out of Scope

- 不改变 Product effect authority、Kernel lifecycle、AGT-004 已冻结的导航验证语义。
- 不启动/重启/停止/修改共享 DSH 3080。
- 不做 Settings 全树穷举（覆盖边界由配置声明；未覆盖项如实报告）。
- 不以自由文本或最终截图替代 route/effect/typed 验证。

## Decisions

1. 单 Kernel run + director 包装 ConsultAgent 缝（非多 run 拼接）：DSH session
   天然复用（adapter attach 一次），DecisionId 序号连续，逐步 trace 单源。
2. 覆盖指令经派生 AgentDecisionContext（Objective 附加 directive 段）注入
   DSH prompt；协议面零改动（context 是 advisory 摘要，Kernel 校验仍对原
   contract 执法）。
3. 终局：coverage-complete → director 返回 NoAction；mandatory obligation =
   ui.screen.route == endOnRoute（返回根页的 typed 观察背书 Completion）；
   bounds 耗尽 → 诚实 bounded-stop（TerminalNotProven 语义），不伪装完成。
4. 滚动 = grounded effect（target = scrollable 容器 occurrence，spatial bounds
   派生 swipe 命令），不是裸 adb 调用；返回 = tap "Navigate up"（可 grounding
   目标，经导航验证）。两者都走 Kernel grounding→receipt→verify 全链。
5. 模型容量错误沿用 DshOpenedHttpPeer 既有策略（识别→延迟重试→同一
   session）；其他模型错误 fail closed 并记录。

## Acceptance

1. 配置加载 fail-closed：缺字段/非法值（maxSteps≤0、空目标、未知终止条件、
   非法 desiredState 词汇等）被拒绝并有明确原因；合法配置完整驱动遍历参数。
2. 账本记账正确：可见入口发现、滚动发现（区分滚动前后）、二级页进入、
   返回验证（route 回到父页）、重复进入（新步骤+新回执、无重复 effect）、
   路由未变失败、目标消失、首个分歧点。
3. 滚动步骤：swipe 命令由 target bounds 派生；验证 = 路由未变 ∧ 内容变化；
   内容未变或路由变化均 fail closed。
4. director：指令注入、计划偏离（有界纠正再咨询后仍偏离 → fail closed 记录）、
   coverage-complete 终局、各终止条件（maxSteps/maxScrolls/consecutive
   failures/coverage complete）按配置评估。
5. solution build 0 errors；全量自动测试通过；场景认证通过（Kernel/Agent
   源哈希变化后重认证）。
6. 真实遍历（环境可用时）：覆盖清单逐项有结果；每个已执行步骤有完整 trace；
   报告输出覆盖率、成功率、未覆盖项、首个分歧点；结束清理专用 3081 与测试
   设备，确认 3080 未受影响。环境不可用 → ENVIRONMENT_BLOCKED 证据，不将
   部分路径宣称为全量遍历。

## Constraints

- 真实测试仅用专用 DSH 服务（默认 3081）；禁止操作 3080。
- 模型必须 zai-coding-cn/glm-5.3-flash；autoCloseTurn 默认 false；
  task 目的命名默认「遍历设置菜单覆盖测试」；复用配置的 workspace/session。
- 验证等级：CONTRACT/DETERMINISTIC（单测）+ SCENARIO（组合闭环）+
  ENVIRONMENT（真实遍历）。

## Verification

```yaml
level: ENVIRONMENT
method: focused config/ledger/director/scroll/session tests; deterministic scenario closure (simulated reactive Settings world through SettingsCoverageRunner); solution build/test; scenario recertification; real coverage traversal on dedicated 3081 + emulator-5556
expected: "config fail-closed on illegal values; ledger accounting correct (discovery/enter/scroll/back/repeat, route-unchanged and target-gone fail-closed, duplicate-effect divergence); director bounds/termination enforced; scroll verified only with route-unchanged + content change; all deterministic checks pass; real run covers root + all visible first-level entries + >=1 scroll-only entry + >=2 second-level pages with per-step trace, ends cleaned up with 3080 untouched"
actual: "deterministic PASS: solution build 0 errors; solution tests 1162/1162 (Kernel 712, Simulation 184, Host 94, Agent.Dsh 132, Core 14, Agent 17, FileSystem 9); scenario certification --check PASS 29/29 (recertified by AGT-005); coverage-trx chain PASS; git diff --check clean. Real run (evidence/real-settings-coverage-20261001): dedicated DSH 3081 + zai-coding-cn/glm-5.3-flash + session 遍历设置菜单覆盖测试 (workspace reuse, autoCloseTurn=false) + emulator-5556 API35; Completed/Completion, 19/19 verified steps, coverage 100% (root + 7/7 first-level + scroll-discovered 'Search settings' + 8 second-level routes + 9 verified backs + Apps repeated entry with distinct receipts), divergence NONE, single DSH session across all consults; per-step trace complete (DecisionId/session/uniqueness/receipt/routes/capture/checks). Cleanup: emulator stopped, 3081 killed, 3080 alive-untouched."
evidence: evidence/real-settings-coverage-20261001/; src/UniClaw.Host/SettingsCoverage/; .dsh/profiles/settings-coverage.yaml; tests/UniClaw.Host.Tests/SettingsCoverage*.cs; tests/UniClaw.Kernel.Tests/Assurance/ScrollVerificationTests.cs; tests/UniClaw.Kernel.Tests/Effects/AdbSwipeEffectTests.cs; tests/UniClaw.Host.Tests/ScrollableOccurrenceTests.cs; tests/UniClaw.Agent.Dsh.Tests/SessionReuseTests.cs
```

## Status log

- 2026-10-02 · persisted · 复验 AGT-004 closed 于 7b49038d；两步闭环与
  route/typed 验证语义已在；设计定形：单 run + director 缝 + 配置化 bounds。
- 2026-10-02 · persisted→implemented · Kernel：swipe-up/down effect 支持集
  （bounds 派生命令）、scrollable occurrence 角色、滚动验证（路由未变 ∧
  内容变化 fail-closed）、PostActionEffectVerification 升 public（白名单
  显式修订）。Host：SettingsCoverageConfig/Loader + settings-coverage.yaml、
  SettingsCoverageLedger（发现/进入/滚动/返回/重复记账 + 指令引擎——
  可见性约束 + 滚动候选进入 + 离根恒返）、SettingsCoverageDirector（指令
  注入/有界纠正/终局 NoAction）、SettingsCoverageRunner（交错记账缝 +
  coverage-steps/report 工件）、Host.Dsh --settings-coverage。
- 2026-10-02 · implemented→verifying · 确定性验证：solution build 0 errors；
  全量测试通过；29 场景经 AGT-005 重认证 + coverage 链验证 PASS。
- 2026-10-02 · verifying→verified · 真实覆盖遍历完成：专用 3081 +
  emulator-5556，Completed/Completion，19/19 步全验证，覆盖率 100%，
  分歧 NONE，单一 DSH session；结束清理测试设备与 3081，确认 3080 存活
  未受影响。首次尝试因 3081 服务被管道 SIGPIPE 中断仅完成 1 步（失败
  尝试如实保留），无管道重启后完整重跑成功。
- 2026-10-02 · verified→closed · 验收四元组齐备（CONTRACT/DETERMINISTIC/
  SCENARIO/ENVIRONMENT 四级）；文档同步（dsh/README、evidence README、
  component-report.md 含感知链/世界模型章节）；负路径（有界停止、滚动
  到底、真机失败恢复）的真机验证移交 AGT-006。

## Gate disposition

本 change 的验收全部通过：可配置（范围/步数/滚动/目标/终止条件全在
`.dsh/profiles/settings-coverage.yaml`）、有界（maxSteps/maxScrolls/
连续失败/咨询轮次均配置执法，确定性场景测试证明有界停止不伪装完成）、
可追溯（19 步真实 trace 逐字段齐备）。kernel 终态与覆盖判定分离：
Completion 证明 end-on-root 义务，覆盖完成以 CoverageReport 为准，进程
退出码取覆盖判定。本证据证明的是「配置声明的覆盖边界」的完成（7 个
一级入口 + 1 个滚动发现入口 + 8 个二级路由），不是 Settings 全树穷举；
扩大覆盖只需改配置。共享 profile 的 sessionTitle 已按文档化工作流更新为
「遍历设置菜单覆盖测试」（仅影响未来新 session 命名，3080 进程未动）。
