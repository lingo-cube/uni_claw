# SIM-016 Host Truth 增量评审

## 结论

本轮 Standards 与 Spec 检查未发现阻塞项。Host buyer 测试迁入 `UniClaw.Host.Tests` 后，SIM-016 以同一次 Host.Tests 执行生成的 TRX 和二进制 Trait 与 Simulation.Tests 结果共同驱动场景覆盖；保持 Host 与 Simulation composition root 分离，也没有触碰产品代码、旧场景预期或冻结组件闭包。

## Standards

- `scenario-coverage.py` 保留 `scenario_test_map(ids, no_build=...)` 与 `parse_trx(Path)` 用法；默认 discovery 对两 buyer root 各做一次 warmup，后续每个场景按 Trait 过滤并 `--no-build` 枚举，保留完整展示名供精确 TRX join。跨 root 同名 FQN 和同一 TRX 冲突均拒绝，不静默覆盖。
- CLI 支持重复 `--trx`，`--run` 对两个 buyer 各执行一次；默认要求两边都有 TRX。合并结果缺失时不会给场景判通过。freshness 检查包含 Host 源、项目文件、Host.Tests 源/项目文件及既有 Kernel/Agent/Simulation/场景输入，并排除生成的 `bin/obj` 内容。
- `verify-change --scope full` 在既有 Host.Tests 执行上追加 TRX logger，并把它与 Simulation TRX 一并传入 coverage；没有为 coverage 重跑 Host.Tests。quick 路径仍清楚标注其为场景子集，未冒充全库覆盖。
- 新增工具测试覆盖双根完整 FQN、Trait 重名、TRX 重名、Host 结果缺失、重复/默认 TRX 要求、Host freshness 与 full wiring；实现上未见降低缺失结果拒绝强度。

## Spec

- 新 Runtime/提取场景由 Host.Tests 承载，符合公开 Host 分类 buyer 的 composition root 边界；共享 `ReplayCompositionGuard.cs` 以 Compile Link 复用测试侧内容门，不复制 Runtime 或 Host 产品实现。Simulation 项目仍保留原 Kernel + Agent 引用组合。
- 来源 hash、capture/trace 关联、录制前缀范围、受控替代输入和 post-CANCEL 合成限制沿用既有声明；classifier 原规则及 Default apps 已知语义误判均未修改或被重新标为已解决。SIM-017 仍为候选设计，不构成此增量的产品行为依据。
- Host.Tests 真实执行产生的 TRX、同一二进制 Trait 发现与 freshness 构成完整 carrier/result 链；三个场景的隔离状态、派发和咨询断言仍由原场景/oracle承载，没有看到放宽既有场景验收。

本次为静态增量评审；依 WorkItem 未构建、未运行测试。

## 全量尾部修正复核

针对 attempt2-full 暴露的成功结尾 `NameError`，已静态复核 `scenario-coverage.py` 改为从 `trx_paths` 输出所有输入 TRX 名称；同文件无其他成功路径引用旧单一 `trx` 变量。新增 `test_cli_reports_both_trx_files_and_exits_success_after_valid_join` 覆盖 CLI 返回 0 且报告同时显示 sim/host TRX。该修正与重复 TRX 汇总语义一致，未影响场景匹配或产品行为。本段为静态复核，未重跑测试；18 个工具测试及全量结果以执行方记录为准。
