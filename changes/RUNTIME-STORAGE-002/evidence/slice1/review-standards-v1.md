# Slice 1 Standards Review

范围：仅比较 `entry-source` 与 `candidate-v1.patch` 所列 13 个文件；未以工作区 HEAD 差异或后续 A3–A10 缺口作为本片问题。未运行测试。

## 硬规范违规（1）

1. **P1：诊断观察的字节配额缺失，实际可无界地序列化/写出。** `DiagnosticObservationOptions` 只限制记录数和两个等待时间（`candidate-v1/src/UniClaw.Kernel/Runtime/DiagnosticObservations.cs:8-10`）；队列实际只按条数接收（`:44-45,59-66`）。但两个 Host 回调分别把未设大小上限的 `consultations` 和 `facts` 整体 `TryJson` 后直接 `File.WriteAllTextAsync`（`candidate-v1/src/UniClaw.Host/HostRunner.cs:269-274`；`candidate-v1/src/UniClaw.Host/SettingsCoverage/SettingsCoverageRunner.cs:331-333`）。因此单个超大上下文/恢复事实即使队列容量为 1，仍能占用任意内存和磁盘；`AuxiliaryArtifactLimits` 不能覆盖这两条路径。应把诊断回调的 payload/累计字节限额纳入同一 Host 配置与状态，并在超过限额时记录明确 dropped/error，而非只依赖操作超时。

依据：Change Decision 7 要求辅助面具有独立错误、**配额**、等待和关闭策略（`changes/RUNTIME-STORAGE-002/state.md:32`），A1 要求每个产物声明配额（`:41`），契约要求辅助采集暴露限额且避免无限积压（`storage-contract.md:46,66,74`）。ADR-0055 同样要求批量具备数量、字节和等待上限（`docs/adr/0055-async-batch-storage-requires-confirmed-commit.md:9`）。

## 判断性 smell

0。未发现本候选引入且未被仓库规则覆盖的命名、重复、越权或中间人问题。

结论：1 个硬规范问题；修复字节限额后再进入本片 Standards 通过判断。
