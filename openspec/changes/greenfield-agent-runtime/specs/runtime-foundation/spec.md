# Runtime Foundation

## ADDED Requirements

### Requirement: Isolated Runtime Foundation

**SHALL**

- SHALL 建立独立工程边界：`src/UniClaw.Runtime/` + `tests/UniClaw.Runtime.Tests/`。

- SHALL 第一阶段 `UniClaw.Runtime` **不引用** `UniClaw.Core`（Greenfield isolation，机械约束）。

- SHALL 建立 Architecture Contract（12 条 invariants，`docs/system/constitution/`）。

- SHALL 建立机械 Architecture Guards（`UniClaw.Runtime.Tests`）。

- SHALL AGENTS.md 增加唯一导航入口（指向 Contract + 本 change）。

- SHALL 新 Runtime Guard 验证：csproj 零 ProjectReference；源码零旧 Runtime namespace 引用；契约文档 + 导航存在。

#### Scenario: Mechanical foundation verification

Given 独立工程边界、Architecture Contract、机械 Architecture Guards 与 AGENTS.md 导航入口已建立；
When 新 Runtime Guard 验证运行；
Then 验证 csproj 零 ProjectReference、源码零旧 Runtime namespace 引用，以及契约文档 + 导航存在。
