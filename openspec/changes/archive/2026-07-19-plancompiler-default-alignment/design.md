## Context

`PlanCompiler`(Graph 层,`IntentSlots → TraversalPlan` 确定性编译)当前 dormant —— baseline 手搓 plan 不经过它。但它发出的 `CompletionPolicy.Type` 是 D-86 Mode 自动分流(`ResolveModeAndTarget`)的上游输入,等真机链路接通即被消费。现存在字段读错、词表错位、派生错位、默认漂移四类问题,且 `ExitCondition.Type`、`ContainerHandler`、D-86 auto-derive 一连串 dormant。

本 change 是终止模型 model B 的 **plan 侧**(Change A)。完整设计见 `docs/refactor/2026-07-19-plancompiler-default-alignment-design.md`;engine 侧(Change B,`container-handler-canonicalization`)另立。

## Goals / Non-Goals

**Goals:**
- PlanCompiler 派生正确的 `CompletionPolicy`(`full→None`、`target_only→TargetFound`)
- 修正字段读取(`ElementHandling` 查 template sets,非 `Scope`)
- Scope 词表收窄到 `{full, target_only}`,与 D-86 双 Mode 1:1 同构
- IntentSlots 加 `Entry`(子菜单穷尽边界)、`Depth` 语义澄清(priority「紧者胜」)
- fail-fast 未知 `Completion` override(对齐 C-1~C-4)
- dormant 安全:不动 baseline、不动引擎、711 绿不受威胁

**Non-Goals(显式排除):**
- 接通 PlanCompiler 到 baseline / 真机(dormant→live 留真机 change)
- `None → Exhaustive` 改名(引擎 L286 耦合,延 Change B)
- 删 `ExitCondition`/`ExitConditionType`(InterceptionHandler 还 live-set,延 Change B)
- `IntentSlots.Depth → 引擎 MaxDepth` 实际接通(priority 规则只定义,接通在 Change B)
- wire `ContainerHandler` 正宗化(Change B)
- config 顶层分类(更高层维度,后期)

## Decisions

**D1: Scope 词表 `{full, target_only}`(非 Python 的 4 值)。** 从业务场景盘出系统只需 2 种遍历形状(穷尽 / 找目标即停),与 D-86 Exact/Subset 1:1 同构。`partial`(步数预算)= `full + Completion=max_steps` override,不需进词表;`target_path` 零场景,YAGNI。替代(保留 4 值)引入无场景背书的死词表,否决。

**D2: Completion override 覆盖 Type(非「叠 bound 不改 Type」)。** 引擎 bound 检查以 Type 为门(TraversalEngine L315/L323:`Type==Timeout`/`Type==MaxSteps` 才触发),Type 不变则 bound 失效。故 `full+max_steps → Type=MaxSteps`(= partial 归约)。经引擎代码验证,非照搬 Python。

**D3: IntentSlots.Depth 两来源 + priority「紧者胜」。** `config.MaxDepth`(部署硬天花板)与 `IntentSlots.Depth`(intent)按 `min` 解析;同一作用,关系是优先级非合并;咬了都算预期(无异常 depth 档),失控归 AntiLoop+MaxSteps。Change A 只定义规则,接通在 B。

**D4: `Entry` 字段表达子菜单穷尽边界。** 子菜单穷尽 = `full` + `DescendAll` + `Entry=sub-menu-root`(边界内禀于 Entry+Back 导航),不需 SingleLevel。Entry 是「更小的树」的参数,非新形状。

**D5: `None` 保留不改名(Exhaustive 延 B)。** 改名需同步引擎 L286 判定,属 engine 侧。A 只澄清 None 语义为 exhaustive intent,PlanCompiler 对 `scope=full` 派生 `Type=None`。

**D6: ExitCondition 延 B 删。** InterceptionHandler 生产中 live-set `ExitCondition`(nav 子帧 L213 + 动态子节点继承 L643);A 删字段会破生产。B wire ContainerHandler、停止 set 后再删。

## Risks / Trade-offs

- [Scope 词表 BREAKING] → 仅 GraphTests 6 调用点受影响(PlanCompiler dormant,无生产调用);调用点迁移机械化。
- [override 覆盖 Type 语义] 与「override 只加上限」直觉相反 → 文档明确 + 单测验(`full+max_steps`→Type=MaxSteps)。
- [PlanCompiler 仍 dormant] → 派生正确性仅单测验,全链路未验证 → 接受(与现状一致;真机 change 接通时端到端验)。
- [partial×Exact 张力] → 步数受限遍历在 Exact 下判 missed;零场景,出现时配 allowedMisses(类 D-jump)。

## Migration Plan

无生产迁移(PlanCompiler dormant)。仅测试侧:`GraphTests.cs` PlanCompilerTests 6 调用点重写(`full_interaction` 作 Scope → 作 ElementHandling;`target_path` → `target_only`)。回滚= revert PlanCompiler.cs + TraversalPlan.cs(IntentSlots) + GraphTests.cs。

## Open Questions

1. `Entry` 字段命名(Entry / Root / TraversalRoot)?倾向 Entry。
2. `Depth` 是否加上界 1000?倾向加(对称)。
3. `Scope=full + Target` 忽略 vs fail-fast?取忽略。
4. EntryStrategy 枚举确切值实现时确认(ColdLaunch 存在?)。
