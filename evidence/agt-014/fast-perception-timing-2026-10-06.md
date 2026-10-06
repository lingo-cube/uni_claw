# AGT-014 快感知分段复测 — 2026-10-06

## 目的

核对真实 Settings 首帧 `fastLatency=63.745s` 是否来自当前页面截图推理，还是
把视觉服务首次启动混入了同一计时。

## 方法

对真实 DSH 回合保存的当前页面截图
`dsh-task2-first/run-20261005-173509-345/evidence/capture-84635063c871414cbd8ebf6dcf738aff.png`
调用同一产品侧 `VisionServiceSession.Analyze`，不改变截图、不改变模型和协议。
新增分段计时记录服务启动、PNG 解码和 `/v1/analyze_raw` 当前截图请求。

## 结果

| 阶段 | 耗时 |
|---|---:|
| 服务首次启动（进程 + 健康探活 + 模型预热） | 63.314s |
| PNG 解码 | 0.056s |
| 当前截图推理请求 | 0.619s |
| 总耗时 | 63.992s |

同一 Settings 覆盖回合中，服务调用已经进入稳定状态的快路径记录为
`0.274–0.345s`；覆盖回合的 `fastAvailable=false` 是服务失败后的 fail-closed
结果，不能作为成功推理速度的对照。

## 结论

快感知只接收一张当前页面截图；`63s` 不是截图推理耗时，也不是 586 个
proposals 造成的模型上下文耗时。原始 `fastLatency` 是外层快路径总耗时，首次
调用包含懒启动视觉服务。Settings trace 已增加 `FastServiceStartupLatency`、
`FastDecodeLatency`、`FastInferenceLatency` 和 `FastInferenceSucceeded`，后续
运行会把这几段分开落证。

最终构建的真实 DSH 复验进一步确认同一结论：`environment-preflight.json` 标记
预热成功，预热 `63.233s`；首帧快路径 `0.721s`，其中 PNG 解码 `0.055s`、
当前截图推理 `0.664s`，`fastServiceStartupLatency=0`。
