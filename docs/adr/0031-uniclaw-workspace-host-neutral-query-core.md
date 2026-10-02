# ADR-0031：UniClaw Workspace 采用 Host-neutral Query Core，来源保留由 Host/Source 负责

状态：accepted（2026-10-02）。UniClaw Workspace 的通用查询、关联、规范化和读模型投影放在独立的 Host-neutral Query Core；语言无关契约进入 `schemas/workspace/`，DSH、独立 Web 和未来 Host 通过窄 capability 与 source adapter 接入。Workspace 不成为 Product Runtime，也不拥有来源数据的长期归档、缓存和保留策略；Host/Source 自己管理这些生命周期，若 DSH 需要向 Workspace 暴露管理或读取能力，必须提供显式版本化 interface。

选择这一边界是因为 DSH 的现有 `workspace/session/trace/evidence/artifact` API 同时承担 Host/RPC 装配、来源读取和投影，直接把它提升为产品前端契约会锁定 DSH 语义；而让每个入口各自实现查询会产生不同的关联和失败行为。Host-neutral core 加上语言无关 schema 允许共享前端和读模型，同时保留 Product Runtime、Host Session、来源 authority 与缓存/归档 owner 的边界。

## Considered Options

- DSH-first：拒绝，通用前端会依赖 DSH Host、session 和存储语义。
- 独立 Web 与 DSH 各自实现：拒绝，关联、局部失败和版本兼容会分叉。
- Workspace 自己复制并长期保留所有来源数据：拒绝，会产生第二个 retention/permission owner，并把只读观察面变成事实存储方。

## Consequences

- `TaskInstance` 必须由 Product Runtime/Task Repository 确认；只有 Host session 的观察记录标为未关联。
- DSH 的旧 API 可以继续作为 adapter 内部实现，但不能成为共享前端或 schema 的真相源。
- 首版需要验证来源版本/读取时间、stale、权限拒绝、删除和 Host 重启复用；不能用一个 snapshot ID 伪造跨来源原子一致性。
- 未来引入 OTel、外部证据或新的 Host 时，实现既有 capability 和 schema 即可，不修改通用前端主线。
