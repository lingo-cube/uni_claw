# ADR-0034：任务发起采用分阶段元信息生成

状态：accepted（2026-10-03）。任务发起不是一次性构造完整 metadata，而是先接收声明式 launch request，再在创建过程中逐阶段生成和关联字段。这样可以如实表达“任务已接受但 Runtime/Host/设备或产物尚未就绪”，也让每个字段保留正确的来源与 authority。

## Decision

1. Launch request 先携带 `projectRef`、`testSetRef`（当前默认 `default`）、`taskRef`、请求方/幂等关联和可选环境意图；这些字段的 `valueOrigin` 标为 `configured`。
2. 创建过程中记录 Task Instance 与 storage namespace、由产品 authority 确认的 Product Session、Runtime 产生的 `runId`、primary Host Session，以及后续设备/Runtime 观察、Trace/Evidence 引用和 terminal outcome。字段在其产生阶段补齐；实际调用顺序由现有 lifecycle 依赖决定，不在此预定。
3. 每个字段记录 `source`、`authority`、`availability`、`valueOrigin` 和 correlation reference；`valueOrigin` 只能是 `configured`（配置/manifest）、`generated`（创建 authority 生成）、`observed`（设备/Runtime/Host 实测）或 `derived`（由已有关联记录计算）。需要时间语义时再记录 `observedAt`；阶段未完成时不得用空值伪装成功。
4. 发起接口可以先返回稳定的 launch/instance reference，查询接口观察后续补齐；重试必须使用幂等关联，不重复创建 Product Session、Primary Run 或 Task Instance。配置值不能升级成 observed，derived 值不能替代来源记录。
5. 部分失败留下可查询的 partial/unavailable 状态和诊断原因。Workspace 只投影这些阶段记录，不把中间状态升级为 Runtime Outcome 或 Evidence。
