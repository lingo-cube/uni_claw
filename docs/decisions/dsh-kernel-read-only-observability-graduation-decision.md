# PROJECT_LEADER_DSH_KERNEL_READ_ONLY_OBSERVABILITY_GRADUATION_DECISION

- **Authority**: `PROJECT_LEADER_DSH_KERNEL_READ_ONLY_OBSERVABILITY_GRADUATION_REVIEW_V2`
- **Date**: 2026-08-15
- **Input**: 独立最终毕业审查（V2）+ 仓库事实核对 + 全新构建/回归/一致性/OpenSpec 验证
- **Mode**: Graduation review only. No implementation performed.

---

## 决策：**GRADUATED — READ_ONLY_KERNEL_OBSERVABILITY_INTEGRATED**

`dsh-kernel-read-only-observability` 已建立真实、只读的 Kernel 可观测性边界
（RuntimeEvent + RunSnapshot + EvidenceRef），供未来 DriverHost / DSH 消费。
它**不**意味着全部 Kernel 语义均可观测；剩余可观测性缺口显式保留（见 §6）。

前序审查（V1）的唯一阻断项 OBS-F9 语义分离证明不足，已通过 **TEST-ONLY**
修复并独立复核通过（OBS-F9A/B/C/D）。

---

## 1. Slice 名称 / 成熟度

- **Slice**: `dsh-kernel-read-only-observability`
- **Maturity**: `READ_ONLY_KERNEL_OBSERVABILITY_INTEGRATED`
- **OpenSpec**: `ARCHIVED`（`openspec/changes/archive/dsh-kernel-read-only-observability/`）

## 2. 零 Runtime 修改 / 零认知依赖

- **Runtime modification**: 零。`src/UniClaw.Runtime/` 未被本 change 修改
  （本分支工作树中 Runtime 的差异全部归属并发的 wifi/ADB/scroll 工作流，
  经 grep/mtime 独立核实不含 DriverHost/DSH/observability 内容）。
- **DriverHost 生产代码**: 零修改（本 change 全部 11 个生产文件 mtime 为
  10:07–10:14 的 APPLY 阶段；OBS-F9 修复窗口 11:10 之后仅改动测试与 OpenSpec 文档）。
- **认知依赖**: 零。DriverHost 无 LLM/VLM/IBrain/IDecisionProvider/DecisionEngine/
  OpenAI/Anthropic/DeepSeek/TokenBudget/Provider 命名空间（架构 Guard 10b + 组装级
  测试双重复核）；`src/UniClaw.Runtime` 亦无任何 DriverHost/DSH 引用（Guard 10b 通过）。

## 3. OBS-F9 语义域规则（冻结）

`RuntimeEvent.Sequence` 与 `ObservationSequence` 属于**两个独立的语义域**：

- `RuntimeEvent.Sequence` — 投影事件排序元数据（run 内单调）、EventId 输入；
  **非**世界真相、**非** Observation 身份、**非** GoalEvidence 新鲜度。
- `ObservationSequence` — Kernel 观察证据锚，仅来自真实的 observation-bearing
  Kernel 证据源。
- **数值相等允许**（巧合）：如 `Sequence=3` 与 `ObservationSequence=3` 可共存，
  仅表示两个独立语义值恰好同数。
- 从 `Sequence == ObservationSequence` 或 `Sequence != ObservationSequence`
  均**不得**推导任何语义含义。

### OBS-F9A — 数值碰撞（PASS）
确定性对抗 fixture：1 个 refresh span + 1 个 Kernel seq=3 的 observation，
使 `NavigationDecision` 恰好落在 `RuntimeEvent.Sequence=3` 且
`ObservationSequence=3`。断言：投影成功无诊断、相等共存、provenance 仍指向
`NavigationEvidence[0].SequenceNumber`、事件排序保持、无实现强制数值不等。

### OBS-F9B — Observation 锚 provenance（PASS）
全部可发射 kind 中携带 ObservationSequence 的仅 4 个：
ObservationProduced / NavigationDecision（源 `NavigationEvidence.SequenceNumber`）、
ViewportExplorationDecision（源 Kernel 轨迹 Reason `source-seq=N` 解析）、
TrapRaised（源 `Agent.LastTrap` expected/observed）。测试断言每个非空
ObservationSequence ∈ 由 Kernel 输入构建的锚集（{1,7} / {3,7}），且投影
Sequence 值域超出锚集（6/8/9 存在）却无任何事件携带非锚 ObservationSequence
——证否 `ObservationSequence = RuntimeEvent.Sequence` 类投影伪造。

### OBS-F9C — GoalEvidence 新鲜度分离（PASS）
`GoalEvidenceProduced.ObservationSequence == null`（完整源 sequence 不可用时）；
`RunCompleted.ObservationSequence == null`（其投影 Sequence=9 未变为 9）；
`RunSnapshot.LatestGoalEvidence.SourceObservationSequence == null`
（当前 partial 投影）。`RunCompleted.Sequence = N` 不蕴含
`GoalEvidence.SourceObservationSequence = N`（生产赋值点审计：终端事件
不设置 ObservationSequence；Store 仅打 Sequence/EventId，不触碰
ObservationSequence）。

### OBS-F9D — EventId 身份分离（PASS）
EventId = `evt-{runId}-{sequence}` 标识**投影事件**而非 Observation。
同一 Kernel 锚 7 的 ObservationProduced（EventId `evt-run-1-4`）与
NavigationDecision（`evt-run-1-5`）拥有不同 EventId；run 内 EventId 全局唯一。

### 错误不变量移除（PASS）
`Sequence_IsProjectionOrdering_NotObservationIdentity` 中原
`Assert.NotEqual(Sequence, ObservationSequence)` 不变量已移除，替换为语义断言
（单调排序、EventId 唯一、ObservationSequence ∈ Kernel 锚、终端事件锚为 null、
自然巧合共存）。全仓测试/文档/代码已无任何要求两者数值相异的语义不变量
（仅剩带「coincidence-dependent, not invariant」注释的 fixture 布局事实与
EventId 比较，属允许的偶然不等式）。

## 4. 回归证据

- **Fresh build**: `dotnet build src/UniClaw.Runtime.sln` → 0 errors（2 个 NU1900
  环境性警告，沙箱禁 NuGet http-cache 写入所致，属既有基线）。
- **Targeted observability**: 53/53 PASS。
- **Architecture guards**: 16/16 PASS（含 Guard 10a/10b/10c）。
- **Full regression**: 1004/1004 PASS（全新二进制、干净运行记录
  `/tmp/fulltest_1.log`；全程多次运行中偶发的 1 个失败为 ADB 计时 flake，
  见 §5）。
- **Consistency**: `scripts/check-consistency.sh` → C1–C10 ALL PASS。
- **OpenSpec validation**: `openspec validate dsh-kernel-read-only-observability
  --strict --no-interactive` → valid。

## 5. ADB 计时 flake 分类（诚实记录）

全量并行运行偶发失败：
`Perception.Pf01ConcreteAdbMechanismTests.PF01_ProcessRunner_TimeoutKillsShortLivedChildWithoutShellInterpolation`
（75ms 超时杀 `/bin/sleep 5` + `<2s` 墙钟断言，line 158）。

- 该文件**未被本 change 修改**（committed，mtime 2026-08-13 21:58，git 无状态）。
- 隔离复跑 5/5 通过（405–689ms）。
- 最终全新全量回归 1004/1004 PASS。

分类：**PRE_EXISTING_TIMING_FLAKE**（非 OBSERVABILITY_REGRESSION）。
本 gate 未削弱、未禁用该测试。

## 6. 剩余可观测性缺口（显式，未来买家）

- **B-class**: BindingUpdated、StateBeliefUpdated、PostActionObserved、VerificationCompleted
- **C-class**: DecisionProposed、DecisionAccepted、ActionAuthorized、RecoveryVerified
- **其他**: 完整 GoalEvidence source sequence、当前 Container/Observation 读模型、
  持久化 EvidenceRef 解析、传输选择

本 slice 不解决以上缺口。

## 7. P0 零模型结果

`ZeroModel_ReadOnlyObservability_WorksEndToEnd`：**PASS**。零模型（无 LLM/VLM）
下只读可观测性端到端成立，与「零认知依赖」一致。

## 8. 后续变更

- **NextChange**: `dsh-shadow-cognition`（本 gate 未创建、未实现）。
- Shadow 原则：DSH 消费 RuntimeEvent / RunSnapshot / EvidenceRef；DSH 可产出
  记录的认知提议/诊断；Kernel 对执行**不消费**任何 Shadow 产物。
- 无 Advisory、无 Blocking、无 C-class Runtime 发射购买（本 gate 不做）。

## 9. 结论

决策：**GRADUATED — READ_ONLY_KERNEL_OBSERVABILITY_INTEGRATED**。
真实只读 Kernel 可观测性边界成立，供未来 DriverHost / DSH 消费。
