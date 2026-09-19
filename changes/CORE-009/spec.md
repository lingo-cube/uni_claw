# CORE-009 — 可靠执行记录契约设计

## Problem Statement

CORE-008 的仓库审计确认：普通诊断 Trace 可关闭或丢弃，EffectBoundary 当前的 Receipt
又在 driver 返回后才追加；因此不能证明关闭 Trace 且没有 Receipt 时仍可恢复已可靠登记的
尝试信息。需要优先设计可复用现有 EffectBoundary / Runtime 记录的执行记录契约。

## Solution

- 保留 Attempt 的产品语义，不预设独立类、独立存储或新的 Core 记录。
- 设计发送前准备、固定请求与绑定依据、提交确认、无 Receipt 未决查询、未知结果协调和
  迟到反馈关联的必要保证。
- 物理上允许复用已有 Runtime 记录或存储；逻辑上保持单一可写执行事实来源。
- 普通诊断 Trace 只能引用或展示执行关联，不得成为恢复依据的唯一来源。
- 本 Change 只提交契约与验收计划，不实现 Runtime、Trace、存储或真实发送。

## Acceptance

1. 现有 EffectBoundary / Runtime / Trace 记录的复用能力有证据化审计结果。
2. 执行记录契约覆盖发送前准备、固定请求/Binding、提交确认、无 Receipt、未知协调和迟到反馈。
3. 契约明确 Attempt、Receipt、AttemptReport、Trace、Evidence/Claim 的责任边界。
4. 验收计划覆盖关闭 Trace、崩溃、回执丢失、迟到反馈、重试和队列丢弃场景。
5. 本 Change 不包含产品实现；未通过的恢复能力保持为未知，不被文档宣称为已验证。

## Out of Scope

- 修改产品 Runtime、Trace、EffectBoundary 或存储实现。
- 新增 Attempt Core 类或独立持久化表。
- 触发真实设备、外部服务或发送操作。
- 根据契约预先决定最终存储技术和迁移方案。
