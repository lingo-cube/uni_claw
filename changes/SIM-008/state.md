# SIM-008 — SCN-SMOKE-001 与 SCN-WIFI-001 载体重复裁决

lifecycle_state: persisted · disposition: none · depth: standard · base: 12c5763f2e3ba1bda9db4e3ba8b4c78d2ac12c76

## Intent（WHAT/WHY）

SIM-006 审计核实：SCN-SMOKE-001 与 SCN-WIFI-001 共用 wifi-off-to-on carrier，certification 三个 digest 逐字节相同，SMOKE 断言（3 条 Type-B）为 WIFI-001 断言的严格子集——库内冗余条目。SMOKE 名义上的独立价值（Host 整装冒烟）与其断言面不匹配：它不提供 WIFI-001 之外的任何行为证明。

目标：裁决两场景关系并实施——差异化（SMOKE 承担真正独立的断言面，如组装面冒烟语义）或合并/退役其一；消除"两个场景一个证明"的冗余。

## Scope / Out of scope

### 范围

- 裁决：a) 差异化 SMOKE 断言面（期望值随之变更 → C8 重认证搭乘本 change）；b) 退役 SMOKE-001（场景库条目处置、trait/coverage/基线矩阵同步）；c) 维持现状 + 显式声明子集关系（须给出为何值得保留两条目的理由）。
- 实施与同步：scenarios JSON、测试载体、testsets/simulation-baseline manifest、docs/analysis/sim-006-first-baseline-matrix.md 相应行。
- 认证变更按 C8 搭乘本 change；coverage/certification 全绿。

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

## Verification（本轮 PERSIST）

```yaml
level: CONTRACT
method: python3 tools/gen-open-changes.py; 引用检查（SIM-006 审计 §1 证据路径有效）; git diff --check
expected: SIM-008 为 persisted；索引含本 change；未动任何场景/测试
actual: PASS（仅 PERSIST 文档检查）
evidence: 本 state、changes/INDEX.md
```

## Status log

- 2026-10-05 · UNDERSTAND → RESOLVE → PERSIST · 依据 SIM-006 终审（所有者）指令创建；事实基础为 SIM-006 审计 §1（digest 逐字节相同、断言严格子集）。
