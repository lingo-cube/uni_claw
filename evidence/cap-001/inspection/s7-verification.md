# CAP-001 Inspector Contract Verification (S7)

| level | method | expected | actual | evidence |
|---|---|---|---|---|
| DETERMINISTIC | `dotnet test tests/UniClaw.Kernel.Tests/UniClaw.Kernel.Tests.csproj --filter 'FullyQualifiedName~Capability' --no-restore` | 语言 Inspector 输入、Finding 输出和缺失/不可用/部分覆盖/错配/迟到状态通过 | 35 passed, 0 failed | `tests/UniClaw.Kernel.Tests/Capability/LanguageInspectorContractTests.cs` |

协议只定义 ObservationAccepted 的只读文本投影、规则版本、期望语言、覆盖状态、输入状态和 correlation；`declaredText` 与 `renderedText` 保持分列，并保留 occurrence/source reference。`CreateFinding` 只构造已有 Finding，不执行语言算法，也不写入 Evidence、WorldModel、Assurance 或 Effect。
