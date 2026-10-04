---
status: accepted
date: 2026-10-04
---

# Capability Management Role 与信任域注册

Capability Hub 被提升为 Capability Plane 能力管理角色的 realization，负责能力注册、分类、协议、作用域、健康、背压和生命周期；它不是新的 L2、Product Owner 或 Authority。为保持 Product Runtime、Runtime Integration 与 Development Harness 的结构隔离，各信任域使用独立的 Capability Registry / Hub 和 Composition Root，只共享 Host-neutral lifecycle fact 与 correlation 词汇。

canonical Owner 在自身 post-commit 点发布 immutable lifecycle fact，Hub 只分发、短期关联和投影，不推断、重排或改写事实。现有 `IRunTrace`、`RuntimeStageMetrics` 保持各自 Owner/Adapter；Effect Provider 仍必须经过 Effect Boundary；Host/Harness Teardown 独立于 Hub 通知并且幂等；Artifact Sink 不直接产生 Evidence Record。

## Considered Options

- **单一跨域注册表**：拒绝。它把 Harness 与 Product capability 的装配和权限混在一起，无法由名称上的 scope 声明证明隔离。
- **把 Hub 提升为新的 Product Owner**：拒绝。Hub 只管理能力，不拥有 Evidence、WorldBelief、Run、Control、Assurance 或 Effect authority。
- **每种能力各自发明 Hook**：拒绝。语言检查、测量、审计和外部传感器需要共享 lifecycle fact、correlation 和 failure 语义，但输出协议仍按分类隔离。

## Consequences

- 第一版只实现 Observation / Inspector / Measurement 的被动接缝；Advisory、Effect、Fixture 和 Fault Injection 保留独立协议，不进入同一个 Product Registry。
- Product Registry 中的 `Text Semantic Perception` 是 Fast YOLO + OCR 与 Slow Text 的强绑定组合；Slow Text 不独立注册，且其请求必须携带同一 capture/cycle 的有界前置依据或已接受引用。
- 感知对外使用 `Semantic Perception` 与 `UI Element Perception` 两个协议接口；`Text Semantic Perception` 至少实现前者，并可由 Fast 投影同时提供后者；`Slow Visual` 可独立实现一个或两个接口。
- XML/Hierarchy、YOLO、OCR 和 Slow provider 按 capture/cycle、字段和 element association 做融合，不做数组拼接；`declaredText` 与 `renderedText` 保持分离。
- `PerceptionAssessment` 暴露 disposition、coverage、uncertainty、association quality、source path 和可选 provider confidence；它是非权威评估，provider confidence 不直接升级为 Product truth 或 Gate。
- 事件 Envelope 必须区分 EventId、Run 内 EventSeq、Runtime elapsed、Host 时间和外部设备时间；外部关联必须使用显式 token 或 association record。
- Hub 丢失、迟到、重复和模块失败只产生显式诊断，不改变 canonical Owner；资源清理由 Host/Harness 的独立幂等 Teardown 负责。
- 本 ADR 不冻结 C# 接口、进程数、部署结构或跨进程传输实现。
