# Slice 2 Standards Review v1 — ADR-0055 applicability supplement

本补充仅重判 `review-standards-v1.md` 第 1 项；以 ADR-0055:5-11 与冻结候选为准。

**结论：撤回该项 P1。** ADR-0055:7 明确允许“单条逻辑操作亦可异步提交”，并禁止为凑
批跨越“先提交、后咨询/投递/发布”的因果屏障。现有报告 buyer 没有一组可安全合并而被
候选伪异步退化的写入：

- `Accept` 必须先固定生成器再确认 `AwaitingInputs`（`owned.patch:88-94`），HTTP 激活才可
  继续；延迟至与其他报告合批会改变“要求已接受”的准入含义。
- `Prepare` 的 inputs/manifest 发布后才可写 `Ready`（`162-183`）；`Publish` 又必须先确认
  `Generating`，才启动生成器（`218-224`）。这是同一 delivery 的不可跨越顺序。
- 生成器退出、输出/receipt 已校验和发布后，才可确认 `Published`（`226-240`）；`Confirm`
  还必须重新可读核验后才写 `Delivered`。这些不是可交换的多条独立事实。

候选使用原生异步文件 I/O（`FileOptions.Asynchronous`、`ReadAsync`/`WriteAsync`，
`owned.patch:419-469`），未见 `Task.Run` 包同步写或“入队即成功”。它仍没有证明实际批次
减少 flush，也没有新增报告并发压力下的性能数据；这应记录为 A10/后续性能证据缺口，而非
要求本稀疏、强因果的报告状态另造队列、通用日志或跨 Owner 事务。

若未来出现真实 buyer：同一 Host 内、彼此无因果依赖、均已接受且不阻塞启动/发布的多个
独立报告版本，可在 Host adapter 以数量/字节/等待上限批写各自 immutable frame，并仅在
每个 frame/head 在声明故障范围可读取后完成各自 Task；不得合并或推迟上述同一 delivery
的 `Accept → Prepare → Generating → Published → Delivered` 屏障。本候选没有该 buyer 或
收益实证，故不能据 ADR-0055 强加此实现。
