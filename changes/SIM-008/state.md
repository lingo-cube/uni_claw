# SIM-008 — SCN-SMOKE-001 与 SCN-WIFI-001 载体重复裁决

lifecycle_state: closed · disposition: none · depth: standard · base: 12c5763f2e3ba1bda9db4e3ba8b4c78d2ac12c76

## Intent（WHAT/WHY）

SIM-006 审计核实：SCN-SMOKE-001 与 SCN-WIFI-001 共用 wifi-off-to-on carrier，certification 三个 digest 逐字节相同，SMOKE 断言（3 条 Type-B）为 WIFI-001 断言的严格子集——库内冗余条目。SMOKE 名义上的独立价值（Host 整装冒烟）与其断言面不匹配：它不提供 WIFI-001 之外的任何行为证明。

目标：裁决两场景关系并实施——差异化（SMOKE 承担真正独立的断言面，如组装面冒烟语义）或合并/退役其一；消除"两个场景一个证明"的冗余。

## Scope / Out of scope

### 范围

- 裁决：a) 差异化 SMOKE 断言面（期望值随之变更 → C8 重认证搭乘本 change）；b) 退役 SMOKE-001（场景库条目处置、trait/coverage/基线矩阵同步）；c) 维持现状 + 显式声明子集关系（须给出为何值得保留两条目的理由）。
- 实施与同步：scenarios JSON、测试载体、testsets/simulation-baseline manifest、docs/analysis/sim-006-first-baseline-matrix.md 相应行。
- 认证变更按 C8 搭乘本 change；coverage/certification 全绿。

## Decision / Plan（Owner 已批准方案 B）

- 退役 `SCN-SMOKE-001`，保留 `SCN-WIFI-001` 作为 Host 整装闭环证明。
- 删除 SMOKE 场景 JSON 与唯一测试载体；从 simulation-baseline manifest 和当前基线矩阵移除条目。
- 不改 WIFI-001 断言、不重写剩余场景 expectations；验证场景映射、manifest、认证和 coverage 无悬空。

### 不在本 Change 内

- 不改 WIFI-001 的行为断言（它是超集方，为基准）。
- 不动其他 6 个首批场景。
- 不做场景库大规模重构。

## Acceptance（后续实现完成的判据）

| ID | 用户可观察行为 | 预期与证据 |
|---|---|---|
| A1 | 两场景关系有显式裁决记录 | 含被拒方案与理由；基线矩阵行同步 |
| A2 | 实施后无"同载体同断言子集"冗余 | 差异化则断言面独立且期望搭乘 C8 重认证；退役则库/manifest/矩阵无悬空引用 |
| A3 | 首批基线语义不降级 | 剩余场景仍覆盖原 Host 整装闭环证明；scenario_certify/coverage 全绿 |

## Verification

```yaml
level: SCENARIO
method: manifest/schema/certification checks; Simulation.Tests 全量；scenario-coverage 使用新鲜 TRX；verify-change SIM-008 quick; git diff --check
expected: SMOKE 场景、唯一载体和 manifest 引用删除；WIFI-001 保留 Host 闭环；无反向 trait/悬空引用；测试与 coverage 通过
actual: PASS：SMOKE JSON/测试/manifest/matrix 已移除；Simulation.Tests 188/188；coverage 28/28；verify-change quick 7/7 PASS；certification check 28 files 0 violations；git diff --check
evidence: evidence/sim-008/verification-2026-10-05.md
```

## Status log

- 2026-10-05 · UNDERSTAND → RESOLVE → PERSIST · 依据 SIM-006 终审（所有者）指令创建；事实基础为 SIM-006 审计 §1（digest 逐字节相同、断言严格子集）。
- 2026-10-05 · RESOLVE → PLAN → IMPLEMENT · Owner 批准方案 B：退役 SMOKE，保留 WIFI-001 超集闭环，等待 REVIEW/VERIFY。
- 2026-10-05 · IMPLEMENT → REVIEW → VERIFY → CLOSED · 删除 SMOKE 场景、载体、manifest 任务和当前矩阵行；WIFI-001 保留；188/188 Simulation、28/28 coverage、quick 验证通过。
