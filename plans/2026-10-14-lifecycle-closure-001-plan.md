# LIFECYCLE-CLOSURE-001 实现计划

状态：已完成并关闭（2026-10-14）

## 目标

沿当前最小 buyer 闭包完成一条可恢复的纵向路径：显式 Task/Session binding → P24 activation record → Primary Run → P25 consultation journal。每个切片同时覆盖字段、Owner、拒绝、重试、恢复和场景证据。

## 切片顺序

### Slice 1：显式身份 binding

行为：`/runs` 只消费已有 `taskInstanceId + productSessionId`，并校验 Task/Session binding 关系与 Contract View/generation。

模型：补齐 binding lookup/conflict result；缺失、找不到、错配统一 fail-closed，禁止静默 mint。

运行时：移除 `run-pending-*` canonical 路径；真实 Run 创建成功后再 `BindRun`。

测试：Store 单测、HTTP contract test、同 key 异 payload、跨 Task 错配、零副作用检查。

证据：有效请求、缺失 binding、错配、重试四组 method/expected/actual/evidence。

### Slice 2：P24 activation record

行为：显式四字段组成 activation key；`launchId/idempotencyKey` 只作外部查询/传输幂等。

模型：activation record 至少包含 key、status、nullable RunId、owner/recovery reference、final/terminal replay reference。

运行时：`reserved → finalized|failed|blocked`；并发只保留一个 record；`reserved` 中断后只能原 key 查询或显式 resume。

测试：并发、重启、finalize 单写、终态回放、失败回放、禁止第二次 mint。

证据：P24 record journal 与状态机检查，覆盖 reserved/failed/blocked 分支。

### Slice 3：P25 consultation journal

行为：四元组 journal key；同 correlation retry 复用结果；新 correlation 是新 semantic consultation。

模型：journal entry 至少包含 key、status、disposition、semantic count、HardCap remaining、result/no-response。

运行时：timeout/no-response 计一次并 fail-closed；同 boundary 新 correlation 受 HardCap 限制；不得直接产生 Effect。

测试：transport retry、同 boundary 重复、HardCap 边界、timeout/no-response、late response、Contract View 切换拒绝。

证据：P25 journal 快照、计数断言、零 Effect 断言、重启重放。

### Slice 4：预算来源逐维度化

行为：`maxConsultations` 与 `maxTotalSteps` 分别记录 `configured/default`。

模型：替换聚合 `BudgetValueOrigin` 或增加兼容投影，保证旧读取面不伪造来源。

测试：全缺省、全显式、单项显式/单项缺省、同内容重试和 View immutable。

证据：字段快照、幂等比较、来源值域检查。

### Slice 5：场景与回归

按 Slice 1–4 的结果重认证最小场景集；只重新认证当前 buyer 场景，不把 deferred P5/Memory/Revision/Cancel delivery 等扩展加入本 Change。

实际结果：Host 211/211、Kernel 887/887、Simulation 188/188、DSH plugin 66/66；场景认证工具检查 28/28 文件通过，20 个认证块由本 Change 重签。

## 依赖与停止条件

- Slice 2 依赖 Slice 1 的显式 binding，不允许并行绕过 binding。
- Slice 3 依赖 Slice 2 的 current ContractView/Run identity。
- Slice 4 可与 Slice 2/3 的字段建模并行，但必须在 P25 journal 进入 VERIFY 前完成。
- 任一 slice 若发现新的产品语义歧义，退回 RESOLVE，不在实现中猜测。
- 现有全量测试失败若由 identity-required 迁移引起，先区分本 Change 回归与外部 dirty/旧 fixture，再决定修复或保持 deferred。

## 不做的事

不实现 ContractRevision、Host Cancel endpoint、WaitingForInput watchdog、GoalEvaluation delivery、Memory、P5 Run Snapshot、跨 Host resume 或 P1 UniAgent authoring seam。这些只有在 Buyer Evidence 达标后另开 Change。
