# TEST-ASSET-AUTOMATION-001 slice1 — Spec review v3

复核范围仅限两处增量：取消时的组输出保全，以及 resolved 快照/信号消费者异常的
收尾归属；未审归档、清理、诊断或重放入口。

`run()` 已将子进程 stdout/stderr 写入受管理的临时文件，正常等待或取消后的
`killpg`、`wait` 完成后再读取（`tools/verify-change:47-74`）。调用者捕获中断时
把已保全输出写回本 Run 的组日志，并在 finally 中完成 TRX 摘要与 receipt
（`tools/verify-change:570-589,546-569`）。真实 OS SIGTERM 测试证明 writer 已
退出、返回码为 130、`ARCHITECTURE.txt` 留有中断前输出，且清单仍可校验
（`tools/tests/test_verify_assets.py:92-131`）；符合规范“停止写入后枚举、取消也
保留部分结果/未决”的要求（`docs/agents/test-asset-retention.md:30-37,97-99`）。

验证结论也没有被消费者异常改写：先计算并记录 `summary.status=final`，再分别
捕获 resolved-config 与 evolution-signals 的错误，写入 `consumerErrors`，令
`consumersCompleted=false` 以产生 pending receipt，但保持原返回 verdict
（`tools/verify-change:669-691`）。快照写入 OSError 的回归断言验证 quick 原本的
成功返回仍为 0，同时索引保存该消费者错误（`tools/tests/test_verify_assets.py:83-90`）。

定向复跑：资产入口 6/6、原 verify-entry 9/9 均通过；本轮两处未发现可行动的
Spec 问题。真实 quick 的资产清单与 `sourceStable=true` 可作为首片执行证据，但
其 Kernel 43、Host 26 虽通过，Simulation 仍 1 项失败且认证 stale，故不是产品
绿灯。A4/A5 和其他入口尚未实施，父 AUTOMATION Change 仍不得关闭。
