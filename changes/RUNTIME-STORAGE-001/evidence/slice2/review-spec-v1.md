# RUNTIME-STORAGE-001 Slice 2：Spec 评审

结论：0 项缺失、部分实现、范围膨胀或错误实现。

固定输入已核：base 为 `a05176f3c66e0a3206f49dde463f51df1335975c`；`candidate-v1.patch` 恰覆盖清单 16 路径各一次，候选快照及当前工作区均与 `candidate-inputs-v1.json` 的 16 个 SHA 一致。

- B1：`WorldHistory.cs:28-33` 定义 `IAsyncWorldJournal`；`WorldModel.HistoryRecording.cs:56-126` 使同步桥与异步入口共用候选、确认、发布和失败锁存，确认前恢复旧 Owner，并以单门拒绝重入。
- B2：`UniKernel.cs:373-405,657-676,721-742` 和 `KernelRunDriver.cs:1178-1180,1369-1370,1915-1916` 已把观察、冲突、动作后导航/视口、contract demand 与驱动等待接到 async World；观察门和既有 Drive 门保持在等待期间生效。
- B3/B4：`FileWorldJournal.cs:96-147,190-194` 以原生异步写入、flush、head 确认和异步关闭实现 16MiB 可配单帧上限、零等待准入、v2 delta/v1 readonly。`host-final/kill` 显示提交后 ack 前 Kill、Owner seq=2、SourceRows=3，两个新 reader 保留一条 revision 与来源 SHA；`host-final` 新增 4 case 全通过。旧 649/1205/1208 失败已在对应 baseline 重现，不计为本候选新失败。
- B5：改动只涉及 World async、两条 Host await 接线及其契约/文件/实际 Kill 证据；初始化和其他存储仍同步，未引入队列、合批或跨操作事务。
