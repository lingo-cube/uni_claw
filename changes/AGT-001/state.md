# AGT-001 — UniAgent Runtime Architecture（DSH Product Realization）

lifecycle_state: closed · review_state: passed · disposition: none · depth: decision-heavy · base: 83fa8c0e
implementation_commit: N/A（纯架构 change；设计冻结，无产品代码）

## Status

**CLOSED**（2026-09-24，owner 接受 focused re-grill 裁决并授权 closure）。

设计稿：`plans/2026-09-24-agt-001-uniagent-runtime-architecture.md`
（v0.2 · **FROZEN**；lineage：Explore → Draft v0.1 → Full Grill →
Disposition → Revision v0.2 → Focused Re-Grill PASS → FROZEN）

## Grill 记录

**Full Adversarial Grill（2026-09-24）：PASS_WITH_FINDINGS**
（无 authority inversion / 第二 Kernel / 第二 Run·World Model / reality
bypass；双 loop 主权成立；F1-F3 SUBSTANTIVE / F4-F6 MEDIUM / F7 MINOR；
owner 裁决 GQ1=A / GQ2=B-DEFER / GQ3=YES / GQ4=YES）

**Focused Re-Grill（2026-09-24，仅一次，范围限六 finding + F7）：PASS**

```text
F1 CLOSED        — interruption plane：AbortCurrentTurn = realization-private
                   机械中断（五不边界）；deadline MUST be bounded + turn MUST
                   be externally abortable（60s=realization 默认）；late
                   response MUST NOT re-enter Kernel（§3.2/§3.4）
F2 CLOSED        — Policy = typed closed AST（禁 arbitrary expression/script/
                   evaluator/workflow language）；Match/ActionTemplate/Guard
                   全 typed/discriminated；词汇表归 RUN-005（§5.2-5.4）
F3 CLOSED        — 三层机械 headless：launch profile 禁载清单 / static
                   allowlist（submit_decision 单工具）/ runtime handshake
                   机械比对，未批准 capability → REFUSE STARTUP（§6.2）
F4 CLOSED        — DecisionId 唯一 Product semantic correlation（不新增
                   ConsultationId）；transport correlation realization-private；
                   五条丢弃规则（drop+diagnostic，不 re-enter/不生 effect/
                   不耗轮）；per Run ≤1 active live consultation（§3.4/§3.1）
F5 CLOSED        — .NET records 唯一 schema authority → 生成 JSON Schema
                   三方同源消费；handshake 钉 protocolVersion/schemaVersion/
                   schemaHash，失配 FAIL CLOSED（§6.4）
F6 CLOSED        — 不新建 durable consultation/decision owner；D1 收窄至
                   runtime lifecycle；restart = owner records → fresh observe
                   → re-evaluate → 新咨询合法；旧 transcript/session/decision
                   不恢复现实（§2/§8.4）
F7 CONFIRMED     — 历史 session 内容 ≠ current reality authority；latest
                   AgentDecisionContext wins（§7）
Trace/UI CONFIRMED — DSH UI=projection；session log≠Trace authority；
                   UI command 必经 UniClaw Product command surface（§11）
```

**Remaining structural findings：0**

## 冻结范围（AGT-001 冻结的架构决定）

```text
UniAgent Product Role ↕ DSH realization boundary
Kernel decides WHEN · UniAgent decides WHAT
1 active live consultation per Run
DSH turn bounded + externally abortable（AbortCurrentTurn 无产品 authority）
DecisionId = Product semantic correlation；transport correlation = realization-private
.NET records = Product protocol schema authority（生成物三方同源）
DSH Product profile = mechanically headless（三层）
Agent Strategy State ≠ World/Run truth；latest AgentDecisionContext wins
Policy = closed typed bounded contingent package（advisory；词汇表归 RUN-005）
DSH UI = projection only；Product commands remain UniClaw-owned
```

## DEFER 项（不得因 closure 被误认为已解决）

cross-process exactly-once consultation · durable AgentDecision ownership ·
Agent Continuation（**独立 future change：Recovery / Agent Continuation**）·
full L2 Policy vocabulary · Policy runtime expansion · real DeepSeek sidecar
implementation（AGT-002）· context byte-budget · real-dsh realization
annotation · concrete JSON-RPC envelope。

## Baseline Impact

Product baseline reopened? **NO** · RUN-004 reopened? **NO** ·
Simulation baseline reopened? **NO**（closure 未回写任何冻结上游）。

## Verification

```yaml
verification:
  level: CONTRACT
  method: full adversarial grill（8 面 + 代码反证）→ owner disposition → v0.2 修订 → focused re-grill（6 gate + 2 确认）
  expected: F1-F6 闭合、F7/Trace-UI 确认、零新增 authority/owner/state、裁决逐字落地
  actual: PASS（focused re-grill 逐条对照冻结条款原文；remaining structural = 0）
  evidence: 设计稿 v0.2 全文（冻结条款行号核验）+ 本文件 grill 记录
```

## Status log

- 2026-09-24 · understand→designing · Step 1 设计稿 v0.1 + spec/plan/state。
- 2026-09-24 · designing · Full Adversarial Grill：PASS_WITH_FINDINGS
  （F1-F7 + GQ1-GQ4 owner 裁决；commit a23511c0 含 CONTEXT.md 两词条）。
- 2026-09-24 · designing · v0.2 修订（F1-F6 闭合、F7 注记）→
  ready-for-focused-grill。
- 2026-09-24 · reviewed · Focused Re-Grill：**PASS**（六 gate 全 CLOSED +
  F7/Trace-UI CONFIRMED；remaining = 0）。
- 2026-09-24 · reviewed→verified→**closed** · owner 接受裁决并授权
  closure；设计稿升 FROZEN；本 closure commit 仅文档收口。
  下一阶段：AGT-002（DSH Realization 实现，以本稿为上游权威）；
  RUN-005 独立拥有 L2 vocabulary / expansion runtime / per-application
  fresh 链语义（不得吞入 AGT-002）。
