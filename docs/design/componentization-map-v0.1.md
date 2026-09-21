> Status: DRAFT（活地图：每次 change 关闭时更新）
> Authority: NONE
> 日期: 2026-09-20 · 事实基线: HEAD 01ced9bf · 全量 587/587

# 组件化地图 v0.1 —— 当前架构 + 分离进度

## 1. 包级总图（箭头 = 引用方向，全部已核实）

```text
                    ┌─────────────────────────────┐
                    │  platforms/perception (Py)   │  外部感知服务（UDS）
                    └──────────────▲──────────────┘
                                   │ 运行时调用（截图→推理）
┌──────────────┐  只有翻译器  ┌───┴────────────┐   单向引用    ┌──────────────┐
│ UniClaw.Core │◀─────────────│ UniClaw.Kernel │◀────────────│ UniClaw.Agent│
│ 核心记录包 ✓ │ (CoreSemantic│   大脑包        │  (GEV-004 D1)│ 目标评价包 ✓ │
│ 5记录+3结构  │  Projection) │ （见 §2 模块表）│              │ UniAgent     │
└──────▲───────┘              └───┬────────────┘              └──────────────┘
       │ 唯一引用Core（编译级执法）  │ 自驱缝已公开（RUN-003）
       │                          ▼
┌──────┴─────────────────┐        ┌──────────────────────────┐
│ FileSystemRealization  │        │ UniClaw.Host（待建 HOST-001）│
│ .Tests  ✓第二域消费者   │        │ 组装机：装配+跑一轮+落盘     │
└────────────────────────┘        └──────────────────────────┘

外部效应目标（真机）：Android/ADB（AdbLiveEffectDriver）、ego-browser
```

## 2. Kernel 内部模块表（一个包里的部门）

| 模块 | 职责一句话 | 备注 |
|---|---|---|
| World/ + World/UiRealization/ | 世界认知：信念 revision、容器关联、连续性、UI 接地 | **留在 Kernel**（2026-09-20 裁决：Kernel=UI 运行时，单消费者不拆包——NO_REAL_BUYER）；模块边界由 UiRealizationBoundaryTests 执法 |
| Evidence/ | 入证台账（观察如何变成证据） | 与 World 有接口级交叉（死结①） |
| Assurance/ | 行动前判断：授权、新鲜度 | ProductFreshnessEvaluator 今天落的产品件 |
| Control/ | 意图签发（该观察还是该动手） | |
| Effects/ | 执行边界：绑定→门→派动→回执；执行记录 journal | CORE-013 可靠执行源在这里 |
| Runtime/ | 自驱司机（KernelRunDriver 相位机） | RUN-003 后对产品公开 |
| Run/ + Outcome/ | Run 状态与终局证明 | |
| Trace/ + Diagnostics/ | 只读痕迹与度量（不入关键路径） | |
| Perception/ | 感知接驳（截图采集、FastPerception、视觉服务宿主） | |
| Core/CoreSemanticProjection | → Core 记录的唯一翻译器 | 调用方目前只有测试（设计意图：契约关系非数据流） |

**模块间依赖备注（2026-09-20 裁决后仅作记录）**：
① World 的观察策略接口吃 `EvidenceRecord`（Evidence 在 Kernel 侧）；
② Kernel 的 Runtime/Control 用 `TargetDescriptor`（定义在 UiRealization 里）。
两条只在「World 拆独立包」场景下才是死结；**裁决不拆包后是普通模块内部依赖**，边界测试已覆盖。若未来通用运行时抽取（触发条件见 §4）改变包结构，此记录重新生效。

## 3. 插口清单（可换零件 = 插件化的实体）

| 插口 | 真实零件 | 测试/替身零件 |
|---|---|---|
| IEffectDriver | AdbLive / Adb / EgoBrowser | DeterministicDeliveryDriver（待建，Host v0） |
| IFreshnessEvaluator | **ProductFreshnessEvaluator（今天）** | Satisfying double |
| IUiObservationStrategy / IAssociationStrategy | 产品默认（null 路径） | Seed/Replay 系列 |
| IReliableExecutionSource | FileExecutionJournal ✓ | 内存实现 |
| IControlPolicy | AgentPlanPolicy / DescriptorTargetPolicy | — |
| NextInput / ConsultAgent（driver 缝） | — Host 提供 | ScriptedUniAgent / Feed |
| clock（Func<DateTimeOffset>） | 组合根注入 | 冻结虚拟钟 |

## 4. 分离进度（「完全分离」终点线 · 2026-09-20 修订）

**当日裁决**：Kernel = UI 运行时（路 A）；UIWorld 不单独拆包（单消费者无买家）；通用运行时抽取 = buyer-driven（触发条件：第二个需要「自主动手+逐步验证」的非 UI 领域出现——由该领域与 UI 运行时 diff 出 SPI 面）。

| 包 | 状态 |
|---|---|
| UniClaw.Core | ✅ 独立 + 双域验证（UI 投影 + 文件系统直连） |
| UniClaw.Agent | ✅ 独立（单向引用 Kernel） |
| UniClaw.Kernel | ✅ UI 运行时（定位定格）：模块边界测试执法；驱动面公开+白名单；Core/Agent 翻译缝与单向引用 |
| UniClaw.Host | ✅ **已落地（HOST-001，2026-09-20）**：单次 headless run 首跑 Completed/Completion/EXIT=0；journal/trace/facts 落盘；两跑 digest 一致（仿真=正式能力）；闭包 {UniClaw.Kernel}。**「完全分离完成」达成** |
| ~~UniClaw.World~~ | ❌ 不拆（2026-09-20 裁决；CORE-015 升格条件从未满足——CORE-016 未消费 UiRealization 类型，此前「条件已满足」为误记，修正） |
| Simulation/FileSystem 测试侧 | ✅ 各自独立组装 |

## 5. 维护约定

- 每次 change 关闭时同步 §4 勾选与 §2 备注；
- 本图只记**已核实事实**；推断性内容必须标注「待核实」。
