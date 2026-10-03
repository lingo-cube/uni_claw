# ADR-0033：任务发起负责建立运行关系，查询通过可替换 Storage Adapter

状态：accepted（2026-10-03）。当前阶段不先建设完整的 Project/Test Set 管理系统：测试集和 fixture 作为代码库里的可引用资产，统一使用默认版本 `default`。显式的 Task Launch Seam 负责引用这些资产，并协调 Runtime 产生的 `runId`/Product Session、Host Session 和本地 storage namespace 的创建与绑定；Workspace 的查询接口只读取这些关系。文件夹和 JSON 是第一种临时 storage adapter，未来可替换为 Host 或远程实现，不能成为共享前端契约或第二份产品真相。

## Consequences

- 页面和 Workspace 只依赖逻辑引用与查询 capability，不直接读目录或文件。
- 查询接口可以统一返回 Session、Trace、Evidence、Metadata 的关联状态；缺失关系显示未关联或不可用，不通过文件名猜测。
- 测试集资产提供输入与引用来源，不是 Runtime Outcome、完成状态或 Evidence authority。
- `runId` 仍由 UniClaw Runtime 产生，并与 Session 显式绑定；Task Launch Seam 负责记录绑定，不自行发明运行身份。
