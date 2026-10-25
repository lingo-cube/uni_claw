# 原生异步观察批量证据

范围是state.md“当前实施片”的S1–S4，不是全域A1–A4。父change继续implementing。
`IAsyncEvidenceJournal → EvidenceLedger.AdmitBatchAsync → UniKernel.ProcessBatchAsync → Driver观察pull/DriveCore → DriveAsync → 两个正式Host与CLI/HTTP入口`已贯穿。同步兼容入口明确保留；其他领域、初始化、completion/attempt reflux和cancel证据仍有同步写入。

最终选择`candidate-inputs-v2.json` / `candidate-source-v2/` / `candidate-v2.patch`固定22文件的精确dirty入口增量，包含前一轮已接受的ADR-0055。v1及其两轴M保留，v2已修复全历史复制和等待边界；没有新提交，不能用HEAD diff替代本片入口。

- `ledger-red.log → ledger-first-green.trx`：先缺公开异步原来源缝RED，再由原Owner一次准备/确认/发布。`ledger-driver-contracts.trx`的8例覆盖未确认不可见、同批parent/derived/duplicate/rejected、调用者改集合、写前/提交后ack失败锁存、取消前不入队、Driver等待/重入/不重拉/不咨询/不投递。
- `file-red.log → file-first-green.trx`：真实文件批量。原v1 frame与head格式兼容；每个编码chunk默认最多128条/1MiB，超大单帧拒绝。先前chunk可已提交，整个观察仍不发布，不伪装批次回滚。
- `host-final.trx`：v2的288通过，包含当前6例文件契约（初次`file-contracts.trx`只有5例；随后新增partial case已由最终288覆盖）。`host-final/io-counts.json`：64条逐条写与批量写字节相等，扣除初始化后frame write/flush、head write/flush各从64次变1次；单位是实际managed FileStream调用，不是物理磁盘syscall或延迟加速证明。
- `host-final/kill/`：v2实际非terminal进程在文件批次提交后、Evidence Owner获ack前被Kill；ready中已发布admission/canonical均0、World为空、来源已有4条。两个新进程沿FileRunRecoveryArchive.RestoreEvidence读取原4条（2 canonical，含duplicate/rejected/derived），完整原payload与原文件SHA保持。没有执行恢复。
- `kernel-final.trx`：最终v2的1195通过、3失败/1198总计，包含新增第9例跨批历史parent。`kernel-all.trx`与`kernel-all-v2.trx`是补第9例前的1194/1197历史尝试；不代表最终候选。`baseline-three-retry.trx`在入口643份源码、相同资源配置下复现同3失败。前一个baseline载体漏了embedded producer-trust.json而未编译，失败日志保留。已补本片Evidence目录登记；`readme-current.trx`仅保留10个原有其他文件登记缺失，不声称整个Kernel全绿。
- `dsh-final.trx`：最终v2的24通过，含SettingsCoverage事件场景与RuntimeToolHost procedure；CLI/HTTP生产入口已改await异步Host入口，无设备/模型运行。`test-summary-final.json`与`candidate-verification-binding.json`明确绑定最终22候选、TRX与当前程序集指纹，旧尝试不混用。

`review-summary.json`记录最终Spec v2和Standards v3均0；初始问题及修订记录保留。`verification.json`为最终exit0，核固定候选、631非本片构建输入、RECOVERY016封存239项、当前契约/基线差异、真实Kill/双reader/SHA及I/O计数。`acceptance.md`逐项列出本片四元组；`state-snapshot.md`保存完成时父change状态，`final-artifacts.json`只封存本片，不锁死后续仍需演进的父state。

`baseline-entry-source.tar.gz`保留全部643入口构建输入，`baseline-archive.json`绑定哈希；临时入口验证目录已清理。`reproduce.py`只在新目录复验，不覆盖本片旧结果。父Change的全域批量、背压队列和初始化迁移尚未完成；没有数据库、续跑、路径优化或提交推送。
