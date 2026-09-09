# Plan — DSE-001 Dispatch Seam 规格化（P14 收窄 + P15 三态）

> PlanType: dse-001-dispatch-seam-specification / Status: ADOPTED /
> References: changes/DSE-001/state.md · 协议基线 P14/P15/Deferred ⑤⑬ ·
> ADR-0011 · PER-003（ExportTransitionContext 迁移面）

## 垂直切片链（Human Gate 裁决指定）

```text
CanonicalBinding（EB 内部，永不下穿）
        ↓  EB lowering（Dispatch 内私有派生）
DispatchRequest（bounded command：target / effect / params / revision anchor）
        ↓
IEffectDriver.Deliver(DispatchRequest)
        ↓
DispatchResult（三态 outcome × reason）
        ↓  EB 转换（唯一 receipt canonicalization 点）
EffectReceipt（三态沿载）
        ↓
UnknownOutcome → ControlLoop recovery / re-observe
              → never blind redispatch
```

## PLAN 前侦察事实（2026-09-09，源码级）

- **F1 — CanonicalBinding 零消费**：全部 9 处 driver doubles
  （ControlToEffect / TerminalOutcome / Freshness / RunTrace / RuntimeView /
  UIWorldGroundingSeam / RealAssetEntityModel / GoalEvaluation×2）的
  `Deliver(binding)` 一律忽略参数（`=> _results.Dequeue()` 式）。整传
  CanonicalBinding 是零消费纯泄漏——收窄是纯减法，迁移面 = 9 文件签名
  机械替换。
- **F2 — spatial 无数据源**：`OccurrenceBelief` / `BindingView` /
  `GroundingView` 候选 facts / `CanonicalBinding` 全链无 spatial 字段。
  DispatchRequest 第一版 target 只能是 occurrence 引用（唯一有据形式）；
  **不造无源 spatial 字段**（ADR-0011）。physical-target 完整化 = 显式
  后续缺口，随首个真实 driver（ADB 移植）buyer 出现时经 World 侧
  spatial fact 扩展另立 change。
- **F3 — UniKernel.cs 零改动**：`EB.Dispatch(binding, judgment, view)`
  对外签名不变，lowering 全在方法体内；Act pipeline 与 PER-003 的
  `ActResult.TransitionContext` 填充零影响。ControlLoop 侧仅
  `NoteDispatchOutcome` 方法体一行条件扩展——CTL-001 out-of-scope 已
  禁改 ControlLoop，无 in-flight 冲突。

## Before / After

| 文件 | 变化 | Owner |
|---|---|---|
| `Effects/EffectDispatch.cs` | ① 新 `DispatchRequest(string Target, string EffectClass, string? Parameters, string RevisionId)`——UI 通道 Target = TargetOccurrenceId、非 UI 字符串通道 = TargetSubject（Deferred ⑮ 沿载）；**无 IntentId / BindingId / RevisionNumber / judgment 字段**。② `DispatchOutcome`：`Delivered→DeliveryCompleted`、`Failed→DeliveryFailed`、新增 `UnknownOutcome`。③ `DispatchResult` 增 `string? Reason = null`（diagnostic 分类，string 不锁词汇——协议通则 4）。④ `IEffectDriver.Deliver(DispatchRequest)` | 协议面 |
| `Effects/EffectBoundary.cs` | `Dispatch` 体内新私有 lowering：CanonicalBinding → DispatchRequest（四字段派生）；`_driver.Deliver(request)`；receipt 构造沿载 `result.Reason`。`ExportAttemptEvidence` 的 claim value 随枚举值变为 `deliverycompleted / deliveryfailed / unknownoutcome`（描述性 provenance，语义安全；测试断言适配） | EB |
| `Control/ControlLoop.cs` | `NoteDispatchOutcome`：触发条件 `Failed` → `is DeliveryFailed or UnknownOutcome`（re-observe recovery 扩展；DeliveryCompleted 不触发） | Control |
| `tests/`（9 文件） | doubles 签名迁移（`Deliver(DispatchRequest _)`）+ 枚举值机械改名；架构断言逐条保持（CBA-005 先例） | —— |
| `tests/`（新增） | 结构断言 + UnknownOutcome recovery 闭环场景（见验收映射） | —— |
| 协议基线 | P14 Reference Realization 更新为 `Deliver(DispatchRequest)`；P15 Status 增三态注记；Deferred ⑤⑬ 标注闭合；P15 minimal payload 增 reason 一行 | 文档 |
| `CONTEXT.md` | Dispatch Request / Dispatch Result 词条微同步（三态 outcome 语义） | 文档 |

## 关键语义（固化，防漂移）

1. **binding 不穿透（验证点 1/3）**：`IEffectDriver` 接口文件不再出现
   CanonicalBinding 类型——编译级隔离；另加反射结构断言：DispatchRequest
   属性白名单恰为 `Target / EffectClass / Parameters / RevisionId` 四字段。
2. **字段 = 协议即 buyer（验证点 2）**：现存 driver 零字段消费（F1），
   字段集按 P14 minimal payload 语义落定；spatial 不落（F2），在协议基线
   P14 注记「physical-target anchor 待 World 侧 spatial fact buyer」。
3. **三态 × reason 覆盖（验证点 4）**：三态场景各一（Completed / Failed+
   reason / Unknown+reason=timeout-killed），断言 reason 透传至 receipt 且
   claim value 正确；「结果不可解读」= driver 返回
   `UnknownOutcome` + reason `result-uninterpretable`（Deferred ⑬ fail-closed
   表达）。
4. **Unknown → re-observe 真闭环（验证点 5，最重）**：场景测试全链断言——
   driver 返回 UnknownOutcome → receipt 入 log → `NoteDispatchOutcome` 置
   pendingRecovery → 下轮 `SelectIntent` 产出 **Recovery intent**（非 Act）→
   零新 dispatch；同场景追加：对同一 binding 再次 `Dispatch` 被
   `binding-already-dispatched` 拒绝（never blind redispatch 的 EB 侧执法面）。
   验收即证明这不是枚举改名——recovery 行为随 outcome 真实变化。
5. **reason 不进共享协议词汇**：string diagnostic，分类词表（timeout-killed /
   device-offline / …）不声明 exhaustive（协议通则 4）；Control 决策只消费
   outcome 三态，不消费 reason。

## 切片（tracer bullet，不按层拆）

1. **切片 1 — seam 切换**：EffectDispatch 新类型/枚举/签名 + EB lowering +
   9 文件迁移 + 结构断言。既有测试全绿（机械改名）。
2. **切片 2 — 运行语义闭环**：NoteDispatchOutcome 扩展 + UnknownOutcome→
   Recovery 场景 + never-blind-redispatch 断言 + PER-003 形状复验。
3. **切片 3 — 协议/词条收口**：协议基线 P14/P15/Deferred ⑤⑬ + CONTEXT.md
   词条同步。

实施建议：切片 1+2 合并为单 WorkItem（TDD：先红结构断言与 Unknown 场景）；
切片 3 文档面随同 change 收口（CBA-005 先例）。IMPLEMENT 时按
`schemas/work-item.schema.json` 立契。

## 风险

- doubles 迁移面广（9 文件）但零行为变化（F1）——逐文件机械替换可复核。
- CTL-001 并行：其禁改 ControlLoop（out-of-scope 声明）、DSE-001 不触
  UniKernel / Control 新增面（F3）——文件面零交集；若 CTL-001 提前 closed，
  无需本 plan 修订。
- 若实施中发现 driver doubles 有隐藏的 binding 字段消费（与 F1 侦察矛盾），
  停下重估字段集——以真实消费为准重新过 buyer。
