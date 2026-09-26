# CSC-001 — Coordinate Space Contract

lifecycle_state: closed · disposition: none · depth: decision-heavy · base: 5bd8432e

## Intent（WHAT/WHY）

见 spec.md。capture → grounding → effect 坐标空间机械绑定；修 viewport
魔数导致的正确检测/错误 tap 结构问题。

## Gate 状态

- **Phase 0 inventory：完成**（Flash subagent，2026-09-27）。链路表 + 八项
  file:line 证据 + R1–R7 风险点归档（plan.md 与本文件）。核心结论：
  归一化空间贯穿 grounding 链（设计干净），但两端基准——感知归一化基准
  （截图实测 W/H，每帧自适应）与 tap 投影基准（HostOptions 静态 1080×2400）
  **从未碰头**；无 rotation 概念；无 wm size 查询；HostOptions viewport
  读写点仅 HostRunner.cs:33-34/:89-90（爆炸半径极小）。
  R1 双基准分叉（根因）· R2 clamp 静默吞错 → Slice D · R3 frame 词汇无
  执法 + "f" 缺失静默回退 v0.frame → Slice B/C · R4 px 证据不自描述 →
  Slice B（frame claim 带尺寸）· R5 跨源时撕裂 → PER-011 域记录 ·
  R6 typed UiBounds 无消费缝（防旁路注意项）· R7 viewport 唯一真源是
  手填配置 → Slice B。
- **Gate 0：PASS（AUTO_GATE，2026-09-27）**——四问裁决：
  1. **canonical coordinate space 由谁定义**：不存在也不应存在全局
     canonical 常量。坐标空间是 per-capture 事实（screenshot PNG IHDR
     实测 W/H = 现实基准），effect 侧 dispatch 前用当前设备实况声明自己的
     空间；绑定 = 两空间机械相等检查。Kernel 只拥有 contract 类型与相等性
     执法，不拥有 viewport。修法方向采纳 inventory 结论：**device-viewport
     frame 携带实测尺寸 + driver 对设备实测 viewport 校验**，不新增第二套
     坐标词汇。
  2. **可复用类型**：SpatialLocator（frame-bound 锚 + [0,1] 构造执法——
     全链唯一单位哨兵）；HierarchyCaptureDescriptor（per-capture 载体）；
     CapturedScreenshot.Width/Height + ArtifactMetadata（**截图侧 W/H/Frame
     metadata 已存在**，Frame="artifact"）；PngImage（IHDR 实测尺寸源）；
     AdbEffectDriver frame 匹配语义（升级为带维度检查）。
  3. **CaptureMetadata 是否扩展**：是——HierarchyCaptureDescriptor 增
     `CoordinateSpace?`（参与 RenderCanonical，仅影响 typed 新 capture 的
     EvidenceId；legacy 路径不受影响）；screen.frame claim 增实测尺寸字段
     （additive，双 Host 孪生同步改：ScreenFrameOccurrenceStrategy +
     DevV0Runtime/AsyncPerceptionTracer）。
  4. **必须删除的默认 viewport**：`HostOptions.ViewportWidth=1080 /
     ViewportHeight=2400`（HostRunner.cs:33-34）。Slice B 替换：capture
     实测 > live device query（wm size override 优先）> 显式已验证配置；
     不可确定 → Unknown fail-closed before effect。「是否正式移除 Host
     fallback」= OWNER_GATE 项。
  **authority 冲突检查：NONE**——全部触点在 realization 词汇层
  （SpatialLocator 字段 / frame claim 格式 / driver 校验 / Host 配置），
  EffectBoundary.ToDispatchRequest 与 WorldModel/Grounding authority 零改动。
  无 IMPLEMENTATION_DESIGN_CONFLICT。
- **Slice A / Gate A：PASS（AUTO_GATE，2026-09-27）**。
  `CoordinateSpace`（Kernel/Perception，白名单 +2 含 ScreenRotation）：
  id 确定性渲染 `device-viewport:{W}x{H}@{rot}`（frame 词汇携带实测尺寸，
  不再裸字符串）；构造期 fail-closed（dims>0 / rotation 已定义 / id 非空）；
  `Matches` = dims+rotation 语义兼容（CaptureId 为 provenance 不参与）；
  record 相等 = provenance 级。DeviceViewportPrefix 与既有
  AdbEffectDriver.SupportedFrame 词汇族对齐——未引入第二套坐标词汇。
  Gate A 五场景测试 12 例：1080×1920 portrait / 配置 1080×2400 不匹配
  实测 1080×1920（E4 根因形态）/ landscape 1920×1080 / rotation change
  （含同 dims 异 rotation）/ capture ≠ configured default（语义兼容 +
  provenance 区分）。范围注记：Slice A 为 contract 层；四类对象
  （screenshot/hierarchy/grounding/effect）的声明接线按 owner 序列落
  Slice B/C/D。Kernel 588/588（+12）；全量 986/986；场景库再认证
  （change=CSC-001）。authority 改动：NONE。
- **Slice B：PASS（AUTO_GATE，unit/fixture/regression gate）**。
  1. **frame claim 携带实测尺寸**：LivePerception.Frame 增 `"w":shot.Width,
     "h":shot.Height`；双孪生（Host ScreenFrameOccurrenceStrategy +
     Simulation DevV0Runtime.FrameOccurrenceStrategy，逐行同步）解析 w/h →
     `ProposedOccurrence.Space`（缺 w/h 的 legacy claim → null，不伪造）；
     dev 帧源同步产 w/h=1080×1920。
  2. **hierarchy 携带空间**：UiHierarchyParseContext/HierarchyCaptureDescriptor
     增可选 `CoordinateSpace?`（RenderCanonical 参与，仅影响 typed 新 capture）。
  3. **viewport 魔数删除**：HostOptions 1080/2400 默认 → `int?`（null =
     不配置；配置必须成对且为正——构造执法）。
  4. **投影基准分辨率**（AdbLiveEffectDriver）：dispatch 前 wm size 实测
     （Override 优先 Physical；0x0 等非法值拒绝；成功缓存）> 显式已验证
     配置；均不可得 → `DeliveryFailed("coordinate-space-unresolved")`
     **zero effect + explicit diagnostic**——R2/R7 修复，silent fallback
     消除。E4 形态负测试：wm 报 1080×1920 时配置 1080×2400 被实测压制
     （tap 540 960 而非 ×2400）。Kernel 600/600（+12）；Host 59/59（+4）；
     全量 1002/1002；再认证 change=CSC-001。capture-carried space 优先层
     归 Slice C 追加。
- **Slice C：PASS（AUTO_GATE）**。空间绑定全链贯通：
  `ProposedOccurrence.Space`（B）→ `OccurrenceBelief.Space`（WorldModel 铸造）
  → `BindingView.TargetOccurrenceSpace` → `CanonicalBinding.TargetSpace`
  （EB lowering）→ `DeliveryTarget.Space`。Driver 执法顺序（frozen 语义
  保持）：① `ValidateSupport`（支持集拒绝不被遮蔽——从 TryBuildTap 抽出）
  → ② binding 无空间 = `coordinate-space-unknown-on-target` → RE-OBSERVE
  fail-closed → ③ 设备空间解析（B 机制）失败 = `coordinate-space-unresolved`
  → ④ `!Matches` = `coordinate-space-mismatch` → **RE-GROUND/RE-OBSERVE，
  zero effect + diagnostic**（含两侧 space id）。测试 6 例：E4 形态
  mismatch（1080×1920 grounded vs 1080×2400 设备）零 effect；rotation
  change（portrait grounding vs landscape 实况）拒绝；legacy 无空间
  fail-closed；支持集拒绝不被空间检查遮蔽；WorldModel 真件链路贯通
  （frame claim w/h → occurrence → BindingView）。副作用修正：
  DevServiceReplayTests 半真档 2400 魔数 → 1920（E4 形态再现于测试，
  一并修复）；RuntimeViewExposure 白名单 +TargetOccurrenceSpace。
  Kernel 606/606（+6）；全量 1008/1008；再认证 change=CSC-001。
  authority 改动 NONE（全是 realization 词汇数据载体 + driver 机械检查）。
- **Slice D：PASS（AUTO_GATE）**。五行 mechanical gate 矩阵 consolidated
  证据（EffectGateMatrixTests 5 例）：① coordinate-space valid
  （unknown-on-target / unresolved，B+C 层）② dimensions valid
  （CoordinateSpace 构造 + driver 配置成对执法 + wm 解析拒 0x0）
  ③ rotation compatible（Matches 含 rotation，mismatch 零 effect）
  ④ bounds inside viewport（SpatialLocator [0,1] 构造执法 = 结构性域
  检查；合法 locator 中心恒 <1.0 → clamp 永不触发——R2 的错位吞错被
  空间链根除，实证 Row4）⑤ freshness（EB 既有 StaleRevision 拒绝，
  ControlToEffectTests:171，D 不重做）。全部失败路径 = zero effect +
  explicit diagnostic；EB 本体零 perception/authority 判断。
- **Slice E：PASS（真机 + 回归）**。环境按工程规则：immutable base →
  /tmp clone（含 ini 重指向）→ cold boot（Leader-held job）→ 测试 →
  kill + 清除。真机（API 35 emulator，Override 1080×1920 = 非 2400
  设备）：
  - LiveCoordinateGateTests 2/2：**stale grounding mismatch 负例**
    （grounded 1080×2400 vs 设备实况 1080×1920 → coordinate-space-
    mismatch + RE-GROUND + 双侧 space id，zero effect）+ **matching
    正例**（实测 1080×1920 基准投影 tap 59 105——2400 魔数下会是
    59 132，E4 形态在此断言暴露）。
  - **HostLiveFull PASS**（真截图→真视觉→真 tap→wifi 翻转→真复查→
    Completion，全程途经 CSC 链：frame w/h → occurrence space →
    binding space → driver Matches）。
  - TypedLiveChain PASS（重试一次：transient dump flake 已知类别，
    PER-009 closure audit 在案）。
  - 环境实证注记：本 emulator 配置**锁死 wm override**（设置 1080×2400
    不生效，唤醒后亦然）——故 viewport-changed 负例采用镜像方向
    （stale grounding vs 实况），同一 Matches 执法点；换设备方向实测
    留作 gap。
  全量回归 1015/1015（Host 61 含 2 live 门控；Kernel 611）；再认证
  change=CSC-001。
- **OWNER_GATE：Owner 终裁 CLOSED（2026-09-27）**——必改五项验收通过；
  后继 change 指令显式声明「Base: CSC-001 CLOSED 后最新 HEAD」= 终裁
  依据；Host viewport fallback 移除冻结为正式语义。——Architecture direction
  PASS · Authority boundary PASS · Scope expansion NONE；**Host
  1080×2400 magic fallback：ACCEPT REMOVAL AND FREEZE**（正式语义：
  viewport 只能来自 实测 > 显式已验证配置，无 fallback 常量）；
  Final closure：**HOLD**。必改五项已全部执行：
  ① 跨 dispatch wm-size 缓存移除（每次 dispatch 实测；`ResolveDispatchSpace`
  无状态）② 动态 viewport-change 回归（`ViewportChangeBetweenDispatches_
  DetectedOnNextDispatch_Regression`：dispatch1 ×1920 → 设备改 2400 →
  dispatch2 mismatch 零 effect → re-ground 后 dispatch3 恢复 ×2400）
  ③ lifecycle_state 修正为合法词汇（understanding→verified；正文 gate
  叙事保留）④ Verification 四元组补齐（下）⑤ focused 35/35 + 全量 +
  再认证（见 Verification）。无重设计、无 authority 重开、无 PER-011
  扩张。**等待 Owner 终裁 CLOSED。**

## Verification

```yaml
level: DETERMINISTIC（live 项 = ENVIRONMENT，已执行）
method: >
  focused：CoordinateSpace/AdbViewportResolution（含动态 viewport-change
  回归）/GroundingSpaceBinding/EffectGateMatrix 四套件；全量：dotnet test
  七套件；场景库再认证（python3 tools/scenario_certify.py --change
  CSC-001 --all → --check）；live（Slice E 时点，API 35 emulator
  1080×1920）：LiveCoordinateGateTests 2/2 + HostLiveFull PASS +
  TypedLiveChain PASS
expected: >
  Owner 必改五项后零回归；动态 viewport 变化在下一 dispatch 被捕获
  （mismatch 零 effect → re-ground 恢复）；全量 0 失败；certification
  0 violations
actual: >
  focused 35/35（含新增动态回归 3-phase 断言）；全量：Agent 17 ·
  Agent.Dsh 121 · Core 14 · FileSystemRealization 9 · Host 61 ·
  Kernel 611 · Simulation 182 —— 1015/1015（Owner 必改后复跑回填）；
  certification PASS（29 files, 0 violations）
evidence: >
  本文件 + tests/UniClaw.Kernel.Tests/Effects/{AdbViewportResolution,
  GroundingSpaceBinding,EffectGateMatrix}Tests.cs +
  tests/UniClaw.Kernel.Tests/Perception/CoordinateSpaceTests.cs +
  tests/UniClaw.Host.Tests/{ScreenFrameSpace,LiveCoordinateGate}Tests.cs
```

## Status log

- 2026-09-27 · verified→closed · Owner 终裁（后继 change base 声明）；
  提交固化。
- 2026-09-27 · verified·owner-hold · Owner 必改五项执行（缓存移除/动态
  回归/词汇/四元组/复跑）；等待终裁。
- 2026-09-27 · implementing→owner-gate · Slice D（gate 矩阵 5 例）+ Slice E
  （真机三件套：live gates 2/2、HostLiveFull PASS、TypedLiveChain PASS；
  全量 1015/1015）完成；OWNER_GATE 十项汇报交付，等待 Owner 裁决。
- 2026-09-27 · implementing（Slice C 完成）· 空间绑定全链（occurrence→view→
  binding→target→driver 四级检查）+ mismatch/unknown fail-closed + frozen
  支持集语义保持。全量 1008/1008。下一步 Slice D（effect boundary 收尾
  gate：dims/rotation/bounds-in-viewport/freshness 机械检查）。
- 2026-09-27 · implementing（Slice B 完成）· capture owns reality dimensions：
  frame claim w/h + 双孪生解析 + descriptor Space + HostOptions 魔数删除 +
  driver wm-size 分辨率（fail-closed）。全量 1002/1002。下一步 Slice C
  （grounding 绑 CoordinateSpaceId + mismatch → re-ground）。
- 2026-09-27 · understanding→implementing · Phase 0 inventory 完成（Flash，
  R1–R7 归档）；Gate 0 四问裁决 PASS（无 authority 冲突）；Slice A
  CoordinateSpace contract 落地 + Gate A 五场景 12 例全绿（Kernel 588、
  全量 986/986、再认证 change=CSC-001）。
