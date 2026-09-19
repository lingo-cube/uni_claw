# CORE-003 Core 对象与最小语义关系验证

日期：2026-09-19

## 本轮范围

- 在 `UniClaw.Core` 内建立并验证 Clause、Segment/Slice、Evidence、Claim、Event
  组合语义、Effect、Attempt、TargetBinding。
- 移除不可证伪的 `ProducesWorldResult` 常量谓词；投递状态不再伪装成 World 结果。
- `TargetBinding.BasisSliceId` 改为可选，允许使用固定快照或其他可靠依据；不强制
  全局 Slice 或 World 版本。
- 未修改旧 Kernel/UI 模型的职责和字段，未进行源码迁移或旧模型一一映射。

## 验证结果

| 检查 | 结果 |
|---|---|
| `dotnet test tests/UniClaw.Core.Tests/UniClaw.Core.Tests.csproj --no-restore --logger 'console;verbosity=minimal'` | 11/11 通过 |
| Core projection seam + ProductHostClosureTests | 9/9 通过 |
| `dotnet test UniClaw.Kernel.slnx --no-restore --logger 'console;verbosity=minimal'` | 527/527 通过（Core 11、Agent 17、Kernel 388、Simulation 112） |
| Core dependency closure | 无 `UniClaw.*` 产品引用 |

NU1900 是 NuGet 漏洞缓存目录权限警告，不影响构建和测试执行。

## 语义结论

- Clause 是显式规格记录；Evidence/Claim 不自动产生 Permission。
- Segment 通过多个 Slice 保留局部、重叠和历史观察；新 Slice 不覆盖旧 Slice。
- Event 的发生、来源、时间、同一性和因果判断可由 Evidence + Claim 组合表达，未
  擅自增加独立 Event 类型。
- Effect、Attempt、TargetBinding 分开；Attempt 记录实际尝试，Binding 记录当次
  目标和依据；Unknown、Stale、Ambiguous、Unauthorized、Unverified 保持 fail-closed。

## 尚待后续验证

- 用实际浏览器、机器人和仿真模型反驳或支持该最小候选集。
- Core 对象与旧模型的责任对齐、拆分、组合、替换或删除另立 Change。
- 严格最小性仍未宣称；本证据只证明当前对象集能够表达已覆盖的语义路径。
