# Memory System 组件声明（占位）v0.1

> Status: CANDIDATE
> Authority: NONE
> Date: 2026-10-07 · 出处：AGT-018（所有者声明指令）

## 这是什么

Memory System 是 L1 四组件（UniAgent / Uni Kernel / Capability Plane /
Memory System）之一，**需要声明出来的记忆组件**：当前零落地（src/ 全树
无产品代码），本声明先占维度，实现待真实 buyer。

## 能力定位（所有者裁决 2026-10-07）

- **给 UniAgent 提供记忆**：对系统常用规则、常用知识进行管理与记忆；
- 可由 **DSH 既有记忆组件映射过来**（realization 经 adapter 复用宿主
  能力，同 UniAgent 的复用思路），也可独立实现；替换不动消费缝。

## 基线权威（§9，不改述）

唯一拥有：durable memory records、memory provenance、retention
metadata、recall result production。不拥有：Evidence admission、
Current WorldBelief、Run State、Control Intent、action authorization /
Effect Verification / Outcome Proof。

Memory recall 可提供 prior / context / hypothesis / historical
reference；**不独立建立 current-world claim**（那需要新鲜的 accepted
Evidence）。

## 升格条件

UniAgent 出现真实记忆 buyer（常用规则/知识的存取需求有实际调用方）→
独立 change 落地实现 → 本声明迁入 `docs/architecture/` 组件基线
（docs/README §3 生命周期）。在此之前不预造接口与实现（ADR-0026
buyer-driven）。
