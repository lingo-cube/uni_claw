# Evidence观察异步批量：首片验收

本片完成，父RUNTIME-STORAGE-001仍implementing；只验S1–S4，不代替八领域A1–A4。

| 项 | method | expected | actual | evidence |
|---|---|---|---|---|
| S1 | 公开Evidence Owner与Kernel/Driver契约 | 同批/跨批lineage、duplicate/rejected顺序保持，确认前不可见 | 9例通过；提交后ack失败锁存；跨批历史parent可解析 | kernel-final.trx；ledger-red.log；candidate-source-v2/ |
| S2 | 生产文件adapter、原reader及真实managed I/O计数 | 原v1兼容，数量/字节有界，实际write/flush减少 | 6例通过；默认128条/1MiB分块；64条frame/head的write/flush各64→1，编码总字节相同 | host-final.trx；host-final/io-counts.json |
| S3 | Driver延迟确认、失败、取消及重入契约；正式Host回归 | 未确认不咨询/投递/重拉；同Owner串行；生产入口await | Driver契约通过；Host288/288、DSH24/24；原同步兼容路径明确保留 | kernel-final.trx；host-final.trx；dsh-final.trx；candidate-verification-binding.json |
| S4 | 真实非terminal子进程Kill、两个新reader、哈希检查、独立双轴review | 提交后ack前Kill不丢原记录；发布前World空；原来源不改写；无新增未处置问题 | 1次Kill，2个新进程恢复4条原admission；ready发布数0/World空；原payload与SHA保持；两轴均0；verify通过 | host-final/kill/；review-summary.json；verification.json |

全量Kernel1195通过/1198；同3失败在入口643份源码和相同资源配置复现。10个原有其他源码README登记缺失未顺手修改。入口源码归档baseline-entry-source.tar.gz与baseline-archive.json保留复现载体；测试中间失败与v1候选保留。

只证明managed FileStream实际调用减少和进程Kill后可读；未测物理磁盘syscall、整体延迟或断电持久性。无通用共享排队、数据库、自动续跑或路径优化；其他领域与若干初始化/取消/完成路径仍同步。631非本片构建输入、前片239封存项保持不变，无提交推送。

下一步处理World的外层操作delta提交与发布等待链，随后迁移其他领域；以真实可合并记录证明收益，不合并跨Effect/Agent屏障的因果记录。
