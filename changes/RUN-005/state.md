# RUN-005 — L2 Policy Protocol & Runtime

lifecycle_state: designing · review_state: ready-for-grill · disposition: none · depth: decision-heavy · base: 4022ce40

## Status

**designing / ready-for-grill**（Step 1 Explore/Design 完成；不 CLOSED）。

设计稿：`plans/2026-09-24-run-005-l2-policy-runtime.md`

## 核心裁决预览

| 议题 | 裁决 |
|---|---|
| PolicyState owner | **A：KernelRunDriver private ephemeral**（baseline §24.2「Run Model 不存内容」+ AGT-001 GQ2 DEFER；不持久、restart 丢弃重咨询） |
| 执行路径 | **零新执行器**：PolicyExpand 逐轮物化单步 → 复用 StepAct→StepVerify 全链（含 43 屏障） |
| 新相位 | 唯一 `PolicyInvalidated`（六 typed reason；吸收 T5 词位；步级失败复用 StepRejected/VerificationFailed） |
| 词汇表 v1 | 4 谓词（ClaimEquals/ClaimNotEquals/ElementExists/ElementMissing）+ 1 守卫（ObservationUnchanged）+ 1 模板；A1/B4 语义 DEFER 登记 |
| 预算 | Contract > Policy local 两层；V6c 机械执法不得扩大合同 |
| 完成语义 | Termination ≠ Goal Completion；完成判定恒在 UniAgent（GEV） |

## Verification

```yaml
verification:
  level: CONTRACT
  method: 设计交叉核验：每裁决 → FROZEN 上游条文 / 18 场景编号 / 代码行号
  expected: 无 Kernel 规划权增量；展开链逐项 fresh；primitive 全有消费者
  actual: READY_FOR_GRILL（设计稿 §15；3 个 Open Questions 登记）
  evidence: plans/2026-09-24-run-005-l2-policy-runtime.md（全稿）
```

## Status log

- 2026-09-24 · understand→designing · Step 1 设计完成：最小 Policy records +
  场景反推词汇表 + PolicyExpand 单轮算法 + PolicyInvalidated 映射 + 两层预算
  + P1-P12 验收矩阵 + authority delta（无规划权增量）→ ready-for-grill。
  下一步 = plan 步骤 1（grill）。
