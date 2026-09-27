# WMP-005 — Engineering Verification Consolidation

## Intent

把已验证的 live harness、architecture freeze tests、scenario certification 和
全量测试收敛为可重复的验证入口。只做 tooling/test consolidation，不改变产品
semantic、authority 或 lifecycle。

## Scope

- `tools/verify-live`：复用现有 Android harness、selector、capability manifest
  和三个 live gate。
- `tools/verify-change`：只读编排 diff、architecture、Simulation、full solution、
  certification check、coverage，并区分环境失败、行为回归和 certification stale。
- 扩展现有 freeze/semantic tests 与 self-test。

## Out of Scope

Product semantic/authority/lifecycle、PER-011、自动 re-certification、自动修改
state/evidence/INDEX、自动关闭 change、ADR 或新平台。

## Acceptance

1. live verification 复用既有机制，明确设备选择、能力、readiness、三个 gate、
   first failure 和 cleanup。
2. architecture tests 对 legacy=0、semantic axis 和 live device contract 执法；
   违规 self-test 必须 RED，正常树 PASS。
3. `tools/verify-change <id>` 默认 check-only，不写治理文件，不自动 re-certify。
4. Simulation 行为失败分类为 `BEHAVIOR_REGRESSION`；仅 runtime source hash 过期
   分类为 `CERTIFICATION_STALE_ONLY`。
5. T1-T13 有可执行 self-test 或现有测试证据。
6. T14 明确区分 live 环境失败；恢复条件为 API35 AVD 可启动并完成三个 live gate。

## Verification

level: CONTRACT/DETERMINISTIC/SCENARIO/ENVIRONMENT
method: tools/verify-change WMP-005 --self-test；full solution；scenario certification --check；coverage truth chain；verify-live
expected: all requested checks pass, environment failures remain distinct
actual: T1-T14 self-test PASS；architecture filters PASS（Kernel 42/42、Host 26/26）；full solution 1040/1040；Simulation 184/184；certification 29/29；coverage 29/29；live blocker 为 API35 AVD emulator `Abort trap: 6`，最终分类 `ENVIRONMENT_UNAVAILABLE`，三个 live gate 未执行。
evidence: evidence/2026-09-27-wmp-005-verification.md
