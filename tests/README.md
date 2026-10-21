# tests/ 测试代码导航

`tests/` 保存可执行测试代码和测试 harness；静态测试数据位于 `testsets/`、`scenarios/`，运行所得证据位于 `evidence/`。三类内容分开维护：fixture 是输入或替身，不能作为真实运行证据；证据也不是可执行测试。

## 测试项目

solution 当前包含 8 个测试项目：`UniClaw.Agent.Tests`、`UniClaw.Agent.Dsh.Tests`、`UniClaw.Core.Tests`、`UniClaw.FileSystemRealization.Tests`、`UniClaw.Host.Tests`、`UniClaw.Host.Dsh.Tests`、`UniClaw.Kernel.Tests`、`UniClaw.Simulation.Tests`。可从 [solution 文件](../UniClaw.Kernel.slnx) 查看完整项目路径；按被测程序集名定位同名项目即可。

`UniClaw.Simulation.Tests` 组合测试侧 Simulation Host 与确定性场景；它不代表生产 Host 有仿真默认档。场景数据和认证期望分别从 [scenarios](../scenarios/README.md) 与 [simulation baseline testset](../testsets/simulation-baseline/manifest.json) 进入。少量项目另有 `Fixtures/` 目录，例如 `UniClaw.Agent.Dsh.Tests/Fixtures/`；这些是测试代码消费的静态样本。

## 验证范围

[`tools/verify-change`](../tools/verify-change) 的 `--scope full` 覆盖上述 8 个测试项目、两组架构 tripwire、scenario 认证检查和 Simulation TRX 覆盖率；Simulation 只执行一次并共享结果。`--scope quick` 只跑架构 tripwire 和 manifest 选出的 Simulation 场景子集，跳过其他测试项目与覆盖率。`--live` 才额外运行设备环境 gate。命令的成功状态仅对实际选中的范围成立；未运行就不能声称通过，也不能把 quick 说成全仓全绿。

Workspace JavaScript 测试位于 canonical 源目录 [`web/uniclaw-workspace/tests/`](../web/uniclaw-workspace/tests/)；DSH 插件自己的测试位于各 `dsh/*/tests/`。它们不在 .NET solution 的 8 个测试项目清单中，也不会仅因上述 full 范围通过而自动获得通过结论。

## 数据与证据

- `tests/`：断言、harness、测试替身和局部 fixture；测试替身的结果只证明替身设定下的行为。
- [`testsets/`](../testsets/README.md)：输入数据与期望集合，如图片、XML、manifest 和性能基线。
- [`scenarios/`](../scenarios/README.md)：可回放场景输入；场景期望经认证工具检查。
- [`evidence/`](../evidence/README.md)：已发生的验证输出、评审记录、诊断和裁决；每条证据应带命令/路径/摘要，历史证据不因新运行而被覆盖。

## 相关入口

- [源码入口](../src/README.md)
- [验证工具入口及范围细节](../tools/README.md)
- [跨语言契约](../schemas/README.md)
