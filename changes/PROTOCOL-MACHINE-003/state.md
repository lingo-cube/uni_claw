# PROTOCOL-MACHINE-003 — 同步 P24/P25 实现与协议状态机

lifecycle_state: closed · disposition: none · depth: decision-heavy · base: working-tree

## Intent（WHAT/WHY）

把 `LIFECYCLE-CLOSURE-001` 已经落地的 P24/P25 语义正式表达进协议状态机、
payload 引用和基线文档，消除“实现已闭合、状态机仍声明 deferred”的规范漂移。

## Scope / Out of Scope

### In

- 为 P24 增加 activation record 的 `reserved → finalized|failed|blocked` 分支、
  explicit resume 和 terminal replay 语义引用。
- 将 `taskInstanceId + productSessionId + acceptedContractViewRef + contractGeneration`
  接入状态机 payload/启动 wire schema 引用。
- 为 P25 journal 增加 `pending / completed / rejected`、transport retry、
  semantic retry、HardCap、timeout/no-response 分支引用。
- 重新判断 `contract-view` 的 admitted hang：闭合已实现的 activation seam，
  或明确留下可回指的真实 deferred 边界。
- 同步协议基线、protocol map、domain cards、相关分析文档与审计证据。

### Out

- 不新增 ContractRevision、Cancel transport、WaitingForInput watchdog、
  GoalEvaluation delivery、Memory 或跨 Host 完整现场恢复语义。
- 不改变 `LIFECYCLE-CLOSURE-001` 已验证的 P24/P25 行为。
- 不把 YAML 升格为第二语义权威；语义仍由基线、ADR-0051 和实现承担。

## Decisions

- `LIFECYCLE-CLOSURE-001` 是实现事实来源；ADR-0051 是语义决策来源；
  `schemas/protocol/state-machine.yaml` 只承载结构、边挂载和可验证引用。
- P24 的 durable activation record 是独立对象机，不能只隐藏在 Run 的
  `admitted → activated` 边里。
- P25 的 retry/timeout/no-response 是 journal 分支，不能只用一个
  `decision-failed-closed` 投影覆盖。
- 所有 generated outputs 必须由生成器重出并通过 `--check`，禁止手改产物。

## Acceptance

| # | 判据 |
|---|---|
| A1 | P24 activation record 的状态、Owner、resume、终态回放和零副作用拒绝分支在状态机中可达且可审计。 |
| A2 | P24 启动四字段和 activation key 有明确 payload/schema 引用，字段缺失、错配、冲突和 generation 不一致有拒绝路径。 |
| A3 | P25 journal 的 pending/completed/rejected、同 correlation retry、新 semantic correlation、HardCap 与 no-response 分支可达。 |
| A4 | `contract-view` 的 admitted hang 与当前实现事实一致，不再保留无依据的 Deferred ⑰ 声明。 |
| A5 | 协议基线、ADR 引用、protocol map、domain cards 与 `LIFECYCLE-CLOSURE-001` 状态一致。 |
| A6 | 生成器图审计、payload 引用执法、全解测试、场景认证和 prompt/schema checks 全部通过。 |

## Constraints

- 继承 `PROTOCOL-MACHINE-001/002` 的单源、生成、审计和漂移检查纪律。
- 只修订当前实现已经证明的字段和分支；任何新语义回到 RESOLVE。
- 保留当前工作区的其他 dirty 文件，不 reset、clean 或 sweep。

## Verification

```yaml
verification:
  level: DETERMINISTIC
  method: "state-machine generator/audit + schema/payload reference checks + protocol baseline grep audit + full solution/plugin/scenario verification"
  expected: "状态机覆盖当前 P24/P25 实现，旧 Deferred ⑰ 漂移消失，所有生成物与测试保持一致"
  actual: "PASS；协议图 7 machines/36 transitions/44 edges/27 steps/12 domain cards；整解 1530/1530；DSH decision plugin 32/32；task workbench 66/66；场景认证 28 files/0 violations；invariant matrix 47/47；workspace schemas 13/13；protocol machine anchors 44/44；git diff --check 通过"
  evidence: "evidence/protocol-machine-003/audit-report.txt; changes/LIFECYCLE-CLOSURE-001/state.md; docs/adr/0051-field-closed-lifecycle-controls-and-revisions.md; docs/analysis/uniclaw-completeness-check-2026-10-14.md; schemas/protocol/state-machine.yaml"
```

## Status log

- 2026-10-14 · UNDERSTAND → RESOLVE · 协议状态机审计确认结构图通过，但 P24/P25 实现分支未完整进入状态机，且基线仍保留旧 Deferred ⑰ 表述。
- 2026-10-14 · PERSIST → PLAN · 建立窄同步 Change；不改变已闭合实现，只补状态机、payload/schema 引用和文档一致性。
- 2026-10-14 · PLAN → IMPLEMENT · 已按计划开始同步状态机与启动 payload；实现语义不变。
- 2026-10-14 · IMPLEMENT → REVIEW → VERIFY · 完成 P24 activation-record/P25 consultation-journal 对象机、四字段启动 schema、contract-view admitted→activated、基线与字段完备性文档同步；全量验证通过。
- 2026-10-14 · CLOSED · 状态机图审计与 payload/schema 执法通过；实现行为未改，仅消除协议模型漂移。
