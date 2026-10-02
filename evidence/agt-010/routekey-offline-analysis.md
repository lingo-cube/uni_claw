# AGT-010 Evidence — RouteKey/ViewportDigest 离线区分度分析与确定性验证

> worktree：../uni_claw-agt009。等级：DETERMINISTIC（语料区分度 + 纯函数）
> + SCENARIO（模拟世界端到端）。语料 =
> evidence/real-settings-coverage-negative-20261001/run-e1-with-evidence/evidence/
> （23 份实录 XML，撞名现场 step 20 同源）。

## 1. 离线区分度分析（字段冻结依据）

- method：python 脚本对 23 份 XML 抽取（标题、标题来源、up 按键、滚动容器
  集、clickable 景观摘要）并按逻辑页分组（脚本一次性，结论固化进
  SettingsRouteKeyTests 的固定采样断言）。
- 关键结论（expected = §2 判据）：
  - 撞名页 capture-7323abdbfd05（Security & privacy）：title="Settings"，
    src=title，up=1，sc={content_parent, recycler_view}；
  - 根页 12 份采样：title="Settings"，src=homepage_title，up=0——
    撞名页与根页 src/up 全不同（标题相同）；
  - **字段证伪**：根页初始视口（capture-4a77/21d9）滚动容器集 =
    {main_content_scrollable_container, settings_homepage_container}，滚动后
    （10 份）= {main_content_scrollable_container}——sc 全集与文档序
    first/last 均不满足同页稳定 → 从 RouteKey 剔除（Decision 6）；
  - ViewportDigest：根页三视口成组稳定（同组重复采样 digest 全等），
    视口间互异；Display 页与 Internet 页同 digest 不同标题（独立性反例）。
- actual：测试固化断言全部通过（SettingsRouteKeyTests 5/5）。
- evidence：tests/UniClaw.Host.Tests/SettingsRouteKeyTests.cs；
  src/UniClaw.Host/SettingsTraversalLiveFeed.cs（DeriveRouteKey /
  DeriveViewportDigest）。

## 2. 撞名页可验证进入（Acceptance 3，Drive 级）

- method：`FullCoverageMission_CompletesWithTraceableSteps`——模拟世界加入
  撞名页（Security & privacy，二级页标题渲染为 "Settings"，与根页同标题），
  RootRoute 迁移为多信号 key 后全任务跑通。
- expected：撞名页进入步骤验证通过（RouteAfter =
  `android.settings|rk1:Settings|src=title|up=1` ≠ RootRoute），任务完成，
  全部 21 步 verified。
- actual：PASS（137/137 Host 套件）。
- evidence：tests/UniClaw.Host.Tests/SettingsCoverageScenarioTests.cs。

## 3. 滚动到底有界停止（Acceptance 4，Drive 级，§3 确定性背书）

- method：`ScrollAtBottom_ContentUnchanged_FailsVerification_BoundedStop`——
  世界置于列表底部（swipe 永不改变内容）。
- expected：swipe 投递成功但 post-action-content-transition 如实 false →
  验证失败 → 连续失败有界停止（bounded-stop:max-consecutive-failures），
  滚动发现项保持未覆盖（不折叠、不伪装完成）。
- actual：PASS。
- evidence：同上文件（真机 d1/d3/d4 实录分支的确定性复刻）。

## 4. RootRoute 迁移与回归（Acceptance 5）

- method：`dotnet test UniClaw.Kernel.slnx`（全量）+
  `python3 tools/scenario_certify.py --check` + `git diff --check`。
- expected：全绿；场景 seal 不受影响（认证只哈希 Kernel/Agent 源，本 change
  仅 Host 源变更）。
- actual：1227/1227 通过；29/29 seal 一致；diff 检查干净。
- 迁移面：SettingsCoverageScenarioTests/ConfigTests/LedgerTests fixture 与
  常量（后者经 LoadDefault 与真实 profile 耦合——未迁移会真实失败，已验证）
  + .dsh/profiles/settings-coverage.yaml。
- evidence：git log（feat/fix(agt-010) commits）。

## 5. 真机复跑（Acceptance 6，如实 blocked）

- 环境：本会话无 Android 设备/adb（HostRunner 真实档不可用）。
- plans §3 主路径（settings-coverage-neg-c 复跑）与备路径（自建浅列表 app，
  立项属待裁决点）均未执行；解锁条件 = 真机可用。确定性背书见第 3 节。
