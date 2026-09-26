# CSC-002 — Viewport Resolution Normalization & Validity Hardening

版本：v0.1（implementation change）

```text
Leader: 5.3
Worker: 5.3-Flash
Base: 3dca53b4（CSC-001 CLOSED 后最新 HEAD；ID 取自 changes/INDEX.md 下一可用）
```

## Intent（WHAT/WHY）

在 CSC-001 正确性基础上：① 去掉「每次 dispatch 都 adb wm size」的不必要
成本；② 明确 explicit viewport config 的有效边界；③ viewport resolution
归一成唯一机制——禁止 Host/Capture/Effect 各自猜尺寸。

原则：不恢复裸缓存、不引入 lease/version 子系统；利用现有 device
session、capture CoordinateSpace 与 CSC-001 contract 做最小闭环。

## Scope / Out of Scope

Scope（AUTO_GATE 推进实现与测试；最终停 OWNER_GATE）：

- Phase 0：resolution 现状唯一链路图（Flash inventory）
- Gate 0：现有 seam 确认（需要第二套 device/session authority → 
  STOP DESIGN_CONFLICT）
- Slice A：唯一 resolver（如 `DeviceViewportResolver`；输出复用
  CoordinateSpace + `ResolvedViewport{Space, Source(LiveDevice|
  ExplicitValidatedConfig), DeviceSessionIdentity}`；无复杂 revision/lease）
- Slice B：session-scoped cache（attach 查一次 → 多 dispatch 复用）；
  失效条件：serial/session 变、ADB transport 失败、capture Space 冲突、
  config 变、观测不一致；失效 → fresh query → re-ground if required；
  禁 TTL、禁永久缓存
- Slice C：explicit config 有效性（live wins；transient 不可用 + 同
  session + capture 证据不冲突 → 可 fallback；任何证据冲突 → config
  invalid → zero effect/re-resolve；禁 global default / 跨设备 stale
  config / config 压倒矛盾证据）
- Slice D：capture 作 cheap invalidation 信号（cached vs new capture
  尺寸冲突 → invalidate → fresh device resolution → 旧 grounding
  RE-GROUND）；capture ≠ device authority
- Slice E：effect 路径归一（Target.Space vs 当前 ResolvedViewport
  Matches；cache 有效直用 / 失效 resolve once / config 合法 fallback /
  仍无解 fail closed；driver 不复制 resolution 规则）
- 归一化审计：全仓单一 policy（live > validated config > unresolved），
  全文搜索 wm size / ViewportWidth / ViewportHeight / CoordinateSpace /
  device-viewport 无旧魔数与平行 fallback（测试 fixture 显式尺寸允许
  并标清）
- V1–V10 + CSC-001 focused + HostLiveFull + TypedLiveChain + full +
  certification

Out of Scope：PER-011 Fusion、概率/confidence、lease framework、TTL
cache、background polling、display 事件子系统、新 WorldModel/Grounding/
Effect authority/path、多显示器过度设计。

## Gate 类型

AUTO_GATE 全部实现与测试；**最终 OWNER_GATE**（十项汇报，不自行 CLOSED）。

## Acceptance

1. Phase 0 链路图在案；Gate 0 无 DESIGN_CONFLICT。
2. 唯一 resolver；Host/Capture/EffectDriver 无各自 viewport 判断。
3. V1–V10 全绿；wm 查询次数从 per-dispatch → session-scoped（V1 断言）。
4. 失效条件全部机械化测试（V3/V4/V8）。
5. config 边界测试（V5/V6/V7）+ fail-closed（V9）+ E4 回归（V10）。
6. HostLiveFull / TypedLiveChain 真机绿；full solution + certification 绿。
7. 归一化审计报告（全文搜索证据）。
8. OWNER_GATE 十项汇报，等待 Owner 裁决 A/B/C 三问。

## References

- changes/CSC-001/（contract 与 Owner 冻结裁决）
- evidence/2026-09-27-per-013-execution-feedback.md（E4 根因谱系）
