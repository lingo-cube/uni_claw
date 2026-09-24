# AGT-001 — UniAgent Runtime Architecture（DSH Product Realization）

> 设计稿：`plans/2026-09-24-agt-001-uniagent-runtime-architecture.md`（v0.2，
> post-grill revision）。

## Intent（WHAT/WHY）

在 Product Architecture、RUN-004 咨询协议、GEV-004 目标评价均已冻结、
ADR-0022 + uniagent-realization-baseline-v0.1 已裁决「DSH = Product
Realization」的前提下，设计 **DSH 如何作为 UniAgent 的 runtime
realization**——填 baseline §9 的 Deferred 槽位，产出可冻结的产品架构。

核心原则：**UniAgent 是产品角色，DSH 是实现机制**；**Kernel owns WHEN，
DSH-UniAgent owns WHAT**；DSH 的 session/turn/tool/transcript 全部
realization-private，不取得任何 Product Authority。

## Scope

Q1-Q8 全量（见设计稿目录）：sidecar 边界 · session 映射与跨重启语义 ·
consultation-turn 生命周期 + interruption plane + 预算语义 · 四元
AgentDecision（Policy 正式成员）· L2 closed/typed Policy 语言 · DSH 三层
headless 机械关闭 + 单一 schema 权威源 · strategy state 边界 · 12+3 项失败
模型 · simulation/replay · provider/determinism · trace/UI 显式条款。

## Grill 记录（2026-09-24，第一次正式 adversarial grill）

**PASS_WITH_FINDINGS**：无 authority inversion / 第二 Kernel / 第二 Run
Model / 第二 World Model / reality bypass；双 loop 主权关系成立。

### Owner Decisions（已应用）

| # | 裁决 | 落点 |
|---|---|---|
| GQ1 | **A** — adapter 带外 AbortCurrentTurn（不改冻结缝）；≠cancel/≠preemption/≠lifecycle/≠lease authority；deadline MUST be bounded + externally abortable（60s=realization 默认非架构常量） | 设计稿 §3.2 |
| GQ2 | **B / DEFER** — 不新建 consultation/adopted-decision durable owner；D1 收窄至 runtime lifecycle；restart = owner records + fresh observe/reconcile + re-evaluate；exactly-once/continuation = 独立 Recovery change | §2 / §8.4 |
| GQ3 | **YES** — 每次 semantic consultation attempt 消耗一轮（含 timeout/malformed/no-submit/provider failure/null）；transport retry 不消耗（两个预算分层） | §3.3 |
| GQ4 | **YES** — .NET records 唯一 schema authority → 生成 JSON Schema artifact（adapter 校验/DSH tool schema/生成 TS 三方消费）；handshake 校验 protocolVersion/schemaVersion/schemaHash，失配 fail closed | §6.4 |

### Findings 处置

F1 SUBSTANTIVE→闭合（interruption plane + late-response 保护）；F2
SUBSTANTIVE→闭合（Policy = typed closed AST，词汇表归 RUN-005）；F3
SUBSTANTIVE→闭合（三层机械 headless：launch profile / static allowlist /
runtime handshake 比对）；F4 MEDIUM→闭合（DecisionId 唯一 product
correlation；transport 分层丢弃规则；one-in-flight）；F5 MEDIUM→闭合
（GQ4 单源）；F6 MEDIUM→闭合（GQ2 DEFER + restart 语义冻结）；F7
MINOR→注记入 §7（latest context wins；quality 非 authority）。

## Acceptance（本 change）

1. 设计稿覆盖 8 问 + failure model + replay + provider/determinism +
   trace/UI 条款 + 四图；grill F1-F7 全闭合；
2. 与 FROZEN 上游零冲突（只引用不重定义）；
3. focused re-grill 通过标准（7 条）全部满足 → CLOSED。

## Out of Scope

实现（bridge/sidecar/AbortCurrentTurn/JSON Schema 生成/submit_decision
tool/真实 DeepSeek 调用）· RUN-005 Policy runtime · 新增 Kernel owner
record · 新增 Product ConsultationId · 修改 Product baseline · 修改
RUN-004 frozen seam · Memory · Goal NLP · multi-agent · AGT-002。
