# Core family storage audit（只读盘点）

范围：Run canonical history、Control traversal、RuntimePlan，以及 Host 的
`RuntimeRunStore` 投影。本文只记录当前源码和已有测试可证明的事实；不把
接口形状当作需求，不据此发明通用 latest/changes API，也不宣称父
`RUNTIME-STORAGE-002` 或 A1–A10 完成。

## 结论摘要

| 记录族 | 已证实 | 尚未证实/缺口 |
|---|---|---|
| Run canonical | 完整顺序事实、RecordingId/Sequence、hash 链、独立 head；只读恢复校验 | 没有真实 buyer 核验过 latest-only、按版本前缀或 changes 读取成本 |
| Control traversal | 完整操作 Input/State/Result、来源绑定、selection/work 关联、末状态派生、hash/head | 没有真实 buyer 核验过 latest-only 或 changes 读取；当前 reader 全量扫描 |
| RuntimePlan | 完整 adoption/replacement、单调版本表、latest adoption/pending replacement、hash/head | 没有真实 buyer 核验过按 plan/version 或 changes 读取；当前 reader 全量扫描 |
| RuntimeRunStore | Host snapshot latest、Revision/EventSequence、事件 source/cursor 分页 | 事件 `PayloadAvailable=false`；未证明是 canonical Run 来源、完整重建来源或 hash/head 事实链 |

## Run canonical

真实 Owner/buyer 是 `RunModel` 的状态提交与只读恢复；RunModel 在持久化
确认后才发布内存状态。`IRunStateJournal` 只承诺完整已提交历史读取和追加：

- `src/UniClaw.Kernel/Run/RunModel.History.cs:5-18,21-33`：journal、顺序
  `RunHistoryRecord`、`RunHistoryPosition`。
- `src/UniClaw.Kernel/Run/RunModel.History.cs:73-110`：写入先于内存发布，
  `HistoryPosition` 只投影已提交 RecordingId/Sequence。
- `src/UniClaw.Kernel/Run/RunModel.History.cs:112-155`：
  `RestoreReadOnly` 校验来源、初始身份、连续 transition 和只读恢复。
- `src/UniClaw.Host/Runtime/FileRunStateJournal.cs:15-24,45-74,79-84`：
  v2 frame 的 `Previous`/SHA-256 `Hash`、独立 head、EOF/Sequence/来源校验，
  `ReadRecords()` 返回完整记录。
- `src/UniClaw.Kernel/Run/README.md:43-53`：历史位置不是执行许可；Traversal
  事实与核心 History 分层；完整 Control/Plan continuation 未提供。

因此完整不可变事实和可校验版本前缀已存在。但当前公开 reader 是完整
`ReadRecords()`，没有证据表明真实 buyer 需要或受益于按 latest/sequence/
changes 读取；成本也没有本片测量。

## Control traversal

真实 buyer 是原始 `ControlLoop` 操作提交及两正式 Host 的遍历历史恢复观察面，
不是继续执行授权：

- `src/UniClaw.Kernel/Control/ControlTraversalHistory.cs:7-24`：
  `IControlTraversalJournal`/异步 append，成功表示完整原操作已提交。
- `src/UniClaw.Kernel/Control/ControlTraversalHistory.cs:26-39`：记录含
  `Sequence`、`Operation`、`Input`、`State`、`Result`、Run/source 绑定；
  `ContinuationSupported=false`。
- `src/UniClaw.Kernel/Control/ControlTraversalHistory.cs:41-89`：
  `ReadOriginal`/`Read` 校验 recording source、初始化身份、连续序号、
  selection/work 关联和 invocation ordinal，返回末行 State。
- `src/UniClaw.Host/Runtime/FileControlTraversalJournal.cs:7-22`：生产文件
  adapter 使用 `FileCommittedHistory`，schema/head/hash 与独立 RecordingId。
- `src/UniClaw.Kernel/Runtime/RunRecoveryReader.cs:81-91`：真实恢复调用链为
  `ReadControlTraversalHistory(expectedRunId)` → `RestoreRunState` → 原身份
  `ControlTraversalRecordingId` → `ControlTraversalHistoryRead.ReadOriginal`。
- `src/UniClaw.Host/Runtime/FileRunRecoveryArchive.cs:85-87`：Host 对外转发入口。

已有测试引用：

- `tests/UniClaw.Host.Tests/ControlTraversalHistoryFileTests.cs:13-74`：缺失、
  撕裂、head 不一致、错误来源/Run、未知 state/schema、legacy 均拒绝为空游标。
- `tests/UniClaw.Host.Tests/ControlTraversalHistoryFileTests.cs:80-130`：实际
  Kill 后 reader 两次复读完整 source rows/state，且不提供 continuation。

已满足完整不可变事实、来源绑定、顺序/hash/head。尚未核验真实 buyer 的
latest-only/changes 需求和读取成本；目前是全量 journal 读取后验证/派生末状态。

## RuntimePlan

真实 buyer 是 Kernel Driver 的 plan adoption/replacement 原始提交及恢复查询，
不是执行 cursor 或续跑许可：

- `src/UniClaw.Kernel/Runtime/RuntimePlanHistory.cs:7-19,22-37`：
  journal、`RuntimePlanHistoryRecord`（Versions、Graph、DecisionId、replacement
  refs、Run/source）、`LatestCommittedAdoption`/`PendingReplacement`，并明确
  `ContinuationSupported=false`。
- `src/UniClaw.Kernel/Runtime/RuntimePlanHistory.cs:39-114`：
  `ReadOriginal`/`Read` 校验来源、初始化身份、Graph、replacement 配对、plan
  version 单调递增及每行 Versions 表，最后派生 latest/pending。
- `src/UniClaw.Host/Runtime/FileRuntimePlanJournal.cs:7-22`：生产 adapter 使用
  独立 schema/head/hash 的 `FileCommittedHistory`。
- `src/UniClaw.Kernel/Runtime/RunRecoveryReader.cs:93-101`：真实调用链为
  `ReadRuntimePlanHistory(expectedRunId)` → `RestoreRunState` → v5 identity 的
  `RuntimePlanRecordingId` → `RuntimePlanHistoryRead.ReadOriginal`。
- `src/UniClaw.Host/Runtime/FileRunRecoveryArchive.cs:88-89`：Host 转发入口。
- `src/UniClaw.Host/Runtime/FileCommittedHistory.cs:11-87,89-147`：全量
  frame/head/hash/Sequence 读取与提交实现。

已有测试引用：

- `tests/UniClaw.Host.Tests/RuntimePlanHistoryFileTests.cs:21-41`：known-empty、
  删除物理目录后用内存 source 跨实例只读复核，重复读取无写入。
- `tests/UniClaw.Host.Tests/RuntimePlanHistoryFileTests.cs:43-100`：missing、
  torn、head/hash、错误 plan/Run source、schema、unknown payload、legacy 拒绝。
- `tests/UniClaw.Host.Tests/RuntimePlanHistoryFileTests.cs:102-155`：实际 Kill
  保留完整 plan commit，两个 reader 复核 source rows 和文件 hash。

已满足不可变 adoption/replacement 事实、plan 版本前缀和 latest/pending 的
逻辑派生。尚未核验真实 buyer 是否需要独立 latest/changes API；当前每次
恢复仍展开完整链，暂无本片成本证据。

## RuntimeRunStore：Host 投影，不升级为 canonical

- `src/UniClaw.Host/Runtime/RuntimeRunStore.cs:13-17,31-85`：创建 snapshot
  projection，Revision/LastEventSequence 从 1 开始，并写 `run.accepted`；事件
  明确 `PayloadAvailable: false`（:70-83）。
- `src/UniClaw.Host/Runtime/RuntimeRunStore.cs:89-125`：`Get`、幂等/launch 查询、
  按 session/status 的稳定分页 List，属于 latest 投影查询。
- `src/UniClaw.Host/Runtime/RuntimeRunStore.cs:128-175`：Transition 递增
  Revision/EventSequence，terminal 后拒绝变更。
- `src/UniClaw.Host/Runtime/RuntimeRunStore.cs:215-280`：事件按 source/opaque
  cursor 分页；snapshot 与 ndjson 是 Host 文件布局，未形成 canonical Run
  hash/head 事实链。

已有测试：`tests/UniClaw.Host.Tests/RuntimeRunStoreTests.cs:9-117` 覆盖跨实例
  重启、latest projection、terminal 防改、source/cursor、非法 cursor、路径
  穿越和稳定 List cursor。它证明 Host 投影行为，不证明可替代 Run canonical
  history 或可独立重建完整事实。

## 给 Leader 的 Run 首片最小 buyer 候选

最小候选是现有恢复 reader 的“只读最新状态/指定提交位置”需求，而不是先造
通用查询：

`FileRunRecoveryArchive.Read()`/`RunRecoveryReader.RestoreRunState(expectedRunId)`
→ `RunModel.RestoreReadOnly` → `IRunStateJournal.ReadRecords()` →
`RunModel.State`/`HistoryPosition`。

首片只需用一个真实调用方确认：该调用方是否必须得到“末个已确认 Run 状态”或
“截至指定 Sequence 的历史”，以及完整链扫描是否已成为可测成本问题。若 buyer
只需要完整恢复/来源核验，则保留现状；若确有 latest/前缀成本问题，再以该
调用方的字段和一致性位置定义最小读取缝。Control/RuntimePlan 不因接口相似而
合并或泛化。当前没有 latest/changes buyer 核验、没有成本 probe 证据，不能把
“缺少接口”写成实现缺陷。

## 后续实测更正（2026-10-25）

上面的“没有latest buyer核验”是本报告入口时的结论，不能继续用于Plan运行热路径。后续发现`KernelRunDriver.BeginRuntimePlanReplacementAsync`及`CommitRuntimePlanAdoptionAsync`确实反复调用完整`RuntimePlanHistory`，仅为取两个已确认序号；三种实际Driver场景16/64/128已测，见`../control-plan-contract.md`及`control-plan/README.md`。当前改为确认后缓存序号，原完整公开历史/恢复保持。持久化State/Versions的重复及周期基线仍是独立剩余范围，不据此关闭父A4。
