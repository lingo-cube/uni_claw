# 协议卡片 · memory-system（生成产物）

> 派生自 `schemas/protocol/state-machine.yaml` 边表（PROTOCOL-MACHINE-001
> D8 第四出口，纯派生视图——权威不搬家）；手改会被 `--check` 抓出。

## 出边（本组件 → 对端）

| id | kind | status | 方向 | 载荷 | 基线锚 |
|---|---|---|---|---|---|
| P21 | edge | track | memory-system → control-loop | — | ### P21 Memory Recall（placeholder，Memory System → Agent / Control / World Model） |

## 入边（对端 → 本组件）

| id | kind | status | 方向 | 载荷 | 基线锚 |
|---|---|---|---|---|---|
| P20 | edge | track | uni-agent → memory-system | — | ### P20 Memory Persist（placeholder，→ Memory System） |
