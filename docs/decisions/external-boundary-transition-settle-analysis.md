# External Boundary Transition Settle Analysis

> Date: 2026-08-23
> Role: Implementation Worker (DeepSeek-V4-Flash)
> Task: PROJECT_LEADER_EXTERNAL_BOUNDARY_TRANSITION_SETTLE_ANALYSIS — analyze
> why the ExternalBoundary flow fails to detect the external foreground after a
> SUCCESSFUL action. **Analysis only — no code changed.** Constraints honored:
> no scroll-stability / OCR / Vision / ADB-primary / Semantic / Agent-authority
> / normal-dispatch changes.
>
> Evidence source: EBD real-device run (`/tmp/ebd_real_evidence.txt`,
> `/tmp/ebd_obs_*.xml` — per-observation uiautomator frames, auxiliary analysis).

---

## Phase 1 — Human Symptom

用户可见问题：点击 "App location permissions" 权限入口后，**系统确实进入了外部权限页面**
（com.android.permissioncontroller 显示 "Location / Apps with this permission..."），
但 Runtime **没有识别到外部页面已打开**，把成功判成了失败
（"Authorized boundary source 'applocationpermissions' did not produce an
external foreground"），流程中止。

## Phase 2 — Evidence Collection

### Foreground package timeline (per-observation frames, numerical order)

| frames | package | content |
|--------|---------|---------|
| ebd_obs_0-13 | com.android.settings | Settings Root (Settings / Search settings) |
| ebd_obs_14-21 | com.android.settings | Location sub-page (Use location / Recent access) — incl. stability-confirmed frames + the boundary tap's first post-action frame |
| **ebd_obs_22** | **com.android.permissioncontroller** | **Location / Apps with this permission can access this device's location / Allowed all the time...** |

### Answers

1. **外部页面是否真实出现？** — **是**。ebd_obs_22 的首节点
   `package="com.android.permissioncontroller"`，内容为外部权限页（不是 Settings）。
2. **出现时间距 action 完成多久？** — tap 后 **≥1 个观察周期**：失败消息的
   `post=com.android.settings`（tap 后第一帧仍在旧应用，转场进行中），
   permissioncontroller 在**后续帧**才被捕获（uiautomator dump 每帧约 1-3s）。
3. **当前 ExternalBoundary 用哪个 observation 判断成功？** — `firstPostAction`
   = `entry.PostActionObservation`（**tap 后第一帧**），其 `ForegroundApplication`
   必须 != applicationIdentity，否则立即 fail-closed
   （`TryHandleExternalBoundaryAsync` line 1313-1316）。
4. **普通 action 和 external boundary 是否使用不同 settle 策略？** — **是**：
   - 普通 branch dispatch：`SettlePostActionObservationAsync`（bounded
     `MaxPostActionSettleObservations = 3`）→ 候选帧确认转场 → 用
     `settle.Confirmed`（确认帧）判断，第一帧只是 PROVISIONAL。
   - external boundary 的**进入判断**：直接读第一帧前台，无 settle；
     而同一 handler 的 **SystemBack 返回判断**（line 1349）反而有
     `SettlePostActionObservationAsync`（bounded）。

## Phase 3 — Failure Classification

**A — External boundary detection 缺少 transition settle**（决定性）。第一帧
仍在旧应用（转场延迟）被当作最终判断；后续帧证实外部页面已打开。排除项：

- **B — Environment foreground detection 错误**: NO — 前台检测正确
  （ebd_obs_22 正确识别 permissioncontroller；第一帧 settings 也是真实的旧页）。
- **C — Action execution 失败**: NO — tap 成功，外部页面实际打开。
- **D — Permission flow/fixture 问题**: NO — 权限页面正常出现。

## Phase 4 — Ownership

**Owner: Agent — external transition handling**
（`TryHandleExternalBoundaryAsync` 的进入判断）。Agent 决定"外部前台是否被
观测到"；它选择了第一帧（无 settle），而普通 dispatch 与 boundary 返回都
用 bounded settle。Environment 前台检测正确；Semantic Capability / test
fixture 无关。未改变：DFS、Traversal、GoalEvidence、Vision-first、
ADB auxiliary-only。

## Phase 5 — Decision Boundary

**1 — 只是 ExternalBoundary 自己缺少 bounded transition confirmation → 最小修复任务。**

- 修复范围：给 `TryHandleExternalBoundaryAsync` 的**进入判断**加与普通
  dispatch / boundary 返回相同的 `SettlePostActionObservationAsync`
  （bounded `MaxPostActionSettleObservations`），候选帧的前台 != owned 应用
  即确认外部转场；确认帧作为 `ExternalForeground`。预算耗尽 → 既有
  fail-closed（第一帧语义保留为 fail-closed 默认）。
- 不动：普通 dispatch、scroll stability、OCR/Vision、Semantic、Agent
  authority / Lifecycle ownership、场景知识。
- 不属 2（统一契约）：普通 dispatch 与 boundary 返回已用 settle，仅"进入
  外部"缺——局部补上即可，无需跨层契约评审。
- 不属 3（测试假设）：真实设备转场需要时间，测试断言合理。
