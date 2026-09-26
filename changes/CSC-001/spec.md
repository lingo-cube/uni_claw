# CSC-001 — Coordinate Space Contract

版本：v0.1（implementation change）

```text
Leader: 5.3
Worker: 5.3-Flash
Base: 5bd8432e（uni-harness HEAD，PER-013 已闭含 viewport 根因修复）
```

## Intent（WHAT/WHY）

修掉 PER-013 真机 E2E 暴露的结构问题：perception/capture 的坐标空间与
effect/tap 的坐标空间没有机械绑定——`HostOptions` 默认 viewport
（1080×2400 Pixel 魔数）可在检测正确的情况下把 tap 投到错误位置（实测
`input tap 967 1030` vs 真实 switch 中心 824，evidence/
2026-09-27-per-013-execution-feedback.md E4）。

本 change 只解决 `capture coordinate space → grounding coordinate space →
effect coordinate space` 的机械绑定；不做 PER-011 Fusion。

## Scope / Out of Scope

Scope（每 slice 独立 gate）：

- Phase 0：坐标空间链路 inventory（Flash 委派）
- Gate 0：canonical coordinate space 归属、可复用类型、CaptureMetadata 是否
  扩展、必须删除的默认 viewport——四问裁决
- Slice A：`CoordinateSpace` typed contract（CoordinateSpaceId / PixelWidth /
  PixelHeight / Rotation / CaptureId）；Screenshot / Hierarchy observation /
  Grounding target / Effect coordinate 各自声明所属空间
- Slice B：真实 viewport 只来自 actual capture/device observation（优先级
  capture metadata > live device query > explicit validated config）；无法
  确定 → Unknown fail-closed before effect，禁 silent fallback
- Slice C：grounding 绑定 Target + CoordinateSpaceId + Bounds；effect 前
  机械检查 grounded space == effect/device space；mismatch/stale/rotation/
  viewport changed → RE-GROUND/RE-OBSERVE，不 dispatch
- Slice D：tap/swipe 前最小 mechanical gate（space/dimensions/rotation/
  bounds-in-viewport/freshness）；失败 = zero effect + explicit diagnostic；
  Effect Boundary 不重做 perception/authority 判断
- Slice E：回归（PER-013 typed / Grounding / Assurance / HostLiveFull）+
  真机（1080×1920 路径、非 2400 设备、rotation/viewport mismatch negative）

Out of Scope（禁止）：Visual/XML Fusion、OCR/VLM arbitration、PER-011
implementation、WorldModel authority change、second Grounding path、second
Effect path、legacy *.state migration expansion。

## Gate 类型

- AUTO_GATE（5.3 自审继续）：Gate 0、Gate A、unit/fixture/regression gates
- OWNER_GATE（停止等 Owner）：final architecture closure、任何 authority/
  interface scope expansion、是否正式替换 Host viewport fallback
- **不自行宣布 CLOSED**

## 工程环境规则

`LONG_LIVED_ENVIRONMENT_OWNER = LEADER`：emulator / adb session /
perception server / 长期进程由 5.3 Leader 启动持有；Flash 不拥有长生命周期
进程。AVD 测试：immutable base AVD → APFS clone to /tmp → cold boot →
test → cleanup；不依赖已污染的原始 AVD runtime state。

## Decisions

（Gate 0 起回填）

## Acceptance

1. Phase 0 链路表（producer/space/metadata/conversion/consumer）在案。
2. Gate 0 四问有明确裁决；如需改 WorldModel/Grounding authority →
   STOP IMPLEMENTATION_DESIGN_CONFLICT。
3. Slice A：四类对象（screenshot/hierarchy/grounding/effect）可声明坐标空间；
   Gate A 测试覆盖 1080×1920 / 1080×2400 / landscape / rotation change /
   capture size ≠ configured default。
4. Slice B：viewport 来源优先级执法，silent fallback 消除，fail-closed。
5. Slice C：binding 携带 CoordinateSpaceId；mismatch → re-ground 不 dispatch。
6. Slice D：effect 前 mechanical gate，失败 zero effect + diagnostic。
7. Slice E：全回归绿 + 真机三场景（含 negative mismatch）+ HostLiveFull GREEN；
   原 bug 形态（detected correct + viewport wrong → tap wrong）变为
   （actual capture dims → correct grounding → correct tap）。
8. OWNER_GATE：十项汇报，等待 Owner 裁决。

## References

- evidence/2026-09-27-per-013-execution-feedback.md（E4 根因）
- changes/PER-013/state.md（post-closure-fix 行：viewport 魔数债登记）
- docs/architecture/product-architecture-baseline-l0-l3.md
