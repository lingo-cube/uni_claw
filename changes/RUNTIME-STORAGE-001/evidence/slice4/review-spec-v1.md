# Slice 4 Spec review v1

范围已固定：base `a05176f3c66e0a3206f49dde463f51df1335975c`；`candidate-v1.patch` SHA256 `81760ff…4976e37`、inputs SHA256 `ce898f…87795` 均与 binding 一致，patch 恰为 inputs 的 20 路径，逐一路径当前 SHA 一致。未审整棵 dirty tree；D5 所列 DSH 并发文件不在候选，未归入本片。

**Control — 0 项 Missing/partial/wrong。** D1 要求的候选→await→发布、同 Owner 拒绝重入及失败锁存见 spec L5/L10；实现把所有外层入口接到 `ChangeAsync`（`TraversalControl.History.cs:69-122`），在 append 确认后才 `Load`（L117-119），Driver 在 select await 后、发布 work 前检查取消（`KernelRunDriver.cs:2380-2397`）。

**Plan — 0 项 Missing/partial/wrong。** D2 顺序要求（spec L11）由 `BeginRuntimePlanReplacementAsync` → `AbandonPausedPlanAsync` → `CommitRuntimePlanAdoptionAsync` 串行 await（`KernelRunDriver.cs:1003-1026`）；adopt 记录确认后才设置执行 Plan。

**D3–D5 — 0 项 scope creep / looks implemented but wrong。** 两 adapter 仅共享 Host 内部 `FileCommittedHistory<T>`，仍各保留 schema/严格 reader（`FileControlTraversalJournal.cs:8-21`；`FileRuntimePlanJournal.cs:8-21`）；真实 WriteAsync/FlushAsync/head 替换、写前取消/限额和 close 等待在 `FileCommittedHistory.cs:81-135`，无 Task.Run、队列或跨 Owner 事务。D4 的双 Kill/每种两个 reader 是新增 Host 测试的同源 probe 路径（`ControlPlanAsyncStorageTests.cs:133-178`）。

结论：候选实现与 `control-plan-async-group.md` D1–D5 及 Group A 独立 Owner/仅物理机制共用的计划（`grouped-storage-plan.md:2-10`）一致；本报告仅作静态 Spec 审查，未重跑测试或替代其验证证据。
