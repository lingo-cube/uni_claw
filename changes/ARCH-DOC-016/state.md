# ARCH-DOC-016 — 不变量 × 执法覆盖矩阵 + 完整性守护

lifecycle_state: closed · disposition: none · depth: standard · base: fa2e043

## Intent

把"哪条不变量被什么看住"从隐性知识变成受守护的显性地图（用户指令"把值得做的
做完"——2026-09-22 软工价值裁决的可执行子集，台账事件 #14）。买家：变更评审
（查表代替考古）、Phase 5/6 开冻时的不变量复核清单、AI coder 跨会话记忆。

## Scope

- `docs/analysis/invariant-enforcement-matrix.md`：47 行矩阵（判定词表
  TEST / STRUCT / TEST+STRUCT / PARTIAL / DEFERRED + 载体 + 复核命令）
- `tools/check-invariant-matrix.py`：完整性守护（基线 §20 + §24.10 提取
  不变量号集 ↔ 矩阵行集比对；缺失/重复/未知号/非法判定词 → exit 1）
- 台账 #14 追加（用户主动指令先例 #11/#13）

## Out of Scope

- 新增强化测试：**NO_REAL_BUYER**——高危组（串行屏障 43 / UnknownOutcome /
  终局关闭 42 / Freshness Unknown / 激活幂等 44）经 grep 证据判定零缺口
  （TwoStepBarrierTests 3 例、Outcome9/10/11/12、FreshnessEnforcementTests、
  KernelRunDriverTests 等均已执法）
- 形式化证明文档：NO_REAL_BUYER（论证结论留在会话与矩阵汇总，不成文）
- DEFERRED 行的实现（Memory #7 / Grant #46 / untrusted #47 / 恢复编排
  #30-31：随 Phase 5/6 落地，落地时先补测试再摘标记）

## Decisions

- D1 判定基于 2026-09-22 对 HEAD 14be825 的 grep 命中扫描（矩阵附复核命令）；
  基线增删不变量由守护脚本强制同步，测试改名靠维护规则约束。
- D2 #13（Belief≠Reality）判 STRUCT：真值不可测是设计立场，执法落点是
  #18 的可修正性表达，不虚构测试。

## Acceptance

1. 矩阵 47 行与基线 47 条一一对应（守护 PASS）
2. 每行判定在允许词表内，TEST/TEST+STRUCT 行载体可 grep 复核
3. 高危组缺口 = 0 已被证据固定；新增测试与证明文档的 NO_REAL_BUYER 已显式记录

## Verification

```yaml
level: DETERMINISTIC
method: python3 tools/check-invariant-matrix.py
expected: 47 rows = 47 invariants，PASS
actual: INVARIANT-MATRIX CHECK: PASS (47 rows = 47 invariants)，exit 0
evidence: 本文件 + 提交 diff（guard 脚本可随时复跑）
```

## Status log

- 2026-09-22 · created·implemented·verified·closed · 单会话完成（台账 #14；
  高危组零缺口 → 新测试按 NO_REAL_BUYER 诚实豁免，非跳过）。
