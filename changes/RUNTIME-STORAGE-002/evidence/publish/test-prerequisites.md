# RUNTIME-STORAGE-002 private test-view prerequisites

## 做了什么

只读比对 private Host.Tests 缺失的 `RunResourceRecoveryTests` 及当前三个测试文件与其 closed recovery evidence copy；同时检查 `RuntimeDiagnosisPacket` 的 producer/consumer 和 closed ownership。没有构建、测试、源码或 index 修改。

## 原来的问题

生产 `Host` 与 `Host.Dsh` 已在 private view 构建，但 Host.Tests 的既有 history/recovery tests 共享 `RunResourceRecoveryTests.Run(...)` 作为真实 SettingsCoverage Runner 的 fixture。把它漏出会造成大量测试编译/fixture 闭包失败，而不是生产 recovery 行为故障。

## 精确测试闭包与 hunk 裁剪

### 先决裁决：Overlay 是 closed predecessor，但本轮不应重新引入它

先前“closed recovery baseline 已含 Overlay，所以可随测试带入”的表述不足以决定 private-index 范围。复核结果如下：

* `SIM-019` 是 **closed** 的明确 owner。其 state scope 直接包含 typed overlay observation、World 派生、`ObservationNeed` 与 Runtime 补采；`evidence/review-input.json` 和 verified source pin 明列 `World/OverlayObservation.cs`、`World/PageObservation.cs`、`World/WorldModel.Overlay.cs`。
* 最小协议组不是单个 codec：`OverlayObservation.cs`（codec/subject）、`PageObservation.cs`（`RequiresOverlayEvidence`）、`WorldModel.Overlay.cs`（World 派生/Need），以及 `WorldModel.cs` 中对 Overlay reconcile/grounding 的路由。把其中任一部分孤立加入都会形成半协议。
* 但本轮 Storage private view **不得因测试引用而重新 stage 该组**：此前已经把 Overlay sibling 从 candidate 排除，且当前 production Kernel 已构建。正确做法是把测试放在已经由 pure base 提供的、精确 pinned 的闭合源上；不是从当前脏树把 SIM-019 再带入。
* `changes/RUNTIME-STORAGE-002/evidence/slice1/pure-build-inputs.tar.gz`（SHA `efa73ecf3c9c915f4527ece9be2c37a6527d0763eb0bfb27686c72cd6821f1b0`）已包含三份 provider 和三份相关 Host.Tests：`OverlayObservation.cs`、`PageObservation.cs`、`WorldModel.Overlay.cs`、`RunResourceAccountingTests.cs`、`RunResourceRecoveryTests.cs`、`WorldHistoryRecoveryTests.cs`。这是本轮可用的真正原 Storage build baseline。

因此，若 private index 当前没有这份 pure base 的等价已验证 blob，结论不是“删除 Overlay assertions 以通过编译”，而是回到该 pure-base archive/reconstruct 流程，或停止让这三份 Overlay-bearing recovery tests 进入本次 test subset。不得用当前 dirty Overlay 反填。

### 1. `tests/UniClaw.Host.Tests/RunResourceRecoveryTests.cs`：整文件保留

* 来源/所有权：`RUNTIME-RECOVERY-003`（closed，ownership 明列该文件）。
* 是 003 的原 Owner identity archive / 原 Resource read fixture；`Run(...)` 被 RunState、Run/Evidence/World recovery、Control traversal、Plan history 和 storage-abstraction tests 多处调用。
* 无当前 HEAD diff：文件在当前共享树是 untracked；与 `changes/RUNTIME-RECOVERY-003/evidence/final-source/...` 完全一致。
* 因此最小安全选择是**整文件**。不能为减少 test scope 移除其中的 source mutation/negative assertions；这些正是原归档完整性和“不可认证 continuation”的测试语义。

### 2. `tests/UniClaw.Host.Tests/RunResourceAccountingTests.cs`：使用 closed fixture 基线 + 四个 storage-compatible 选择项

* 基线来源：`RUNTIME-RECOVERY-002/evidence/final-source/.../RunResourceAccountingTests.cs`（其 Source 是 003 helper 的 producer）。
* 当前相对此 accepted baseline 只有四处实质差异：约 120、132、144、164 行把 `new RunDriverInput.Observation(...)` 换为 `directive.CompleteObservation(...)`。这四处仅让同一 observation fixture 通过当前 directive completion seam；保留，且不改断言或业务预期。
* 当前约 131/134 的 `RequiresOverlayEvidence = true` 与 `OverlayObservationCodec.Subject` 已在 RECOVERY-002 closed final source 内，**不是当前 parallel Overlay hunk**。它们是既有 fixture 输入形状的一部分。裁掉会把测试改成另一条 observation path，违反“不删既有断言/不改预期”。
* 选择策略：仅在已使用上述 pure base 的前提下，以 closed 002 文件作为 base，叠加四个 `CompleteObservation` 替换；不要额外从当前 dirty tree 带入任何 Overlay test/provider 文件。

### 3. `tests/UniClaw.Host.Tests/WorldHistoryRecoveryTests.cs`：closed World recovery 基线 + 三个 storage-version hunk；不删除既有 Overlay subject

* 基线来源/所有权：`RUNTIME-RECOVERY-009`（closed，ownership 明列该文件；其 candidate v3 是可比较 accepted source）。
* 当前相对 009 candidate-v3 的 Storage/原历史增量只有三组：
  1. 约 245 行，unknown-version fault 的 schema token `v1 → v3`；
  2. 约 451 行，`WorldHistoryVersionReader.ReadLatest()` 的 immutable revision 对照；
  3. 约 764–773 行，`read-world-version` 子进程的 version-reader 只读、`ContinuationSupported=false`、不做 full history validation 的断言。
* 以上三组属于 RUNTIME-STORAGE-002 的 versioned World history/read contract，可保留；均不改变原 recovery 断言的业务含义。
* 当前约 507–508 行的 `OverlayObservationCodec.Subject` 是 009 accepted baseline 内已有的 fourth auxiliary subject，非当前差异。只有 pure-base 已闭合时才保持它；不额外选择当前 Overlay production/test hunk。

## `RuntimeDiagnosisPacket` 不是可省略的测试辅助

`src/UniClaw.Host.Dsh/RuntimeDiagnosisPacket.cs` 是 `RuntimeToolHost` 的直接 codec provider：`RuntimeToolHost.cs` 约 246 调 `Encode`，约 277 调 `Hash`；`RuntimeToolHost.DevelopmentAssets.cs` 还将它列为诊断输入 source。其 owner 是 `EVO-002`（closed），不是 RUNTIME-STORAGE-002 的 storage 实现；但既然 private Dsh Host 含 `RuntimeToolHost`，该 source 文件是必需的 production compile closure，应整文件保留。它只生成/校验非权威 diagnosis evidence，不授予 runtime/Effect/continuation authority。

## 建议的 private-index 路径与层次

1. 先确认 private index 的 source blobs 等价于 slice1 `pure-build-inputs.tar.gz`；它已闭合 closed SIM-019 Overlay predecessor，不需也不得从 current dirty tree 重新取 Overlay。
2. 在此前提下加入 `RunResourceRecoveryTests.cs`（RECOVERY-003 完整 fixture）。
3. 加入 `RunResourceAccountingTests.cs`：RECOVERY-002 base + 四处 `CompleteObservation` 兼容 hunk；保留 closed baseline Overlay inputs。
4. 加入 `WorldHistoryRecoveryTests.cs`：RECOVERY-009 base + 上述三组 RUNTIME-STORAGE-002 versioned-history hunk；保留 closed baseline auxiliary subject list。
4. 若尚未在 Dsh private view，加入 `src/UniClaw.Host.Dsh/RuntimeDiagnosisPacket.cs`（EVO-002 完整 provider），不把它误归为 test-only 文件。

## 验证结果

* `RunResourceRecoveryTests.Run` 有 17 个当前 Host.Tests caller，覆盖 Plan、World、Evidence、RunState、Control、Run/World common-point 及 storage abstraction 的已有历史用例。
* `RunResourceRecoveryTests` 与 RECOVERY-003 final source 无 diff。
* Accounting 的四个变更是 completion constructor 选择；World 的三个变更都围绕 v3/version reader。两处提问中的 Overlay reference 在对应 closed evidence baseline 已存在。
* 未运行 build/test；本报告不声称私有 Host.Tests 已通过。

## 不扩大范围

不加入新的 Overlay feature 测试、ExpectedEffect/Act-scope 测试、完整 resume 测试或无关 Host.Tests 文件；也不删除已有历史断言来换取更小的编译面。
