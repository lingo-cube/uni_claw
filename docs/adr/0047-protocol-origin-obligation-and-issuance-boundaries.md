# ADR-0047：P3 origin、P7 obligation projection 与 P8 issuance 边界

状态：accepted（2026-10-11，ARCH-ALIGN-001）

本决策补齐协议首轮审阅中三个容易把责任混在一起的点：观察从哪里来、Run
需要证明什么、控制指令由谁签发。

## Decision

### 1. P3 单独记录 observation origin

P2/P3 的观察记录除了 semantic kind、claim 和 provenance 外，还必须表达
observation origin。origin 与 producer 身份、ObservationContext 分开：

- producer 说明是谁产生了这条观察；
- ObservationContext 说明观察处于哪种流程上下文；
- origin 说明它属于外部观察，还是动作后 effect flow 产生的观察。

MaterialEffect 的判断必须能读取 origin。当前类型尚未有独立 origin 字段，保持
known gap；不得把 producer 名称或 Context 的组合直接宣称为完整实现。

### 2. P7 只保存 obligation definition

Run Model 的 P7 view 只表达这次 Run 要证明哪些项目，包括 obligation identity、
kind、subject、required value 和 mandatory 等定义字段。

Assurance 根据 Evidence 和相关视图计算 obligation status。状态投影不是 P7 的
第二个 canonical authority，也不允许反向修改 obligation definition。P7 的定义
与 P16 的终局 proof 保持分离。

### 3. P8 暂只保证进程内 issuance

当前 P8 用进程内的 issuance/ownership 检查确认 Control Intent 确实由
Control Loop 签发。这个实现边界继续有效。

跨 Host 传递需要额外 authority token 或等价的可验证凭证，但当前没有真实 buyer。
本 Change 不预造 token 格式、签发者或验证协议；出现真实跨 Host 场景时另开协议
变更。

## Known gaps

- ~~P2/P3 类型和持久记录尚未加入独立 origin 字段~~（已落地：PROTOCOL-FIX-001 /
  ADR-0048——`ObservationOrigin` 封闭二值，canonical 值由账本从 Context 机械
  推导，自报仅诊断；MaterialEffect 门槛要求推导的 PostActionSelf）。
- P7 的跨边 transport 与持久化投影仍是现有 in-process 范围。
- P8 没有跨 Host issuance 证据；这不是当前 Product Runtime 的已承诺能力。

## Consequences

- Evidence admission 不再把来源身份、流程上下文和 post-action 语义混在一个字段里。
- Run Model 与 Assurance 的职责可以分别审计，P7 不会侵入 P16。
- 没有真实买方的跨 Host 凭证不会提前成为公共架构名称或协议依赖。
