# ADR-0039：Tool 与 Capability 的概念边界及 Harness 工具暴露清单

状态：accepted（2026-10-06）。PNL-006 交付的全链路报告工具与规划中的结果诊断既服务研发态（AI Coder），又需要被 Workbench / 前端模型当作可调用的工具；若不先钉死概念，Tool 会退化成 ADR-0038 已明确拒绝的形态——"把 Provider、模型、Capability、Tool 和 Task Instance 都作为同一级 Registry entry"。本决策由 [PNL-007](../../changes/PNL-007/state.md) 固化。

## Decision

### 1. 概念边界

- **Capability** 是产品层概念：Kernel / Runtime 运行时可调用的能力单元，语义权威在 CapabilityHub，按 ADR-0035/0038 的 Definition / Implementation / Binding 三层治理。
- **Tool** 是 Harness 层概念：面向 harness 消费者（AI Coder、Workbench、前端模型调用）暴露的调用单元。调用形态（CLI、MCP、HTTP 接口、skill procedure）是 Tool 的 implementation detail，不是语义。
- **skill** 是 procedure 载体：model-procedure 类工具的说明书。skill 与 Tool 不是平行概念——"以 skill 为 procedure 载体的 Tool"才是被牵引到产品消费面的东西。

### 2. 单向映射

Tool 可由 Capability 映射而来（`backing: capability` + `capabilityRef` 单向引用），也可以是 harness-native（脚本、以 skill 为载体的模型过程）。映射只持暴露元数据（叫什么、怎么调、给谁用、什么姿态），语义真相永远在 CapabilityHub；反向引用不存在。capability-backed 变体本期只冻结词汇，解析机制待第一个真实 buyer（ADR-0026）。

### 3. skill 单源双路径

skill 永远单源：研发态经 skill catalog 直接加载（现有机制，零改动）；产品态经 `tool-registry.yaml` 的 `skillRef` 由 adapter 在调用时解析注入。产品代码不得硬编码 harness skill 路径；研发态与产品态用法 genuinely 分化时，显式拆分为不同 Tool 条目与不同 skillRef，禁止静默 fork。

### 4. 管理层级

`tool-registry.yaml` 是 Development Harness 信任域的注册面——ADR-0035 已预留 Product Runtime / Runtime Integration / Development Harness 各自独立注册域；本清单与 Product Runtime 的 CapabilityHub 不共享可变注册状态，只共享词汇。确定性校验器 `tools/validate-tool-registry.py` 执法（词汇枚举、绑定字段、路径与 capability 引用的存在性、status 规则），不用模型推理。

### 5. 字段最小集

十字段（name / summary / backing / invocation / entry|skillRef / outputSchema / requiredCapability / posture / surfaces / status）加 `consumes`（工具间依赖声明，由 run-diagnosis 的真实需求引入）。权限审批流、版本、废弃治理等有了真实需求再说。`requiredCapability` 接 `model-routing.yaml` 的 capability → tier 映射；模型绑定只在各 Host adapter。

## Consequences

- `run-report`（implemented）与 `run-diagnosis`（planned）成为头两个登记项；后续 harness 工具要暴露给 Workbench / 前端，先登记再接线。
- Workbench 或其他前端消费工具时只依赖本清单，不依赖 `tools/` 目录结构或 `.agents/skills/` 路径；实现挪动只改清单一行。
- 枚举词汇（backing / invocation / posture / surfaces / status）冻结于清单头注与校验器两处，扩展需同步修改并记录于本文档。
- Workbench 调用面（ToolInvoke 类 capability 或工具面板）与 model-procedure 的模型路由属后续 Change；本决策不预造。

## Rejected alternatives

- Tool 作为新 Capability 类型进 Kernel Hub：ADR-0038 已拒绝；混淆产品语义与 harness 暴露两个层面。
- 产品直接读 `.agents/skills/`：产品耦合 harness 目录结构，无法独立演化。
- 复制 skill 内容进产品资产：双份真相，必然漂移。
- 一步建成 Workbench 调用面或 MCP 网关：无真实 buyer 的预造缝（ADR-0026）。
