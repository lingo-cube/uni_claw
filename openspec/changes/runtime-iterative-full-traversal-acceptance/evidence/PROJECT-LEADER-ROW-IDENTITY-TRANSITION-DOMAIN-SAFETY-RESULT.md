# PROJECT_LEADER_ROW_IDENTITY_UNRESOLVED_CONTAINER_TRANSITION_DOMAIN_SAFETY_RESULT

> Gate：ROW_IDENTITY_UNRESOLVED_CONTAINER_TRANSITION_DOMAIN_SAFETY（STABLEKEY_CONTAINER_DOMAIN 毕业前
> 的必要安全闭合）。
> 状态：**实现完成（harness 侧，构建曾干净）；验证因并行代理正在编辑共享 runtime 树而瞬时阻塞**——
> 按并行隔离纪律不触碰兄弟的在改文件；精确待验证项见 §8。Phase 2.6 维持 STOPPED。

## 1. Exact lifecycle trace（null 帧下 X-Known-Rows 的真实暴露链）

```
authorized child entry（TAP 执行）→ actionTap 记录 DeviceAction
→ transition settle → 观测 obs → transform:
    if lastAction is Tap/ScrollBackward → MarkTransitionPending()
    BeginContainer(SettingsStrategyBinding.ResolveSemanticPage(obs))
      · valid identity → activate/create domain + 清除 pending
      · null + pending → 保持 pending（offer 空）
      · null + 无 pending → 保持当前域（同容器）
    Stabilize(obs)（pending → quarantine 重键；非 pending → 当前域重键）
    ToHeaderJson() → pending 时 = null / 否则当前域 rows
→ python D5 只对 offer（=当前提供面）匹配 → StableKey 赋值
```
- **Parent→Child、首帧 null**：pending → offer=null → child 无法获得 parent 任何键（修复后）。
- **同容器滚动 title-off null**：无 pending → 当前域保留（含 offer）→ 续行不变。
- **旧行为对比**（修复前）：`BeginContainer(null)` 一律保留当前域 → child 首帧 null 时 root 域仍暴露
  → python 文本命中 → child 继承 parent 键（**BUG CONFIRMED**，单元级可证：新 falsifier 旧行为 RED）。

## 2. Null-frame falsifier（§6-1）→ GREEN（修复后，实现级）

`UnresolvedChildEntry_ParentKeyNotExposed`：root 有 `Accessibility`→row_NNN；MarkPending→null →
`ToHeaderJson()==null`、child 标题重键 ≠ 父键、不暴露父行。同容器对照
`SameContainerScrollNull_Frames_RetainCurrentDomain`：无 pending 的 null → 当前域保留（offer 含行、键不变）。

## 3. Same-container vs transition-null 区分（语义 A-D）

| 情形 | 信号 | 行为 |
|---|---|---|
| A 同容器未解析帧 | ScrollForward + null | 保留当前域（offer 照常）|
| B 过渡未决-未解析目的 | Tap/ScrollBackward + null | **CORRELATION_SUSPENDED：offer 空、quarantine 重键、不暴露任何容器行** |
| C 新容器已落地 | valid 新身份 | 激活/创建其域（pending 清除）|
| D verified return | valid 已知身份 | 重激活保全父域（原键恢复）|

`NULL_LOCATION != SAME_CONTAINER` 已落地（pending 状态区分，而非 "null==same"）。

## 4. Owner / 最小 diff

- Owner：ValidationHarness（`RowIdentityContext` + `SettingsCampaignProgram.ObservationTap` —— known-rows
  内容的唯一属主；production 仅传输、归属权证明见上 gate §6）。
- diff：
  - `RowIdentityContext`：+`MarkTransitionPending()`；+`#_transitionPending` + `#_quarantine`；
    `BeginContainer(null)` 在 pending 时保持 pending（不切域）；valid 身份清除 pending；
    `Stabilize`/`FindOrCreateId` pending 走 quarantine；`ToHeaderJson` pending 返回 null。
  - `SettingsCampaignProgram`：`ObservationTap` 增加 `actionTap`（记录每个已执行 DeviceAction——
    运行时的可信决策，无启发）；transform 依 action 类型设定 pending（Tap/ScrollBackward→pending；
    ScrollForward→否）后再 `BeginContainer`。

## 5. Trusted transition signal

`DeviceAction`（运行时执行的真实动作）+ `SettingsStrategyBinding.ResolveSemanticPage`（运行时建容器同源
解析）——两者均为既有权威信号；ValidationHarness 无需新 authority（无 HUMAN_GATE_REQUIRED）。
未从感知启发推断过渡状态。

## 6. Tests（gate §6 八项）——已写入，验证被并发阻塞（见 §8）

`tests/UniClaw.Runtime.Tests/ValidationHarness/RowIdentityContextTransitionSafetyTests.cs`：
1 未解析 child 入口不暴露父键 · 2 同容器滚动 null 保留域 · 3 pending→child 落地激活子域 ·
4 pending→return 父域完整恢复 · 5 嵌套无祖先下行泄漏 · 6 兄弟域无泄漏 · 7 同容器回访不变 ·
8 Z4 falsifier 仍 GREEN。
另：原 `RowIdentityContextDomainTests`（11）延续。

## 7. Fresh buyer（Z6 计划，待树可用）

`settingscampaign 1`（OCR 新环境 + 域修复收据）→ 捕获：每观测 resolved semantic location /
active RowIdentity domain / X-Known-Rows 域 / root 'Accessibility' key / child title+content keys /
跨域继承计数 / 重复签名计数 / 进入至少一个真实 child。截图 AssetRef 缺失时标 MISSING_ASSET（
stage evidence 足够身份证明则不等截图）。

## 8. 并发阻塞（诚实声明）

实现已在 harness 构建中验证过干净（0 error，改动前的 `dotnet build ValidationHarness`）；
但**并行代理此刻正在编辑共享 runtime 树**（`Agent.ContainerReconciliation.cs`/`Agent.OpenWorld.cs`
mtime 21:47+ 实时变化；中间态缺 `using UniClaw.Runtime.World;` → CS0103 'Reconcile'，runtime 工程暂不编译），
测试工程依赖 runtime → **本 gate 的 GREEN/全量/真机验证被兄弟进程的中间态瞬时阻塞**。
按 parallel-agent 隔离纪律：不碰兄弟在改文件。待其提交后立即执行：
```
dotnet test --filter RowIdentityContextTransitionSafetyTests|RowIdentityContextDomainTests   # 期待 19/19
dotnet test src/UniClaw.Runtime.sln                                                          # 全量 A/B（零新增）
settingscampaign 1 → Z6 真实 child 证据
```

## 9. Graduation 状态

- STABLEKEY_CONTAINER_DOMAIN_SCOPING：**NOT YET GRADUATED**（待 §8 验证 + Z6 真实 child 证据：
  CROSS_CONTAINER_KEY_LEAK=0 含未解析入口/嵌套/兄弟/返回 + 同容器连续性不变）。
- Phase 2.6 current first blocker：`ROOT_UNKNOWN_PERCEPTION_VARIANCE`（'LoO' 族，与身份无关）。
- Slow/Semantic Calibration（shadow）就绪度：**ready after 本 gate**（§9 推荐路径：Fast perception
  改进 + Slow VLM Container Semantic Calibration shadow，针对 LoO/OCR、Wallpaper 短读、Bluetooth 碎片、
  Accessibility 节标题/描述、PageTitle vs 内容行——不连运行时 authority）。