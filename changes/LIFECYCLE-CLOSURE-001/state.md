# LIFECYCLE-CLOSURE-001 — 当前最小 buyer 闭包：显式身份、P24 激活记录与 P25 咨询账本
lifecycle_state: closed · disposition: none · depth: decision-heavy · base: d86f036e660801699430fb732946136055f00105

## Intent（WHAT/WHY）

把当前最小 buyer 闭包中仍未闭合的三条字段链落到一个可验收的生命周期切片：

```text
TaskInstance / ProductSession binding
→ P24 activation record
→ Primary Run
→ P25 consultation journal
```

当前 `/runs` 会重新 mint Task/Session 并写入 `run-pending-*`，P24 的
`acceptedContractViewRef + contractGeneration` 尚未形成持久激活记录，P25 也没有
按 semantic consultation 区分 retry、重复 boundary 和 HardCap 消耗的 durable journal。
这些断点会直接破坏 identity continuity、activation idempotency、预算有界性和恢复判定。

## Scope / Out of Scope

### In

- `/runs` 消费显式 `taskInstanceId + productSessionId + acceptedContractViewRef + contractGeneration`。
- 缺失、找不到或错配 binding 时 fail-closed，零新身份、Run 和 Effect，并保留 rejected reason。
- P24 activation record：按 activation key 预留，绑定真实 RunId 后 finalize；失败/终态可回放，禁止第二次 mint。
- P24 `reserved` 中断后的显式恢复与 BLOCKED/failed 处置。
- P25 四元组 journal：`runId + contractViewRef + decisionBoundaryId + consultationCorrelationId`。
- transport retry 复用 correlation 且不重复计数；新的 semantic consultation 生成新 correlation 并消耗一次 HardCap。
- P25 timeout/no-response 的持久 fail-closed disposition。
- `maxConsultations` 与 `maxTotalSteps` 各自记录 `configured/default` 来源。
- 对应的字段级、异常分支、生命周期场景和重启/重试验收。

### Out

- ContractRevision 的实现与审计链（保留 ADR-0051 语义，等待独立 buyer）。
- Host Cancel endpoint、在途 Attempt 对账和完整 SafeStop/Blocked transport 恢复。
- WaitingForInput watchdog、GoalEvaluation user/session delivery retry。
- P5 Run Snapshot、P20/P21 Memory、跨 Host authority token、通用跨进程现场 resume。
- P1 HostRunner authoring 偏差修复；继续记录 `known deviation`，出现真实 UniAgent authoring buyer 后另开 Change。

## Decisions

- 当前 buyer 必须同时具备 consumer、登记场景/验收和可回指 Change；Owner 单独裁决只冻结语义，不自动构成实现 buyer。
- 生命周期基数保持 `1 Task Instance / 1 Product Session / 1 Primary Run`。
- P24 activation key 固定为 `acceptedContractViewRef + contractGeneration`；`launchId` 和 `idempotencyKey` 只作查询/传输幂等键。
- P24 采用 `reserved → finalize`；中断后保留 `reserved`，无法证明 owner 时转 BLOCKED/failed，恢复必须显式 resume。
- P25 同一 boundary 可重复咨询；每次新的 semantic consultation 使用独立 correlation，transport retry 复用原 correlation。
- timeout/no-response 计一次 semantic consultation，进入 journal 并 fail-closed。
- P1 authoring 偏差不阻塞本 Change 的 P0。

## Acceptance

| # | 判据 | 实际 |
|---|---|---|
| A1 | 有效 `/runs` 请求必须带四个 canonical 字段；Runtime 只消费已存在且同属一个 binding 的 Task/Session，不重新 mint。 | PASS：Host.Dsh 路径与 runtime-provider 测试 |
| A2 | binding 缺失、找不到、跨 Task 错配或 Contract View/generation 不一致时：零新 Task/Session/Run、零 Effect，返回稳定 rejected reason，并有审计 correlation。 | PASS：HTTP 负向分支与 Host 211/211 |
| A3 | ProductSession 只在真实 Run 创建后绑定真实 RunId；`run-pending-*` 不得进入 canonical Store。 | PASS：RuntimeRunStore/Host 测试 |
| A4 | 相同 P24 activation key 的并发/重试只产生一条 activation record；`reserved`、`finalized`、`failed`、`blocked` 状态可区分且可查询。 | PASS：ActivationRecordStore 2 项持久化测试 |
| A5 | P24 finalize 只发生一次；终态或失败重试只回放原记录，不重新 mint Run 或 Effect；`reserved` 恢复需显式 resume。 | PASS：activation replay/resume 分支 |
| A6 | P25 journal 四元组键可区分同 boundary 的新 semantic consultation 与同 correlation 的 transport retry；retry 返回原结果且不重复消耗 HardCap。 | PASS：Kernel journal 3 项 + Host reload 测试 |
| A7 | P25 timeout/no-response 计数一次、持久化 disposition、零新 Effect；新 correlation 才可在 HardCap 内重试。 | PASS：rejected journal、Simulation 188/188 |
| A8 | `maxConsultations` 和 `maxTotalSteps` 分别记录 `configured/default`，部分缺省不丢失来源事实。 | PASS：BudgetRenewal 测试 |
| A9 | 通过最小 buyer 场景集：显式 binding 成功/拒绝、P24 并发/重启/终态回放、P25 重试/重复 boundary/HardCap/timeout；场景证据可回指本 Change。 | PASS：Simulation 188/188；20 个认证块按本 Change 重签 |
| A10 | P5、Memory、ContractRevision、Host Cancel transport、watchdog、GoalEvaluation delivery 等 deferred 范围没有被空 DTO 或 speculative protocol 偷渡进实现。 | PASS：实现 diff 未引入 deferred DTO/endpoint |

## Constraints

- 遵守 ADR-0051、ADR-0046 的局部修订、`CONTEXT.md` 的 Buyer Evidence/Minimal Buyer Closure 词汇。
- 不把 Host/DSH identity、transcript 或 `launchId` 提升为 Product canonical identity。
- 不修改 P1/P2–P18 已验证语义；只补当前 buyer 直接依赖的字段和记录。
- 保留现有并行 dirty 文件；不 reset、clean 或 sweep 无关变更。
- 实现必须先补字段验收矩阵和负向测试，再接入 HTTP/driver 路径。

## Verification

```yaml
verification:
  level: SCENARIO
  method: "字段闭包矩阵与协议基线对照；P24 activation JSONL 预留/finalize/replay；P25 in-memory 与 File JSONL journal；Host/Kernel/Simulation/DSH 分层测试；协议图/invariant 检查；场景认证按唯一工具重签"
  expected: "当前 buyer 闭包的身份、P24、P25 和预算来源字段逐项可判定；deferred 范围不进入实现；每个异常分支有 method/expected/actual/evidence"
  actual: "PASS；整解 1530/1530（Core 14、FileSystem 9、Agent 29、Kernel 887、Simulation 188、Host 211、Agent.Dsh 159、Host.Dsh 33）；DSH decision plugin 32/32、task workbench 66/66；Host.Dsh build 0 errors；protocol machine 5 machines/28 transitions/44 edges，invariant matrix 47/47；场景认证 20 个块由 LIFECYCLE-CLOSURE-001 重签"
  evidence: "docs/analysis/uniclaw-field-flow-optimization-proposals-2026-10-14.md §十至十七；docs/analysis/uniclaw-completeness-check-2026-10-14.md §六；docs/adr/0051-field-closed-lifecycle-controls-and-revisions.md；tests/UniClaw.Host.Tests/ActivationRecordStoreTests.cs；tests/UniClaw.Host.Tests/FileConsultationJournalTests.cs；tests/UniClaw.Kernel.Tests/Runtime/ConsultationJournalTests.cs；evidence/lifecycle-closure-001/"
```

## Status log

- 2026-10-14 · UNDERSTAND → RESOLVE · 完成最小 buyer 闭包复核与四轮 grill；确认 P0 为身份链/P24/P25，P1 authoring 及无 buyer 扩展不阻塞。
- 2026-10-14 · RESOLVE → PERSIST → PLAN · Owner 确认进入实现 Change 规划；建立本 Change，尚未修改产品实现。
- 2026-10-14 · IMPLEMENT · Runtime/Host 接入显式四字段 binding、P24 activation record、P25 四元组 journal 与逐维度 budget origin；补齐 Host/Kernel/Simulation 旧 fixture 的显式身份。
- 2026-10-14 · REVIEW → VERIFY · 修正 no-response 的终局映射为持久 rejected + `TerminalNotProven`，补齐异步 typed checked 场景证据；整解 1530/1530、DSH decision plugin 32/32、task workbench 66/66 全部通过。
- 2026-10-14 · CLOSED · 按 `scenario_certify.py --all --change LIFECYCLE-CLOSURE-001` 重签 20 个认证块；协议图、invariant matrix、diff check 均通过；deferred 范围留在本 Change 之外。
