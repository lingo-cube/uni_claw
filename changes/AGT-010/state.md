# AGT-010 — Settings 路由指纹去撞名（RouteKey/ViewportDigest）与滚动到底解锁
lifecycle_state: closed · disposition: none · depth: decision-heavy · base: a4d3b391（AGT-009 线续作，基线 dfd141c4）

## Intent（WHAT/WHY）

plans/2026-10-02-settings-traversal-remaining-issues.md §2+§3：页面身份只有标题，
Security & privacy 页（标题 = "Settings"）与根页指纹相同 → 进入成功但
post-action-target-unique 判"没进去"（e1 step 20 实证）；滚动到底失败分支被
撞名+弹窗连锁堵死（AGT-009 已清弹窗侧）。本 change 把页面身份升级为多信号
RouteKey，并建立与身份独立的 ViewportDigest 视口摘要。

## Scope

- Host/Settings：DeriveRouteKey（标题×标题来源×up 按键×滚动容器多信号）、
  DeriveViewportDigest（独立视口摘要）、feed/trace 接线、RootRoute 配置
  语义迁移一次（测试 yaml + .dsh/profiles/settings-coverage.yaml）。
- Tests/Evidence：e1 语料（23 份实录 XML）离线区分度验证；撞名页可验证进入
  e2e；滚动到底（内容不变 → 验证失败 → 有界停止）确定性背书。

## Out of Scope

- 不改 Kernel（route claim 值对 Kernel/WorldModel 为不透明字符串，页面身份
  语义纯属 Host 职责）；不重建场景 seal（scenario 认证只哈希 Kernel/Agent 源）。
- 不做 AGT-011 范围（§4 Wi-Fi 证据接线、§5 滚动候选质量、§6 补测）。
- 不新增全局身份接口、不给 ViewportDigest 建 world claim/第二判断面。
- 真机复跑 e1（settings-coverage-neg-c）与备路径（自建浅列表 app）不在本
  change 自动执行：真机不可用如实报告；备路径立项是 plans §3 待裁决点。

## Decisions

1. RouteKey v1 字段以 e1 语料离线区分度分析为准（2026-10-02，23 份实录；
   过程含一次字段证伪，见 Decision 6）：
   `android.settings|rk1:<title>|src=<homepage_title|title>|up=<0|1>`。
   实证：撞名页与根页在 src/up 两信号全不同；根页 12 份采样（初始视口 2 +
   滚动后 10）key 全稳定；条目集不进 key（身份必须滚动不变）。
2. 无标题/不可解析 → 维持回退身份 `android.settings`（AGT-006 语义不变）。
3. ViewportDigest v1 = `vd1:<sha256[:8]>`（clickable text∥resource-id 短 id
   排序多重集）。实证：根页三种视口态 digest 成组稳定（344e1c95/dfdf5eae/
   050bf728），内容变则变。与 RouteKey 字段集不相交、不得互相替代（语料
   双向实证：同 key 不同 digest、同 digest 不同 key）。
4. 滚动内容变化的**判定权威不变**：仍是 Kernel post-action occurrence 集合
   比较（AGT-005 语义）。ViewportDigest 是带来源标签的可观测摘要
   （TraceEntry + 纯函数），供真机 trace、后续 OCR/Slow 来源与测试断言使用
   ——不建第二判断面、不进 world claim（§2「不预造全局身份接口」）。
5. RootRoute 迁移一次：新根页 key =
   `android.settings|rk1:Settings|src=homepage_title|up=0`；
   旧标题值不再作为完整身份。迁移面：测试 fixture yaml、LedgerTests 常量
   （其 Config 走 LoadDefault → 真实 profile）、.dsh/profiles/settings-coverage.yaml。
   Ledger/Director 中不与 config 耦合的任意 route fixture 保持原样（对账本
   为不透明字符串）。
6. 字段证伪记录（诚实留痕）：sc（滚动容器短 id 集合）曾入 key 草案，被
   语料证伪剔除——根页初始视口含建议条容器（`settings_homepage_container`）
   而滚动后消失（2 容器 → 1 容器），全集与文档序 first/last 选取均不满足
   「同页相邻观察稳定」。锚点信号 = 标题 + 标题来源 + up 按键。

## Acceptance

1. e1 语料离线区分度：撞名页（capture-7323abdbfd05）与根页（12 份采样）
   RouteKey 不同；同页相邻观察 key 一致；无标题页回退身份。
2. ViewportDigest：同视口重复观察 digest 一致；滚动后 digest 变化；与
   RouteKey 独立（同 key 不同 digest、同 digest 不同 key 均在语料中出现）。
3. 撞名 e2e：模拟世界中标题同为 "Settings" 的二级页（Security & privacy）
   进入验证通过（路由指纹变化可判），覆盖任务完成。
4. 滚动到底确定性背书：无可新内容的 swipe → 验证失败 → 连续失败有界停止
   （诚实 bounded stop，不盲重试）。
5. RootRoute 迁移后全量 Host/Kernel/Simulation 测试绿；scenario 认证不受
   影响（仅 Host 源变更）。
6. 真机复跑：环境不可用，如实报告为 blocked（非伪造 PASS）。

## Constraints

- RouteKey 与 ViewportDigest 字段集不得相交、不得互相替代。
- 身份/摘要均为纯函数（同 XML 同值），保留来源（XML）与观察周期关联。
- 不修改 Kernel/Agent 源（避免触发场景重认证与 L2 语义面）。

## Verification

```yaml
level: DETERMINISTIC + SCENARIO + ENVIRONMENT
method: >
  dotnet test tests/UniClaw.Host.Tests（SettingsRouteKeyTests 语料区分度 5 项
  + 撞名 e2e（FullCoverageMission 含撞名页断言）+ ScrollAtBottom 滚动到底
  有界停止 + 配置/账本迁移回归）；API 35 p26_pixel emulator 上执行
  DSH_TEST_PERCEPTION_LIVE=1 dotnet test tests/UniClaw.Host.Tests
  --filter FullyQualifiedName~SettingsCoverageLiveTests；dotnet test
  UniClaw.Kernel.slnx 全量；python3 tools/scenario_certify.py --check；
  git diff --check
expected: "撞名可判、同页稳定、滚动分支有界、配置迁移后全绿"
actual: >
  离线与场景验证保持全绿：全量 1227/1227 通过（Host 137 含新增 6 项），
  场景 29/29 seal 一致。真机复跑已在 API 35 p26_pixel emulator
  （emulator-5554，1080x2400）完成：dotnet build 0 errors；
  SettingsCoverageLiveTests 1/1 通过，共 25 步且 25 步 verified；
  Security & privacy 为 step 13，RouteAfter=
  android.settings|rk1:Settings|src=title|up=1，无 target-unique 失败；
  verified swipe 2 次；终局 BoundedStop，未覆盖项为 scroll-discovered-entry
  与 repeated-entry（无 obstacle consult）。回归全量测试 1237 通过，
  scenario_certify.py --check 为 29/29 PASS，git diff --check 通过。
  真机证据见 evidence/agt-010/live-rerun/run-20261002-155017-713/，
  结果四元组见 evidence/agt-010/live-rerun-result.md。
evidence: evidence/agt-010/routekey-offline-analysis.md; evidence/agt-010/live-rerun-result.md;
  evidence/agt-010/live-rerun/run-20261002-155017-713/coverage-steps.json;
  evidence/agt-010/live-rerun/run-20261002-155017-713/coverage-report.json;
  evidence/agt-010/live-rerun/run-20261002-155017-713/facts.json
```

## Status log

- 2026-10-02 · persisted · 由 owner 指令立项（"尝试修复 AGT-010"）；离线区分度分析完成（23 份实录 XML，字段冻结见 Decisions 1-3）。
- 2026-10-02 · verified · DeriveRouteKey（标题×来源×up；sc 字段被语料证伪剔除，Decision 6）+ DeriveViewportDigest（独立摘要，TraceEntry 接线）落地；RootRoute 迁移（测试 yaml/LedgerTests/LoadDefault profile）；撞名 e2e 与滚动到底确定性背书全绿（1227/1227）；真机复跑如实 blocked。
- 2026-10-02 · verified·live-rerun-prepared · 更正：本机有 adb + API35 AVD（此前「环境无设备」判断错误，已收回）；ENV 门控真机终考 harness 已写入 tests/UniClaw.Host.Tests/SettingsCoverageLiveTests.cs（neg-c 同源 + rk1 RootRoute，consult=本地指令跟随 double；编译验证被中止，由执行方预检）。执行与收尾指令固化于 evidence/agt-010/live-rerun-INSTRUCTIONS.md，移交下一 agent。
- 2026-10-02 · verified·live-rerun · emulator 真机链路通过：撞名 Security & privacy 可验证进入，列表底部 verified swipe 可达；终局为带未覆盖项的诚实 BoundedStop。证据与 e1 step 20/digest 55291AC… 对照已落盘，AGT-010 唯一悬空项关闭。
- 2026-10-02 · verified→closed · 验收全满足：撞名可判（RouteKey rk1 真机验证 Security & privacy 进入）、同页稳定、滚动到底分支有界（确定性+真机 verified swipe 可达、终局诚实 BoundedStop）；全量 1237 通过、场景 seal 一致、配置迁移完成。与 e1 step 20/digest 55291AC… 的对照证据已落盘。无未授权改动，关门。

## Gate disposition

验收全满足：撞名可判（RouteKey rk1 真机验证 Security & privacy 进入）、同页稳定、滚动到底分支有界（确定性+真机 verified swipe 可达、终局诚实 BoundedStop）；全量 1237 通过、场景 seal 一致、配置迁移完成。与 e1 step 20/digest 55291AC… 的对照证据已落盘。无未授权改动，关门。
