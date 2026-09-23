# SCN-002 — 生成式场景能力（ScenarioBuilder + DynamicStimulusScheduler）

lifecycle_state: implemented · disposition: none · depth: decision-heavy · base: 0c280a81

## Intent

场景库从"只能录制回放"扩展为"也能参数化生成"。核心价值：快速模拟
实际产生的问题——发现 bug → 写条件重建场景 → 修复 → 入库为回归。
不替代录制（GoldenBundle 保真度高），是其扩展。

## Decisions

- D1 API：Fluent Builder（C# 链式调用）→ 序列化 JSON 入场景库；
  不做直接写 JSON 创建场景的逆向（无买家）
- D2 模板：从 GoldenBundle 提取初始模板（录制资产再利用），
  后续新页面手工扩展；模板 = 预填的元素列表，非独立系统
- D3 调度器：内部 = 条件式接口 `IStimulusScheduler.Poll(state)`——
  固定时机 / 条件触发 / 对抗者 = 同一接口的三种实现，架构不变；
  Builder 先只暴露 `afterStep(n)` 作为语法糖
- D4 格式兼容：产出与 GoldenBundle 相同的 `ScenarioStimulus[]` +
  `MinimalScenarioBundle`——共用 ScenarioRunner / SimulationHost /
  覆盖率工具，零新基础设施
- D5 参数化 = C# 函数模式（零基础设施）；JSON 加可选 `templateRef`
  字段分组；不做 Parameterize DSL（过度工程）
- D6 确定性 = 三层防线：
  层1 Builder 类型契约（不接受无种子 Random / 真实时钟）
  层2 运行时注入（VirtualClock / 种子化 Random 由 Host 注入）
  层3 Digest 验证（两次运行同 digest，自动抓违规）
- D7 第一个场景 = WiFi 开关 off→on 生成版（验证与录制等价）
- D8 归置 = SCN-002（SCN-001 谱系）

## Architecture

```text
ScenarioBuilder（Fluent API）
       │ Build() →
       ▼
MinimalScenarioBundle（与录制同格式）
       │ consumed by
       ▼
ScenarioRunner / SimulationHost（不变）
       │
       ├── IStimulusScheduler（新增组件）
       │     Phase 1: FixedTimingScheduler (afterStep(n) → condition)
       │     Phase 2: ConditionalScheduler (when(predicate))
       │     Phase 3: AdversarialScheduler (agent decides)
       │     —— 同一接口，实现演进，架构不变
       │
       └── 确定性三层防线
             Builder 类型契约 → Host 运行时注入 → Digest 验证
```

## Scope

- ScenarioBuilder Fluent API（Screen/AgentDecides/AfterEffect/Inject/Expect）
- GoldenBundle → 模板提取器（解析录制数据为可参数化模板）
- IStimulusScheduler 接口 + FixedTimingScheduler 实现
- 第一个验证场景：WiFi off→on 生成版 ≡ 录制版
- 序列化 JSON 入场景库（templateRef 可选字段）

## Out of Scope

- Parameterize DSL（C# 函数天然支持）
- ConditionalScheduler（Phase 2，等条件注入买家）
- AdversarialScheduler（Phase 3，等对抗域需求）
- 直接写 JSON 创建场景（无买家）

## Acceptance

1. ScenarioBuilder 可构造 WiFi off→on 场景，Build() 产出与录制等价的 bundle
2. 生成版场景通过全部 DeterministicScenario 断言（与录制版相同结果）
3. `Inject(Stimulus, afterStep: n)` 生效——第 n 步后 stimulus 到达
4. 生成场景以 JSON 入库（source: "generated"），覆盖率工具正常聚合
5. 两次运行 digest 一致（确定性三层防线验证）
6. GoldenBundle 录制场景不受影响（零回归）

## Status log

- 2026-09-23 · implemented（Phase B 首切片：Acceptance 1-6 全闭）·
  ScenarioBuilder（Fluent：FromTemplate / WithScenarioId / Screen /
  AgentDecides / Expect / Inject(afterStep)）+ IStimulusScheduler 条件式
  接口 + FixedTimingScheduler 实现（D3 Phase 1；Inject(stimulus, n) 即
  Scope 中 AfterEffect(n) 语法糖——构建期插入位 = 第 n 个 post-action
  观察帧之后，与同步 feed 的 FIFO+context 匹配模型一致）。验证：
  level DETERMINISTIC——A1/A2 生成版 ≡ 录制版（同 runner 全语义字段
  + 消费轨迹一致，GeneratedScenarioTests 4/4）；A3 Inject 迟到帧
  保持 unconsumed、run 不扰动（期望面经 Expect 参数化 unconsumed
  0→1——API 用例本身）；A5 两次 Build 同 bundle digest（层1 构造
  幂等）+ 两次运行同 semantic digest（层3）；A6 录制零回归
  （DeterministicScenario 全绿）；A4 SCN-WIFI-006 入库（synthetic +
  templateRef=SCN-WIFI-001，认证 SCN-002，schema v2 += templateRef，
  trait 承载，覆盖率 18/18 passing 派生，工具 exit 0）。
  勘误记录：Acceptance 4 原文 source "generated"——S6 更名后落
  "synthetic"（语义不变）。附带处置（C8 搭乘补执行）：并行 RUN-004
  会话提交 3b7268e8（Defer 链）后未做 golden 重认证，17 条旧章源码
  哈希报警——经机械核对映射测试全绿（期望值未受影响）后，以
  --change RUN-004 重认证（纯哈希刷新，期望值零改动）。
  Known RED 保持：Simulation 3 失败（ImportReDrive /
  AsyncImportRedrive / AsyncPerceptionRealization）为 HEAD 存量，
  与本 change 零接触（stash 复验在案）。

- 2026-09-22 · created·persisted · grill 八问三轮（含 Q3/Q5/Q6 架构修正）
  全部落定。Q3 修正：调度器内部条件式接口（非固定时机架构）；
  Q5 修正：参数化 = C# 函数模式（非 DSL）；Q6 修正：三层防线（非单层禁令）。
