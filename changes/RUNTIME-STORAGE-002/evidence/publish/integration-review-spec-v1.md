# Private-index integration Spec review v1

范围：只读审查 `draft-run.json` 的当前私有索引、新增依赖清单及其选中文件；未改工作树或 index，未构建/测试，未重审既有 Storage/Host/Python 机制。

## Findings

0 项。

闭合判断：

- SIM-019 的 `OverlayObservation.cs`、`PageObservation.cs`、`WorldModel.Overlay.cs` 已在私有索引中，且分别与 `closed-overlay-base.json` 的纯输入 pin 一致；`WorldModel.cs` 保留该闭合协议所需的 reconcile、current-overlay 与 grounding 路由。它替代了早期 blanket Overlay 排除，不是从当前 dirty source 补入半协议。
- `RunRecoveryRead`、`RunEvidenceRecoveryRead`、`RunWorldRecoveryRead`、`RunRecoveryReader` 与 `FileRunRecoveryArchive` 同时进入，保持「Host 选择/释放 source，Kernel 解释身份与历史」边界；未加入 Drive、activation、current World restoration 或 continuation。
- `RunResourceRecoveryTests` 的原 fixture、Accounting/World recovery 的既有闭合 Overlay 输入、原始归档/真实 XML/全局集合输入及 recovery probes 都在选中闭包内；没有通过删除断言、阈值或旧 Overlay fixture 输入换取更小测试面。
- `TraversalExecutionTests` 只有 `partial` 与可注入 `RunModel`/`ControlLoop`/policy/`IRuntimePlanJournal` 的测试缝；未带入 parallel candidate/condition。`KernelRunDriver.cs` 与 `UniKernel.cs` 仍不含 `ProjectExpectedEffect`、`authoredExpected`、`OverlayDismissed`、Overlay evidence 或 Act-scope 字段；这些 sibling 继续排除。

本次绑定的当前私有 tree 为 `b4ed4d4e692bffec5f8b9326938381b28a422093`。已核对的选中 SHA-256：

| 文件 | SHA-256 |
|---|---|
| `src/UniClaw.Kernel/World/OverlayObservation.cs` | `a1f0ae9186eca8b2c5464f81cc301074c195150ec55010191357e0f23bcd12dd` |
| `src/UniClaw.Kernel/World/PageObservation.cs` | `a5d1b6bd5fc96bc07d8bb88bf2b3bfd8c70a9a3ff1e560c65037353c55a2a032` |
| `src/UniClaw.Kernel/World/WorldModel.Overlay.cs` | `f0483dd92d62d34d48571be700d4aec48f174d22657466dfd4526258d749434d` |
| `src/UniClaw.Kernel/World/WorldModel.cs` | `2b519e769187c5d30a46ad9caec3ceb921bfb8b890cfbd5005b49cea6ef1ab08` |
| `src/UniClaw.Host/SettingsCoverage/SettingsCoverageRunner.cs` | `1bcd1609de566fb4a7b506717534f4968bdf8502a404596d0c1090c660a083c3` |
| `src/UniClaw.Kernel/Runtime/RunRecoveryRead.cs` | `8ee79990e5d005f1588e67f870ec033657c715cdfc8354ce620605325517a7aa` |
| `src/UniClaw.Kernel/Runtime/RunEvidenceRecoveryRead.cs` | `368d6a200e17421e9db05921af85e6570bb57370c99158751d88e8459b832f05` |
| `src/UniClaw.Kernel/Runtime/RunWorldRecoveryRead.cs` | `e7ad70552211eba888a2a71f37daba23edb5c0588c6b9f4aab2eb1b1ae5f375d` |
| `src/UniClaw.Kernel/Runtime/RunRecoveryReader.cs` | `ca00dac41f00c9637e593172b8f3c58d51b9159b69b3775038678f3334626404` |
| `src/UniClaw.Host/Runtime/FileRunRecoveryArchive.cs` | `0f28ea6c5d565fbe3bae6ecd2a0c45b6f8836db39c054e8cf1c9b3a4e6b44256` |
| `tests/UniClaw.Kernel.Tests/Runtime/TraversalExecutionTests.cs` | `fc5ba7855937bd23e498183772cef76ed57b5556d6db2cd3b9f06d3e03208415` |

此结论只覆盖新增集成选择。Coverage 语义和最终联合构建仍由后续独立验收决定。
