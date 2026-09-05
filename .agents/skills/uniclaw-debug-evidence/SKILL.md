---
name: uniclaw-debug-evidence
description: UniClaw runtime diagnostic evidence & localization extension. Compose from diagnosing-bugs when a UniClaw runtime / FSM / traversal / async / real-device / flaky failure needs project-specific evidence semantics — evidence levels E0-E4, Expected/Observed/Gap/First-Divergence-Point analysis, failure taxonomy A-F, owner localization. Returns evidence only; never owns reproduce/hypothesis/fix/TDD/review/verify/complete.
source: LOCAL_UNICLAW
---

# UniClaw Debug Evidence（diagnostic extension）

> 定位：**diagnostic evidence / localization extension**，不是又一套 debugging
> workflow。通用诊断循环（feedback loop / reproduce / minimise / falsifiable
> hypotheses / minimal fix）由上游 `diagnosing-bugs` 拥有；本 skill 只在其需要
> UniClaw 运行时证据语义时被组合，产出证据后交还控制权。

## 0. Composition Contract

```text
diagnosing-bugs
      ↓ 需要 UniClaw runtime evidence？
uniclaw-debug-evidence（本 skill）
      ↓ E0-E4 / Trace / FDP / Owner
返回 evidence packet
      ↓
diagnosing-bugs 继续 → Root Cause
```

本 skill **不拥有**：general reproduce lifecycle、general hypothesis
lifecycle、fix、TDD、review、verification、completion。不宣布任何完成状态。

## 1. Evidence Levels（E0-E4）

按任务风险选择需要的最低等级（不是每个任务都要 E4）：

| level | risk | evidence required |
|-------|------|-------------------|
| E0 | compile / format / trivial edit | compiler error / message |
| E1 | unit test / local component | stack trace, assertion, input |
| E2 | stateful component / async flow | state snapshot, execution history, action/result sequence |
| E3 | Runtime / Agent / FSM / Traversal / Lifecycle | trace + state transition + observation + decision record |
| E4 | real device / integration / nondeterministic | trace timeline + observation frames + environment state + action history + reproduction context |

可用性视角（先查手头有什么）：

| level | evidence available |
|-------|--------------------|
| E0 | error message only |
| E1 | logs |
| E2 | action / history records |
| E3 | trace / state timeline |
| E4 | trace + observation timeline + fact/evidence ledger |

**规则：E0-E1 允许代码分析；E2-E4 必须先分析证据、分类落在证据上，然后
才允许代码修改。没有帧/trace 证据，不得归因于「OCR / 设备 / 模拟器」。**

## 2. Failure Taxonomy（A-F，先分类再读码）

| class | meaning |
|-------|---------|
| A | discovery failure（sources not found / inventory wrong） |
| B | grounding failure（occurrence / logical-source resolution failed） |
| C | authorization failure（candidate denied） |
| D | execution failure（action / settle / return failed） |
| E | recovery / revisit failure（bounded recovery did not cover） |
| F | environment failure（device / emulator / vision / fixture） |

同时记录：所处 lifecycle stage、最后正确状态、触发的 invariant / guard /
fail-closed gate。

## 3. Reality Analysis（Expected → Observed → Gap → FDP）

对 E2-E4 复杂失败，在提出任何代码方案前输出：

```text
Expected Reality:   用户可见目标 / 最短人类可行操作路径（可证伪假设）
Observed Reality:   观测/trace 证据显示的实际行为
Reality Gap:        二者分歧的本质
Evidence needed:    定位分歧所需的最小证据集
First Divergence:   期望与现实首次分歧的确切位置
Owner:              分歧所属的最小 owning seam
```

用 UI 路径指导取证，不因此授予 Runtime authority；不得把坐标、固定点击
序列、偶发 label 或偶然 UI 路径硬编码为场景知识。

## 4. Owner Localization

确认 owner（Agent / Traversal / Container / Environment / Semantic
capability / Perception seam）并**显式声明不触碰项**：

- 遍历 / 树的所有权边界
- 完成 / 目标证据的判定权威
- 语义判定权威
- 场景知识（Settings / 子索引 / 列表尺寸 / 坐标记忆）

若证据指向需要修改 runtime 架构、authority 或跨 seam 所有权才能解释/修复：
**停止扩展取证并携带 escalation 标记返回**（该裁决属 Human Gate /
diagnosing-bugs 的上游，不在本 skill 内）。

## 5. Evidence Packet（返回格式）

```text
- evidence summary（等级 + 关键证据条目）
- failure class（A-F）+ lifecycle stage + last correct state
- First Divergence Point（带证据指针）
- Owner（带 seam/代码/架构引用支撑）
- root-cause support（解释全部已知证据；未解释项列出）
- remaining uncertainty
- escalation（如触及 §4 边界，否则 none）
```

典型案例见 [references/canonical-cases.md](./references/canonical-cases.md)。
