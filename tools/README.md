# tools/ 开发与验证工具导航

`tools/` 放环境准备、生成、校验和验证入口；产品语义仍由架构基线、协议和源码定义。目录边界见[仓库职责基线](../docs/architecture/repository-role-boundaries-v0.1.md)。

## 验证入口

- [`verify-change`](verify-change)：接受 Change ID；`--scope quick|full` 控制回归范围，`--scenarios` 只用于 quick，`--live` 才增加 live gate，`--self-test` 运行编排器契约自检。`--help` 输出实际参数。默认 `full` 包含 diff check、Kernel/Host 架构 tripwire、Simulation、其余 solution 测试项目、scenario 认证检查和 Simulation 覆盖率。Simulation 只运行一次并复用 TRX。quick 只验证 tripwire 和 manifest 选中的 scenario 子集，明确跳过其余项目及覆盖率；两者都不等于仓库内所有工具、外部服务或设备均通过。命令会写解析配置快照到 `evidence/sim-006/runs/`。
- [`verify-live`](verify-live)：连接已有 Android 设备，或按其脚本调用测试设备准备/清理入口；会运行设备相关 gate。不要把它和默认离线回归混为一谈。
- [`scenario_certify.py`](scenario_certify.py) 与 [`scenario-coverage.py`](scenario-coverage.py)：前者认证/检查场景期望，后者读取 Simulation 的 TRX 计算场景覆盖；也可从 `verify-change` 的完整范围调用。

## 按用途找工具

- 仓库与声明校验：[`validate-tool-registry.py`](validate-tool-registry.py)、[`validate-model-bindings.py`](validate-model-bindings.py)、[`validate-workspace-schemas.py`](validate-workspace-schemas.py)、[`validate-testset-manifests.py`](validate-testset-manifests.py)。
- 协议生成：[`gen-protocol-machine.py`](gen-protocol-machine.py)、[`gen-open-changes.py`](gen-open-changes.py)、[`gen-run-report.py`](gen-run-report.py)、[`gen-state-machine-viz.py`](gen-state-machine-viz.py)。生成工具的输入与输出应以脚本实现和对应文档为准。
- Android 测试设备：[`android/`](android/) 下有测试设备启停、监督器及能力清单。调用设备脚本会产生环境副作用。
- Perception 环境：[`perception-env/setup.sh`](perception-env/setup.sh) 是环境准备入口；Provider 当前仍在 [`platforms/perception/`](../platforms/perception/README.md)。
- `experiments/` 保存有范围的实验脚本和局部说明；其中部分会访问模型、设备或外部服务，逐项阅读实验 README 后再决定是否运行。

## 相关入口

- [源码与启动选项](../src/README.md)
- [测试、fixture 与 evidence 分层](../tests/README.md)
- [契约索引](../schemas/README.md)
- [验证规则说明](../docs/agents/engineering-verification.md)
