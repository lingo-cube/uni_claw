# RUN-005 — L2 Policy Protocol & Runtime

lifecycle_state: designing · review_state: awaiting-owner-readjudication · disposition: none · depth: decision-heavy · base: 4022ce40

## Status

**designing / v0.2 dual-grill revision 完成，待 owner 复裁**（不 CLOSED）。

设计稿：`plans/2026-09-24-run-005-l2-policy-runtime.md`（v0.2）

## 核心裁决（v0.2 后）

| 议题 | 裁决 |
|---|---|
| PolicyState owner | KernelRunDriver private ephemeral（含 GuardCursor + _pendingPolicyOutcome；逐元素记忆随 B2/A1 DEFER） |
| 执行路径 | 零新执行器；模板（AgentActionStep 同形，自带 TargetRole）物化单步 → 复用 StepAct→StepVerify |
| 求值格 | PolicyTruth 三态，逐 primitive 推导表；conflict/absence 永不满足终止；Unknown 一律回 decision boundary |
| 新相位 | 唯一 PolicyInvalidated（八 typed reason；含 control-non-act；带预算门）；FailClosed 终局分支删除 |
| 预算 | 展开轮零咨询；V6c 对 StepsRemaining；v1 无 MaxRounds |
| Scope | = 当前单根容器身份；内容变化不失效；semantic lease 深语义 DEFER 专项 |
| 词汇表 v1 | ClaimEquals / ClaimInSet / ElementExists + ObservationUnchanged + 自带目标模板 |
| DAG 性 | 良基不变量：重入必严格减 remainingApplications 或退出 |

## Dual-Grill 记录（2026-09-24）

- Owner 预审：PASS_WITH_FINDINGS（F1-F4 SUBSTANTIVE / F5-F8 MEDIUM + lease 点）。
- 独立盲审（fresh subagent 零泄漏）：REOPEN（BLOCKER×1 FailClosed 违
  §24.2 原文 + SUBSTANTIVE×4 + MEDIUM×3 + MINOR×2）；核心方向确认存活。
- 交叉对表：四根双命中（预算域/认识论/FailClosed/Scope-lease）；互补命中
  （owner：target 表达、ClaimInSet 过冲；盲审：逐元素记忆、E4 楔死、
  ScriptedUniAgent 重做、预算门）。
- v0.2 全部吸收（设计稿 §16）。

## Verification

```yaml
verification:
  level: CONTRACT
  method: 双审合并：owner 预审 + fresh-subagent 盲审（对基线与真实代码交叉核验）→ 逐 finding 处置进 v0.2
  expected: findings 全闭合；核心方向存活；无规划权增量
  actual: v0.2 REVISED — awaiting owner re-adjudication
  evidence: plans/2026-09-24-run-005-l2-policy-runtime.md（v0.2 全文 + §16 修订日志）+ spec.md 处置记录
```

## Status log

- 2026-09-24 · understand→designing · Step 1 v0.1 → ready-for-grill（1739bca2）。
- 2026-09-24 · designing · Owner 预审（PASS_WITH_FINDINGS，8 findings + lease 点）；
  盲审委托（fresh subagent，零泄漏）。
- 2026-09-24 · designing · 盲审返回（REOPEN，10 findings）；交叉对表（四根双
  命中）；**v0.2 合并修订完成**（删 MaxRounds/PolicyFallback/PolicyScope 字段/
  三谓词；PolicyTruth 表；模板自带目标；良基不变量；GuardCursor+pending
  outcome；E4 映射；预算门；B2/B6/A1/B4 DEFER）→ awaiting-owner-
  readjudication。下一步：owner 复裁 → 通过则冻结设计、进实现拆分（plan 步骤 3）。
