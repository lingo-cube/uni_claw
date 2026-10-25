# Slice 5 Spec review v1

范围：固定 base `a05176f3c66e0a3206f49dde463f51df1335975c` 的 entry→candidate-v1 补丁；20/20 候选路径 SHA 与 `candidate-inputs-v1.json` 一致。未运行构建、测试、网络或设备操作；提交列表为空。

## 结论

缺失/部分：**0**；越界：**0**；看似实现实际错误：**0**。

`E1` 的“确认前零新咨询/投递、不发布binding/新序列”由资源 await 及确认后更新状态落实（`KernelRunDriver.Resources.cs:44-60,87-99`）；`E2` 的“reserve未确认零咨询；结果未确认零采纳”由 reserve/complete barriers 落实（`KernelRunDriver.cs:3108-3125,3172-3189`）。

`E3` 的“prepare未成功零delivery；Unknown/Failure不发送；结果失败仍返回实际Receipt”由 `DispatchAsync` 的 prepare-await、非 Success 拒绝和追加失败锁存实现（`EffectBoundary.cs:228-285`），并保留同 Owner 重入拒绝（`167-175`）。

`E4` 所称“原生async/关闭/字节边界、旧读回”在三文件适配器的 `WriteAsync/FlushAsync`、关闭门及原格式 replay 中可见（`FileRunResourceJournal.cs:58-85,215-221`；`FileConsultationJournal.cs:48-64,115-128,158-164`；`FileExecutionJournal.cs:87-103,173-204,206-237`）。执行严格保持“prepare保持独立提交”及“submission＋receipt…共同写入/flush”（规格 `resource-consult-execution-group.md:8`；`FileExecutionJournal.cs:118-119,173-195`），最多两帧和 16MiB 默认上限。

归档初始化、恢复游标及全域收尾仍留 C 组（规格 `:9`），未作为源码缺陷；最终回归和封存进行中亦不构成此候选的 Spec 缺陷。
