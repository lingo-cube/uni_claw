# RUNTIME-STORAGE-002 private-index dependency ownership

## 做了什么

只读核对 `draft-build-closure-v1.json` 的 36 条 clean-view 编译错误、当前源码的声明/调用位置、三个已关闭 Change 的 `state.md` 与 ownership manifest，以及 slice1 对并发 Kernel hunk 的既有隔离记录。没有构建、测试、暂存、修改源码或索引。

## 原来的问题

本次 private index 从 `HEAD + manifest allowlist` 取 RUNTIME-STORAGE-002 的资产。它已带入调用方（例如 `EffectBoundary`、`WorldModel.Async`、`RuntimePlanHistory`、`UniKernel`、`KernelRunDriver`），却没有带入这些调用方所依赖的已闭合合同/partial counterpart；因此 clean view 的 36 条错误不能解释为“当前共享工作树不能编译”，也不能据此把整个 dirty `src/UniClaw.Kernel` 纳入。

## 这次定位

### 必须随 private index 闭合的已关闭前置

| 错误组（36 条） | 最小 provider/对应文件 | 已接受来源 | index 决策 |
|---|---|---|---|
| `EffectReceipt.AttemptCorrelation`，`EffectBoundary.cs:258/328/334/343`（4） | `src/UniClaw.Kernel/Effects/EffectDispatch.cs` 的 `EffectReceipt` init-only `AttemptCorrelation`；其结构化类型和 canonical record 形状来自 `Evidence/AttemptObservationLink.cs`、`Evidence/EvidenceRecord.cs`、`Evidence/ObservationProposal.cs`、`Evidence/ObservationOrigin.cs`、`Evidence/EvidenceLedger.cs` | `ORIGIN-CORRELATION-001`（closed）：实际 Receipt → 有寿命的 link → explicit echo → Evidence v2 identity/history；不能删除 correlation 字段来“过编译”，否则破坏已接受 Origin/历史身份协议 | 带入完整的 ORIGIN 最小合同组；`EffectBoundary.cs` 与 `KernelRunDriver.cs` 只按该 Change 已接受 hunk 选择，不能整文件接管 |
| `WorldModel.Async.cs` 的八个 `*Core` 名称（8）及 `HistoryRecording.cs` 的 `_owner`（7） | `src/UniClaw.Kernel/World/WorldModel.cs`：`OwnerState _owner` facade、八个既有同步 API 改名为 `*Core`；`WorldModel.HistoryRecording.cs` 和 `WorldModel.Async.cs` 是同一封装的 caller/recording side | RUNTIME-STORAGE-002 当前 World 子片（其 state 已记录 World baseline public RED/实现/验证完成）；`WORLD-HISTORY-001` 已关闭但仅是离线编码评估，**不是**本运行时 provider 的来源 | 带入该 World async/history 的闭合四件套：`WorldModel.cs` 的 facade+renames、`WorldModel.Async.cs`、`WorldModel.HistoryRecording.cs`、`WorldHistory.cs`（及已被它直接引用的 journal contract）。不要以删 Async/History caller 替代：这会撤掉本片 storage 行为 |
| `RuntimePlanGraph` / `PlanGraphFormatException`，`RuntimePlanHistory.cs`、`KernelRunDriver.cs:1010/1011`、`KernelRunDriver.PlanHistory.cs`（12） | `src/UniClaw.Kernel/Runtime/RuntimePlanGraph.cs`、`RuntimePlanHistory.cs`、`KernelRunDriver.PlanHistory.cs`；主 `KernelRunDriver.cs` 中只取 Plan-history 调用/partial 声明的已接受 hunk | `RUNTIME-RECOVERY-016`（closed）：原 Plan adoption 的冻结图/reader，明确不包含完整 cursor/handoff/restart | 带入 RECOVERY-016 manifest 所列 Runtime Plan 组；不要删除 `RuntimePlanHistory`/调用来假装本片独立，Storage 已把该原 recovery 信息作为实际 consumer |
| `TraversalControl` wrapper/partial counterpart（当前 draft 未单列为错误，但 candidate 已引用） | `Control/TraversalControl.cs` 的 `partial` 与 `*Core` rename，配对 `Control/TraversalControl.History.cs` | RUNTIME-STORAGE-002 Control traversal history 子片；它的 wrapper 是异步 journal 的唯一公开入口 | 两文件必须作为一个原子组；不能仅带 `TraversalControl.History.cs` 或只带 rename hunk |

上述三组是“已闭合前置/本片已接受子片的最小运行时闭包”，不是授权把所有 Kernel 变更都带入。特别是 `WORLD-HISTORY-001` 的 closed scope 是离线 codec 评估且“未改产品源”，不得把它误标为本次 runtime World provider。

### 不属于本任务、应从 private index 排除的 dirty siblings

| clean-view 错误 | 反向依赖 / hunk 位置 | 归属判断与安全处理 |
|---|---|---|
| `ChangePredicate.OverlayDismissed`、`BuildOverlayDismissalEvidence`、`PostActionEffectVerificationInput.Overlay`，`UniKernel.cs:644-645`（3） | `UniKernel.cs` HEAD-relative verification hunk（当前约 566-645）；provider 在 untracked `UniKernel.OverlayVerification.cs`，以及 Assurance 的 Overlay field/enum | Act/Overlay/ExpectedEffect sibling，不是 storage prerequisite。**排除 caller hunk**，不要仅把 provider 文件加入 index；否则会把另一片协议半带入 |
| `OverlayObservationCodec`，`KernelRunDriver.cs:3919/3927`（2） | `KernelRunDriver.cs` HEAD-relative post-action observation hunk（当前约 3902-3958），依赖 Overlay World codec/state | 同一 Overlay sibling。排除这两个 consumer hunk及其 Overlay state/capture companions，不要把 `OverlayObservation.cs`/`WorldModel.Overlay.cs` 当作“编译修复”带入 |
| 同一文件内的 `_stepExpectedEffect`、`ProjectExpectedEffect`、`authoredExpected`、`_actOriginContainerId`/`_actOriginOverlayState`、Act tail boundary | `KernelRunDriver.cs` HEAD-relative hunk：字段约 180/361、Act 初始化约 971、scope guard 1104、expected projection/verify 1317-1411；`UniKernel.cs` authored ExpectedEffect hunk 约 483-645 | 既有 slice1 README 明定这些是并发 `Act-scope/failed-step` business hunk，`parallel-kernel-driver-v1.patch` 仅记录当时分离的版本。当前新增 Overlay state 后不能反向直接 apply；按**当前 diff hunk**逐段 exclude/recreate selected index，不套用旧 patch |

## 验证结果

* `ORIGIN-CORRELATION-001/state.md` 声明 v2 canonical identity 包含实际 Origin 与显式关联字段，history 恢复校验原身份；其 `ownership.json` 明列 Effect/Evidence/Driver/Kernel seam。
* `RUNTIME-RECOVERY-016/state.md` 与 `ownership.json` 明列 `RuntimePlanGraph.cs`、`RuntimePlanHistory.cs`、`KernelRunDriver.PlanHistory.cs` 和主 driver，范围只到 original Plan adoption history。
* 当前 `WorldModel.cs` 可见 `_owner` facade（约 29）及实际 `*Core` provider；`WorldModel.Async.cs`、`WorldModel.HistoryRecording.cs` 只是一侧，单带必然产生 clean-view 错误。
* `RUNTIME-STORAGE-002/evidence/slice1/README.md:24` 和 `candidate-v6.json` 都明确 parallel Act scope/failed-step 改动必须从纯 slice candidate 排除。
* 本 WorkItem 禁止 build/test；本报告不声称私有 index 已通过构建。

## 新发现

1. Draft 的 36 条错误可按最小闭包归为：ORIGIN 4、World async/history 15、RECOVERY Plan 12、Overlay 5。前 31 条是应由 closed/accepted provider 或本片子片补齐的方向；最后 5 条是 caller 被误带而暴露的 Act/Overlay sibling。
2. `UniKernel.cs` 与 `KernelRunDriver.cs` 同时含 Storage、Origin、Plan、Act、Overlay hunk，因此只能按 hunk 精选；“整文件加入”会越过 WorkItem acceptance。

## 下一步（给 private-index 操作者）

1. 以 manifests/已接受证据为准加入 ORIGIN、RECOVERY-016 Plan、World async/history、TraversalControl wrapper 四个原子组；对混合文件仅选相应 hunk。
2. 从 current diff 明确排除上表 Act/ExpectedEffect/Overlay consumer hunk及其 provider，不用旧 `parallel-kernel-driver-v1.patch` 做反向应用。
3. 重新生成 clean view 后，再把剩余错误逐条映射到“缺 provider”或“误带 caller”；未得到新的 clean-view 结果前，不扩大为 all-Kernel。
