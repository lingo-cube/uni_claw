# RUNTIME-STORAGE-002 Slice 2 — Standards Review v1

审阅基点为 `slice2 entry-source (new files empty)`；只读 `candidate-v1/manifest.json` 与
`owned.patch`，未将 dirty HEAD 或父 Change 的未完成要求归入本结论。未重复工具执法。

## Documented standard breaches

1. **P1：新增可靠报告存储没有合法批量提交机制。** `owned.patch:93-94`、`175-176`、
`238-240`、`364-372`、`461-469` 对生成器、输入、发布物和每次状态版本都逐项直接
写入/flush/移动；`ReportDeliveryLimits` 只有字节、并发和生成时限（`28-37`），没有
批量的数量、字节、等待上限或确认后的批提交。触发：同一 Host 接受/准备/发布多个报告
或同一报告产生连续状态时。后果：每条记录独立 flush，既不满足 ADR-0055 “异步批量写入”
及其三上限/背压要求，也无法证明 A7 的生产 async 未退回为“仅 Async 名称”的逐条 I/O。
这不要求引入通用队列：应在该 Host 存储 adapter 内补受限 batch、确认点及对应实证。

2. **P1：报告生成被挂在未追踪的 fire-and-forget RunBackground 上，正常关闭没有等待或
显式未确认项出口。** `owned.patch:739` 启动后即返回，`779-800` 才在 Run 已终态后执行
必交付报告；冻结补丁未加入 Host 生命周期对该 Task/`RequiredReportDelivery` 的登记、
有界 drain 或 shutdown 未确认项报告。触发：终态 Run 的生成/确认期间正常停止 Host。
后果：进程退出可留下 `Generating`/`Published`，虽可由后续重启恢复，却不满足 ADR-0055
“正常关闭须等待已接受记录完成或明确报出未确认项”，也违反 state Decision 7 / A7 的
有界关闭、未确认可见要求。应由 Host 持有并在停止时等待受限时间，或持久化并明确输出
未确认 delivery，再结束。

## Judgement-only smells

无。`RequiredReportDelivery` 把来源固定、状态、校验与单 writer 租约集中在同一 Host
交付模块；在本 slice 的真实 buyer 下未见应判为 Mysterious Name、重复、消息链、预造
抽象或职责外移的明确迹象。

## 边界

观察到终态先写入、报告失败不回写 Outcome（`owned.patch:758-800`），与独立 terminal
outcome 的边界一致。未验证 live/startup、真实 Host 正常停止、性能/批量收益，也未宣称
父 A1–A10 已完成。
