# ARCH-DOC-017 — 场景推理文档（150 场景 × 等价类论证）+ 结构守护

lifecycle_state: closed · disposition: none · depth: standard · base: 67a14cb

## Intent

用户指令（2026-09-22，台账 #15）：把数学/哲学推理落到"可落地性"——产出一个与
VNext 文档族（`capability-coverage-derivation-v0.1.md`）同放的场景推理文档，
在一两百个实际场景内做推理，论证系统可靠性与设计逻辑正确性。

## Scope

- `docs/design/scenario-reasoning-v0.1.md`：150 场景（13 组 × 7 轴网格）+
  13 等价类推理（走法/数学/哲学/判定四要素）+ 覆盖论证 + 证伪条款
- `tools/check-scenario-reasoning.py`：结构守护（场景数 100–200、id 唯一、
  锚点词法、G1–G13/C1–C13 完整、类四要素齐全）

## Out of Scope

- 新增可执行场景测试：✅122 项全部锚定既有测试；空缺处为 ◐登记（F8/F9/
  恢复编排）或 D（矩阵 DEFERRED 面）——按买家原则不预造。
- 逐场景定制散文：等价类方法是参数扫描的数学诚实版本（决策 D1）。

## Decisions

- D1 等价类方法：150 场景由 7 轴网格系统枚举，深度推理在类级；理由：
  场景间转移结构与不变量压力同构，仅参数增量不同。
- D2 判定分布以逐行为准：✅122 · ◐14 · D13 · NB1 = 150；类级判定行已与
  逐行对账（C4/C9/C13 三处初稿计数误差已修正）。
- D3 覆盖声明与矩阵联动：47/47 不变量被场景压力轴命中；T1–T6 各被 ≥2 类
  实例化；三个登记缺口各有专属组。语义覆盖由构造保证，守护只查结构。

## Acceptance

1. 场景数 150 ∈ [100,200]，id 唯一（守护 PASS）
2. 每行有锚点判定词；每类四要素齐全（守护 PASS）
3. 与 capability-coverage-derivation 的 ✅/◐ 分布一致，无矛盾
4. 证伪条款四条显式（含公理-实现映射破裂这一最高严重级）

## Verification

```yaml
level: DETERMINISTIC
method: python3 tools/check-scenario-reasoning.py + 逐行判定分布人工对账
expected: 150 scenarios, 13 groups, 13 classes, 4 elements each；✅122·◐14·D13·NB1
actual: SCENARIO-REASONING CHECK: PASS (150 scenarios, 13 groups, 13 classes, 4 elements each)；
        分布逐行复算一致（修正 3 处类级计数后）
evidence: 本文件 + 提交 diff
```

## Status log

- 2026-09-22 · created·implemented·verified·closed · 单会话完成（台账 #15）。
