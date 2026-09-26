# CSC-002 — Viewport Resolution Normalization & Validity Hardening

lifecycle_state: verified · disposition: none · depth: decision-heavy · base: 3dca53b4

## Intent（WHAT/WHY）

见 spec.md。per-dispatch wm 查询成本去除 + explicit config 边界 + 唯一
resolution 机制。

## Gate 状态

- **Phase 0 inventory：完成**（Flash subagent；链路图 + file:line +
  P1–P7 残留清单归档 plan.md 语境）。
- **Gate 0：PASS（无 DESIGN_CONFLICT）**——四项裁决：
  (a) P1 dry-run `AdbEffectDriver` = 纯命令构造器（不执行、无设备、
  不做 resolution 判断），豁免并标注（非平行 policy）；(b) P3 typed
  hierarchy Space 填充源 = 同窗截图实测（capture 并流；R5 串行假设归
  PER-011）——已实现；(c) P6 config fallback **保留**（owner Slice C
  明确定义其边界与 V5–V7 测试；窄窗口无妨）；(d) session identity =
  resolver 实例生命周期（HostRunner per-run 新建；不建 serial↔session
  绑定对象 = 不建第二 device/session authority）。
- **Slice A–E：完成（AUTO_GATE）**——
  A/B：`DeviceViewportResolver`（internal，唯一 policy：live > validated
  config > unresolved；session-scoped cache 只存 LiveDevice 结果；
  失效 = 新实例/transport 失败形态（保守）/capture 冲突/config=实例固定；
  禁 TTL/永久缓存）；C：config fallback 不缓存（transient），capture
  冲突由调用侧 Matches 落 zero effect（config 不压倒证据）；D：capture
  只触发失效不写缓存（≠ device authority）；E：driver 只消费 resolver
  （零复制规则），tap 后 transport 失败形态 → 保守失效。
- **V1–V10：全绿**——resolver 级 V1–V9 + 失效/invalidate（11 例，
  `DeviceViewportResolverTests`）；driver 级 V1（5 dispatch = 1 次 wm
  查询）/V3（capture 信号失效 → 重实测 → 旧 grounding 拒绝；stale
  capture 每次触发重实测 = Slice D 语义本体）；V5/V6/V7/V9 resolver 级
  覆盖；V10 = 既有 E4 回归 suite 保持绿。Kernel 623/623（+12）。
- **归一化审计：PASS**——生产 `wm size` 调用唯一（driver liveQuery）；
  ViewportWidth/Height 唯一写（HostOptions 定义）+ 唯一读（driver ctor）；
  CoordinateSpace 生产构造 4 处各司其职（capture 信号/config 输入/live
  实测/typed P3 填充）无平行判断；`device-viewport` 命中 = 词汇定义处
  与测试 fixture（P2 三段式 = SpatialLocator 开放词表 fixture，非
  CoordinateSpaceId，豁免标注）。Host 61/61；全量 1027/1027；
  再认证 change=CSC-002。
- **真机三件套：PASS（2026-09-27，API 35 emulator /tmp clone → cold
  boot → 测 → 清理，Leader-held）**——CSC-001 focused 36/36；
  TypedLiveChain PASS（typed 证据现携带 Space——P3 填充生效）；
  HostLiveFull PASS（wifi 真翻转 Completion；session cache 真实生效）；
  LiveCoordinateGateTests 2/2（resolver live 路径 + stale grounding
  mismatch 真机负例）。
- **OWNER_GATE：到达（2026-09-27）**——十项汇报已交 Owner（A/B/C 三问
  附直接证据），等待终裁；不自行 CLOSED。

## Verification

```yaml
level: DETERMINISTIC（live 项 = ENVIRONMENT，已执行）
method: >
  V1–V10（DeviceViewportResolverTests 11 例 + driver 级 V1/V3 + 既有
  E4 回归 suite）；CSC-001 focused 四套件；真机三件套 + live gates
  （DSH_TEST_PERCEPTION_LIVE=1，API 35 emulator clone）；全量七套件；
  场景库再认证（--change CSC-002）；归一化审计 grep（wm size /
  ViewportWidth / CoordinateSpace / device-viewport 生产命中清单）
expected: >
  session 内多 dispatch 单次 wm 查询；capture 信号失效 → 重实测 → 旧
  grounding 拒绝；config 仅 transient 合法 fallback 且不缓存；无平行
  viewport 判断；全量零回归
actual: >
  Kernel 623/623（+12）· Host 61/61 · 全量 1027/1027 · certification
  PASS（29 files, 0 violations）· focused 36/36 · live：TypedLiveChain /
  HostLiveFull / LiveCoordinateGate 2/2 全 PASS
evidence: >
  本文件 + DeviceViewportResolver.cs + AdbViewportResolutionTests（V1/V3
  driver 视角）+ DeviceViewportResolverTests（V1–V9）+ 归一化审计输出
  （status log 2026-09-27 行）
```

## Status log

- 2026-09-27 · implemented→verified·owner-gate · 真机三件套 + live gates
  全 PASS（clone→cold boot→测→清理）；归一化审计 PASS；OWNER_GATE 十项
  汇报交付，等待 Owner 终裁 A/B/C。
- 2026-09-27 · understanding→implemented · Phase 0（Flash）+ Gate 0 四裁 +
  Slice A–E 实现 + V1–V10 + 归一化审计；全量 1027/1027；再认证 PASS。
  下轮真机三件套 + OWNER_GATE。
- 2026-09-27 · understanding · change 落档（ID 取 INDEX 下一可用 CSC-002）；
  base 3dca53b4（CSC-001 已闭）；Phase 0 委派 Flash；工程规则延续
  LONG_LIVED_ENVIRONMENT_OWNER=LEADER。
