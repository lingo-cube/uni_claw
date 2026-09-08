# 0012 — Runtime Transition Context（P22）是 non-evidentiary association prior

UWM-009 v0.1 adversarial grill 的 G1（BLOCKER）：Container Association 的
transition-context 输入没有合法 ingress——ControlIntent（P8）没有通向 World
Model 的边；AttemptReport 经 P2 admit 后被 belief relevance gate 定义性判非
world-relevant（ING-006）；只有 verified-effect Observation
（PostActionEffectFlow）有合法通道。而 UWM-009 S2 Scroll Continuity 是真实
scenario buyer，依赖该输入。人工裁决（D1，2026-09-09）拒绝 grill 提出的
「Intent/Attempt 完全排除」方案，改为引入窄协议 P22。

## Decision

```text
ControlIntent / 期望的 transition
→ MUST NOT 进入 WorldModel reconciliation

actual runtime attempt/effect context
→ MAY 经显式 non-evidentiary TransitionContext（P22）
  进入 Container Association

TransitionContext
≠ EvidenceRecord ≠ World claim ≠ ContainerIdentity truth
→ 只影响 candidate prior / ranking
→ MUST NOT 单独 establish Matched/New
→ MUST NOT create / replace ContainerIdentity
→ MUST NOT mutate canonical WorldState
```

canonical belief mutation 仍只由 accepted world evidence 触发（不变量 15
的 UIWorld 侧精化，不放宽）；verified post-action effect 可另行由 accepted
PostActionEffectFlow Observation（P2/P3）支撑，与 P22 正交。P22 不携带
expected next page/container、desired outcome、Control plan。

## Considered Options

- **完全排除 Intent/Attempt（grill G1 建议）**：被拒（人工 D1）——S2 是
  真实 buyer；evidence-only 无法表达「runtime scroll attempt 刚发生」的
  prior，每次都要从零判别，而 attempt 事实又不具备 world evidence 资格。
- **TransitionContext 作为 EvidenceRecord 走 P2/P3**：被拒——会把 attempt
  事实升级为 world evidence，破坏 belief relevance gate（AttemptReport
  定义性非 world-relevant）与 Admission ≠ Truth 链条；association 是
  belief owner 内部的判别过程，其 prior 输入不需要 admission 身份。
- **直接开 Control → World Model 数据边携带 intent**：被拒——intent /
  期望属 Control 权域，进入 belief 输入违反不变量 16 的精神与 Authority
  分离（expectation 不得修改 observation interpretation）。

## Consequences

- 协议基线新增 P22（Runtime Effect Flow → World Model；producer 具体绑
  定属 realization），Protocol Map 与逐边协议同步。
- UWM-009 v0.2 §2/§6/§9/§12/§12.1/§13/§21/§28/§30/§31 按本 ADR 与人工
  裁决 D2–D6 更新；CONTEXT.md 新增 Container & Association 词条族。
- ObservationNeed 外部协议边、外部 AssociationCandidate 直连 ingress、
  AssociationDisposition 外部发布、SliceContext payload 维持
  DEFER — NO CURRENT BUYER（D4）。
- 无当前实现；P22 实现随 UWM-009 vertical slice 另立 change 走 UniFlow。
