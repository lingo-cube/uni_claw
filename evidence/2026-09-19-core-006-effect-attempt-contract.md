# CORE-006 Effect → Attempt → TargetBinding 契约验证

日期：2026-09-19

## 采用的最小规则

- Effect 表示逻辑操作，Attempt 表示一次实际尝试；同一 Effect 可以有多次 Attempt。
- Attempt 的请求快照、执行端和授权依据分别保留，不从观察内容推导权限。
- 投递进展、外部执行判断、协调状态是三条独立轴，允许同时处于不同状态；`Unknown`
  不被升级为成功、失败或 World 结果。
- TargetBinding 的 basis 可以是 Slice，也可以是 `BasisReference` 表达的固定非 Slice
  记录/快照/资源版本；缺少固定 basis 的 Canonical binding fail-closed。
- Core 只定义可检查的语义位置；Kernel driver、设备 SDK、传输、恢复和持久化仍在下层。

## 代码与测试

| 验收语义 | 证据 |
|---|---|
| 请求快照、执行端、授权依据和三条执行轴同时保留 | `PhoneScrollTracerTests.Attempt_keeps_request_executor_authorization_and_three_execution_dimensions` |
| 固定非 Slice basis 可用，Slice 不是强制依据 | `PhoneScrollTracerTests.Binding_basis_slice_is_optional_when_a_fixed_non_slice_basis_is_used` |
| Canonical 但无固定依据不允许投递 | `PhoneScrollTracerTests.Canonical_binding_without_fixed_basis_fails_closed` |
| stale、ambiguous、unauthorized、unverified 继续 fail-closed | `PhoneScrollTracerTests.Binding_dispositions_remain_distinct_and_fail_closed`；Kernel projection seam tests |
| Core 不依赖 Kernel/设备/Runtime 类型 | Core 与 Kernel 的程序集/记录面纯度测试 |

## 验证结果

- Core.Tests：13/13。
- Kernel.Tests：388/388。
- Agent.Tests：17/17。
- Simulation.Tests：112/112。
- 全解决方案：530/530。
- NU1900 仅为 NuGet 漏洞缓存目录权限警告，不影响测试执行。

## 保留的局部 gate

Kernel 当前 `CoreSemanticProjection.ProjectTargetBinding` 仍按现有 Slice seam 投影；
非 Slice `BasisReference` 的生产投影尚未接入，留待明确映射契约后再处理。该 gate 只
阻塞依赖该投影的用途，不阻塞 Core 候选契约本身。

本证据证明 CORE-006 的最小语义契约可表达并通过当前回归，不证明最终字段、继承树、
存储布局、生产协议或旧模型迁移已经确定/完成。
