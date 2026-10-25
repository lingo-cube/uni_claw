# Standards 复核（SIGTERM 小修）

## 做了什么

仅复核 `tools/verify-change` 的 `run()` 输出保全改动与 `test_verify_assets.py` 的真实 OS SIGTERM 场景；未复审归档、清理或其余入口。

## 原来的问题

父进程取消 `PIPE` 读取时，子进程已经写出的标准输出可能尚在读取缓冲中，从而不会进入组日志；v2/v3 证据记录了 `writer started` 缺失。

## 这次修改

`run()` 让子进程标准输出直接写入受 `with` 管理的临时文件。正常退出及 `killpg`/`wait` 完成后才 `seek/read`；取消路径把已落盘输出挂到异常，调用方再写入本 Run 的组日志。临时文件退出作用域即释放，没有引入任务外遗留资产。

## 验证结果

`entry-contracts-green-v4.txt` 为 15/15。真实子进程先写 TRX、输出 `writer started` 并阻塞；SIGTERM 后父进程以 130 退出，子进程已消失，Run 摘要为 `CANCELLED`，ARCHITECTURE 日志仍含该输出，清单校验有效。这覆盖了此前失败的在途输出路径。

## 最终可行动结论

该小 diff 未见可行动的 Standards 缺陷。进程组只处理本次 `Popen(..., start_new_session=True)` 所有的子树，消费者和收尾仍在其退出后执行；结论不外推至未实现的归档能力。
