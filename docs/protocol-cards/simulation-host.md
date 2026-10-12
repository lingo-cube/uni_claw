# 协议卡片 · simulation-host（生成产物）

> 派生自 `schemas/protocol/state-machine.yaml` 边表（PROTOCOL-MACHINE-001
> D8 第四出口，纯派生视图——权威不搬家）；手改会被 `--check` 抓出。

## 入边（对端 → 本组件）

| id | kind | status | 方向 | 载荷 | 基线锚 |
|---|---|---|---|---|---|
| P26 | edge | keep | uni-kernel → simulation-host | record UniClaw.Kernel.Trace.RunTraceArtifact | #### P26 Sealed Trace → ScenarioStimulus（测试 / 治理侧，非产品内边） |
