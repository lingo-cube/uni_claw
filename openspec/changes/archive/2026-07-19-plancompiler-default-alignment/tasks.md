## 1. IntentSlots 形状变更(`Graph/Models/TraversalPlan.cs`)

- [x] 1.1 加 `Entry` 字段(`string?`,默认 null)+ XML doc:遍历根,null=app-root;子菜单穷尽用 `Entry=sub-menu-root`
- [x] 1.2 `Scope` 词表注释收窄到 `{full, target_only}`(与 D-86 Exact/Subset 1:1 同构;legacy 值 full_interaction/menu_only/safe_mode/read_only/target_path 移到 ElementHandling 或删除)
- [x] 1.3 `Depth` 语义注释:intent 深度约束,null=无约束(DescendAll);与 config.MaxDepth 按 priority「紧者胜」(`min`)解析(engine 接通在 Change B)
- [x] 1.4 `Completion` 语义注释:override ∈ `{max_steps, timeout}`,**覆盖 Type**(非 side-bound)
- [x] 1.5 确认 `CompletionPolicyType` 不改(`None` 保留,Exhaustive 改名延 Change B;XML doc 澄清 exhaustive intent 语义)

## 2. PlanCompiler 修正(`Graph/Services/PlanCompiler.cs`)

- [x] 2.1 **修 P1**:`BuildDynamicRules` 用 `slots.ElementHandling ?? "full_interaction"` 查 `TemplateSets`(原用 `Scope`)
- [x] 2.2 **修 P2+P4**:`ValidateSlots` — Scope ∈ `{full, target_only}`(拒 legacy 值,含 `target_path`);ElementHandling(若给)∈ TEMPLATE_SETS keys;`target_only ⇒ Target` 非空;`Depth ≥ 0`;`Completion`(若给)∈ `{max_steps, timeout}`,非法值 throw `DomainValidationException`(原 `_ => None` 静默吞)
- [x] 2.3 **修 P3**:`BuildCompletionPolicy` — `full → Type=None`;`target_only → Type=TargetFound(TargetName=Target, MatchMode=Contains, ActionOnFound=MarkAndStop)`;`Completion` override 覆盖 Type(`max_steps → MaxSteps(+MaxSteps)`,`timeout → Timeout(+TimeoutSeconds)`)
- [x] 2.4 移除 `target_path` 分支 + `BuildStaticNodes`(target_path 词表删,静态节点构造退役)
- [x] 2.5 `BuildRootNode`:RootNode 反映 `slots.Entry ?? slots.TargetApp`;移除 `scope==target_path → ChildrenStrategy.STATIC` 分支(统一 DYNAMIC_MATCH)
- [x] 2.6 **修 P5**:`BuildEntryPolicy` 默认改 `ColdLaunch`/`fallback=null`(从 `DirectDeeplink`/`cold_launch`)
- [x] 2.7 **修 P6**:数值抽命名常量 `DefaultCompletionTimeoutSeconds=300`、`DefaultCompletionMaxSteps=500`、`EntryTimeoutSeconds=10`(switch 臂内 magic number 移出)

## 3. PlanCompilerTests 重写(`tests/UniClaw.Core.Tests/Graph/GraphTests.cs`)

- [x] 3.1 迁移 6 个 `new IntentSlots(...)` 调用点:`full_interaction` 作 Scope → 作 ElementHandling(`Scope=full, ElementHandling=full_interaction`);`target_path` → `target_only`(带 Target)
- [x] 3.2 验 `Scope=full → Type=None`、`Scope=target_only → Type=TargetFound(TargetName, Contains, MarkAndStop)`
- [x] 3.3 验 DynamicRules 来自 ElementHandling(非 Scope):`full + menu_only` → 仅 menu_container 规则
- [x] 3.4 新覆盖:`Entry → RootNode` 反映;override 覆盖 Type(`full + max_steps → Type=MaxSteps`)
- [x] 3.5 新覆盖 fail-fast:unknown Completion throw、`target_path` 作 Scope throw、`target_only` 缺 Target throw
- [x] 3.6 保留 TEMPLATE_SETS 4 值 + match conditions 场景(未变,应仍绿)

## 4. 验证

- [x] 4.1 `dotnet build src/UniClaw.Core.sln` 0 错误、0 功能性警告
- [x] 4.2 `dotnet test src/UniClaw.Core.sln` 全绿,数 ≥ 711 (实测 723,0 失败)
- [x] 4.3 确认 `ExitCondition`/`ExitConditionType` 未删(留 Change B);baseline 未动(dormant)
- [x] 4.4 确认 `ArchitectureGuardTests` 不受影响(未锁 IntentSlots/PlanCompiler/CompletionPolicyType)
- [x] 4.5 grep 验:`new IntentSlots("...", "target_path"` 与 `"full_interaction"` 作 Scope 的旧写法在 src+tests 已清零(残留 2 处为 fail-fast 负测试,断言 throw)
