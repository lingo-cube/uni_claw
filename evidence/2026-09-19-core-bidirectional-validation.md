# Core 双向语义验证：UI/Slice 与非 UI/资源版本

日期：2026-09-19
验证等级：`SCENARIO + CONTRACT`

## 验证目的

先用现行 Core 指南检查真实 Core/Kernel 投影，再用场景反驳指南，判断缺口属于
Core 语义、下层投影，还是指南表述。不新增 Core 对象，不进行旧模型迁移。

## 输入依据

- CORE-001 至 CORE-006 Change State 与证据；
- vNext.1 Effect/Attempt/TargetBinding、Segment/Slice、Claim/Event/Evidence 规则；
- 场景枚举库 v0.1：滚动后定位变化、POST 超时但服务端已成功、请求未到达、幂等重试、
  异步 job、批处理部分成功、接口版本变化、动作已发出结果未知、动作部分执行、标定漂移、
  人与 Agent 同时编辑文档等场景；
- 当前 Kernel Core projection 与 Core/Kernel 测试。

## 场景 A：手机滚动后点击（UI / Slice）

### 指南预期

1. Segment 提供持续引用，多个 Slice 保存滚动前后的局部观察；新 Slice 不覆盖旧 Slice。
2. TargetBinding 固定在建立时的 Slice；当前 revision 变化后旧 binding 变 stale，不能跟随
   latest 自动换目标。
3. Attempt 时间与观察时间分开；receipt/未知投递不自动生成 World result。

### 实际结果

| 检查 | 结果 | 证据 |
|---|---|---|
| 多 Slice、重叠观察、点击时间独立 | 通过 | `CoreProjectionSeamTests.ScrollThenTap_RealizationProjectsOntoCore_WithFixedHistoryAndSeparateAttemptTime` |
| revision 前进后 binding stale，basis 不漂移 | 通过 | 同上；`CanonicalBindingBecomesStaleAfterRevisionAdvance_BasisStaysFixed` |
| Unknown 投递不成为终态或 World result | 通过 | `CoreProjectionSeamTests.UnknownDelivery_RealizationProjection_StaysUnknown_NoWorldResult` |
| Attempt 保留请求快照、执行端、授权、三条状态轴 | Core 直接构造可表达；当前 Kernel projection 未携带 | `Core-006` 测试通过；`CoreSemanticProjection.ProjectAttempt` 目前只映射 receipt delivery |

### 反驳结果

场景没有反驳 Core 对象集或 `Effect → Attempt → TargetBinding` 层次；反例落在下层投影
责任：当前 `EffectReceipt` → `Attempt` 投影尚未填入候选的请求/执行/授权/三轴信息。
这不证明应把 receipt 生命周期重新提升为 Core 生命周期。

## 场景 B：非 UI 资源版本（文件/API）

### 指南预期

1. TargetBinding 可以引用固定资源版本，不强制制造 Slice。
2. 稳定记录引用、固定快照引用和现实身份判断分开。
3. 缺少固定依据时不能因为 Canonical 或当前定位而放行。

### 实际结果

| 检查 | 结果 | 证据 |
|---|---|---|
| Core 可表达非 Slice 固定 basis | 通过 | `PhoneScrollTracerTests.Binding_basis_slice_is_optional_when_a_fixed_non_slice_basis_is_used` |
| Canonical 且无固定 basis fail-closed | 通过 | `PhoneScrollTracerTests.Canonical_binding_without_fixed_basis_fails_closed` |
| 当前 Kernel 投影可直接投影资源版本 basis | 未满足 | `CoreSemanticProjection.ProjectTargetBinding` 仍要求 `basisSliceId` |

### 反驳结果

非 UI 场景没有反驳 `BasisReference` 语义，也没有要求新增 Core 类型；它反驳的是当前
Kernel projection seam 的覆盖范围。非 Slice 生产投影应另行设计并验证，不能把资源版本
伪装成 Slice，也不能用 opaque locator string 偷渡。

### 仓库输入盘点

当前仓库没有独立的文件/API/资源目标模型或对应的 canonical binding buyer。现有
`CanonicalBinding` 明确属于 Kernel 的 UI/字符串 realization，`ProjectTargetBinding`
也只接收 Slice basis。因而现在增加一个“通用资源投影 Adapter”会先发明输入模型，
不能作为真实双向验证；该工作应等第二个实际 realization 出现后再进入实现 Change。

## 场景库的交叉约束

- POST 超时但服务端已成功、请求未到达、付款状态未知、动作已发出结果未知：要求
  dispatch 与外部执行分开，`Unknown` 不升级。
- 幂等重试、异步 job、批处理部分成功、动作只执行一部分：要求同一 Effect 下保留
  多个 Attempt 或明确的部分执行判断，不能用万能生命周期覆盖。
- 接口版本变化、人与 Agent 同时编辑文档、标定漂移：要求固定 basis 与当前有效性分开，
  不能跟随 latest/canonical 静默改写历史。

这些场景支持 CORE-006 的局部 gate，但场景库本身不是生产测试通过证明。

## 决定与后续边界

1. **Core 语义**：当前最小对象集足够；不新增 Event、Expectation、Verification、
   OpenDuties 或第二套 Effect 记录。
2. **下层投影**：存在两个待处理局部 gate：Attempt 丰富字段的只读映射、非 Slice
   `BasisReference` 的生产投影。两者都不在本轮迁移。
3. **指南**：无需回退已确认规则；在“候选 Core 契约”和“生产投影保证”之间保持明确
   分层，缺少保证只阻塞依赖它的用途。
4. **下一轮验证**：为一个非 UI 资源版本实例和一个 UI/Slice 实例各建立真实投影输入，
   再决定修改 Kernel projection、Core 候选字段，还是保持现状。未经该验证，不冻结最终
   字段、继承或存储。

本记录不表示源码迁移完成，也不表示生产 Runtime 已具备请求快照、恢复、重试或 exactly-once
保证。
