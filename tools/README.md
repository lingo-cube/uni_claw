# tools/ 开发与验证工具导航

`tools/` 放环境准备、生成、校验和验证入口；产品语义仍由架构基线、协议和源码定义。目录边界见[仓库职责基线](../docs/architecture/repository-role-boundaries-v0.1.md)。

## 验证入口

- [`verify-change`](verify-change)：接受 Change ID；`--scope quick|full` 控制回归范围，`--scenarios` 只用于 quick，`--live` 才增加 live gate，`--self-test` 运行编排器契约自检。`--help` 输出实际参数。默认 `full` 包含 diff check、Kernel/Host 架构 tripwire、Simulation、其余 solution 测试项目、scenario 认证检查和 Simulation 覆盖率。Simulation 只运行一次并复用 TRX。quick 只验证 tripwire 和 manifest 选中的 scenario 子集，明确跳过其余项目及覆盖率；两者都不等于仓库内所有工具、外部服务或设备均通过。新运行从 `--asset-config`、`UNICLAW_TEST_ASSET_CONFIG`、本地 `~/.config/uniclaw/test-assets.json`（支持 XDG_CONFIG_HOME）中依次选择一份完整 JSON 配置，缺失或非法直接失败。`--print-asset-config` 只查询解析结果。TRX、日志、信号和解析快照写到该次独立 Run，覆盖率复用相同 TRX；旧仓库内产物保持原路径。`--signal-dir` 现在是 Run results 内的相对子目录，绝对覆盖会拒绝；`--retry-of` 只连接相同来源/配置/范围批次中的历史 Run。`--self-test` 仍独立且不生成资产。
- [`verify-live`](verify-live)：连接已有 Android 设备，或按其脚本调用测试设备准备/清理入口；会运行设备相关 gate。不要把它和默认离线回归混为一谈。
- [`test-assets`](test-assets)：开发资产读取入口；`resolved [--asset-config 文件]` 查询布局，`list CHANGE` 读清单，`validate CHANGE RUN` 校验每文件大小/SHA256，`path CHANGE RUN 相对路径` 返回历史 Run 原布局中的登记文件。路径绑定注册在固定本机 `~/.local/state/uniclaw/test-asset-bindings/<repositoryKey>/<configDigest>.json`（独立于配置选择和 XDG_CONFIG_HOME）；后改配置不迁移旧 Run。配置的 parentRoot 为仓库外绝对根，workPath/archivePath 与四类 categoryPaths 必须相对且不重叠。分类内可用 `raw/captures`，分类之间禁止祖先覆盖。当前材料保留为验收用途；支持Run归档/恢复，归档不自动删源。任务退役见下方显式入口；自动GC、批量存量清理/迁移仍不启用。

- `test-assets` 的第二片操作：`begin/scope/finish`供显式开发调用者创建和结束Run；`path-ref test-assets:CHANGE/RUN/相对路径`返回校验后的实际文件；`replay CHANGE --case 文件`调用原封存脚本并在TRX/重放结果消费后收尾。诊断HTTP请求可选`developmentAssets: {changeId, assetConfig?, retryOf?}`，原产品模型路由保持；packet/信号进入同一资产Run，finish失败单独报告。一般来源列protectedRefs，正式baseline不自动晋升。Run归档/恢复见下；任务退役使用下方显式计划/执行入口，不扩展为GC或存量迁移。

- `test-assets archive CHANGE RUN`、`verify-archive CHANGE ARCHIVE`、`restore CHANGE RUN [--archive-id ID]`：按原Run布局归档与恢复，归档不会删源。新包保留纳秒mtime，旧v1无精确时间时保守恢复，防止旧TRX冒充新结果。`path`/`path-ref`可完整恢复缺失Run后读取；`validate`只读，不恢复、不掩盖现有损坏。字节可用但外部来源未决时restore返回1、validate不通过、path/ref拒绝成功；详见[收尾规范§10](../docs/agents/test-asset-retention.md#10-run归档与恢复读取)。

- `test-assets release-work CHANGE RUN`：仅对创建时通过`begin --work-release-reason 理由`声明的全新Run释放可恢复工作副本；要求无未完物理读写者、最终收尾、v2包及消费者复验、引用核对通过。默认Run与全部旧Run不获得授权。逐文件删除及pending回执追加`workReleases`，原索引/事实/归档保留，`path-ref`仍可恢复读取；通用GC/存量迁移未启用，任务退役见下方入口。引用检查依赖`rg`；边界见[收尾规范§11](../docs/agents/test-asset-retention.md#11-明确登记的归档工作副本释放)。

- `test-assets plan-delete-task CHANGE [--out FILE]`：只读盘点任务自有资产，按每个Run旧binding定位，输出planId、候选身份/摘要、保护项、依赖与未决项；不按当前profile猜根，也不删除数据。`test-assets delete-task-assets CHANGE --plan FILE`：重算盘点核对同一计划后，才逐项删除仍一致的独占普通文件；保护外部引用、baseline、原来源、链接/未知材料，并保留state、assets索引、原结果/hash和portable `taskDeletions`审计回执。退役/退役中的Run拒绝读取、归档、恢复和新Run登记；部分失败只计实际删除，下一次用新计划重试。该入口不做自动GC、批量历史清理/迁移、产品存储或聊天删除；边界见[收尾规范§12](../docs/agents/test-asset-retention.md#12-显式任务资产退役)。

- [`evolution-signal.py`](evolution-signal.py)：统一测试TRX、回放资产漂移与运行终态的非权威失败信号。`verify-change`失败出口和`run-diagnosis`声明的退出适配复用此脚本；显式预期红不生成候选，重复失败去重。输出仅为evidence，不创建Change或自动修复。参数与范围见[信号契约](../changes/EVO-002/signal-contract.md)。
- [`evolution-case.py`](evolution-case.py)：显式从Settings运行产物、capture和独立Oracle导出封存组件case，校验后通过[`EvolutionReplay/`](EvolutionReplay/)公开分类器载体重放并记录真实TRX。仅组件输入可重放，不执行原动作；不覆盖旧case、改写原信号或采纳分类修复。范围见[EVO-003](../changes/EVO-003/state.md)。
- [`scenario_certify.py`](scenario_certify.py) 与 [`scenario-coverage.py`](scenario-coverage.py)：前者认证/检查场景期望，后者读取 Simulation 的 TRX 计算场景覆盖；也可从 `verify-change` 的完整范围调用。
- [`RunRecoveryProbe/`](RunRecoveryProbe/)：由存储测试启动的独立进程探针；`write-report-delivery` / `read-report-delivery` 只经公开报告交付缝验证进程终止与重开，不运行任务、模型或设备。

## 按用途找工具

- 仓库与声明校验：[`validate-tool-registry.py`](validate-tool-registry.py)、[`validate-model-bindings.py`](validate-model-bindings.py)、[`validate-workspace-schemas.py`](validate-workspace-schemas.py)、[`validate-testset-manifests.py`](validate-testset-manifests.py)。
- 协议生成：[`gen-protocol-machine.py`](gen-protocol-machine.py)、[`gen-open-changes.py`](gen-open-changes.py)、[`gen-run-report.py`](gen-run-report.py)、[`gen-state-machine-viz.py`](gen-state-machine-viz.py)。生成工具的输入与输出应以脚本实现和对应文档为准。
- Android 测试设备：[`android/`](android/) 下有测试设备启停、监督器及能力清单。调用设备脚本会产生环境副作用。
- Perception 环境：[`perception-env/setup.sh`](perception-env/setup.sh) 是环境准备入口；Provider 当前仍在 [`platforms/perception/`](../platforms/perception/README.md)。
- `experiments/` 保存有范围的实验脚本和局部说明；其中部分会访问模型、设备或外部服务，逐项阅读实验 README 后再决定是否运行。

## 相关入口

- [开发测试资产收尾规范](../docs/agents/test-asset-retention.md)：诊断、重放、仿真/验证每次结束后的必要证据保全与多余产物清理；调用者在结果/coverage/失败信号消费者完成后执行，不改变现有CLI的check-only语义。

- [源码与启动选项](../src/README.md)
- [测试、fixture 与 evidence 分层](../tests/README.md)
- [契约索引](../schemas/README.md)
- [验证规则说明](../docs/agents/engineering-verification.md)

- [`ResourceStorageProbe/`](ResourceStorageProbe/)：离线资源增长样例的同输入存储/读取成本probe；legacy/versioned只选择读取方式，旧writer对照由封存入口源独立构建。输出managed调用、字节、耗时与GC后存活量，不调用设备或产品模型，不等同物理磁盘或峰值内存测量。
