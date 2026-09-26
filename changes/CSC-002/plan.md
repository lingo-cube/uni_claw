# CSC-002 Plan — Phase 0 → Gate 0 → Slices A–E → 归一化审计 → OWNER_GATE

## Phase 0（Flash inventory 进行中；Leader 已知事实预写）

现状链路（CSC-001 后）：`AdbLiveEffectDriver.Deliver` → ValidateSupport →
Target.Space 检查 → `ResolveDispatchSpace()`（**每次 dispatch** wm size 实测
Override>Physical > 显式 config > null fail-closed）→ Matches → TryBuildTap。
config 来自 HostOptions（nullable 成对）；capture 侧 LivePerception 用 shot
实况尺寸产 frame claim w/h（capture-owned reality，不是 device resolution）。

## Gate 0 设计裁决（待 inventory 确认后定稿）

**seam 确认**：现有 seam 足够——
- device session 生命周期 = resolver 实例生命周期（HostRunner.RunOnce 每次
  运行新建 driver/resolver → 新 session）；serial 构造期绑定；
- ADB transport 断连表现 = AdbProcessResult 失败形态（Started=false /
  ExitCode!=0 + stderr）——作为**保守失效信号**（见下），不需要新
  device/session authority；
- capture CoordinateSpace（CSC-001）即 invalidation 信号源；
- HostOptions config 是唯一 explicit config 入口。

**无 DESIGN_CONFLICT**：不新建第二套 device/session authority。

## Slice A — DeviceViewportResolver（唯一 resolver）

Kernel/Effects，internal（driver 内部组件，无外部 buyer；ControlBeliefView
internal 先例）：

```text
internal enum ViewportSource { LiveDevice, ExplicitValidatedConfig }
internal sealed record ResolvedViewport(
    CoordinateSpace Space, ViewportSource Source, string DeviceSessionIdentity)

internal sealed class DeviceViewportResolver
    ctor(string serial, CoordinateSpace? explicitConfig, Func<CoordinateSpace?> liveQuery)
    - explicitConfig 成对/为正（caller 保证；HostOptions nullable 对）
    ResolvedViewport? Resolve(CoordinateSpace? captureEvidence)
    Invalidate()                       // 显式失效
    ObserveTransportFailure()          // ADB 失败形态 → 保守失效
    LiveQueryCount（测试观察位）
```

唯一 policy 封装于此：live > validated config > unresolved；Host/Capture/
EffectDriver 不得再有各自判断（driver 只消费 resolver）。

## Slice B — session-scoped cache

- `Resolve`：cache 有效 → 直用（零查询）；无效/缺失 → liveQuery 一次 →
  成功缓存（Source=LiveDevice）；失败 → config（Source=
  ExplicitValidatedConfig，**不缓存**——transient 语义，下次再试 live）。
- DeviceSessionIdentity = serial + 实例 id（新实例 = 新 session，V4/V8 的
  隔离即实例隔离 + per-run 构造）。
- 失效条件（全机械）：① 新实例（session/serial 变）② ObserveTransportFailure
  （adb 进程失败形态 → 保守清缓存；假失效无害：多一次查询）③ capture
  evidence 与 cached space 不 Matches（Slice D 信号）④ config 变（实例
  构造期固定——不变即不失效；文档化）⑤ 观测不一致 = ③ 的等价表述。
- 禁 TTL、禁永久缓存（live 成功结果只在失效条件未触发期间有效）。

## Slice C — explicit config 边界

- live available → live wins（config 不参与，V6）；
- live transient 失败 + config 存在 + capture evidence 与 config 不冲突
  （capture null 或 Matches(config)）→ config fallback（V5）；
- capture evidence 与 config 冲突 → resolved=config 但随后 Matches 检查
  必然失败 → zero effect RE-GROUND（V7 的自然落点——config 不压倒证据）；
- 禁 global default / 跨设备 stale config（per-serial/per-instance 构造，
  V8）/ config 压倒矛盾 live（live wins）。

## Slice D — capture 作失效信号

`Resolve(captureEvidence)`：cached 存在且 `!cached.Matches(captureEvidence)`
→ Invalidate → fresh liveQuery。**capture ≠ device authority**：fresh
resolution 仍是 device 实测；capture 只触发失效，不写入缓存。

## Slice E — effect 路径归一

`AdbLiveEffectDriver.Deliver`：
```text
ValidateSupport → Target.Space 检查（C 层不变）
→ resolvedViewport = _resolver.Resolve(Target.Space)
→ null → coordinate-space-unresolved（zero effect）
→ !Target.Space.Matches(resolvedViewport.Space) → mismatch RE-GROUND
→ dispatch（Tap 后 ObserveTransportFailure(result)）
```
driver 不复制 resolution 规则（全部在 resolver）。

## V1–V10 落点

resolver 单测（liveQuery delegate 注入 + 查询计数）V1–V9；V10 = 既有
E4 回归（CSC-001 suite 保持绿）。driver 级：FakeRunner 计 wm 调用数
（V1/V2/V3 的 dispatch 视角）。

## 归一化审计

全文搜索 `wm size` / `ViewportWidth` / `ViewportHeight` /
`CoordinateSpace` / `device-viewport`：生产代码只允许 resolver 一处
resolution + HostOptions 一处 config 入口 + LivePerception 一处 capture
尺寸（frame claim）；其余命中 = 测试 fixture（标清）。
