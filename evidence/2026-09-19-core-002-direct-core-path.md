# CORE-002 阶段 3：直接 Core 路径验证

日期：2026-09-19

## 变更边界

- `UniClaw.Core` 保持领域无关，零 `ProjectReference`。
- `UniClaw.Kernel` 直接引用 `UniClaw.Core`，投影实现归属 `UniClaw.Kernel.Core`。
- `UniClaw.Agent` 仍只引用 `UniClaw.Kernel`。
- 删除 `src/UniClaw.CoreAdapter` 项目、源码和解决方案条目；不复制第二套投影。
- 不迁移 Kernel 的 UI、设备、Runtime、Trace 或 Harness 类型，不冻结 Core 最终字段和继承树。

## 验证命令与结果

| 检查 | 结果 |
|---|---|
| `dotnet test tests/UniClaw.Core.Tests/UniClaw.Core.Tests.csproj --no-restore --logger 'console;verbosity=minimal'` | 3/3 通过 |
| `dotnet test tests/UniClaw.Kernel.Tests/UniClaw.Kernel.Tests.csproj --no-restore --filter 'FullyQualifiedName~CoreProjectionSeamTests\|FullyQualifiedName~ProductHostClosureTests' --logger 'console;verbosity=minimal'` | 9/9 通过（seam 6、闭包 3） |
| `dotnet test UniClaw.Kernel.slnx --no-restore --logger 'console;verbosity=minimal'` | 520/520 通过（Core 3、Agent 17、Kernel 388、Simulation 112） |
| `rg 'CoreAdapter' src tests UniClaw.Kernel.slnx` | 预期无产品/测试引用；历史 evidence 与 state 记录除外 |

NU1900 是 NuGet 漏洞缓存目录权限警告，不影响构建和测试执行。

## 语义结论

投影仍是 realization 到 Core 的可追溯转换，不拥有第二套 World 事实；删除独立
Adapter 后，转换 Owner 与 Kernel 当前 realization 同处一个程序集边界。该收口只
修订依赖闭包，不代表 UI realization 已完成源码拆分或 Core 已成为最终生产 API。

## 尚待验证

- 第二种非 UI realization 的直接 Core 消费仍未实现；后续用实际模型和场景反驳本基线。
- 完整 reconciliation replay 仍是后续验证项。
- `CoreInvariants.ProducesWorldResult` 的结构性不变量替换仍需单独 Change，不在本轮扩展。
