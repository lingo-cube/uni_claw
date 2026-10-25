固定候选：17 路径 SHA 与清单一致，patch 仅含此 17 路径；基线
`a05176f3c66e0a3206f49dde463f51df1335975c`，未审全树。

Missing/partial：0。C1 的确认后发布、C2 的确认后 handler/取消结算、C3 的
frame→flush→head/锁存及 C4 真实 Kill 双 reader 均与实现和证据一致。

Scope creep：0。仅 Run Owner、Kernel/Driver await、文件 journal、两 Host
调用点及其测试、探针、README；无其他存储/初始化迁移、队列、DB、压缩、续跑或额外合批声明。

Looks implemented but wrong：0。单 Owner 拒绝、读端不完整尾部拒绝均成立。C5 的独立
Spec 轴通过；最终验收由 Leader 汇总。
