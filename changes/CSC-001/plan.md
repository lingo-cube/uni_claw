# CSC-001 Plan — Phase 0 → Gate 0 → Slices A–E → OWNER_GATE

## Phase 0 — Inventory（Flash 委派进行中）

产出：链路表（producer | coordinate space | metadata available | conversion |
consumer）+ 八项查清点 file:line + 额外风险点。

Leader 一手预验证（已完成，待 inventory 交叉确认）：

```text
vision px bounds（detector 输出）
  → ÷ shot.Width/Height（实时截图真实尺寸，LivePerception 内联归一化）
  → screen.frame claim {"b":[norm×4],"f":"device-viewport"}（f 无维度）
  → P2 → WorldModel
  → ScreenFrameOccurrenceStrategy.Derive（解析 b/f）
  → ProposedOccurrence(Locator: SpatialLocator(norm, frameId))
  → grounding → DeliveryTarget.Spatial
  → AdbLiveEffectDriver.TryBuildTap：center × 构造期 viewport（静态）
  → adb shell input tap
```

结构缺口：归一化分母（capture 实况）与投影分母（HostOptions 静态 1080×2400）
是**两个独立来源**，frame id "device-viewport" 无维度绑定，任何一层都不校验
两者一致。SpatialLocator 构造已执法「无 frame 无效」（P-UW-16）但 frame
本身无尺寸。rotation 全链路无概念。

## Gate 0 — 四问裁决（草稿，inventory 定稿后生效）

1. **canonical coordinate space 由谁定义**：不存在也不应存在全局 canonical
   常量。坐标空间是 **per-capture 事实**：由 capture（screenshot/hierarchy
   同源）声明实际 PixelWidth/Height/Rotation，effect 侧在 dispatch 前用
   **当前设备实况** 声明自己的空间；绑定 = 两空间机械相等检查。Kernel 不
   拥有 viewport；它只拥有 contract 类型与相等性执法。
2. **可复用类型**：SpatialLocator（frame-bound 归一化锚 + 构造执法，直接
   复用）；HierarchyCaptureDescriptor（per-capture 载体，扩 Space 字段）；
   PngImage.Width/Height（capture 实况尺寸来源）；AdbEffectDriver.
   SupportedFrame 的 frame 匹配语义（升级为带维度检查）；LivePerception
   已持有的 shot.Width/Height（现实尺寸已在手，只是被丢弃）。
3. **CaptureMetadata 是否扩展**：是——HierarchyCaptureDescriptor 增可选
   `CoordinateSpace?`（参与 RenderCanonical，仅影响 typed 新 capture 的
   EvidenceId）；screenshot 侧评估对应载体（inventory 后定：扩展
   ArtifactObservation 还是 frame claim 内嵌尺寸）。
4. **必须删除的默认 viewport**：`HostOptions.ViewportWidth=1080 /
   ViewportHeight=2400` 魔数默认。替换路径（Slice B）：capture 实况 >
   live device query（wm size）> 显式已验证配置；无法确定 → fail-closed。
   是否正式移除 Host fallback = OWNER_GATE 项，带证据上报。

STOP 条件：若实现发现必须改 WorldModel/Grounding authority → 
IMPLEMENTATION_DESIGN_CONFLICT。当前判断：不需要——SpatialLocator/grounding
是 realization 词汇层，加维度绑定不改 authority。

## Slice A — CoordinateSpace typed contract

落点 `src/UniClaw.Kernel/Perception/`（capture 域声明，消费方引用）：

```text
CoordinateSpace(CoordinateSpaceId, PixelWidth, PixelHeight, Rotation, CaptureId?)
- dims > 0；Rotation ∈ {0,90,180,270}；构造期 fail-closed
- identity：CoordinateSpaceId 为键；同 capture 同 dims 同 rotation ==
- 挂点声明（doc 级，接线在 B/C）：screenshot capture / hierarchy descriptor /
  grounding binding / effect dispatch 各自声明所属空间
```

Gate A 测试：1080×1920 / 1080×2400 / landscape(1920×1080) / rotation 90
swap dims 语义 / capture size ≠ configured default（空间不等 → 不得互投）。
AUTO_GATE（5.3 自审）。

## Slice B — Capture Owns Reality Dimensions

- screenshot：capture 时已知 Width/Height（PngImage）→ 进入空间声明与
  frame claim（尺寸随证据携带）。
- hierarchy：ParseHierarchyObservation 增加 bounds 来源尺寸（viewport
  px 语义）→ HierarchyCaptureDescriptor.Space。
- Host viewport 来源优先级执法：capture metadata > live query（wm size）
  > explicit validated config；禁 silent fallback；不可确定 →
  Unknown/unavailable，effect 前fail-closed。

## Slice C — Grounding Binding

- binding/occurrence 携带 CoordinateSpaceId（SpatialLocator 增可选维度或
  平行字段——不改 authority，仅 realization 词汇）。
- effect 前机械检查：grounded space == 当前 effect/device space；mismatch/
  stale/rotation changed/viewport changed → RE-GROUND / RE-OBSERVE，不
  dispatch。禁「旧截图坐标 × 当前设备尺寸直接 tap」。

## Slice D — Effect Boundary Mechanical Gate

tap/swipe 前最小 gate：space valid / dims valid / rotation compatible /
bounds inside viewport / fresh（沿用既有 grounding freshness）。失败 =
zero effect + explicit diagnostic。不在 Effect Boundary 重做 perception /
authority 判断。

## Slice E — Regression + Real-device

全量回归（PER-013 typed / Grounding / Assurance / Simulation）+ 真机：
1080×1920 正路径、非 2400 高设备、rotation/viewport mismatch negative、
HostLiveFull GREEN。证明形态翻转：actual capture dims → correct grounding
→ correct tap。

## OWNER_GATE

十项汇报（root cause / contract / files changed / removed defaults /
enforcement / negative tests / HostLiveFull / full solution / deviation /
remaining gaps），停止等待 Owner。不自行 CLOSED。
