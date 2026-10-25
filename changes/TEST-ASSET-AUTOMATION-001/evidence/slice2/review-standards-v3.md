# Slice 2 Standards Review v3

仅复核 v2 P2 的修复增量；v1/v2 旧报告保留，未运行 dotnet、服务、模型或设备。

## 结论：有界通过

`replay()` 现于 `begin()` 前安装 SIGTERM/SIGINT handler，handler 只置取消标记；即使信号发生在 Run 已登记、调用方尚未收到 `started` 的窗口，begin 返回后也会跳过 `Popen`、以 `cancelled`/130 生成结果并进入 `finish`。子进程等待改为短轮询，取消后由既有专属进程组收束。最外层 `finally` 覆盖 case 前置校验、消费者和 finish 异常，并在收尾后恢复两种 handler。

`finish` 失败时 `replay` 保留既有 replay CLI 返回码及完整原 summary，best-effort 写入 `finalizationPending`；返回值新增 changeId/runId，便于调用方定位未决 Run。`handler-finalizer-red.txt` 证明原先非法 case 会遗留 handler，`python-final-v3.txt` 记录修复后 22/22（含已登记未返回时 TERM、忽略 TERM writer 退出、handler 恢复和收尾异常不改原 rc）。静态调用链与该证据一致，未发现新的可行动缺陷。
