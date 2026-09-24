# AGT-001 — Plan

> v0.2：grill 修订完成，进入 focused re-grill。

## 步骤

1. ~~设计稿 v0.1~~（完成，2026-09-24）。
2. ~~第一次正式 adversarial grill~~（完成：PASS_WITH_FINDINGS，F1-F7 +
   GQ1-GQ4 owner 裁决）。
3. ~~v0.2 修订~~（完成：F1-F6 闭合、F7 注记，设计稿 v0.2）。
4. **Focused re-grill（下一步，仅一次）**：只验证——
   - F1 interruption：AbortCurrentTurn 不拥有 Product cancel/preemption
     authority；stale/late response 无法 re-enter Kernel；
   - F2 closed Policy language：无 arbitrary executable expression；
   - F3 headless DSH isolation：无 interactive/steering reality bypass；
   - F4 transport correlation：分层 + 丢弃规则完整；
   - F5 schema single source：Product protocol 只有一个 schema authority；
   - F6 cross-restart：不依赖 DSH session / driver volatile state，恢复以
     owner records + fresh observation 为准；
   - F7 注记确认（latest context wins）。
   通过标准：上述全部成立 → CLOSED；仅当出现新的 authority inversion /
   second runtime / second truth owner / reality bypass 才 REOPEN。
   **不得**从零重 grill 整份架构。
5. 冻结与实现拆分（focused re-grill 通过后）：设计稿升 FROZEN 候选；
   实现切片（adapter+fake sidecar 协议测试 → DSH profile + mock provider
   冒烟 → Host 配置门控 + failure matrix 测试 → conformance C1-C11）另立
   plan；RUN-005 联动时序由 RUN-005 spec 评裁决。

## 验证策略（focused re-grill 阶段）

- 六项 finding 逐条对照设计稿 v0.2 条款（引用节号）；
- 裁决落地与 GQ1-GQ4 owner 决定逐字一致（无扩大无收窄）；
- 无新增 authority/owner/state（GQ2 DEFER 清单未被越权预造）。
