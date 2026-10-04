# ADR-0037：异构仓库根目录职责与 Provider 边界

状态：accepted（2026-10-04）。UniClaw 同时包含 C# 产品运行时、Web Workspace、DSH 集成、Python 外部能力、测试资产和开发 Harness；本决策冻结根目录按职责分类，并将外部可独立运行的能力实现定义为 Provider。

本决策由 [ARCH-DOC-018](../../changes/ARCH-DOC-018/state.md) 固化；仓库落地基线见 [repository-role-boundaries-v0.1.md](../architecture/repository-role-boundaries-v0.1.md)。

## Decision

根目录使用以下职责边界：

```text
产品代码       → src / web / dsh
外部能力实现   → providers
环境与验证工具 → tools
测试代码       → tests
测试资产       → testsets / scenarios
契约与控制面   → schemas / workitems / .agents
治理资料       → docs / changes / plans / evidence / research
生成状态       → .perception / bin / obj
DSH 开发配置   → .dsh
```

`providers/` 只容纳可以独立启动并通过明确协议接入产品运行时的外部能力实现。当前 `platforms/perception` 的目标目录是 `providers/perception`，但路径迁移另立 Change；迁移前必须完成引用盘点、Provider Manifest 和验证入口更新。`.dsh/` 是 DSH 开发配置与开发装配，不属于产品源码、Provider 或生成状态。

## Consequences

- 新增根目录必须声明职责、owner、允许依赖、禁止依赖和生成/发布方式。
- `tools/` 可以启动和验证 Provider，但不拥有 Product semantic authority。
- `dsh/` 与 `providers/` 保持不同边界：前者是产品集成/插件，后者是外部能力实现。
- `tests/`、`testsets/`、`scenarios/` 和 `evidence/` 不再用“测试文件”作为共同权威语义。
- 目录分类本身不决定程序集拆分；物理迁移必须由独立 Change 提供引用和回滚证据。

## Rejected alternatives

- 继续使用含义宽泛的 `platforms/` 作为外部能力、设备平台和 DSH 的共同容器。
- 因为未来可能拆包就预先建立 `staging/` 或 `contrib/` 目录。
- 将 `.dsh/` 与 `dsh/` 合并，混淆开发配置和产品实现。
