# UniClaw 仓库导航

本仓库同时包含目标产品、宿主适配、开发工具与验证资料。目录职责和依赖边界以[仓库职责基线](docs/architecture/repository-role-boundaries-v0.1.md)为准；产品拓扑与语义以[产品架构基线](docs/architecture/product-architecture-baseline-l0-l3.md)为准，本页只提供入口。

## 先从这里进入

- [本轮工程优化与验收](changes/ENG-OPT-001/report.md)：模块边界修复、验证与保留范围。
- [产品源码](src/README.md)：程序集入口、运行入口与现状。
- [开发和验证工具](tools/README.md)：`verify-change`、工具分类及其输入输出。
- [测试代码与测试数据](tests/README.md)：测试项目、fixture、scenario、testset 和 evidence 的区别。
- [语言无关契约](schemas/README.md)：跨进程/跨语言的 schema 与状态机。
- [Product 声明工件](product/AGENTS.md)：profile、task、skill、prompt 与 policy 的归属说明。
- [DSH 集成与发布](dsh/README.md)：DSH 插件、发布副本和部署入口。
- [Workspace canonical 源码](web/uniclaw-workspace/package.json)：浏览器/Host 共用的前端源码及其脚本。
- [Perception Provider 当前目录](platforms/perception/README.md)：历史路径现状与运行文档。
- [架构、协议与 ADR](docs/README.md)：文档目录规则与架构入口。

## 运行和验证入口

- `src/UniClaw.Host` 的 CLI 入口在 [Program.cs](src/UniClaw.Host/Program.cs)：`--live --device <id> [--target <state>] [--runs <dir>]` 是设备运行参数；`--analyze <png>` 是单图诊断。无 `--live` 或 `--analyze` 会 fail-closed。当前 live CLI 的咨询实现尚未接入，代码传入 `ConsultAgent = null`，因此这不是已验证可完成的端到端路径。此处仅记录参数，不代表本次启动过设备或模型。
- [DSH Host CLI](src/UniClaw.Host.Dsh/Program.cs) 接受且仅接受 `--settings-traversal`、`--settings-coverage`、`--runtime-http` 三类模式之一；设备模式还需 `--device <id>` 或 `UNICLAW_ANDROID_DEVICE`。支持 `--runs <dir>`；遍历可指定 `--target`、`--target-descriptor`。Runtime HTTP 可用 `--runtime-port <p>`（默认 5080）。运行会连接设备、DSH 服务或运行时 HTTP，不应把命令行参数说明当作执行证据。
- [verify-change 帮助](tools/verify-change)列出 `CHANGE-ID`、`--scope quick|full`、`--scenarios`、`--live`、`--self-test` 与 `--no-snapshot`。`full` 是完整回归范围：8 个 solution 测试项目、架构 tripwire、scenario 认证检查和 Simulation 覆盖率（Simulation 共用一次执行结果）；`quick` 只跑架构 tripwire 和 manifest 选出的 scenario 子集，不运行其余测试项目或覆盖率。live 检查需显式 `--live`。范围定义不代表命令已经运行，也不代表全仓任意脚本或外部环境均通过。

## 边界速查

- `product/` 保存宿主中立的产品声明；`.dsh/product/` 保存 DSH 的 provider/model/endpoint 等宿主绑定。两者不是重复配置。
- `web/uniclaw-workspace/` 是 Workspace 的 canonical 源码；`dsh/uniclaw-task-workbench/src/client.js` 是由 `web/uniclaw-workspace/scripts/build-browser-bundle.mjs` 生成的 DSH 浏览器发布副本。修改逻辑时以 canonical 源为准，发布副本需要按生成入口同步。
- `platforms/perception/` 是 Provider 的当前历史路径，产品 Host 仍从此处加载。职责基线将目标语义指向 `providers/perception/`，但迁移尚未完成；本导航不暗示目录已经迁移。
- `tests/` 放可执行测试代码；`testsets/` 与 `scenarios/` 放静态测试数据和回放输入；`evidence/` 放验证、评审或人工裁决的实际证据。三者不可互相替代。
