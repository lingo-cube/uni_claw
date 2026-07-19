# ContainerHandler Canonicalization — 设计文档 (Change B: engine-side)

> 日期: 2026-07-19
> 关联: **Change A** = [plancompiler-default-alignment-design.md](2026-07-19-plancompiler-default-alignment-design.md)(plan-side, 定义 model B 意图层)
> 状态: 设计完成, 待实现(Change A 之后)
> 性质: **engine-side, 行为变更**(非 dormant 安全 — 见 §7 风险)

## 0. 与 Change A 的关系

终止模型 model B 分两半落地:
- **Change A(plan-side,已设计)**:定义意图层 —— CompletionPolicy 语义、IntentSlots.Depth/Entry、PlanCompiler 派生。dormant 安全。
- **Change B(本 change,engine-side)**:让引擎**消费**意图层 —— wire ContainerHandler、接 Depth、保真 reason、删 ExitCondition。**行为变更**(改 live frame 完成路径)。

两者复合才让 model B 端到端生效。Change B 不依赖 Change A 先落地(可独立),但语义闭环需两者皆成。

## 1. 背景:ContainerHandler dormant

`ContainerHandler`(D-16 设计的 3 子组件管线:CompletionDetector / FallbackDecider / ContainerActionExecutor,纯函数、单测覆盖)**未接进引擎** —— src 生产代码零调用,仅单测跑。

生产容器完成实际由 `InterceptionHandler` 跑(`FrameCompleted` 散落 9 处,与 interception 揉在一起,ad-hoc 判定,非 5 优先级链)。这是**容器完成逻辑的两份重复**—— 一份好的(dormant)、一份活的(ad-hoc)。

**本 change:** ContainerHandler 正宗化(wire 进引擎),InterceptionHandler 剥离容器完成、委托之,各管各的。属「完成本该完成的设计」。

## 2. 核心设计:边界切分(推荐方案)

```
InterceptionHandler = 事件检测 + 产出事实
  职责: TryHandleNavigation (导航检测), TryHandleScrollAsync (滚动 reveal 检测)
  产出: 事实集 — nav 发生? scroll reveal 新内容? 当前 visited/total child 数? 指纹变化?
  不再做: 直接判 FrameCompleted (散落的 result.FrameCompleted = true 全移除)

ContainerHandler = 完成决策 (唯一权威)
  职责: CompletionDetector 5 优先级链 (timeout → maxdepth → no-children → all-visited → incomplete)
  产出: FrameCompleted 决策 + CompletionReason (入 trace Layer A) + FallbackAction (FallbackDecider)
```

**数据流:**
```
InterceptionHandler.OnBranch/OnDynamicMatchNodeSelect/OnFrameComplete (FSM hooks)
  → 收集事实 (nav/scroll/child 计数/指纹)
  → 构造 CompletionContext
  → ContainerHandler.HandleContainer(ctx, canContinue, nodeId, traversalContext)
  → CompletionResult (IsComplete, Reason, SuggestedAction, ShouldBacktrack)
  → 设 FrameCompleted + 记 trace (Layer A) + 执行 exit-action
```

**为何这样切:** interception 是「中途发生了什么事件」;完成是「容器是否走完」。两者本质不同,揉一起才需要 9 处 ad-hoc FrameCompleted。分开后,完成有唯一权威(ContainerHandler)、单一逻辑(5 优先级链)、可测(纯函数已单测)。

## 3. wire ContainerHandler 进引擎

- **CompletionContext 构造**:从引擎运行态填充 `ElapsedMs / MaxDepth(见 §4) / CurrentDepth / TotalChildren / VisitedChildCount`。`ExitConditionFallback` 字段移除(见 §6)。
- **FSM hooks 委托**:`OnFrameComplete`(step 10)是天然主委托点;`OnBranch`/`OnDynamicMatchNodeSelect` 内的 FrameCompleted 判定改走 ContainerHandler。
- **StepOrchestrator**:步骤 8-10 接线,ContainerHandler 注入(构造或 DI)。
- **`ContainerActionResult → FrameCompleted` 翻译**:`HandleContainer` 返回 `ContainerActionResult`(Action + Success),非布尔;调用方(InterceptionHandler/StepOrchestrator)据 Action 翻译:`Back`/`AutoEscape`/`Skip` → `FrameCompleted=true`(帧将 pop);`Abort` → 不设 `FrameCompleted`(走引擎错误/终止路径,产 Error reason)。

## 4. Depth 接通:priority (a) 紧者胜

现状断点:引擎 `TraversalEngine.cs:79` 用 `config.MaxDepth`,无视 `IntentSlots.Depth`。

```
effective_depth = min(config.MaxDepth, plan.IntentSlots.Depth ?? ∞)
  → 流入 CompletionContext.MaxDepth
  → CompletionDetector Priority 2: CurrentDepth > MaxDepth → 容器完成
```

- config = 部署级硬天花板;intent 在其内收紧(min)。
- 咬了是预期(约束生效)→ Layer C(plan 承载 Depth)+ 全局 AllVisited。**无异常 depth 档**;失控归 AntiLoop + MaxSteps。
- 哪个来源咬 → Layer A trace 记(config vs intent 溯源)。

## 5. TraversalResult.Reason 保真(Layer B 四档)

CompletionDetector 产出的 CompletionReason 经引擎映射为 TraversalResult.Reason,按四档(前三档规范在 Change A §3 定义,本 change 落地 + 加第 4 档「外部」,见 §8):

| 档 | Reason | D-86 语义 |
|---|---|---|
| 达成 | AllVisited / TargetFound | 正常完备性证明 |
| 约束剪枝 | MaxSteps / Timeout | scoped:超 cap/预算元素 out-of-scope |
| 异常 | AntiLoop / Error | 硬失败,完备性免谈 |
| 外部 | Cancelled | 用户中止(Change B 加,见 §8) |

**关键不变量:异常永不伪装 AllVisited。** MaxDepth / ScrollEnd 是 **Layer A per-container 事件**(级联聚合为全局 AllVisited),**不进** Layer B 全局 reason。

**Layer C(约束上下文):** IntentSlots.Depth 由 plan 承载,D-86 读 plan.IntentSlots.Depth + 结构推导「超 cap 元素 = out-of-scope」(非 missed)。引擎不报剪枝明细。

## 6. 删 ExitCondition / ExitConditionType

**前置条件:** §3 wire ContainerHandler 后,InterceptionHandler 停止 set ExitCondition。具体:
- nav 子帧的 `new ExitCondition(AllChildrenVisited, AutoEscape)`(InterceptionHandler.cs:213)→ 改由 ContainerHandler 按完成决策产出 exit-action(见 §7)
- 动态子节点 `ExitCondition: node.ExitCondition`(TraversalEngine.cs:643)→ 移除(继承不再需要)

**无 live consumer 后删:**
- `ExitCondition` record(TraversalNode.cs:239)
- `ExitConditionType` enum(4 值:AllChildrenVisited/ScrollEnd/DepthLimited/SingleLevel —— 全冗余,见 Change A §2.2)
- `TraversalNode.ExitCondition` 字段
- `CompletionContext.ExitConditionFallback` 字段

**保留:** `FallbackAction` enum(Back/AutoEscape/Skip/Abort)—— ContainerHandler 的 FallbackDecider 用。

## 7. Fallback 归宿(Problem 3 解)

```
exit-action 决策 = FallbackDecider (从 CompletionResult + canContinue 推导, 非字段读取)
  正常完成 (AllVisited) → Back (默认)
  nav 子帧 → AutoEscape (引擎按 context 探测: NodeType/Meta 标记 nav-subframe, 非 ExitCondition.Fallback 字段)
  致命错误 → Abort
  Timeout/MaxDepth → Back
```

`ctx.ExitConditionFallback`(原 AllVisited 时透传 plan-influenced fallback)→ **移除**。AllVisited 默认 Back;exit-action 全引擎内部决策,**非 plan 维度**(plan 不指定退出动作)。

## 8. Result 分类(Problem 4 解)

`TraversalResult.Reasons.Cancelled`(用户主动取消)既非达成也非失败 → **加第 4 档「外部中止」**,分类轴定为四档:**达成 / 预算剪枝 / 异常 / 外部**。D-86 对外部中止:不判完备性(非引擎完成的遍历),单独报「user-cancelled」。

## 9. Exhaustive 改名(Change A 延后的)

```
CompletionPolicyType.None → Exhaustive
引擎 TraversalEngine.cs:286: if (policy.Type != None) → if (policy.Type != Exhaustive)
```
语义坐实(Change A 已澄清 None=exhaustive intent,本 change 改名 + 同步判定)。

## 10. 验证策略

- **ContainerHandler 从 dormant 转 live** → 不再仅单测,引擎集成验(20 baseline 是集成测试)。
- **ExitCondition 删除** → grep 确认零引用后删。
- **Depth 接通** → 单测验 priority (a) 解析;集成验限深场景。
- **reason 保真** → 单测验 CompletionReason→TraversalResult.Reason 映射;不变量「异常不伪装 AllVisited」加 guard 测试。

## 11. 风险:行为变更 + baseline triage(与 Change A 不同)

**Change A 是 dormant 安全(711 绿不动)。Change B 不是** —— 它改 live frame 完成路径(谁判 FrameCompleted 从 InterceptionHandler 换成 ContainerHandler),**20 baseline 直接受影响**。

- ContainerHandler 的 5 优先级链与 InterceptionHandler 的 ad-hoc 判定**可能不等价** → baseline 可能有红。
- 红的可能是:(a) ContainerHandler 逻辑更正确,暴露原 ad-hoc 掩盖的真问题(类 D-87);(b) 实现差异需对齐。
- **预期有 baseline triage**(类 D-86 §6):逐条裁决红的是 engine bug 还是合法差异,前者修、后者记 decision。这是**价值**而非纯风险 —— 让真完成语义浮现。

## 12. 影响面
| 文件 | 改动 |
|---|---|
| `Traversal/InterceptionHandler.cs` | 剥离完成判定 → 委托 ContainerHandler;移除 ExitCondition set;只留事件检测 |
| `StateMachine/ContainerHandler.cs` | wire 进引擎(从 dormant 转 live);CompletionContext 构造;尊重 MaxDepth;产出 reason |
| `Traversal/StepOrchestrator.cs` | 步骤 8-10 接线 ContainerHandler |
| `Traversal/TraversalEngine.cs` | L79 Depth 接通(priority a);L286 None→Exhaustive;reason 映射传播 |
| `Traversal/TraversalResult.cs` | Reason 四档分类(Cancelled 归外部档);**字段结构不改**(不加 constraint context —— D-86 从 plan 读 IntentSlots.Depth 推导 out-of-scope,见 §14 Q4) |
| `Graph/Models/TraversalNode.cs` | 删 ExitCondition record + ExitConditionType enum + ExitCondition 字段 |
| `StateMachine/ContainerHandler.cs`(CompletionContext) | 删 ExitConditionFallback 字段 |
| baseline | **可能 triage**(见 §11) |
| tests | 引擎集成测试 + guard「异常不伪装 AllVisited」+ ContainerHandler 已有单测保留 |

## 13. 验收标准
- [ ] `dotnet build` 0 错误,0 功能性警告
- [ ] `dotnet test` 全绿(baseline 红的经 triage 裁决:engine bug 修 / 合法差异记 decision)
- [ ] ContainerHandler 在 src 生产路径被调用(非仅单测)—— grep 验证有非 test 调用方
- [ ] InterceptionHandler 不再直接设 FrameCompleted(委托 ContainerHandler)
- [ ] `IntentSlots.Depth` 经 priority (a) 流入 CompletionContext.MaxDepth
- [ ] TraversalResult.Reason 四档保真;异常不伪装 AllVisited(guard 测试)
- [ ] ExitCondition/ExitConditionType/ExitCondition 字段/ExitConditionFallback 全删,grep 零引用
- [ ] CompletionPolicyType.None → Exhaustive 改名 + 引擎 L286 同步
- [ ] nav 子帧 AutoEscape 改由 context 探测(非 ExitCondition 字段)

## 14. Open Questions
1. CompletionContext 构造点:在 InterceptionHandler 各 hook 内构造,还是 StepOrchestrator 统一构造后传?倾向后者(单一构造点)。
2. nav-subframe 的 context 标记:用 NodeType 新值、Meta flag、还是 FrameComplete 阶段信号?倾向 Meta flag(避免锁定的 NodeType enum 改动)。
3. 第 4 档命名:Cancelled 归类已定(外部中止,非异常),档名用「外部 / External / UserCancelled」哪个?倾向「外部」。
4. Layer C constraint context:TraversalResult 是否新增字段携带 effective Depth,还是 D-86 从 plan 读?倾向后者(不改 TraversalResult,D-86 读 plan)。
5. baseline triage 预案:若 ContainerHandler 暴露大量红,是否分批(先 wire + 保持等价,再开严格语义)?倾向先等价再严格。
