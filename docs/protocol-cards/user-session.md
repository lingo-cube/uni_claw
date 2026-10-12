# 协议卡片 · user-session（生成产物）

> 派生自 `schemas/protocol/state-machine.yaml` 边表（PROTOCOL-MACHINE-001
> D8 第四出口，纯派生视图——权威不搬家）；手改会被 `--check` 抓出。

## 出边（本组件 → 对端）

| id | kind | status | 方向 | 载荷 | 基线锚 |
|---|---|---|---|---|---|
| G2 | gap | track | user-session → uni-kernel | — | 任务定义（交互模型前奏 0a：Task Catalog，无编号边） |
| O1 | edge | track | user-session → uni-kernel | schema v1 | O1 · task-launch-request（调用方 → Runtime HTTP；交互模型前奏 0b） |
| O10 | edge | keep | user-session → uni-kernel | schema v1 | O10 · capability（五类只读 capability 请求/响应公共封套） |

## 入边（对端 → 本组件）

| id | kind | status | 方向 | 载荷 | 基线锚 |
|---|---|---|---|---|---|
| O2 | edge | track | uni-kernel → user-session | schema v1 | O2 · task-launch-ack（Runtime 铸造；交互模型前奏 0b/0c） |
| O3 | edge | track | uni-kernel → user-session | schema v1 | O3 · launch-stage-record（阶段记录：valueOrigin + availability 分离） |
| O4 | edge | keep | uni-kernel → user-session | schema v1 | O4 · runtime-run-event（source-native 事件流：DSH / Kernel / 设备 / Runtime） |
| O5 | edge | keep | uni-kernel → user-session | schema v1 | O5 · runtime-run-projection（Runtime Run 查询投影，唯一 authority：Runtime Host） |
| O6 | edge | keep | uni-kernel → user-session | schema v1 | O6 · runtime-run-response（单 Run 查询响应） |
| O7 | edge | keep | uni-kernel → user-session | schema v1 | O7 · runtime-run-events-response（事件列表响应，nextCursor 分页） |
| O8 | edge | keep | uni-kernel → user-session | schema v1 | O8 · runtime-run-list-response（Run 列表摘要投影） |
| O9 | edge | keep | uni-kernel → user-session | schema v1 | O9 · run-report（全链路单 run 报告只读投影，PNL-006） |
| O11 | edge | keep | uni-kernel → user-session | schema v1 | O11 · query-error（结构化查询错误，不返回裸字符串） |
| O12 | edge | keep | uni-kernel → user-session | schema v1 | O12 · record-envelope（跨来源记录公共封套） |
| O13 | edge | keep | uni-kernel → user-session | schema v1 | O13 · local-storage-namespace（不透明逻辑 namespace 引用） |
| P19 | edge | track | uni-agent → user-session | — | ### P19 Goal Evaluation（UniAgent → user/session，未来） |
