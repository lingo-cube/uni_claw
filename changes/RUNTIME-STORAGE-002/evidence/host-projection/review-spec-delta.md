# Host projection Spec review delta

范围：仅复核 `review-delta.patch` 对原 Spec 报告两项 P1 的处置；未修改原 `review-spec.md`、源码或测试，也未运行构建/测试。

`candidate-v2.json` 的五个 SHA-256 均与当前候选源一致。

## Findings

0 项。

原 P1-1 已处置：两条变更入口在写入前对 `Revision` 与 `LastEventSequence` 使用 `checked` 递增；`long.MaxValue` 的 Transition 与 UpdateArtifacts 回归均断言抛出、快照字节不变、旧 latest 仍可读取且未创建 revisions 目录。路径：[RuntimeRunStore.cs](/Users/fran/Documents/Code/spacex/uni_claw/src/UniClaw.Host/Runtime/RuntimeRunStore.cs:162)、[RuntimeRunStore.cs](/Users/fran/Documents/Code/spacex/uni_claw/src/UniClaw.Host/Runtime/RuntimeRunStore.cs:203)、[RuntimeRunProjectionVersionTests.cs](/Users/fran/Documents/Code/spacex/uni_claw/tests/UniClaw.Host.Tests/RuntimeRunProjectionVersionTests.cs:184)。

原 P1-2 已处置：版本双向还原使用的请求现在为 `ProjectRef`、`TestSetRef`、`TaskRef`、`Environment` 提供非空且异形 JSON；每个 revision 的 forward/reverse 均与原 projection 全 JSON 比较。路径：[RuntimeRunProjectionVersionTests.cs](/Users/fran/Documents/Code/spacex/uni_claw/tests/UniClaw.Host.Tests/RuntimeRunProjectionVersionTests.cs:14)、[RuntimeRunProjectionVersionTests.cs](/Users/fran/Documents/Code/spacex/uni_claw/tests/UniClaw.Host.Tests/RuntimeRunProjectionVersionTests.cs:48)。

已提供的独立 Host 证据为 32/32；其 scope 明确为隔离的实际 Host 模块，联合全项目构建仍由后续验收负责。
