# ADR-0032：Project/Test Set 由 Host 管理，仓库目录作为临时测试目录

状态：accepted（2026-10-03）。Project 与 Test Set 的 canonical identity、版本和生命周期属于 Host/Task Catalog；UniClaw Workspace 只通过显式 adapter 读取并投影。由于当前 Host 尚未提供这套持久化能力，先在代码库的专用目录维护声明式测试目录与 fixture 关系，当前对外使用默认版本 `default`，并可附带由目录内容产生的 `sourceRevision`。这些目录是查询 adapter 的临时来源，不是 Worktree、Workspace 存储或 Product Runtime truth。这样既能建立测试集管理和可重复 fixture，又不改变 Workspace 只读、Host-owned retention 以及 Product Session/Primary Run 的 authority 边界。

## Consequences

- PNL-004 可以先消费仓库测试目录，提供 Project/Test Set/Task 的只读导航与 `default` revision 关联；查询契约只暴露逻辑引用，不暴露目录结构。
- Workspace 不新增 Project/Test Set 写入、归档、缓存或保留 API；Host 能力就绪后通过版本化 adapter 迁移来源。
- 仅由 Host/Task Catalog 或 Product Runtime 确认的 Task Instance、Product Session、Primary Run 才能成为产品身份；目录或 artifact 只能形成未关联观察。
- `runId` 由 UniClaw Runtime 产生，并与对应 Session 显式绑定；DSH Session、traceId、目录名和文件名都不能替代它。
