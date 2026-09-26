# PER-012 Semantic Migration Decision（设计计划）

版本：v0.1；decision-only；未冻结。

本计划处理 PER-009 current realization 与 PER-010/011 forward design 的语义迁移
不一致。PER-009 不重开，PER-010/011 保持 `FROZEN/closed`；PER-011 implementation
在本 decision 通过前保持阻塞。

## Decision frontier

- checked false 的 Exact proof 与 Collapsed fallback；
- legacy shared `*.state` 的兼容边界和终止条件；
- semantic checked 与 rendered appearance 的切换方式；
- valid shared-leaf dedup 与 `MalformedLineage` admission；
- Aligned 的 temporal compatibility；
- old consumer cutover、rollback 和 implementation entry gate。

## Not in this plan

不修改 Product code、PER-009、WorldModel、Grounding、AGT/RUN 或 Product baseline；
不创建新的 authority owner；不把迁移决策伪装成实现完成。

## Gate

先完成 mapping/compatibility/fixture/rollback 的 Human Gate，再另立实现 change。

