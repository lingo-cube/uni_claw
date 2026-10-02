# Workspace Contract Foundation v0.1

这些 schema 是 UniClaw Workspace 的语言无关公共契约。它们约束查询边界和跨来源记录，不承担 Product Runtime 写操作、来源缓存/归档或 Host 生命周期。

## Owner 与 authority

- `productSessionId` 是产品身份；它由 Product Session authority 确认。
- `hostSessionRef` 只能显式关联 Host session，不能替代 `productSessionId`。
- `auxiliaryExecutionRef` 表示 DSH 或外部能力的辅助执行，不创建新的 TaskInstance 或 Product Session。
- `source` 说明记录来源，`authority` 说明谁对事实负责；Workspace 只读投影不得升级 authority。
- `detailRef` 是受控的 opaque/relative 来源引用，可以包含 adapter 约定的相对层级（例如 `evidence/foo.md`），但客户端不得把它当作任意文件系统路径；绝对路径、协议 URL 和控制字符会被拒绝，路径 traversal 与权限检查由 `DetailResolver` 执行。

## 版本策略

每个公共 schema 都有稳定 `$id`、`schemaVersion` 和 `contractVersion`。`schemaVersion` 只接受当前版本常量，兼容扩展应增加可选非关键字段并提升 minor 版本；删除、改义或改变必填字段必须提升 major 版本。对象默认 `additionalProperties: false`，因此未知字段必须由契约显式加入。

## Capability

v0.1 只提供五个窄的只读 capability：`TaskQuery`、`SessionQuery`、`TraceQuery`、`EvidenceQuery`、`DetailQuery`。每个请求声明 capability 和版本；每个响应回显 capability、版本和 `ok`。失败响应使用结构化 `QueryError`，区分 `unavailable`、`uncorrelated`、`stale`、`permission-denied`、`not-found`、`timeout` 等可观察状态。

`capability.schema.json` 是 capability 请求/响应 envelope 的公共契约：请求和响应必须回显 capability、capabilityVersion、requestId；响应必须声明 `ok`，成功携带 `data`，失败携带结构化 `error`。响应至少携带一个非空的 `snapshotId`、`revision` 或 `observedAt`，分页使用非空 `nextCursor`，局部失败使用 `errors` 数组。五类 capability 各自定义 `data` 的只读载荷形状，envelope 不把这些载荷收敛为 Host 专有字段。

契约对象默认拒绝未知字段；关键字段缺失、错误响应缺少 `error`、成功响应缺少 `data` 都必须失败。新增非关键扩展需要显式加入 schema 并提升兼容版本，不能用“忽略未知字段”掩盖协议漂移。

## 校验

从仓库根目录运行：

```text
python3 tools/validate-workspace-schemas.py
```

`examples/valid/` 中的 JSON 必须通过，`examples/invalid/` 中的 JSON 必须失败。validator 会报告文件、JSON path 和原因。
