# UniClaw 异构仓库职责基线 v0.1

> DocumentType: `REPOSITORY_ROLE_BOUNDARIES_V0_1`
>
> Status: `COMPONENT-BASELINE / ACCEPTED`
>
> Authority: `COMPONENT`（仓库职责与依赖边界；不替代产品架构 baseline）
>
> Date: `2026-10-04`

本文件定义根目录的导航语义和依赖边界，不要求目录立即搬迁。它由 [ARCH-DOC-018](../../changes/ARCH-DOC-018/state.md) 固化，并与 [ADR-0037](../adr/0037-repository-role-boundaries-and-provider-boundary.md) 和 [ADR-0038](../adr/0038-capability-definition-implementation-binding-boundary.md) 互相追溯。

## 根目录职责

| 路径 | 语义 | 允许放置 | 不应放置 |
|---|---|---|---|
| `src/` | 产品运行时程序集 | Kernel、Agent、Host、产品适配器 | Harness 机制、Provider Python 实现 |
| `web/` | canonical Workspace 产品前端 | Query Core、controller、view、browser host | DSH 专属数据真相 |
| `dsh/` | DSH 产品集成和插件 | DSH adapter、client bridge、发布装配 | 外部 Perception Provider |
| `providers/` | 独立运行的外部能力实现 | Provider 进程、其运行依赖和 Provider tests | Product Owner、Kernel state |
| `product/` | 产品声明面：版本化产品运行时装配工件 | UniAgent Profile（profiles/）、静态 prompt manifest（prompt/）、危险动作 policy（policy/）、规范知识目录（knowledge/） | 代码（→src/）、宿主绑定（provider/model/endpoint 只在 `.dsh/product/`）、Harness 语义、运行态（PRF-002/ADR-0041 增补） |
| `tools/` | 环境、生成、验证和开发工具 | setup、lint、manifest/schema validation、live checks | Product semantic authority |
| `tests/` | 可执行测试代码 | unit、contract、integration、E2E harness | 静态 fixture 集合 |
| `testsets/` | 测试数据与期望 | 图片、XML、期望语言、性能基线 | 运行时缓存、live evidence |
| `scenarios/` | 场景和回放输入 | traversal、replay、stimulus 定义 | canonical Run/World state |
| `schemas/` | 语言无关契约 | 跨进程/跨语言 wire schema | 单语言内部 model |
| `workitems/` | 瞬时委派载荷 | Leader→SubAgent contracts | 持久 backlog |
| `.agents/` | Harness/Skill 控制面 | Skill、UniFlow 和开发约束 | Product implementation |
| `docs/` | 架构、设计和研究资料 | ADR、baseline、research | 运行时事实 |
| `changes/` | 持久 Change State | WHAT/WHY/ACCEPTANCE | 临时委派载荷 |
| `plans/` | 执行计划 | 计划和恢复信息 | Product canonical state |
| `evidence/` | 实际验证证据 | test/live/review output | 静态 fixture |
| `.dsh/` | DSH 开发配置 | profile、model binding、本地 observer 配置 | DSH 产品源码、Provider、build output |
| `.perception/` | 感知运行生成状态 | venv、cache | 版本化 Product source |
| `bin/`、`obj/` | 编译生成物 | build output | 手写源码 |

## 迁移边界

当前 `platforms/perception` 是历史路径，目标语义为 `providers/perception`。在独立迁移 Change 完成前，现有路径仍是当前运行入口；新代码和文档不得继续把 `platforms` 解释成通用平台层。

## Capability 可追踪关系

每个可注册 Capability 应能通过稳定 ID 定位到：

```text
CapabilityDefinition
CapabilityImplementation
CapabilityBindingDescriptor
Provider（可选）
wire protocol（可选）
testset / scenario
evidence
```

`CapabilityInstance` 是 Host 或 Task 作用域内的解析结果，不写回全局 Registry。`confidence` 属于感知结果；`stability` 属于 Capability Descriptor。

## Canonical source 规则

`web/uniclaw-workspace` 是 Workspace Query Core 的唯一 canonical source。`dsh/` 只能通过 Host Adapter 装配它，不得重新定义同义 Core；静态发布物必须从 canonical source 构建。
