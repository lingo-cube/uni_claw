# AGT-011 — Settings traversal plan loose ends (§4 Wi-Fi evidence wiring + §5 candidate quality + §6 test gap)
lifecycle_state: closed · disposition: none · depth: standard · base: 7b49038d

## Intent（WHAT/WHY）

打包关闭 plans/2026-10-02-settings-traversal-remaining-issues.md 的 §4/§5/§6：
- §4：HostRunner（Wi-Fi 闭环 SettingsTraversal 模式）接 AGT-008 证据持久化。
- §5：滚动候选质量——基于 e1 语料离线分析的**结构性判据**（见 Decisions）。
- §6：director 多步 Act 提案拒绝分支补显式单测。

## 语料分析结论（§5 前置，e1 全部 XML）

- 对话框按钮：class=Button、resource-id=android:id/button1|button2（框架 ID）。
- 根页入口：LinearLayout、无 rid、子文本 ≥1（标题+副标题）。
- Search settings：ViewGroup + search_action rid——合法可导航项（历史上正是
  成功任务的 scroll-discovered 入口），**必须保留**。
- 子页行与根页入口结构相同——无需区分（候选只统计根页观察）。
- 词汇表过滤被证伪弃用（入口可含动词性文本；结构判据足够）。

## Decisions

1. §5 判据 = 候选 occurrence 的 NativeLocator rid 匹配框架按钮模式
   （`android:id/button\d+`）则排除——平台级事实，非 UI 风格，无需配置化。
2. ledger 观察元组增加可选 NativeRid 字段（带兼容重载，零破坏既有测试）。
3. §4 证据持久化在 SettingsTraversal 模式默认全开（AGT-008 缺省原则），
   不新增 HostOptions 开关。
4. AGT-009 popup 模式为主防线，本过滤为防御纵深（自定义无标记浮层场景）。

## Scope

- HostRunner 接线 + ledger 候选过滤 + bookkeeper 传 rid + director 测试。
- 不改 Kernel/协议/场景库。

## Acceptance

1. SettingsTraversal 模式的 run 目录含 evidence/{captureId}.png+xml。
2. 携带框架按钮 rid 的 occurrence 不成为滚动候选（Button 语义）；
   Search settings 类（非按钮 rid）仍可成为候选。
3. 多步 Act 提案 → 偏离 directive-requires-single-step 记录 + fail closed。
4. 全量测试无回归；认证 PASS。

## Status log

- 2026-10-03 · persisted · 开档：语料分析完成，判据定型。

## Verification

```yaml
level: DETERMINISTIC + ENVIRONMENT
method: corpus analysis (e1 全部 XML)；ledger/bookkeeper/HostRunner 改动 + 3 个新测试；全量回归；SettingsTraversalEvidenceLiveTests 真机活体验证（真实设备+真实 feed+确定性 double）
expected: "框架按钮 rid 不成为候选、Search 类仍可；多步提案被拒并 fail closed；SettingsTraversal 模式 run 目录含非空 evidence png+xml；全量无回归"
actual: "语料判据实证（Button+android:id/buttonN vs LinearLayout 无 rid）；全量 1230/1230（Host 140 含覆盖测试 65）；场景认证 29/29；git diff --check 干净；真机活体测试 PASS（evidence 落盘 1 png + 1 xml，状态 AgentDecisionFailed(hollow-completion) 为 double 语义、符合预期注释）"
evidence: changes/AGT-011/state.md（语料分析结论）；src/UniClaw.Host/HostRunner.cs；src/UniClaw.Host/SettingsCoverage/SettingsCoverageLedger.cs；tests/UniClaw.Host.Tests/SettingsCoverageLedgerTests.cs（FrameworkButtonRid_IsNeverAScrollCandidate）；tests/UniClaw.Host.Tests/SettingsCoverageScenarioTests.cs（MultiStepActProposal_IsRejectedAsDirectiveDeviation）；tests/UniClaw.Host.Tests/SettingsTraversalEvidenceLiveTests.cs
```

## Status log

- 2026-10-03 · persisted · 开档：语料分析完成，判据定型。
- 2026-10-03 · implemented · §4 HostRunner 接线；§5 框架按钮 rid 候选
  过滤（3 元组观察 + 兼容重载）；§6 多步拒绝测试。
- 2026-10-03 · verified→closed · 真机活体验证 §4（evidence 落盘）；
  全量 1230/1230、认证 29/29；设备已清理。

## Gate disposition

计划散项 §4/§5/§6 全部关闭：§5 采用语料实证的结构判据（框架按钮 rid），
词汇表方案被语料证伪弃用；§4 的持久化与 AGT-008 同代码路径，真机复证。
plans 文档中仅余 §7（场景库裁决）、§9（声明性）与已关闭项。
