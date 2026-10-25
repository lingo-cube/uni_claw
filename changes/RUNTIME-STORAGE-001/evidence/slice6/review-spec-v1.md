# Slice 6 Spec Review v1

固定基线 `a05176f3c66e0a3206f49dde463f51df1335975c`。只审 `candidate-v1.patch`：`entry-source`→`candidate-source-v1`、补丁及 `candidate-inputs-v1.json` 均为 23 路径；23 个候选 SHA 全匹配。未运行构建、测试、live 或网络。

## 缺失/部分（0）

F1 要求“归档未确认零咨询/投递、无ResourceBinding”（`archive-init-close-group.md:14`）；候选先 await 归档再发布 binding（`candidate-source-v1/src/UniClaw.Kernel/Runtime/KernelRunDriver.Resources.cs:50-61`）。F2 的“初始化完成前不返回可用Owner/来源”（同文件:15）由文件 factory 与 World factory 实现（`candidate-source-v1/src/UniClaw.Host/Runtime/FileEvidenceJournal.cs:38-43`、`.../FileWorldJournal.cs:36-41`、`candidate-source-v1/src/UniClaw.Kernel/World/WorldModel.HistoryRecording.cs:28-42`），保留同步入口/只读读取。F3 的“裁决不绕Drive门”（同文件:16）由 `ProcessBatchAsync` 与同一门满足（`candidate-source-v1/src/UniClaw.Kernel/Runtime/KernelRunDriver.cs:3509-3529,3547-3575,4050-4059`）。

## 越界（0）

未触及 P24 控制面、历史兼容读取、队列、DB、续跑或路径优化；九路径统计仅覆盖既有九个生产 writer，不新建存储权威（`candidate-source-v1/tools/RunRecoveryProbe/StorageClosureAsyncProbe.cs:83-106`）。

## 看似实现、实际错误（0）

F4 的正式 Host 输出和观察者均 await（`candidate-source-v1/src/UniClaw.Host/HostRunner.cs:171-183,266,360-451`；`.../SettingsCoverage/SettingsCoverageRunner.cs:197-222,329,524-631`）。九路径数字明确区分 marker、语义记录、managed write/flush、head 与 batch，非磁盘 syscall/性能承诺（`candidate-source-v1/tools/RunRecoveryProbe/StorageClosureAsyncProbe.cs:91-105`；`candidate-source-v1/tests/UniClaw.Host.Tests/StorageClosureFileTests.cs:47-58`），符合 F4 的“清楚区分”。F5 的父 A1-A4 仍待最终验证；进行中的回归/封存元数据不作源码缺陷。

**计数：缺失/部分 0；越界 0；看似实现实际错误 0。**
