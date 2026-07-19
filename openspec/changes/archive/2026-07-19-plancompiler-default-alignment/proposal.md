## Why

`PlanCompiler`(确定性 `IntentSlots → TraversalPlan` 编译器)的字段读取、词表、默认值与下游 D-86 Mode 自动分流(`ResolveModeAndTarget`)预期不一致 —— 它用 `Scope` 查 template sets(该用 `ElementHandling`)、把交互词表当遍历形状词表、`target_path → TargetFound` 派生错位、未知 `Completion` override 静默吞。当前 PlanCompiler **dormant**(baseline 手搓 plan 不经过它),故属**预防性正确性修复**:等真机链路(`AI → IntentSlots → PlanCompiler → Plan → ExpectedBehavior`)接通、D-86 Mode 真正消费 `CompletionPolicy.Type` 时,派生必须已正确。

## What Changes

- **Scope 词表收窄**到 `{full, target_only}`(原混 element_handling 词表 + target_path)—— **BREAKING**(IntentSlots 合法取值变更;仅 GraphTests 调用点受影响,PlanCompiler dormant 故无生产裂)
- **PlanCompiler 读对字段**:`BuildDynamicRules` 用 `ElementHandling`(非 `Scope`)查 template sets;`ValidateSlots` 按各自词表校验 Scope 与 ElementHandling
- **Completion 派生修正**:`full → None`(exhaustive 意图)、`target_only → TargetFound(TargetName, Contains, MarkAndStop)`;`target_path` 词表删除
- **Completion override 覆盖 Type**(经引擎代码验证):`max_steps → Type=MaxSteps`、`timeout → Type=Timeout`(非「叠 bound 不改 Type」,否则引擎不认)
- **未知 Completion override fail-fast**(原 `_ => None` 静默吞 → throw,对齐 C-1~C-4)
- **IntentSlots 加 `Entry` 字段**(遍历根,null=app-root;子菜单穷尽用)
- **IntentSlots.Depth 语义澄清**:intent 深度约束,与 `TraversalEngineConfig.MaxDepth` 按 priority「紧者胜」(`min`)解析 —— 规则定义,engine 接通在 Change B
- **`CompletionPolicyType.None` 语义澄清**为 exhaustive intent(改名 `Exhaustive` 延 Change B,因引擎 L286 耦合)
- **数值默认抽命名常量**(timeout 300 / max_steps 500 / entry timeout 10);**EntryPolicy 默认**改 `ColdLaunch`/`fallback=null`(从 DirectDeeplink)
- **ExitCondition 不动**(live consumer 在 InterceptionHandler,删除延 Change B)

## Capabilities

### New Capabilities
<!-- 无新 capability —— 是对现有 graph-foundation 的对齐修正 -->

### Modified Capabilities
- `graph-foundation`: IntentSlots 形状变更(Scope 词表收窄到 {full, target_only}、加 Entry 字段、Depth priority 语义)、PlanCompiler 派生正确性(ElementHandling 读字段、scope→CompletionPolicy.Type 派生、target_path 移除、fail-fast override)、CompletionPolicy.None 语义澄清

## Impact

- `src/UniClaw.Core/Graph/Models/TraversalPlan.cs`:IntentSlots 加 `Entry` 字段(CompletionPolicyType **不改** — Exhaustive 改名延 Change B)
- `src/UniClaw.Core/Graph/Services/PlanCompiler.cs`:P1-P6 修正 + 移除 target_path/BuildStaticNodes + 数值抽常量 + EntryPolicy 默认 + Depth/Entry/CompletionPolicy 派生
- `tests/UniClaw.Core.Tests/Graph/GraphTests.cs`:PlanCompilerTests 6 调用点重写 + 新覆盖(Entry、override 覆盖 Type、fail-fast)
- **不动**:baseline(手搓 plan,PlanCompiler dormant)、引擎(Change B)、ExitCondition(Change B 删)
- 详细设计见 `docs/refactor/2026-07-19-plancompiler-default-alignment-design.md`(refactor 文档,本 change 的设计源)
