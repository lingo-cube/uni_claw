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

## Task Launch 写入契约

Task Launch 是独立的写入 seam，不属于上面的五类只读 capability。`task-launch-request.v1` 接受逻辑引用、幂等键和声明式配置；`task-launch-ack.v1` 返回 launch/task instance 引用及可部分完成的阶段记录。`launch-stage-record.v1` 用 `valueOrigin`（仅 `configured`、`generated`、`observed`、`derived`）表达值来源，并单独用 `availability` 表达数据是否存在。`runId` 只能由 Runtime authority 产生。`local-storage-namespace.v1` 只暴露不透明的逻辑 namespace 引用和 adapter，不暴露物理目录、绝对路径或文件名。

初始 Ack 可以在 Task Instance 尚未生成时只返回 `launchId`；阶段记录中的字段可各自携带 `source`、`authority` 和可选 `observedAt`，以支持同一阶段包含不同来源的元信息。

## Runtime Run 查询契约

PNL-005 的 Runtime Host 是 `RuntimeRun` 生命周期、事件序列和产物关联的唯一
authority。`runtime-run-projection.schema.json` 是查询视图，不是 Workbench 的
第二份运行真相；`runtime-run-event.schema.json` 保留 DSH、Kernel、设备和
UniClaw Runtime 的 source-native 事件。Run response 和 events response 使用
统一的 HTTP 状态码加业务对象，成功响应携带 `run` 或 `events`，失败响应携带
`error.code/message/retryable/details`。

Run 状态固定为 `starting`、`running`、`completed`、`failed`、`interrupted`。
`revision`、`observedAt`、`lastEventSequence` 和 `consistency` 使最终一致的
查询可观察；事件列表使用 `nextCursor` 分页。Artifact 只暴露逻辑
`detailEndpoint`，不暴露 Host 绝对路径。

### Runtime HTTP 结果

| HTTP | 语义 | 业务响应 |
|---|---|---|
| `200` | 查询成功 | `RuntimeRunResponse` 或 `RuntimeRunEventsResponse`，`ok: true` |
| `202` | 创建请求已接受 | `RuntimeRunResponse`，运行可能仍处于 `starting` |
| `400` | 请求格式错误 | `ok: false` 的 `error` |
| `404` | Run 或事件资源不存在 | `error.code: runtime-run-not-found` |
| `409` | 幂等键、状态或版本冲突 | `error.retryable` 按冲突类型声明 |
| `422` | 业务规则不允许 | `error.code` 描述违反的规则 |
| `500` | Runtime Host 内部错误 | `error.retryable` 明确是否可重试 |
| `503` | 依赖不可用 | `error.retryable: true`，可带 `details.retryAfterMs` |

错误响应固定使用以下业务对象形状，不返回裸字符串：

```json
{
  "ok": false,
  "error": {
    "code": "runtime-run-not-found",
    "message": "Run does not exist",
    "retryable": false,
    "details": {}
  }
}
```

事件查询的 `source` 过滤器只过滤投影，不改写来源事件；`cursor` 是不透明值，服务端负责校验和推进。默认返回摘要，明细通过事件或 artifact 的逻辑 `detailEndpoint` 获取。

## 校验

从仓库根目录运行：

```text
python3 tools/validate-workspace-schemas.py
```

`examples/valid/` 中的 JSON 必须通过，`examples/invalid/` 中的 JSON 必须失败。validator 会报告文件、JSON path 和原因。
