# PlanCompiler Default Alignment — 设计文档 (Change A: plan-side)

> 日期: 2026-07-19
> 对应(潜在)change: `openspec/changes/plancompiler-default-alignment/`(空 scaffold,暂未启用为变更任务)
> 状态: 设计完成 (model B 终止模型, plan-side), 待实现
> 关联: D-86 (element_coverage 完备性硬化) 上游正确性依赖; **Change B** (`container-handler-canonicalization`, engine-side) 下游

## 0. Scope 切分 (切开)

终止模型工作跨 Graph + StateMachine + Traversal 三层,且 engine 侧有 dormant 组件待清理(见 §1.2)。**本 change = Change A,只做 PLAN 侧**;engine 侧另立 Change B。

| | Change A (本 change) | Change B (后续) |
|---|---|---|
| 层 | Graph + 文档 | StateMachine + Traversal |
| 做什么 | 定义终止模型 + PlanCompiler 派生正确 | 引擎消费模型 + 接通 dormant 组件 |
| ExitCondition | 暂不删(live consumer 还在 set) | 删 |
| PlanCompiler | dormant 保持(派生对,单测验) | (由真机 change 接通) |
| 风险 | 低 | 中高 |

## 1. 背景

### 1.1 问题起点

`PlanCompiler`(确定性 `IntentSlots → TraversalPlan` 编译器)存在字段读错、词表错位、默认值漂移三类问题,使它发出的 `CompletionPolicy` 与下游 D-86 Mode 自动分流(`ResolveModeAndTarget`)预期不一致。PlanCompiler 当前 **dormant**(baseline 手搓 plan,不经过它),故本 change 是**预防性正确性修复**。

### 1.2 关键发现:终止模型的 dormant 链

探索中发现**一连串 dormant**(声明了、没接通):

| 组件 | 状态 | 本 change 处置 |
|---|---|---|
| `PlanCompiler` | dormant(baseline 不调用) | Change A 修对派生(仍 dormant,单测验) |
| D-86 Mode auto-derive | dormant(JSON 全显式 mode) | 不动 |
| `ExitCondition`/`ExitConditionType` | dead(InterceptionHandler 硬编码 AllChildrenVisited) | **延 Change B 删** |
| `ContainerHandler`(CompletionDetector/FallbackDecider/Executor) | **dormant**(仅单测,src 零调用) | **Change B wire 为正宗** |

`ContainerHandler` 是 D-16 设计的 3 子组件解耦管线(纯函数、单测覆盖),是**更好的容器完成设计**;但生产容器完成实际由 `InterceptionHandler` 跑(9 处 FrameCompleted 散落,与 interception 揉在一起)。**Change B 让 ContainerHandler 正宗化、InterceptionHandler 委托容器完成给它**(各管各的)。本 change(A)不碰引擎。

## 2. 终止模型:model B(消除隐式依赖)

### 2.1 原模型的隐式依赖

原设计两级终止:
- `CompletionPolicy`(全局意图):None / TargetFound / MaxSteps / Timeout
- `ExitCondition`(局部容器):AllChildrenVisited / ScrollEnd / DepthLimited / SingleLevel + Fallback

但 `CompletionPolicy.None` 的终止**隐式依赖** `ExitCondition` 级联到根 —— 而 `ExitCondition.Type` 又被硬编码、不消费。**依赖既隐式又断**。

### 2.2 model B:消除依赖,终止归一

消除隐式依赖:终止权威**归一 `CompletionPolicy`**;`ExitConditionType` 删(职责被 Depth/DescendAll/内容自适应顶替);真实终止原因归**结果层**(三层分层见 §3)。

```
意图 (policy)    CompletionPolicy.Type ∈ { Exhaustive, TargetFound, MaxSteps, Timeout }
约束 (bound)     IntentSlots.Depth (intent, 两来源 + priority (a), 见 §5.6)
边界 (entry)     IntentSlots.Entry (整树 vs 子菜单)
探索 (scope)     DescendAll 唯一 (深度由 bound 管, 滚动由内容自适应)
退出 (exit)      engine 内部决定 (Change B: FallbackDecider 拥有)
结果 (outcome)   TraversalResult.Reason 四档 (达成/约束剪枝/异常/外部), 见 §3 Layer B
```
> 上表是 model B **目标态**。Change A 落地时 `Exhaustive` 仍名 `None`(改名延 Change B,见 §5.3);Depth 的 engine 解析、结果层三档、ExitCondition 删除亦在 Change B。A 只做意图层(CompletionPolicy 语义 + PlanCompiler 派生 + Entry/Depth 字段)。

### 2.3 场景 → 维度映射(model B 验证)

| 场景 | 终止 Type | Depth | Entry | Mode |
|------|----------|-------|-------|------|
| 整树穷尽 | Exhaustive | null | app-root | Exact |
| 子菜单穷尽 | Exhaustive | null | sub-menu-root | Exact |
| 限深穷尽(避深层意外页) | Exhaustive | N | — | Exact |
| 滚动穷尽 | Exhaustive | null | — | Exact(内容自适应探滚底) |
| 找目标即停 | TargetFound | null | — | Subset |

子菜单穷尽 = `Exhaustive` + `DescendAll` + `Entry=sub-menu`(边界内禀于 Entry + Back 导航,**不需要 SingleLevel**)。

## 3. 三层分层(per-container / 全局 / 约束)

```
Layer A (per-container 事件) → Trace         帧pop/cap咬/滚到底, 逐步记录, 不进结果
Layer B (全局终止)            → Result.Reason  对外接口: 干净少量值
Layer C (约束上下文)          → Plan          D-86 读 IntentSlots.Depth 推导 out-of-scope
```

**Layer B 结果四档**(Change B 落地;A 只定义规范):
- **达成**:AllVisited / TargetFound
- **约束剪枝**:MaxSteps / Timeout(步/时预算截断的未访元素算 out-of-scope,非 missed)
- **异常**:AntiLoop / Error(硬失败,完备性免谈)
- **外部**:Cancelled(用户主动中止,既有 reason;Change B 归入此档)

不变量:**异常永不伪装 AllVisited**。`MaxDepth`/`ScrollEnd` 是 **Layer A per-container 事件**(级联聚合为全局 AllVisited),不进 Layer B。

## 4. 问题清单

### 正确性
- **P1 字段读错**:`BuildDynamicRules` 用 `Scope` 查 `TemplateSets`,该用 `ElementHandling`。
- **P2 Scope 词表错位**:`ValidateSlots` 把 element_handling 词表当 Scope 合法值。
- **P3 Completion 派生错位**:`target_path → TargetFound` 错位(应 `full→None`,`target_only→TargetFound`)。
- **P4 override 静默吞**:`Completion` switch `_ => None` 把未知值静默当 None,违反 fail-fast(C-1~C-4)。

### 内聚
- **P5 EntryPolicy 硬编码**:`DirectDeeplink`/`cold_launch` 非普适默认。
- **P6 数值散落**:timeout/max_steps magic number,非命名常量。

## 5. 设计(Change A:plan-side)

### 5.1 词表锁(两套正交)
```
Scope           ∈ { full, target_only }                          ← 遍历形状
ElementHandling ∈ { full_interaction, menu_only, safe_mode, read_only }  ← 交互策略
```

### 5.2 IntentSlots(改:加 Entry,收窄 Scope 词表)
```csharp
public sealed record class IntentSlots(
    string TargetApp,
    string Scope,           // ∈ {full, target_only}  ← 词表收窄
    string? Target = null,
    int? Depth = null,      // intent 深度约束, null=无约束(DescendAll); 两来源 + priority (a) 见 §5.6
    string? ElementHandling = null,  // 现在真被读
    string? Navigation = null,
    bool? Restore = null,
    string? Completion = null,       // override ∈ {max_steps, timeout}, 覆盖 Type (见 5.4)
    string? Entry = null);           // 【新增】遍历根, null=app-root; 子菜单穷尽用
```
> `Entry` 命名待定(候选 Entry/Root/TraversalRoot,避 RootNode 歧义)。

### 5.3 CompletionPolicy:None 语义澄清(改名延 Change B)
- **Change A 保留 `CompletionPolicyType.None`**,但把语义**澄清坐实**为:「不意图打断,目标自然耗尽」(= model B 的 Exhaustive 意图)。PlanCompiler 对 `scope=full` 派生 `Type=None`。
- **`None → Exhaustive` 改名延 Change B**:引擎 `TraversalEngine.cs:286` 有 `if (policy.Type != CompletionPolicyType.None)` 硬判定,改名必须同步这行 → 属 engine 侧,归 Change B。A 不碰引擎,故只澄清语义、不改名。
- **意图层只表达意图**,永不解释「为何真停」(那是 Layer B 结果层职责)。

### 5.4 Completion 派生:scope 定默认 Type,override 覆盖 Type
```
Scope (无 override)   派生 Type                              Mode
──────────────────   ──────────────────                    ─────
full                 None (exhaustive 语义, 改名 Exhaustive 延 B)  Exact
target_only          TargetFound(TargetName=Target,              Subset
                     MatchMode=Contains, MarkAndStop)

Completion override (覆盖 Type, 经引擎代码验证):
  "max_steps" → Type=MaxSteps (+ MaxSteps=N)
  "timeout"   → Type=Timeout   (+ TimeoutSeconds=N)
```
**override 覆盖 Type**(非「叠 bound 不改 Type」):引擎 bound 检查以 Type 为门(TraversalEngine L315/L323),Type 不变则 bound 失效。`full + max_steps` → Type=MaxSteps(= partial 归约)。
> **partial × Exact 张力(前向,零场景):** 步数受限遍历在 Exact 下会把截断未访判 missed;partial 场景出现时需 allowedMisses 配合(类 D-jump)。本期不处理。

### 5.5 PlanCompiler:维度 → 字段(P1-P4 修正)
- `BuildDynamicRules`:`ElementHandling ?? "full_interaction"` 查 TemplateSets(修 P1)
- `ValidateSlots`:Scope ∈ {full,target_only};ElementHandling ∈ 交互词表;target_only ⇒ Target 非空(修 P2)
- `BuildCompletionPolicy`:按 5.4 派生(修 P3)
- `BuildRootNode`:RootNode 反映 Entry(默认 TargetApp)
- 移除 `target_path` 分支 + `BuildStaticNodes`(target_path 词表删,静态节点构造随之移除)
- `Completion` 非法值 fail-fast throw(修 P4)

### 5.6 Depth:两来源 + priority (a) 紧者胜(规则定义)
```
来源 1: TraversalEngineConfig.MaxDepth  (引擎/部署级硬天花板)
来源 2: IntentSlots.Depth                (plan/意图级)
effective = min(config.MaxDepth, IntentSlots.Depth ?? ∞)   ← 紧者胜, config 是硬天花板, intent 在内收紧
```
- 同一作用(都 bound depth),关系是优先级,非合并。
- 咬了都一样 → Layer C(约束)+ 全局 AllVisited;**无异常 depth 档**;失控归 AntiLoop + MaxSteps。
- **Change A 只定义规则**;engine 侧实际解析(`IntentSlots.Depth → 引擎 MaxDepth`,现状 L79 用 config 没接 intent)**在 Change B 接通**。
- config 顶层分类(Ceilings/ErrorPolicy/...)**暂不做** —— 属更高层维度,后期按系统/业务组再做。

### 5.7 校验(fail-fast)
| 校验 | 规则 | 违例 |
|---|---|---|
| Scope | ∈ {full, target_only};target_only ⇒ Target 非空 | throw |
| ElementHandling | 若给 ∈ 交互词表;null→默认 full_interaction | 非法 throw / 空 graceful |
| Entry | null=合法(app-root 默认)/ 非空字符串=合法(app-specific,无词表) | 空串 throw |
| Depth | ≥ 0(可选加上界 1000) | 负 throw |
| Completion | 若给 ∈ {max_steps, timeout} | 非法 throw(修 P4) |

### 5.8 数值默认 + EntryPolicy(命名常量)
```csharp
public const double DefaultCompletionTimeoutSeconds = 300;  // 穷尽安全上界
public const int    DefaultCompletionMaxSteps       = 500;
public const double EntryTimeoutSeconds             = 10;
```
EntryPolicy 默认 `ColdLaunch`/`fallback=null`(从 DirectDeeplink 改,不预设深链存在)。

## 6. ExitCondition 处置:延 Change B 删

**不在 A 删**:`InterceptionHandler` 生产中 live-set `ExitCondition`(nav 子帧硬编码 AllChildrenVisited+AutoEscape,InterceptionHandler.cs:213;动态子节点继承 node.ExitCondition,TraversalEngine.cs:643)。A 删字段会破生产。

**Change B 删的条件**:Change B 把容器完成接给 `ContainerHandler`、InterceptionHandler 停止 set ExitCondition 后,`ExitCondition`/`ExitConditionType` 无 live consumer → 删。`FallbackAction` enum 留(Change B 里 FallbackDecider 用)。

## 7. Dormant 立场 + 测试

- baseline 不动(手搓 plan,JSON 显式 mode)→ PlanCompiler + D-86 auto-derive 继续 dormant。
- PlanCompiler 正确性仅 PlanCompilerTests 单验;全链路验证留真机 change。
- **PlanCompilerTests(GraphTests.cs,6 调用点全在此)**:
  - `("app","full_interaction")` → `("app","full",elementHandling:"full_interaction")`,验 DynamicRules 来自 ElementHandling、Type=None(exhaustive 语义)
  - `("app","target_path","wifi")` → `("app","target_only",target:"wifi")`,验 Type=TargetFound/TargetName
  - invalid scope throw(含 `target_path` 也 throw)+ target_only 缺 Target throw + Completion 非法 throw
  - 新覆盖:Entry→RootNode、override 覆盖 Type
- **guard/enum**:`ArchitectureGuardTests` 不受影响(未锁 IntentSlots/PlanCompiler/CompletionPolicy);`locked-enums.md` 只锁 NodeType。A **不动 `CompletionPolicyType`**(保留 None,Exhaustive 改名延 Change B)。→ 711 绿不受威胁(动 PlanCompiler.cs + GraphTests.cs + IntentSlots 加 Entry)。

## 8. Change B 范围预告(engine-side,本 change 不做)

- wire `ContainerHandler` 为正宗容器完成路径;`InterceptionHandler` 剥离容器完成、委托之,只留 interception
- `CompletionPolicyType.None → Exhaustive` 改名 + 引擎 `TraversalEngine.cs:286` 判定同步(`!= None` → `!= Exhaustive`)
- `IntentSlots.Depth → 引擎 MaxDepth`(priority (a) 解析接通;现状断点修复)
- `TraversalResult.Reason`:Exhaustive 落地、四档(达成/约束剪枝/异常/外部)保真、补 MaxDepth/ScrollEnd 为 Layer A trace 事件
- 删 `ExitCondition`/`ExitConditionType`(此时无 live consumer)
- **Problem 3(Fallback 归宿)**:FallbackDecider 拥有 exit-action,nav 子帧 AutoEscape 改由 context 探测
- **Problem 4(Result 分类)**:Cancelled/外部中止档归属

> Problem 3/4 在 model B 讨论中浮现,但都纠缠在 engine 侧(依赖 ContainerHandler 裁决 + Result.Reason 改动),归 Change B。

## 9. 关键验证(自审)

- **full→Exhaustive 终止**:引擎对 Type=None/Exhaustive 跳过 CompletionPolicy 块,靠 AllVisited(FrameCompleted && Depth≤1)自止(TraversalEngine L277/L285)。→ 不会跑飞。(注:AllVisited→Exhaustive 的 reason 映射在 Change B;A 只保证 PlanCompiler 派生 Type 正确。)
- **target_only→TargetFound 匹配**:引擎用 currentNode.Operation.Target.Value 对 policy.TargetName 按 Contains 匹配(L289-309)→ 命中 CompletionReason=TargetFound → D-86 Subset guard MarkAndStop 分支通过。
- **override 覆盖 Type**:引擎 MaxSteps/Timeout 检查以 Type 为门(L315/L323),故 override 必须改 Type 才生效(5.4 设计经此验证)。

## 10. 影响面(Change A)
| 文件 | 改动 |
|---|---|
| `Graph/Models/TraversalPlan.cs` | IntentSlots 加 Entry(CompletionPolicyType **不改** — Exhaustive 改名延 Change B) |
| `Graph/Services/PlanCompiler.cs` | P1-P6,移除 target_path/BuildStaticNodes,数值抽常量(P6),EntryPolicy 默认(P5),Depth/Entry/CompletionPolicy(None=exhaustive 语义)派生 |
| `tests/Graph/GraphTests.cs` | PlanCompilerTests 6 调用点重写 + 新覆盖 |
| baseline / 引擎 | **不动**(dormant;引擎改在 Change B) |

## 11. 验收标准(Change A)
- [ ] `dotnet build` 0 错误,0 功能性警告
- [ ] `dotnet test` 全绿,数 ≥ 711
- [ ] PlanCompiler:`Scope=full`→Type=None(exhaustive 语义,改名延 B);`Scope=target_only`→Type=TargetFound(TargetName,Contains,MarkAndStop)
- [ ] `BuildDynamicRules` 读 ElementHandling(非 Scope)
- [ ] `Completion` 非法值 fail-fast throw
- [ ] `target_path` 作 Scope 被拒(throw)
- [ ] override 覆盖 Type(`full+max_steps`→Type=MaxSteps)
- [ ] IntentSlots 加 Entry;Depth 语义注释含 priority (a) 规则
- [ ] 数值默认抽命名常量
- [ ] ExitCondition **未删**(留 Change B)
- [ ] 文档记录 Change B 范围(§8)

## 12. Open Questions
1. `Exhaustive` 命名已定(Exhaustive),但**改名执行延 Change B**(引擎 L286 耦合)。A 期保留 `None`,语义澄清为 exhaustive intent。
2. `Entry` 字段命名(Entry / Root / TraversalRoot)?
3. `Depth` 是否加上界 1000?倾向加(对称、低成本)。
4. `Scope=full + Target` 忽略 vs fail-fast?取忽略。
5. EntryStrategy 枚举确切值实现时确认(确保 ColdLaunch 存在)。
