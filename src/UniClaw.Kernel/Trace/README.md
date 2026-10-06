# Run Trace 代码索引（非权威投影 · ADR-0013）

UniKernel composition seam 的**观察面**（TRC-001）：记录经批准的
operation 事件，供诊断与审计消费。reference-oriented、**非 authoritative**
——trace 不成为任何 owner 的事实源，缺失/损坏 fail-closed 但不影响主流程。

## 代码归属

| 文件 | 职责 |
|---|---|
| `IRunTrace.cs` | composition seam 接口（注入点） |
| `TraceModel.cs` | recorder buffer 终局状态（设计三态的现行两态） |
| `TraceReference.cs` | 封闭 union 成员（10 项词表冻结，TRC-001） |
| `SpanDefinition.cs` | 有界事件定义（S1 硬化） |
| `RunTraceScope.cs` | 记录范围 |
| `StructuralOutcome.cs` | 只描述 operation 结构是否关闭，不描述业务成功 |
| `InMemoryRunTrace.cs` / `DisabledRunTrace.cs` | 内存/禁用 realization（可替换） |
| `AsyncFileTraceWriter.cs` | 异步落盘 writer |
| `SealedTraceStore.cs` | sealed trace artifact 完整性校验（fail-closed） |

## 变更规则

观察与留痕语义进本目录；任何把 trace 升格为事实源/权威的改动违反
ADR-0013，不进。新输出格式 = 新 writer realization，经 seam 注入。

## 指向

- ADR-0013；词表冻结与设计：TRC-001 谱系；
- 消费方：诊断（`../Diagnostics/`）、Host 投影、evidence 归档。
