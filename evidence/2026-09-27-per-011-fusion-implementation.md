# PER-011 implementation evidence

日期：2026-09-27
实现状态：IMPLEMENTED；不修改 `changes/PER-011/state.md` 的 design-only CLOSED/FROZEN 状态。

## 做了什么

将 hierarchy、screenshot/visual、OCR 的 typed evidence 接入一个只读 Fusion capability，并让派生结果沿既有 `ObservationProposal → EvidenceLedger → UniKernel → WorldModel` 路径进入 P2。Fusion 没有新增 Evidence writer、WorldModel、identity 或 effect owner。

## 关键调用链

```text
FusionSourceEvidence
  → FusionEngine.Align / ResolveAuthority
  → DerivedObservationProposal
  → ObservationProposal（带 immediate parents、leaf basis、rule/version）
  → EvidenceLedger.Admit（lineage fail-closed）
  → UniKernel.Process
  → WorldModel
```

## 代码变更

- `src/UniClaw.Kernel/Perception/Fusion/FusionContracts.cs`：最小输入、alignment、association、authority、coverage、conflict、derived proposal contract。
- `src/UniClaw.Kernel/Perception/Fusion/FusionEngine.cs`：同 correlation/cycle、bounded timestamp、mutation、CoordinateSpace 对齐；字段 authority 与 semantic/rendered 分轴；Unsupported/Unknown/Conflicted/Unaligned fail-closed。
- `src/UniClaw.Kernel/Perception/Fusion/OccurrenceAssociator.cs`：仅按 resource-id、role、bounds、capture feature 生成 Unique/ManyToOne/OneToMany/Ambiguous/Unassociated candidate，不铸造 identity。
- `src/UniClaw.Kernel/Perception/Fusion/BoundedEscalationPolicy.cs`：focused rescan 一次、deep perception 一次、预算耗尽 Stop。
- `src/UniClaw.Kernel/Evidence/{ObservationProposal,EvidenceRecord,EvidenceLedger}.cs`：派生 lineage metadata、递归 leaf 去重、self/cycle/missing parent/basis mismatch 进入 `MalformedLineage`，拒绝发生在 P2 canonical admission 前。
- `tests/UniClaw.Kernel.Tests/Perception/Fusion/`：融合、association、lineage、P2/WorldModel integration 与 bounded escalation 场景。

## 验证结果

| 项目 | 结果 | 证据 |
|---|---:|---|
| focused fusion | PASS 17/17 | `dotnet test ... --filter FullyQualifiedName~Perception.Fusion` |
| focused P2/legacy regression | PASS 40/40 | `FusionEngineTests`, `FusionP2IntegrationTests`, `ObservationIngressTests`, `EvidenceToBeliefTests`, `RunTraceBulletTests` |
| Kernel | PASS 661/661 | `dotnet test tests/UniClaw.Kernel.Tests/UniClaw.Kernel.Tests.csproj` |
| Simulation | PASS 184/184 | `dotnet test tests/UniClaw.Simulation.Tests/UniClaw.Simulation.Tests.csproj` |
| full solution | PASS 1057/1057 | `dotnet test UniClaw.Kernel.slnx` |
| certification | PASS 29/29，0 violations | `python3 tools/scenario_certify.py --check` |
| scenario coverage | PASS 29/29，100% | `python3 -X utf8 tools/scenario-coverage.py` |
| live selector self-test | PASS 3/3 | `./tools/verify-live --self-test` |
| real-device live (用户提供运行) | PASS | 用户提供的完整 `./tools/verify-live` 自助 provision 运行未预设 `UNICLAW_ANDROID_DEVICE`：HostLiveFull、TypedLiveChain、LiveCoordinateGate 均 PASS，CLEANUP=PASS，FIRST_DIVERGENCE=NONE |
| real-device live (Codex 本轮重跑) | ENVIRONMENT_UNAVAILABLE | Codex 本轮独立重跑时 emulator 触发 `Abort trap: 6`，随后 `adb wait-for-device timeout (emulator-5556)`；三个 live gate 未执行 |
| expectationsDigest | unchanged | 认证前后 29 条 expectations digest 对拍一致 |
| diff hygiene | PASS | `git diff --check` |

## 环境问题复核

本证据包含两次不同运行：用户提供的完整 `./tools/verify-live` 自助 provision 运行通过了三个 live gate；Codex 本轮独立重跑时 emulator 启动触发 `Abort trap: 6`，随后 `adb wait-for-device timeout (emulator-5556)`，因此本轮三个 gate 未执行并返回 `ENVIRONMENT_UNAVAILABLE`。两次结果分别记录，不相互覆盖；后续遇到同类环境问题应以可复现的设备运行及其 `FIRST_DIVERGENCE`、三个 live gate 和 `CLEANUP` 输出作为结论依据。

## Final gate

1. temporal alignment：PASS
2. occurrence association：PASS
3. field authority：PASS
4. semantic/rendered separation：PASS
5. conflict semantics：PASS
6. coverage/absence semantics：PASS（partial/unknown 不推导 absence）
7. derived lineage：PASS
8. no-self-corroboration：PASS
9. malformed lineage fail-closed：PASS
10. P2/WorldModel authority preserved：PASS
11. escalation bounded：PASS

推荐 verdict：**CLOSE**。代码、测试、认证、覆盖率和真实设备 live gate 均通过；不修改 `changes/PER-011/state.md` 的 design-only CLOSED/FROZEN 状态。
