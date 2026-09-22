# PER-009 REVIEW 报告（两轴，`1a709af..cf4a846`，31 文件 +1562/−34）

> 日期：2026-09-22 · 方法：code-review skill（Standards + Spec 并行子代理）
> 固定点：`1a709af`（机制冻结，最后一个纯文档提交）
> 结论：**CHANGES_REQUIRED**

## Standards 轴

**AGENTS.md §1.5 规则 1–4：无硬违规**（缝/边界/验收留痕/裁决链全部有据——白名单授权记录在案、XmlAuthoritySnapshot 投影保持 Kernel 无 Host 类型依赖、台账/state.md 完整）。

### 最接近硬违规（代码违背自身文档契约）

| # | 问题 | 详情 |
|---|---|---|
| S-1 | `TryDumpToDevice` 超时失效 | `ReadToEnd()` 在 `WaitForExit(timeoutMs)` **之前**阻塞——超时永远约束不了读取；且自抛的 fail-closed `InvalidOperationException` 被外层 `catch (Exception)` 吞掉 |
| S-2 | `ProbeStateMachine` 生命周期错配 | 文档说"每 Run ≤3"，但 `_probesThisRun` 无重置机制、实例为 feed 生命周期 → 3 次后**永久耗尽**；`MarkTransient()` 是空操作 |

### 规则 5 投机（判断题，已分阶段但标记）

- `ProducerTrust.DeepProducer`——为明确延后的 Tier 2 预置了常量与信任行
- `ObservationDirective.Depth/Subjects` 在公开驱动面上但**零生产 feed 读取**——全部只解包 `directive.Context`
- `WorldModel.DeriveControlBeliefView()`（抛异常版）零调用方——只有 `OrNull` 版被消费

### Fowler 坏味道（判断题）

- **重复代码**：身份/属性/时序门级联写了两遍（`ConflictResolver.Resolve` 三道门 vs `PostActionXmlRouter.Route` 四门，同谓词不同顺序）
- **基本类型偏执 + 重复 switch**：三态 checked 以字符串 `"false"|"true"|"partial"` switch 级联出现在两个文件；Bounds 为逗号连接字符串

---

## Spec 轴

### (a) 缺失/部分实现（8 项）

| # | 规格 | 现状 |
|---|---|---|
| P-1 | D5 producer-trust.json 落点 | **文件从未落地**——只有内存 `Default()`/`FromJson`；无自动调优/回滚通道 |
| P-2 | Acceptance #7 信任门槛 | `CanAuthorizeRoutine`/`RequiresCorroboration` **零生产调用方** |
| P-3 | Acceptance #3 跨源碰头 | 解析器只发 `ui.node.*`；共享 `*.state` 映射"由消费方解析"——**无此消费方**，XML 与视觉永远不会在同一个 key 上碰头 |
| P-4 | D8 预算 = 视觉+500ms | **无任何预算计算**；D14 新鲜度窗口只是调用方传参，无生产侧推导 |
| P-5 | Acceptance #11 规范调研 | 只有 grill docket，无逐格出处文件 |
| P-6 | mechanism ① 中档裁剪重扫 | Host feeds **忽略** `directive.Subjects`/`Depth` → Focused 重拉返回相同数据 → 只能耗尽 |
| P-7 | D3/D12 text 权威 | `FieldOf` 只处理 state/enabled/selected/focused；text 权威自文档延后 |
| P-8 | 文档偏移 | plan 仍列 S7-wiring/S8 为 `[ ]`，但 state.md 宣称"S1–S8 全部落地" |

### (b) 超范围

- `DeepProducer` 预置（Out of Scope 明说 Tier 2 延后）
- 次要：invariant-matrix 头修复 + 台账 #20（housekeeping，可接受）

### (c) 实现了但错了（3 项——最高风险区）

| # | 规格 | 实际行为 |
|---|---|---|
| **C-1** | D13 权威域内 XML 销案、不升档 | `ConflictResolver.Resolve` **零生产调用方**——真实 XML/视觉冲突直接走聚焦升档（≤3 次）→ `GroundingFailed`，**永远不销案**；mechanism ⑧ SafeStop 也不成立 |
| **C-2** | D9 四门可执行 | `PostActionXmlRouter.Route` 消费调用方传入的 `IdentityUnique` 旗标——**无任何东西构建快照**（S7-wiring 仍开放）；重解析从未运行 |
| **C-3** | D8 每 Run ≤3 | `_probesThisRun` 永不重置 → 3 次后本 feed 永不再探测 |

---

## 一行总结

```text
Standards 轴：0 硬违规 / 2 项代码违背自身契约 / 3 项投机 / 2 项坏味道
             最重：TryDumpToDevice 超时失效 + fail-closed 被吞
Spec 轴：    8 项缺失 / 1 项超范围 / 3 项实现错位
             最重：C-1 —— 裁决器/信任表/路由器是死代码，运行时行为
             反转为"只升档永远不销案"，与 D13 冻结规则直接矛盾
```

## 核心发现模式

**纯函数 + 测试全绿 ≠ 机制生效。** S3–S7 的裁决器、信任表、路由器全部作为独立纯函数存在且有测试，但没有一条生产路径调用它们。唯一的运行时新行为是聚焦环路（正确地有界耗尽），而销案/门槛/路由从未接线。这是典型的"验证了零件、没验证系统"。

## 修复优先级（待裁决）

```text
P1  C-1 裁决器接线（真实冲突 → ConflictResolver → 销案）
    C-3 ProbeStateMachine 每 Run 重置
P2  S-1 TryDumpToDevice 超时修复 + fail-closed 不吞
    P-3 共享层 *.state 映射消费方（跨源碰头前提）
P3  P-1 producer-trust.json 落地
    P-2 信任门槛接线
    P-6 Focused 裁剪（PENDING-ENV）
    P-8 文档对齐
    Scope  DeepProducer 删除
```
