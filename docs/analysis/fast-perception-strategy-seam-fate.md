# Fast Perception Strategy 缝去留调查（收层/删除裁决材料）

> Status: COMPLETE
> Authority: NONE
> Date: 2026-10-07 · 出处：PER-021（所有者质疑「IFastPerceptionStrategy
> 好像不需要了」→ 裁决「先调查再裁决」）
>
> **裁决（2026-10-07，所有者采分析者推荐）：路线 B（收层 internal），
> 缓行——与「感知协议负载词汇冻结」buyer-driven change 合并执行，不
> 独立先行。届时白名单 -2 型、基线 §7 v0.3、缓存键内化、观测词汇随改。**

## 1. 问题

`IFastPerceptionStrategy`（`FastPerception.cs:54`，单方法
`Observe(RawArtifact) → ArtifactObservation[]`）是否仍有存在必要。
所有者直觉：不需要了。

## 2. 缝的现状与谱系

- 出处：UWM-009 §16（Fast/Slow 策略不冻结）、PER-002B（P2 producer 薄
  壳）、FCR-001（`IVersionedFastPerceptionStrategy` 版本缝 = 帧计算
  复用缓存键）。
- **冻结基线条款**：perception-provider-baseline v0.2 §7 realizes 明文
  「本 plane 是其 live realization（IFastPerceptionStrategy 可替换缝
  **不变**）」——收层/删除 = 基线 v0.3 修订（经 change）。
- 架构位势：UniPerception union 内部 Fast（同步先行）面的引擎插槽，
  不在已冻结的 L1 双协议面（ISemanticPerception/IUiElementPerception）
  之列。

## 3. 牵连面全清单（grep 可复核）

| 牵连面 | 内容 | 收层/删除代价 |
|---|---|---|
| 产品实现 | 仅 1 个：`LiveVisionStrategy` | — |
| C# strategy 互换方 | `CorpusFastPerception` 等 3 个测试 double，横跨 **8+ Kernel 测试文件**（LatencyBaseline / WorldModelCanonicalOracle / CoreProjectionSeam / ClaimEvolution / ControlReferencePolicy / FrameComputationReuse / FastPerceptionSlice / RealAssetEntityModel 等） | 最大项：测试族改喂响应式 replay 或保留 internal double |
| 仿真主力 | Simulation.Tests **不走 strategy 互换**：`ScenarioStimulus`/`AsyncPerceptionTracer` 等均直接 `new FastPerception(..., new LiveVisionStrategy())` 喂预录响应 | 无（现状已不依赖互换） |
| 缓存机制 | `StrategyObservationCache` 以 `IVersionedFastPerceptionStrategy.StrategyIdentity/Version` 为键（FrameComputationReuseTests 执法） | 缓存键需另立 identity 来源或随缝内化 |
| 观测词汇 | Trace span `perception.strategy: IFastPerceptionStrategy.Observe`（SpanDefinition）+ Diagnostics 阶段度量 | 词汇更名（trace 兼容性考虑） |
| 公开面白名单 | `IFastPerceptionStrategy` / `IVersionedFastPerceptionStrategy` / `FastPerception` / `LiveVisionStrategy` / `ArtifactObservation` | 白名单修订（change 显式） |
| 冻结基线 | provider-baseline §7 realizes 条款 | v0.3 修订 |

## 4. 两条关键新事实（本次调查发现）

1. **产品实现唯一**：真件侧只有 LiveVisionStrategy——「换引擎」在真件
   链上从未发生第二次；引擎边界实质是 provider transport 契约
   （UDS /v1/analyze_raw，已冻结）。
2. **仿真主力不靠它**：Simulation.Tests 全部经 LiveVisionStrategy +
   喂预录响应实现确定性；C# 层 strategy 互换只存在于 Kernel 测试的
   corpus double（8+ 文件）。

→ 所有者直觉成立的部分：这不是「仿真⇄真件」的现行主缝，主缝在
provider 契约 + 响应 replay。不成立的部分：Kernel 确定性测试族
（canonical oracle、latency baseline 等核心基线）当前深度依赖 C#
double 直插。

## 5. 三条路线

| 路线 | 内容 | 成本 | 风险 |
|---|---|---|---|
| **A 保留现状** | 不动 | 零 | 一缝两插的认知税持续；公开面多 5 型 |
| **B 收层 internal** | 两接口收 internal（白名单 -2 型）；Kernel 测试 double 改 internal 直插（同程序集不可行→改喂响应式或保留 internal 缝给测试）；缓存键内化；trace/度量词汇随改；基线 §7 v0.3 | 中：白名单 + 基线 + 缓存键 + 观测词汇 + 部分测试改造 | InternalsVisibleTo 已有（Kernel.Tests），internal 化后测试仍可插——**实际改动比 C 小得多** |
| **C 彻底删除** | LiveVisionStrategy 并入 FastPerception 内部 | 大：8+ 测试文件重写为响应喂入式；缓存机制重设计 | canonical oracle / latency baseline 基线漂移风险 |

## 6. Owner 裁决点

1. 「换引擎」的正式边界定为**一个**（provider 契约）还是**两个**
   （provider 契约 + C# 缝）？
2. Kernel 确定性测试族的 double 直插（现状便利）是否值得保留为
   internal 缝？
3. 若收层：挂到「感知协议负载词汇冻结」的 buyer-driven change 一起做
   （同族一次动），还是独立先行？

## 7. 建议（分析者倾向，非决定）

倾向 **B（收层 internal）且不急行**：白名单 -2 型、认知税消除、
InternalsVisibleTo 使 8+ 测试零改动或小改；但与感知负载词汇冻结同族，
建议合并为一次 change（避免两拨人两次动同一测试族）。A 亦可接受
（零成本）；C 不建议（收益不抵 canonical oracle 漂移风险）。
