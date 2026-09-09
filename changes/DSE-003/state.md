# DSE-003 — NativeLocator + 第二后端 ego-browser driver（+EB 级联去重搭车）
lifecycle_state: closed · disposition: none · depth: decision-heavy · base: 1cdc4a74

## Intent（WHAT/WHY）
DSE-002 落地 DeliveryTarget 时 NativeLocator 按 Human 裁决「无数据链不建」
推迟。本轮批准的第二后端（ego-browser，收窄版：driver 侧 only、fixture
喂给）使其 buyer 成立：浏览器的 backend-node-id 就是 stable key 本身。
一次验证三件事：可替换性宣言的第二次实证（协议真的后端中立）、
NativeLocator 数据链兑现、stable-key 优先于坐标的 delivery form。
搭车项：RVR-001 移交欠账（EB Bind 双分支拒绝级联去重——有移交凭据，
纯局部重构零行为变化）。

## Scope
- `World/NativeLocator.cs`（新）：`NativeLocator(Kind, Value)`——开放
  kind 词汇（`<platform>.<key-kind>`，如 browser.backend-node-id /
  android.resource-id）；execution anchor，evidence-derived，**不是
  identity**（P-UW-24 纪律——identity 在 WorldModel 域）。
- occurrence 族 + `NativeLocator? Native`（末位可选）；BindingView +
  `TargetOccurrenceNative`（owner fact）；CanonicalBinding +
  `TargetNative`；DeliveryTarget + `NativeLocator? Native`——全链同
  SpatialLocator 模式。
- `Effects/EgoBrowserEffectDriver.cs`（新）：第二产品 driver（dry-run：
  构造命令串，不真开浏览器——真实 CDP 调用随全链接入 buyer）。支持集
  = kind ∈ {browser.backend-node-id} × effect ∈ {click, tap, set-switch}
  （全部产出 click 语义——legacy 先例：SetSwitch 物理即 click）→
  `ego click node:{id}`。
- **EB 级联去重**（RVR-001 欠账）：Bind 的 UI/字符串双分支共享拒绝序
  （stale→ambiguous→分支特定 unknown）提取为单一私有判定——零行为变化，
  既有 Bind 四态测试全绿即证。
- 协议基线 P14（NativeLocator 落地 + 多 locator 并存语义）+ CONTEXT.md
  词条更新。
- 测试：双 driver 交叉（同一 DeliveryTarget 两 locator 各得其所）、
  无 fallback 断言、kind/不支持集失败族、级联去重回归。

## Out of Scope（禁止）
- 真实浏览器/CDP 进程调用（全链接入 = 观察侧 P2 第二 provider，独立
  change 另议——本轮收窄为 driver-only）。
- driver 侧 locator 挑选 / fallback / primary 字段——**定案 b 方案**：
  多 locator 并存 = 同一已授权 target 的不同 delivery material；driver
  固定消费自身支持集（ego 只认 native、adb 只认 spatial），永不挑选、
  永不降级（规则 B/C 延续；比 primary 字段更笨更对）。
- android.resource-id 的 ADB 消费（uiautomator by-id）——无 buyer。
- 机械手 schema / 异步 / AttemptId（defer 台账不变）。

## Decisions
- NativeLocator 归 World 域（与 SpatialLocator 对称：occurrence 携带的
  locator 素材族；kind 开放词汇，不锁枚举——协议通则 4）。
- ego driver 支持集声明即「delivery form 决定权」的实现：lowering 产出
  全部可用 material，driver 按支持集消费——无选择行为、无 fallback 路径。
- 级联去重不改变拒绝序与 reason 词汇（RVR-001 移交凭据边界）。

## Alternatives（被拒）
- DeliveryTarget.PrimaryLocatorKind 字段——显式 primary 需要 lowering
  预知 driver 偏好（破坏后端中立）；支持集方案零新增字段。
- ego driver 内 native 失效 fallback 到 spatial 坐标命中测试——语义
  重定位（Human 裁决明确禁止的 stale grounding 事故源）。
- 本轮接真实 CDP——观察侧 provider 未议，收窄版先行。

## Owner-Authority impact
- 零新 Owner；EB lowering 语义零变化（多携带一种 material）。
- Capability Plane 首次出现第二个产品 driver——可替换性的第二次实证。

## ADR refs
- DSE-002 state（NativeLocator defer 条件兑现）；ADR-0011（字段 buyer =
  ego-browser 后端）；P14。

## Residual risks
- backend-node-id 的真实生命周期（DOM 重建即失效）——失效→
  DeliveryFailed→上游 re-ground 的闭环依赖未来真链接入验证。
- kind 词汇表治理（防泛滥）——随真实后端数增长再议。

## Acceptance
N1 fixture 端到端：occurrence(native=node:827) → bind → DeliveryTarget
   (Native) → EgoBrowserDriver → `ego click node:827`
N2 双 driver 交叉：同一 DeliveryTarget(spatial+native 并存) → adb 出
   tap 命令（只消费 spatial）、ego 出 click 命令（只消费 native）——
   互不感知、无挑选
N3 无 fallback：仅 spatial 的 target 投 ego → DeliveryFailed
   (no-executable-locator)；仅 native 投 adb → 同（规则 C 行为面）
N4 支持集失败族：kind=android.resource-id 投 ego →
   unsupported-native-kind；effect=scroll 投 ego → unsupported-effect
N5 级联去重：既有 Bind 四态拒绝测试全绿零行为变化（含 UI/字符串双通道）
N6 白名单：BindingView +TargetOccurrenceNative（N1 守卫迁移）
N7 既有全量零回归（并行会话半成品除外）

## Constraints
- 新代码仅限 World locator 族 + Effects（含 ego driver + 级联去重）+
  协议基线/CONTEXT.md + 新测试；UniKernel.cs 零触碰（其工作区改动属
  并行 LAT-001 会话）；确定性（driver clock 注入）。

## Verification
```yaml
verification:
  level: DETERMINISTIC
  method: dotnet test（全解决方案，两次独立运行）
  expected: N1–N7 GREEN；全量零回归
  actual: >
    222/222 GREEN（Kernel 205 + Agent 17；两次独立运行）。新增
    EgoBrowserDeliveryTests 6 用例全绿：N1 端到端（native=node:827 →
    "ego click node:827"）；N2 双 driver 交叉（同一 DeliveryTarget
    (spatial+native 并存)：adb 出 "adb shell input tap 540 164"、ego 出
    "ego click node:827"——各消费支持集、互不感知）；N3 无 fallback
    行为断言（spatial-only 投 ego → DeliveryFailed 且 Report 无坐标
    猜测；native-only 投 adb → 同）；N4 支持集失败族。级联去重（N5）：
    Bind 双分支拒绝序收拢为 Decide 骨架，既有 UIW-004/ControlToEffect
    四态测试全绿零行为变化（RVR-001 欠账清偿）。N6 白名单 +1。变更面 =
    World/（NativeLocator 新增 + occurrence 族/BindingView 载荷）+
    Effects/（TargetBinding/DeliveryTarget +Native；EffectBoundary 级联
    去重 + native 携带；EgoBrowserEffectDriver 新增；AdbEffectDriver
    支持集 +click）+ 协议基线 P14 + CONTEXT.md + 测试。并行 LAT-001
    会话期间其 RunTrace 守卫已被其自行修复（本轮两次全量均绿）。
  evidence: dotnet test 输出（2026-09-09，两次独立运行）；in-flight
    文件移开-验证-复原（内容零修改）
```

## Status log
2026-09-09 · understanding→resolved→planned · 下一方向裁决（Human「可以」
  首推 #1）：ego-browser 第二后端收窄版 + NativeLocator buyer 兑现 +
  RVR-001 欠账搭车；to-spec + PLAN（plans/2026-09-09-dse-003-native-locator-ego-driver.md）

2026-09-09 · planned→implemented · Direct 实施。NativeLocator（World 域，
  kind 开放词汇）/ 全链载荷 / EgoBrowserEffectDriver（native-only 支持
  集）/ EB Decide 级联骨架
2026-09-09 · implemented→reviewed · REVIEW 偏离 1 条（正向）：N2 暴露
  跨后端 effect 词汇缺口（adb 支持集无 "click"）——裁决为 driver 内
  物理同义映射（click/tap → input tap），adb 支持集扩为
  {tap, click, set-switch}，两 driver 词汇集对齐；其余与 PLAN 一致
2026-09-09 · reviewed→verified→closed · 两次独立 222/222 GREEN + diff
  审阅合规 → SECOND_BACKEND_DELIVERY_FORM_ESTABLISHED。可替换性第二
  实证完成；RVR-001 移交欠账清偿
