# WMP-005 — Engineering Verification Consolidation

lifecycle_state: closed · disposition: none · depth: standard · base: working-tree

## Intent

统一已有 verification 入口，降低 live、architecture、simulation 和 certification
验证的重复操作与误判风险。

## Scope / Out of Scope

见 `spec.md`。本 change 不改 Product semantic、authority、lifecycle policy，未启动
PER-011。

## Decisions

- 复用现有机制，不新增 Device Platform、Evidence Platform 或 Workflow Engine。
- orchestrator 默认 check-only；验证工具不做治理决策。

## Acceptance

- `tools/verify-live` 复用 start/stop harness、selector、manifest 和三个 live tests。
- `tools/verify-change WMP-005 --self-test` 覆盖 T1-T14。
- 正常验证可输出统一 summary；行为回归与 stale certification 分离。

## Verification

level: ENVIRONMENT
method: `UNICLAW_ANDROID_WM_SIZE=1080x1920 tools/verify-change WMP-005 --live`
expected: unified verification and all three live gates PASS; cleanup PASS
actual: unified verification PASS（architecture Kernel 42/42、Host 26/26；Simulation 184/184；full solution 1040/1040；certification 29/29；coverage 29/29）；live API 35 PASS，HostLiveFull PASS，TypedLiveChain PASS，CoordinateGate PASS，cleanup PASS；wm override 回读 1080x1920；FIRST_FAILURE=NONE；FINAL_STATUS=PASS。
evidence: `evidence/2026-09-27-wmp-005-verification.md`

## Status log

- 2026-09-27 · UNDERSTAND→IMPLEMENT · Gate 0 confirmed PER-015/PER-016 closed；PER-011 未启动。
- 2026-09-27 · VERIFY · deterministic/architecture/certification evidence PASS；初次 live 环境因 AVD `Abort trap: 6` blocked，后续 harness viewport 归一化后恢复。
- 2026-09-27 · VERIFY→CLOSED · `tools/verify-change WMP-005 --live` 全量 PASS；API35 临时 AVD 回读 Physical 1080x2400 / Override 1080x1920；三条 live gate 与 cleanup PASS，解除 blocked 并关闭 change。
