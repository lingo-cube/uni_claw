# src/ 产品源码导航

本目录放 .NET 产品运行时程序集。Target Product 的语义拓扑见[产品架构基线](../docs/architecture/product-architecture-baseline-l0-l3.md)；仓库职责边界见[仓库职责基线](../docs/architecture/repository-role-boundaries-v0.1.md)。程序集是实现组织，和 Product T1/T2 责任域不是一一对应关系。

## 程序集入口

- `UniClaw.Core/`：跨模块的语义投影；从 [Core README](UniClaw.Core/README.md) 开始。
- `UniClaw.Kernel/`：Kernel 装配及其维度索引；查看 [Kernel README](UniClaw.Kernel/README.md)，具体维度入口由它继续链接。
- `UniClaw.Agent/`：Agent/Profile 产品侧类型；查看 [Agent README](UniClaw.Agent/README.md)。
- `UniClaw.Host/`：产品 Host 组合与设备入口；查看 [Host README](UniClaw.Host/README.md) 和 [CLI 参数实现](UniClaw.Host/Program.cs)。
- `UniClaw.Agent.Dsh/`、`UniClaw.Host.Dsh/`：DSH 适配实现；分别查看 [Agent DSH README](UniClaw.Agent.Dsh/README.md) 与 [Host DSH README](UniClaw.Host.Dsh/README.md)。
- [solution](../UniClaw.Kernel.slnx)：列出当前 6 个生产程序集与 `tests/` 下的测试项目。

## 运行入口现状

`UniClaw.Host` 的命令参数由 [Program.cs](UniClaw.Host/Program.cs) 实际解析：`--live` 设备闭环要求 `--device <id>` 或 `UNICLAW_ANDROID_DEVICE`，可选 `--target`、`--runs`；`--analyze <png>` 走真实感知服务做单图诊断。未指定有效模式会退出，不存在默认仿真档。当前 live 入口把咨询回调设为 `null`，代码注释也标明 CLI 真 Agent 尚未接入，因此这里只能将它作为参数导航，不能据此承诺完整产品闭环可用。

`UniClaw.Host.Dsh` 在 [Program.cs](UniClaw.Host.Dsh/Program.cs) 中解析 `--settings-traversal` 或 `--settings-coverage` 设备模式，或 `--runtime-http` 服务模式。设备模式需要 `--device <id>` 或环境变量；可选 `--runs <dir>`，遍历还可给 `--target` 和 `--target-descriptor`。这些运行模式可能操作设备、访问 DSH 或启动 HTTP 服务；本导航不表示它们已执行。

## 相关入口

- [开发工具与验证范围](../tools/README.md)
- [测试项目和数据分层](../tests/README.md)
- [跨进程/跨语言契约](../schemas/README.md)
- [Product 声明](../product/AGENTS.md) 与 [DSH 宿主集成](../dsh/README.md)
