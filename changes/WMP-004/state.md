# WMP-004 — Canonical Publication Order ADR（学习钩子：WMP-002 决策升格）
lifecycle_state: closed · disposition: implemented · depth: standard · base: 542dfd2

## Intent（WHAT/WHY）
UniFlow A8 学习钩子触发：WMP-002 挖出的三项 frozen 顺序语义与 transient
物化决策目前只在 evidence（点时性记录）与测试注释里，而它们约束所有未来
触碰 WorldState/EvidenceBasis 出版的实现选择——不升格为一等 ADR，下一个
实现者几乎必然重踩（WMP-002 一个 change 内踩了三次）。本 change 仅新增
ADR-0018，忠实转录已双轴评审过的决策，不引入任何新决定。

## Scope
- `docs/adr/0018-canonical-publication-order-is-incremental-frozen-chain-contract.md`
  （新）：三项实证事实、增量链契约定义与实现边界推论、transient 物化决策、
  WMP-003 容器不变式、被拒选项（spine/eager/换序/纯枚举）、后果。
- 本 state。

## Out of Scope（禁止）
- 不改任何代码/测试/skill/脚本；不修改历史 evidence；不新增决策（只转录）。

## Decisions
- D1 编号 0018（紧接现有最大号 0017）；标题聚焦「顺序契约」而非「性能」，
  因为约束的本质是可观察顺序兼容面。
- D2 ADR 只转录 WMP-002/WMP-003 已评审决策；若转录与原 evidence 出现语义
  分歧，以原 evidence 为准并回改 ADR。

## Acceptance
- A1 ADR 格式与既有 0016/0017 一致（Context→Decision→Considered Options→
  Consequences）。
- A2 内容与 WMP-002 evidence §3/§5、WMP-003 state 语义一致（独立复核）。
- A3 全量测试不受影响（纯文档）；只提交 2 文件。

## Constraints
- 文件面：ADR + state；零代码改动。

## Verification
```yaml
verification:
  level: CONTRACT
  method: 独立复核 subagent（ADR vs evidence/WMP-003 state/代码事实）+ 格式对照 0016/0017
  expected: 语义零分歧、零事实错误、零新增决策、格式一致
  actual: >-
    语义一致；代码事实（transient 不缓存祖先）核实为真；复核发现 4 处 D2
    越界（checkpoint 推测、scratch 措辞、两处被拒选项新增框架）——已修剪
    为可溯源表述后定稿；格式一致（Context→Decision→Options→Consequences）
  evidence: 本 state（纯文档 change，无独立 evidence 文件）
```

## Status log
2026-09-10 · enter→understanding→resolved→persisted→planned · base=542dfd2；
  触发 A8（决策级新知识，两 change 内三次实证踩坑）；零代码面。
2026-09-10 · planned→reviewing→verifying→closed · 独立复核 4 处 D2 越界修剪
  （checkpoint 推测删除、措辞对齐 evidence、被拒选项改述可溯源）；纯文档
  change，全量测试不受影响（HEAD 树零代码改动）；提交 ADR + state 两文件。
