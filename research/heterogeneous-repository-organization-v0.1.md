# 异构项目仓库组织经验：对 UniClaw 根目录分类的研究 v0.1

日期：2026-10-04  
状态：Research / Authority: NONE  
范围：只研究成熟异构项目如何组织产品代码、扩展实现、协议、注册、测试和生成状态；不修改 UniClaw 目录，也不替换现有架构决策。

## 结论

成熟异构项目通常不是按“语言”或“运行方式”随意分目录，而是同时固定三条线：

1. **稳定核心与可替换实现分离**：核心定义生命周期、协议和装配；Provider、插件、transport、平台适配器通过明确注册或构建清单接入。
2. **契约与实现具有镜像关系**：扩展的配置/协议、实现、元数据、测试和文档通常能按同一标识互相定位。
3. **生成物、缓存和发布装配有显式边界**：生成目录、vendor/第三方、构建配置、测试资产和源码不混在一个“platform”或“misc”目录里。

这支持 UniClaw 当前提出的根目录分类：

```text
产品代码       → src / web / dsh
外部能力实现   → providers
环境与验证工具 → tools
生成状态       → .perception / bin / obj
治理资料       → docs / changes / plans / evidence
```

但还应补上两条：

```text
测试代码与测试资产 → tests / testsets / scenarios
契约与开发控制     → schemas / workitems / .agents
```

## 一手资料观察

### Bazel：核心源码、工具、第三方和示例分开

Bazel 官方仓库根目录把 `src`、`tools`、`third_party`、`examples`、`docs` 和 `.bazel*` 配置分开；构建系统本身的工具也通过明确的 BUILD target 暴露，而不是依靠一个含义宽泛的目录。[Bazel 官方仓库](https://github.com/bazelbuild/bazel)

Bazel 的依赖规则还强调固定 revision、镜像、校验和与可复现元数据；外部仓库被当成依赖输入管理，而不是产品源码的一部分。[Bazel repository rules](https://github.com/bazelbuild/bazel/blob/master/tools/build_defs/repo/git.bzl)

对 UniClaw 的启发：

- `providers/` 适合表达“外部能力实现”，不应继续使用含义不明确的 `platforms/`。
- `tools/` 只放启动、生成、验证和环境工具，不应持有 Product Authority。
- 外部 Provider 的版本、协议版本、校验信息和启动配置应有明确的 manifest 或 schema；不能只靠目录名和脚本约定。
- `bin/`、`obj/`、虚拟环境和缓存应明确标记为生成状态，不进入产品源层级。

### Kubernetes：单仓库中的发布边界和 staging 边界是显式的

Kubernetes 根目录把 `api`、`cmd`、`pkg`、`plugin`、`test`、`staging`、`third_party`、`vendor`、`build` 和 `hack` 分开。[Kubernetes 官方仓库](https://github.com/kubernetes/kubernetes)

Kubernetes 的 `staging/` 不是普通实现目录，而是“已经准备拆到独立仓库、但暂时在主仓库共同开发”的发布边界；其 README 明确说明内容会周期性发布到对应的顶层 `k8s.io` 仓库。[Kubernetes staging README](https://github.com/kubernetes/kubernetes/blob/master/staging/README.md)

对 UniClaw 的启发：

- 目录名应说明生命周期和发布语义。不要把任何“将来可能拆出去”的代码都放进 `platforms` 或 `staging`；只有存在明确拆分/发布边界时才需要 staging 类目录。
- `schemas/`、`src/`、`providers/` 和 `dsh/` 应保持不同职责，不能用“以后会拆包”作为混放理由。
- 如果某类能力将来可能成为独立 Provider，应先通过协议和 registry 形成发布边界，再考虑是否拆成独立仓库或包。

### OpenTelemetry Collector：扩展类别和管线角色比语言/进程更重要

OpenTelemetry Collector 的目标是“无需修改核心代码即可定制”，并将组件按明确角色分类为 receivers、processors、exporters、connectors 和 extensions。[Collector 官方架构说明](https://opentelemetry.io/docs/collector/components/)  [Collector 官方仓库](https://github.com/open-telemetry/opentelemetry-collector)

Collector Contrib 仓库按组件角色组织目录，并为每个组件提供支持级别、信号级别稳定性、codeowners 和生成的元数据；组件可以被选入不同发行版或由用户构建自定义发行版。[Collector Contrib 官方仓库](https://github.com/open-telemetry/opentelemetry-collector-contrib)  [新增组件指南](https://github.com/open-telemetry/opentelemetry-collector-contrib/blob/main/docs/new-components.md)

对 UniClaw 的启发：

- Capability Hub 的分类应该按“能力在管线中的角色/协议语义”组织，而不是按模型名称或快慢实现组织。
- 对每个 Capability 需要有稳定的 descriptor：角色、协议版本、稳定性、支持范围、生命周期、健康状态、owner 和装配入口。
- `Text Semantic Perception` 应是 Product capability；YOLO、OCR、Slow Text 是来源/实现组合，不应成为 Hub 中混乱的并列 Product 名称。
- 可考虑为 Capability 增加类似稳定性级别的字段（例如 experimental/development/stable），但它必须表达契约成熟度，不能代替置信度。
- 组件元数据、注册表和构建/装配清单应可生成，但生成文件要放在生成状态边界，不成为手工权威源。

### Envoy：API、实现、元数据、构建开关和测试使用镜像层级

Envoy 的扩展配置放在 `api/envoy/extensions/...`，实现放在对应的 `source/extensions/...`，两者保持镜像路径；扩展还需要更新元数据、构建配置、文档和测试。官方贡献指南明确要求扩展 API 与实现目录对应。[Envoy API 样式与扩展流程](https://github.com/envoyproxy/data-plane-api/blob/main/STYLE.md)  [Envoy CONTRIBUTING](https://github.com/envoyproxy/envoy/blob/main/CONTRIBUTING.md)

Envoy 还把默认启用、默认关闭和 contrib 扩展写入构建配置，可以在构建时选择是否纳入某个扩展；其测试按 `test/` 与目标路径运行。[Envoy 构建与扩展说明](https://github.com/envoyproxy/envoy/blob/main/bazel/README.md)

对 UniClaw 的启发：

- Capability 协议、实现、注册元数据、装配配置和测试应有可追踪的镜像关系。例如 `CapabilityId` 应能定位到 protocol、implementation、registration 和 test fixture。
- “是否对外提供为 DSH Tool”应是装配/暴露元数据，而不是改变 Capability 本身的协议；这和 Envoy 的 build enablement 类似。
- Product capability、RuntimeIntegration adapter、DSH Tool wrapper 应保持三种不同层级；不能把 wrapper 直接当作 capability 实现。
- 对实验性能力，可以使用显式稳定性/启用状态，而不是把实验代码藏在 `platforms` 或 `misc` 目录里。

### VS Code：扩展通过 manifest、activation 和注册 API 接入

VS Code 扩展由 manifest 的 contribution points、activation events 和运行时注册 API 组成。官方文档用命令扩展说明：`package.json` 声明命令，activation event 决定何时加载，`registerCommand` 绑定实际实现。[VS Code Extension Anatomy](https://code.visualstudio.com/api/get-started/extension-anatomy)

这说明成熟扩展系统将三件事分开：声明“我提供什么”、声明“什么时候激活”、实现“激活后如何注册”。

对 UniClaw 的启发：

- `CapabilityDescriptor` 应描述能力声明；生命周期/作用域决定激活方式；Hub 的 register/resolve 负责实际装配。
- 任务注入能力属于“activation/binding”层，不应把所有可能能力预先变成全局 singleton。
- DSH Tool 是一种外部暴露形态；它可以引用同一个 Capability 实现，但应拥有自己的 manifest/visibility/approval 元数据。
- 将来如果能力支持按任务注入，目录和协议应区分 `definition`、`binding`、`runtime instance`，避免注册表同时扮演三者。

## 对 UniClaw 根目录的改进建议

### 1. 冻结根目录分类，并给每类一个“不得做什么”

建议维护一份根目录职责表，至少包含 owner、允许依赖、禁止依赖、生成状态和发布方式：

```text
src/                 Product runtime assemblies
web/                 canonical Workspace product/frontend
dsh/                 DSH product adapters/plugins
providers/           independently runnable external capability providers
tools/               environment, generation, validation and developer tools
tests/               executable test code
testsets/            fixtures, datasets and expected observations
scenarios/           scenario definitions/replay inputs
schemas/             language-neutral contracts and schemas
workitems/           transient delegation payloads
docs/                architecture/design/reference documentation
changes/             durable change state
plans/               execution plans
evidence/            verification evidence
.agents/             harness/skills/control plane
.perception/         generated provider environment/cache
bin/ obj/             build output
```

目录职责表本身应是治理资料，不应让每个子项目各自解释根目录含义。

### 2. 将 `platforms/perception` 收敛为 `providers/perception`

当前感知 Python 服务是外部可替换能力实现，不是设备平台或产品 Host。推荐的最终语义是：

```text
providers/perception/       YOLO/OCR/fusion Provider
src/UniClaw.Kernel/Perception/   Kernel protocol/client seam
src/UniClaw.Host/                process/session composition
tools/perception-env/            environment setup and checks
.perception/                     generated environment/cache
```

迁移前需补一个 Provider manifest，至少声明：provider id、提供的 Capability IDs、协议版本、transport、启动入口、健康检查、fixture/testset 入口和生成状态目录。本文不执行迁移。

### 3. 给 Capability 建立三层标识，而不是只建立一个名称

建议区分：

```text
CapabilityDefinition   产品承诺的接口和语义
CapabilityImplementation 具体实现/模型链路/Provider
CapabilityBinding       当前 Host 或任务如何注入、激活和暴露
```

例如：

```text
Definition: Text Semantic Perception
Implementation: Fast YOLO+OCR + Slow Text
Binding: task-scoped Kernel binding / optional DSH Tool exposure
```

这能吸收 OTel 的角色分类、Envoy 的扩展镜像关系和 VS Code 的声明/激活/注册分离。

### 4. 为测试资产建立独立的定位规则

成熟项目把测试代码、测试数据和运行时源码分开。UniClaw 当前已有 `tests/`、`testsets/`、`scenarios/`，但应明确：

- `tests/` 放可执行测试和 harness；
- `testsets/` 放输入帧、XML、期望标签、语言期望和性能基线；
- `scenarios/` 放遍历/任务场景及回放参数；
- `evidence/` 只放实际运行产生的证据，不放静态 fixture。

这样可以避免把模拟结果、缓存、Provider 输出和 live/device 证据混成一种“测试文件”。

### 5. 为注册和生成增加可追踪关系

建议每个可注册 Capability 具备一组可定位字段：

```text
capability_id
protocol_version
implementation_id
provider_id (optional)
scope
lifecycle
stability
visibility (internal / host / dsh-tool)
health_contract
testset_refs
```

其中 `visibility` 只决定暴露面，不改变 capability authority；`stability` 表达契约成熟度；`confidence` 属于感知结果/assessment，不能放进 descriptor 后被误读成能力稳定性。

## 不建议现在做的事情

- 不要因为业界有 `staging`、`contrib` 或 `vendor` 就在 UniClaw 复制这些目录；先确认真实发布边界。
- 不要按文件大小拆 `Kernel`，应先找真实 buyer 和可测试 seam。
- 不要把 DSH 放进 `providers/`；DSH 是产品集成/适配层，不是外部感知 Provider。
- 不要把 `.perception`、`bin`、`obj` 当作产品模块；它们是生成状态。
- 不要把注册表、协议、运行实例和 DSH Tool wrapper 合并成一个类或目录。

## 建议的后续 Change 顺序

1. 新增并冻结根目录职责表，确认 `providers/` 作为外部能力实现根目录。
2. 为 `providers/perception` 设计 manifest、健康检查和 testset 入口。
3. 设计 CapabilityDefinition / Implementation / Binding 三层字段，与现有 Hub 对齐。
4. 单独处理 Workspace 重复 Core，确保 canonical source 只有一个。
5. 在有第二个真实 Provider 或 Host buyer 后，再决定是否物理拆分 Kernel.Perception 或 Host。

## 来源索引

- [Bazel repository](https://github.com/bazelbuild/bazel)
- [Bazel external repository rules](https://github.com/bazelbuild/bazel/blob/master/tools/build_defs/repo/git.bzl)
- [Kubernetes repository](https://github.com/kubernetes/kubernetes)
- [Kubernetes staging README](https://github.com/kubernetes/kubernetes/blob/master/staging/README.md)
- [OpenTelemetry Collector architecture](https://opentelemetry.io/docs/collector/components/)
- [OpenTelemetry Collector repository](https://github.com/open-telemetry/opentelemetry-collector)
- [OpenTelemetry Collector Contrib repository](https://github.com/open-telemetry/opentelemetry-collector-contrib)
- [OpenTelemetry new component guide](https://github.com/open-telemetry/opentelemetry-collector-contrib/blob/main/docs/new-components.md)
- [Envoy API extension style](https://github.com/envoyproxy/data-plane-api/blob/main/STYLE.md)
- [Envoy contribution guide](https://github.com/envoyproxy/envoy/blob/main/CONTRIBUTING.md)
- [Envoy Bazel/build and extension guide](https://github.com/envoyproxy/envoy/blob/main/bazel/README.md)
- [VS Code extension anatomy](https://code.visualstudio.com/api/get-started/extension-anatomy)

