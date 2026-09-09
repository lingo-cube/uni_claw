# 0016 — 同 producer 异 scope 的再观察是 Revise，不是 Conflict

UIW-001 把 UWM-009 §18 冻结的 Claim Evolution realization 显式 defer（仅
owner-derived claims 走最小 Revise），观察对观察一律 Conflict（latest 不获胜）。
PER-003 用真机帧实测暴露了该缺口的代价：scroll v1→v2 后 13 条行文本位移
**永久**入 Conflicts——同一观察流对呈现层移动的正常再观察被当成不可消解
矛盾，belief 冲突单调累积、Slice 供旧帧值、目标 subject 的
HasConflictOnTarget 永久 fail-closed。

## Decision

```text
同 subject 新 accepted evidence 到达：
同值                                  → Reaffirm（belief 不变，log 留痕）
异值 ∧ 同 Producer ∧ 异 Scope        → Revise（新值生效；旧 EvidenceId 入
                                        SupersededEvidenceIds；establishing
                                        provenance 更新；不产生 Conflict）
异值 ∧ 同 Producer ∧ 同 Scope        → Conflict（同帧矛盾 = 真冲突）
异值 ∧ 异 Producer                   → Conflict（跨源矛盾）
```

语义依据：同一 producer 的不同 provenance scope（如相邻采集帧）= **同一观察
流对 presentation 的再观察**——行文本随滚动位移是呈现变化，不是世界矛盾；
revision 语义（值替换 + 痕迹链 + decision log）满足「Reconciliation 不静默
覆盖」。跨 producer 或同帧内矛盾仍是独立来源/真矛盾，Conflict 语义保持。

## Considered Options

- **观察对观察永远 Conflict（UIW-001 现状）**：被拒为长期语义——真机证据
  （PER-003 E2）显示冲突单调堆积使 belief 不可用；§18 本就预留了 Revise。
- **latest-wins（时间新者直接覆盖）**：被拒——无 producer/scope 域判别的
  覆盖会把跨源矛盾静默压平（PER-002 冻结的最新不胜诉理由仍成立），且丢失
  痕迹链违反不静默覆盖。
- **整帧 Supersede / Withdraw 一并落地**：被拒——各自无 buyer（整帧取代、
  负证据撤销），§18 语义在、realization 继续 defer。
- **由 association/continuity strategy 顺带裁决**：被拒——claim evolution 是
  reconciliation 的独立语义轴，与 identity 判别正交；混入 strategy 会把
  belief 演进与识别算法耦合。

## Consequences

- WorldClaim 增 establishing producer/scope 摘要与 SupersededEvidenceIds 痕迹链；
  WorldModel 增 owner-internal ClaimEvolutionLog（R-UW replayability 家族）。
- 依赖旧 realization（同 producer 异帧异值 → Conflict）的测试按「保护架构
  事实而非旧测试本身」迁移（PER-002 S3 滚动行、PER-003 E2 断言族）。
- Evidence Ledger 的 supersession / 降权（协议 deferred ⑧）不受影响——本 ADR
  只裁决 World Model reconciliation 侧。
