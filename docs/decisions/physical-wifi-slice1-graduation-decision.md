# PROJECT_LEADER_PHYSICAL_WIFI_SLICE1_GRADUATION_DECISION

- **Authority**: `PROJECT_LEADER_PHYSICAL_WIFI_SLICE1_GRADUATION_REVIEW`
- **Date**: 2026-08-14
- **Input**: `IMPLEMENTATION_RESULT_ONLY — Slice 1`（REALITY_COMPOSITION_FOUNDATION）
- **Mode**: Graduation review only. No implementation performed.
- **Predecessor**: `APPROVED_SLICE_1_AND_SLICE_2`（`docs/decisions/implementation-authorization-physical-wifi-off-to-on.md`）

---

## 1. Composition Root Validation — **PASS**

| 核对项 | 证据 | 判定 |
|---|---|---|
| PhysicalHost 是唯一生产组合根 | `PhysicalHostComposition.cs` 文档明示「唯一允许用真实 Provider 组合 PhysicalEnvironment 的代码位置」；`Program.cs` 是唯一 Main/入口 | ✅ |
| 构造真实 PhysicalEnvironment | `BuildRealEnvironment`（PhysicalHostComposition.cs:43-52）：`new PhysicalEnvironment(AdbScreenshotSource, LocalVisionPerceptionSource, AdbDispatchTarget, ...)` — 仅三个真实 IO Provider | ✅ |
| Runtime 依赖注入正确 | `BuildRuntimeGraph`（:77-124）：Startup/Traversal/Recovery/Container/Agent 全部构造器注入；无 Service Locator；attach 以 delegate 注入（I-12） | ✅ |
| Runtime 零引用 PhysicalHost | Guard 1（Runtime csproj 零 ProjectReference — 全库唯一含该词的是一行 XML 注释）；源码零 `UniClaw.Runtime.PhysicalHost` 标识符 | ✅ |
| Runtime 零引用 Adapters | 同上；源码零 `UniClaw.Runtime.Adapters` 标识符（含注释剔除后） | ✅ |
| 无 provider discovery | Runtime 核心 grep `Discovery|Registry|Selection|IsPhysical` 仅命中 `RunBoundedCrossPageDiscovery`（Phase 1 既有 planner 方法名，与 provider 无关） | ✅ |
| 无 provider registry | 组合根命令式直线组合，无注册表/无轮换（`ResolveDeviceAsync` 仅 CLI serial 或 adb 单设备解析） | ✅ |
| 无环境选择抽象 | Runtime 内无 Fake-vs-Physical flag/switch/分支（F1 断言 + grep 双证） | ✅ |

## 2. Slice 1 Semantic Boundary Validation — **PASS**

代码级验证 `Cold → Attach → Ready → Fresh ObserveAsync → Initial WorldBelief`：

| 环节 | 证据 | 判定 |
|---|---|---|
| Cold | 进程启动 → 设备解析 → 真实 Provider 组合（Program.cs step 1-2） | ✅ |
| Attach | `Startup.AttachAsync`（Startup.cs:115-121）→ 注入的 `AdbDevicePreflight.CheckAsync`（4 轴含真实截图探针）→ 成功返回 null 放行（:65-69）；失败 → `NotReady(设备预检失败（Attach）：…)` 零分发 | ✅ |
| Ready | `StartAsync`：LaunchApp → **动作后新鲜 ObserveAsync**（:75，§3）→ ForegroundApplication 验证（:78-84）→ `Reconcile.FromObservation`（:87）→ RecoveryAnchor → `Ready(anchor)`（:107） | ✅ |
| Fresh ObserveAsync | 观测 seq=2（Agent observeInitial seq=1 之后推进）；live 输出 `beliefEvidence=语义页面解析为「Settings」（观测 seq=2）` | ✅ |
| Initial WorldBelief | `Reconcile.FromObservation` 纯函数：`WorldBelief("Settings", 1f, Evidence, SourceObservationSequence=2)`；证据字符串内嵌观测序号（裁决 2 — 引用支撑观测序列） | ✅ |
| 无场景注入状态 | 观测来自真实 emulator Settings 应用（screencap + vision server）；`resolveSemanticPage` 为调用侧注入静态规则（裁决 11），非环境状态注入；Reconcile 无场景特定字段（裁决 2） | ✅ |

## 3. Startup LaunchApp Classification — **Case A: BOOTSTRAP_ACCEPTED**

| 判据 | 证据 |
|---|---|
| 早于 Agent Goal 执行 | Startup.cs:72 — LaunchApp 在 `Startup.StartAsync` 内直接分发，先于 `RunSemanticGoalAsync` 语义闭环（闭环仅在 Ready 后进入） |
| 非 Capability 选择产生 | 证明 run 传入 `capabilities=[]`（Program.cs:83-84）；能力 SELECT 步骤（Agent.SemanticRun.cs）在证明 run 中未到达（终止于 BindingUnresolved:102，先于选择:113） |
| 非 GoalEvidence 链 | GoalEvidence 仅在 `CompleteSemantic` 构造（`new GoalEvidence(true, …, observation.SequenceNumber)`）；证明 run 未产生任何 GoalEvidence；LaunchApp 分发结果被显式丢弃（`_ =`，Startup.cs:72 — 裁决 10：dispatch 收据 ≠ 启动成功证据，门控是 step 4 ForegroundApplication 验证） |
| 仅建立观测表面 | 目的 = 使前台进入 com.android.settings 以提供可解析语义入口；由 ForegroundApplication 验证而非分发本身门控 |
| 非 Traversal 执行 | 直接 `_environment.ExecuteAsync`，不经 Traversal（无 journal 条目、无序列验证）；Traversal 仅由 Agent 闭环消费（`ExecuteLoweredActionAsync`） |

**结论：LaunchApp = Environment bootstrap/precondition（Case A）。无 Slice 1 scope 违规。**

## 4. F1 / F2 Falsifier Validation — **PASS**

- **F1（Fake 环境无法进入生产组合）**：Tier 1 三断言全绿（宿主源码零 Fake 标识符 / Runtime 无环境选择 flag / 宿主 csproj 恰好 Runtime+Adapters 两引用）+ `F1_BuildRealEnvironment_ConstructsRealProvidersOnly`（`Assert.IsType<PhysicalEnvironment>`）+ Guard 1；组合根文档明示「Fake 环境进入生产的唯一通道不存在」。
- **F2（无设备 → 零分发/零 Traversal/零 Agent 执行）**：
  - Tier 1：失败 attach → `NotReady(显式原因)` + 空 ActionHistory + 空 ObservationHistory；取消 attach → 零分发。
  - Live：`PROOF-F2 deviceUnavailable=true zeroDispatch=true zeroTraversal=true`，exit 2；设备未解析时 Program 在组合**之前**返回 —— Agent 未被构造/执行（强于「无执行」）。
- 两 falsifier 均通过。

## 5. Slice 2 Entry Conditions

| 条件 | 状态 |
|---|---|
| Slice 1 boundary clean | ✅ 13/13 tasks `[x]`，证明与记录齐备（tasks.md + `slice1-baseline-snapshot-physical-wifi.md` + `section-33-emulator-only-gate-physical-wifi.md`） |
| 无 Agent action leakage | ✅ 证明 run 唯一分发 = Startup bootstrap LaunchApp（Case A）；`wifiCapabilityExecuted=False`；`physicalDispatchCount==launchAppDispatches==1` |
| Runtime authority 不变 | ✅ Agent=决策 owner / Environment=传输 owner / Traversal=执行·验证·journal owner 均未变；async seam 仅改形状（`internal async ExecuteLoweredActionAsync`），`ExecuteStep` 同步契约保留（Phase 1 Fake 路径）；语义（Select→Check→Execute→Observe→Verify→Branch）不变 |
| Tier 0 回归接受 | ✅ **915/915 通过、0 失败**（含 SC-P1-001..005、frozen 13 capability、Guard、Perception、9 条新 Tier 1）；build 0 错误 |

---

# 决策：**APPROVED_SLICE2**

Slice 1 REALITY_COMPOSITION_FOUNDATION 毕业。批准进入 Slice 2（WIFI_SEMANTIC_LOOP），开始条件满足：组合根正确、语义边界完整、LaunchApp 分类为 bootstrap、F1/F2 通过、Tier 0 回归接受、Runtime authority 未变。

**Slice 2 开始前注意事项**（非阻断，供实施 gate 使用）：① 5.1 校准资产录制在 `section-33-emulator-only-gate-physical-wifi.md` 边界内（emulator-5554）；② 6.1-6.8 falsifier 依 design.md 执行；③ Tier 2 需 emulator 前置（7.1）。
