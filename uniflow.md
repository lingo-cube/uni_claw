# UniFlow V2 — Development Lifecycle（唯一 Workflow 控制面）

> 本文件定义 UniFlow 生命周期。UniFlow 拥有 workflow；WorkItem 拥有执行边界；
> Skills 拥有工程方法；Artifacts 拥有持久状态；Fresh contexts 执行工作；
> Evidence 证明完成。Codex 与 DSH 只是执行 Harness（adapter），不得在各自侧
> 重定义本文件语义。

## 1. 阶段总览

```text
Intent      用户/目标的真实意图，显式化为一句话目标与成功判据
Explore     只读调查：现状、约束、最近 falsifier / First Divergence
Decision    裁决歧义与分叉；材料性分叉形成 Human Gate；产出 docs/adr/ 的 ADR 记录
Plan        产出 plans/ 工件：结构设计、模块边界、集成策略、WorkItem DAG
ToWorkItems 把 Plan 拆成自包含 WorkItem（垂直 tracer-bullet 切分）
Route       按 capability 选执行方式与 tier；确定性操作 Tool Only
Execute     Fresh Context 内 TDD / Diagnose / Implement
Review      Built Right? 与 Built The Right Thing? 分开评审；高风险 WorkItem
            使用 Fresh Review Context
Verify      对照 acceptance 核对 Evidence（机械验证优先）
Complete    UniFlow 依据 Evidence + acceptance 判定；Worker 自述不算
```

UniFlow 在任一时刻必须能回答：现在处于哪个阶段？下一步做什么？是否需要
WorkItem？哪个 WorkItem 依赖已满足？用什么能力执行？是否存在真实 Human Gate？
Evidence 是否足以完成？

**Execute 阶段硬规则**（跨分级恒定，来源：上游 `diagnosing-bugs` 纪律 +
UniFlow 门槛语义）：

```text
No reliable RED                → No fix
No falsifiable hypothesis      → Continue diagnosis
No sufficient evidence         → Continue diagnosis
No FDP / Owner                 → No implementation WorkItem
No RED → GREEN regression      → Not proven fixed
```

## 2. 分级流程

### Small（单点、无契约变化）
```text
Explore → Direct WorkItem → Execute → TDD/Verify → Complete
```

### Medium（多文件、既有契约内）
```text
Explore → Decision/Plan → WorkItems → Fresh Context Execution
→ Review → Verify → Complete
```

### Large / Architecture（新抽象/边界/生命周期/不变量）
```text
Explore → Domain Modeling / Research / Prototype（按需）
→ Frozen Decisions → Architecture Plan → WorkItem DAG
→ Fresh Context per WorkItem → Independent Review/Verification → Complete
```

分级不确定时取更高一级；禁止把 Large 拆成 Medium 绕过 gate。

## 3. ToWorkItems 拆分规则

- **垂直 tracer-bullet 优先**：一个 WorkItem 完成一条行为切片
  （Model → Runtime → Test → Evidence），不按层水平拆（Model/Runtime/Test 分家）。
- 每个 WorkItem 尽可能：independently understandable / executable / verifiable /
  reviewable，commit-sized，context-sized。
- 依赖用 `dependencies` 显式表达（DAG）；有依赖的串行执行。
- **问题大到一个 Context 无法规划时，先拆 Decision WorkItems，
  不是 Implementation WorkItems**（wayfinder 原则）。

## 4. WorkItem 上下文预算

WorkItem 只回答：做什么？为什么？在哪里？什么不能碰？什么条件算完成？
额外信息到哪里读取（`anchors` / `contract_refs` / `frozen_decisions` 按需加载）。

禁止默认附带：完整架构文档、完整历史 decisions、完整代码树、完整 prior
session、完整测试历史、完整 legacy 记录。

## 5. Fresh Context 政策

```text
One WorkItem = One Disposable Execution Context
```

- Worker 完成 WorkItem 后，Session 可直接销毁。
- 核心验收：**全新 Codex / DSH Session + WorkItem + 必要 references =
  可以正确继续执行**。
- 失败时优先检查：WorkItem 信息完整性、Decision 持久化、anchors、contracts、
  acceptance——**不要优先选择「保持更长 Session」**。

## 6. Routing

```text
Capability Requirement → UniFlow Routing → Execution Profile
→ Harness Adapter（Codex / DSH）→ Concrete Model
```

- 映射的共享部分在 `model-routing.yaml`；provider/model 绑定只在 adapter。
- 确定性操作（文件/符号查找、配置读取、明确命令）→ Tool Only，不派 Agent。
- Skill 可声明 required capability，但不得绑定具体模型。

## 7. Adapter 边界（Codex / DSH 对称）

Adapter 负责：AGENTS.md/Skill 发现、fresh session 创建、WorkItem 注入、
工具调用、模型配置、Result/Evidence 返回。

Adapter 不得修改：WorkItem 语义、Acceptance 语义、Decision 语义、Skill
procedure 语义、Verification 语义、Completion 语义。

同一 WorkItem 在两个 Harness 下必须产生可规范化的同构结果：

```text
status / changes / tests / evidence / unresolved / escalation
```

完成判定只依赖 Evidence 与 acceptance，不依赖「这是哪个 Harness」。

## 8. Human Gate

只有真实的材料性边界才请求人裁决：不变量变化、ownership/authority 转移、
安全语义、不可逆动作、两个均成立且无证据倾斜的产品级选择、显著预算扩张。
普通实现选择、测试修复、文档整理不构成 Human Gate。
