# Plan — CDS-001 Desired-State Satisfaction（occurrence state 载荷 + policy 检查）

> PlanType: cds-001-desired-state-satisfaction / Status: ADOPTED /
> References: changes/CDS-001/state.md（含入口复验记录）· Human Gate HD-4
> 裁决 · ADR-0011（字段 buyer）· CTL-001（TargetSpec/policy 宿主）·
> CLE-001（claim evolution——载荷路径否决依据）

## 垂直切片链

```text
IUiObservationStrategy.Derive（ProposedOccurrence + State）
    ↓ WorldModel 铸造（revision-local，随 occurrence 集合替换）
OccurrenceBelief.State
    ↓ DeriveSlice
Slice.Occurrences → OccurrenceFact.State（Control 消费面）
    ↓ DescriptorTargetPolicy.Decide（TargetSpec.DesiredState）
Satisfied  → visited + 跳过（无 intent —— decision outcome，非 NoOp effect）
Unknown    → 跳过不 visited（observe / resolve per policy）
Unsatisfied → Act（全链不变）
    ↓ （I-3：非幂等动作的事前保证——post-action verify 只能发现不能防止）
```

## Before / After

| 文件 | 变化 |
|---|---|
| `World/UiEntityModel.cs` | `ProposedOccurrence` / `OccurrenceBelief` 各增 `string? State = null`（末位可选） |
| `World/Slice.cs` | `OccurrenceFact` 增 `string? State = null`（末位可选） |
| `World/WorldModel.cs` | occurrence 铸造（:243）与 DeriveSlice（:770）直通 `State`（两处一行） |
| `Control/DescriptorTargetPolicy.cs` | `TargetSpec` 增 `string? DesiredState = null`；Decide 匹配后 satisfaction 三分支；类注释记 I-3 依据 |
| `docs/adr/0017-*.md` | I-3 ADR（pre-dispatch non-idempotent safety） |
| `CONTEXT.md` | Avoid 词条（Operate/IOperation/Executor）+ Desired-State Satisfaction 词条 |
| `tests/.../DesiredStateSatisfactionTests.cs` | 新增 S1–S7 场景 |

不触：Effects/、UniKernel.cs、GroundingSeam（CandidateOccurrenceFact 不加
state——grounding buyer 不需要）、既有 doubles（全部新字段可选默认）。

## 关键语义（防漂移）

1. occurrence State 是 **revision-local presentation fact**：随 occurrence
   集合替换、不进 claim 演化域（CLE-001 Revise 域不受影响——P-UW-25
   occurrence id 每轮重铸，State 与 id 同生命周期）。
2. satisfaction 满足分支标 visited = 该 spec 完成（多 spec 推进）；Unknown
   跳过**不**标 visited（未完成，待新观察可判——observe 语义由 policy 序列
   自然表达：Observe → 新 revision → 新 occurrence 可能带 State）。
3. DesiredState=null（Click 型）不做检查——回归保护（S6）。
4. I-3 的执法点在本 policy；Assurance/EB/Gate 零改动（out-of-scope 声明）。

## 验收映射（state.md S1–S7 → 测试）

- S1 载荷：strategy 输出 State → belief → Slice 全链可见
- S2 满足：DesiredState="true" ∧ State="true" → Observe、visited 含该 spec、
  零 dispatch / 零 receipt / 零 binding
- S3 不满足：State="false" → Act 全链（Bind→Judge→Gate→Deliver 不变）
- S4 Unknown：State=null → Observe 不 Act、不 visited
- S5 端到端 I-3/U2：kernel 全链已满足 → SelectIntent=Observe → 零物理动作
- S6 DesiredState=null → State 任意仍 Act（Click 型回归）
- S7 既有 183 全量零回归（新字段全可选）
