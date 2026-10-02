# WI-AGT009-002 evidence — AGT-009 Worker (branch agt009-worker, base c0dbc63e)

## E1 — Public Slow consultation seam (T1)

- method: `dotnet test tests/UniClaw.Kernel.Tests --filter 'FullyQualifiedName~SlowConsultation'`
- files: `src/UniClaw.Kernel/Perception/SlowConsultation.cs`, `tests/UniClaw.Kernel.Tests/Perception/SlowConsultationTests.cs`
- expected: NotConfigured（空 binding 表）零投影；成功结果经 SlowResultProjector→P2 进入
  EvidenceLedger（request/capture/cycle/provenance 携带）且 `kernel.EffectReceipts` 恒空；
  Timeout 零投影；晚到结果可投影但 `IsLate=true`；无效请求不触碰 realization。
- actual: 6/6 通过（Succeeded=admitted+1 canonical evidence；TimedOut/NotConfigured/Rejected
  均零投影、零 Effect；AcceptLate → Admitted+IsLate）。
- notes: default wiring = DeterministicSlowRealization + SlowReplayProfiles.CreateDefault()
  （按 WorkItem 冻结）；session correlation = kernel.RunId（run 未激活时 BuyerRef 兜底）。

## E2 — Popup classifier + typed claim (T2)

- method: `dotnet test tests/UniClaw.Host.Tests --filter 'FullyQualifiedName~SettingsPopupClassifierTests'`
- corpus: `evidence/real-settings-coverage-negative-20261001/run-e1-with-evidence/evidence/`
  capture-3e3809ea…xml（permissioncontroller 弹窗）与 capture-0d1006df…xml（正常 Settings 页）。
- expected: 真机弹窗 XML→"present"；正常页→"absent"；null/不可解析→null（无声明）；
  `ui.overlay.popup` proposal 与 route claim 同形（producer host.live.settings，scope 带
  observationCycleId，lineage 带 capture:）并被 Kernel P2 接纳；无 XML 恒无 proposal。
- actual: 9/9 通过（含 P2 admission Accepted、零 EffectReceipts）。

## E3 — Bounded Slow triggers (T3)

- method: `dotnet test tests/UniClaw.Host.Tests --filter 'FullyQualifiedName~SlowTrigger|SlowRequest|SlowTrace'`
  （位于 SettingsPopupClassifierTests 内）
- expected: 确定性谓词 NoXml / StructuralVisualConflict（fast 检出⊕hierarchy 零可点击，
  双向）/ PopupConsecutiveFailures（连续 N≥config）/ SemanticUnclear（回退身份）；
  视觉触发且 config 允许才携带截图 RawArtifact；trace 格式 `trigger|status|projected=N[|late]`。
- actual: 5/5 通过。feed 内咨询受 Enabled（默认 false）/ MaxRequestsPerRun / kernel 注入
  三重收口；超时/未配置 → Defer/Unknown 留痕继续，不阻塞周期。

## E4 — Director constrained branches (T4)

- method: `dotnet test tests/UniClaw.Host.Tests --filter 'FullyQualifiedName~SettingsCoverageDirectorPlanTests'`
- expected: popup=present → obstacle 指令（CANCEL 优先，词汇/顺序来自 config，目标从当前
  可见元素发现）；popup=absent → 恢复普通 enter；重试尽（default 2）仍 present →
  NoAction `bounded-stop:popup-not-cleared`；受约束 Plan（≤4 项、单匹配 ActItem +
  observe/control 尾）接受并把 ActItem 物化为 AgentActionStep 归属；多 ActItem /
  失配 Act / 5 项超尺寸 → deviation + fail closed（零采纳）；未知页 Defer 保留。
- actual: 9/9 通过。

## E5 — Config (T5)

- method: `dotnet test tests/UniClaw.Host.Tests --filter 'FullyQualifiedName~SettingsCoverageConfigTests'`
- expected: popup/slow 段缺失 → 现状缺省（HostPackage com.android.settings、三个白名单
  resource-id、CANCEL/button2/DISMISS/button1、MaxObstacleRetries 2、Slow Enabled=false
  /BoundedWaitMs 2000/MaxRequestsPerRun 4/VisualEnabled false/PopupConsecutiveCycles 2）；
  显式段生效；非法值 fail closed。
- actual: 28/28 通过。

## Regression

- `dotnet test tests/UniClaw.Host.Tests --filter 'FullyQualifiedName!~Whitelist'` → 128/128 通过。
- `dotnet test tests/UniClaw.Kernel.Tests --filter 'FullyQualifiedName!~Whitelist'` →
  715/717 通过；2 个失败与本 change 无关且在基线 HEAD 即红：
  `PolicyProtocolTests.Policy_IsFourthMemberOfAgentDecisionUnion`（Leader 的
  AgentDecision.Plan 第五成员所致）与 `DocsMetadataTests.Architecture_DeclaresFrozenAuthority`
  （docs/architecture 文件无 FROZEN 声明）。本分支 diff 不含 Runtime/** 与 docs/**。
- `dotnet test UniClaw.Kernel.slnx --filter 'FullyQualifiedName!~Whitelist'` → Simulation.Tests
  2 个失败（ScenarioCertificationTests，场景库认证读 scenarios/**，本 change 未触碰；
  Simulation.Tests 属禁止修改清单）。
