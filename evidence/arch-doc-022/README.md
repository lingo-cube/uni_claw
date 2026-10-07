# ARCH-DOC-022 证据 — src/ 维度 README 登记执法

## A1 — Core/Agent.Dsh README 补齐

- `src/UniClaw.Core/README.md`（1 文件：候选词汇集定位）
- `src/UniClaw.Agent.Dsh/README.md`（12 文件：DSH realization adapter）

## A2 — 执法测试

`tests/UniClaw.Kernel.Tests/SourceReadmeOwnershipTests.cs`：26+2 归属条目
（硬编码映射，变更须经 change）；两条执法线（目录有条目 + stem 点名）；
obj/bin 豁免。

首跑即拦截真实漂移（见 enforcement-test.txt）：

- 映射表漏 `src/UniClaw.Agent/Goal|Evaluation` 两子目录（本 change 修正）；
- 并行会话 5 个新文件未登记：`RuntimeToolHost`（Host.Dsh）、
  `LanguageInspection*` 三件（Host/Capability，CAP-012/013）、
  `CapabilityProfile`（Kernel/Capability，CAP-012 D7）——已按头注释
  登记进归属 README。

修复后复跑：**通过（156 源文件受检）**（enforcement-test-pass.txt）。

## A3 — 负向探针

临时放置未登记文件 `src/UniClaw.Kernel/Trace/ProbeTemporary.cs` → 测试
RED；删除后复绿（negative-probe.txt）。执法真实有效。

## A4 — 全量

```sh
dotnet test tests/UniClaw.Kernel.Tests
# actual: 已通过! 846/846（kernel-tests.txt；此前并行在途红已由其自行修复）
```

## 文件

- `enforcement-test.txt` / `enforcement-test-pass.txt` — 首跑拦截与修复后通过。
- `negative-probe.txt` — 负向探针 RED→复绿。
- `kernel-tests.txt` — 全量 846/846。
