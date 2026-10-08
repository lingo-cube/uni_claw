# CAP-013 — UniAgent 选择 Runtime Integration 能力与任务级绑定

lifecycle_state: closed · disposition: none · depth: decision-heavy · base: working-tree

## Intent（WHAT/WHY）

收口 CAP-012 D5 的前置边界：语言检查不是 Host/runner 看到配置就自动开启的
开关，而是 Runtime Integration 能力集中的可选能力。能力可以由任务直接写死，
也可以由 `uni-agent` 根据任务要求选择；Host 校验并创建 task-scoped binding，
runner 只消费已选实例。

## Scope

- 任务 profile 声明能力要求与参数：`required`、`expectedLanguage`、精确
  `ignoreRoutes`；可选地直接写死 capability id；不新增 Host `enabled` 语义。
- 能力集通过既有 `capability-profiles.json` / `capability-facts.json` 向
  `uni-agent` 提供发现事实；首次 Agent 请求同时携带当前可用 profile map。
- 定义 agent 选择 → Host 校验/绑定 → runner 消费的最小流程和安全停止语义。
- 在绑定输入确定后，再完成 CAP-012 D5 的每 capture post-commit 文本投影与
  findings 落盘接线。

## Out of Scope

- 不让 runner 直接从 Registry 代选或隐式启用语言检查。
- 不把语言 Finding 升格为 Assurance、Effect 或 Product truth。
- 不读取设备 locale，不新增语言检查专用 Tool，不改全局 Registry 的 Resolve
  语义。
- 不在本 change 猜造尚未验证的 agent→Host wire payload；先用最小 binding
  seam 验证真实买方。

## Decisions

1. 能力注册、能力发现、任务直接选择或 agent 选择、任务 binding、runner 消费是
   不同动作；任务直接写死时优先于 agent 选择。
2. 必选要求没有 binding 时安全停下；可选要求未选择时不运行、不生成空产物。
3. `text`、`content-desc`、`hint` 各自作为 Declared Text；Rendered Text 只有
   真实渲染/OCR 依据才能填入。
4. 精确 route 命中产出 `NotApplicable`；无文字/不完整输入产出 `Unknown`；
   检查器或写文件故障隔离为诊断，主 Runtime 继续。

## Acceptance

1. ADR-0040 与 `CONTEXT.md` 对能力集、agent 选择和 binding owner 的词义一致。
2. 任务配置缺失时不会自动启用语言检查；必选未绑定能明确安全停止。
3. agent 返回的 capability id/参数经 Host 校验后才能进入 runner；runner 不
   调用全局 `Resolve()` 代选。
4. 绑定输入冻结后，CAP-012 D5 的 runner 接线具有独立 deterministic 测试和
   findings 产物证据。

## Constraints

- 继承 ADR-0035、ADR-0038、ADR-0040 与 CAP-005 的注册域/任务生命周期边界。
- 继续保持 Finding 非权威；不改变 Kernel authority path。

## Verification

| level | method | expected | actual | evidence |
|---|---|---|---|---|
| CONTRACT | 对照 ADR-0040、CONTEXT.md、CAP-005 边界 | 任务直选/agent 选择、Host binding、runner 消费三段职责明确 | PASS；`LanguageInspectionTaskRequest`、`LanguageInspectionSelection`、`LanguageInspectionBinding` 三段已分开，runner 不再自行 Resolve | `docs/adr/0040-agent-selected-task-capability-binding.md`; `CONTEXT.md`; `src/UniClaw.Host/Capability/LanguageInspectionBinding.cs` |
| DETERMINISTIC | Host binding focused tests | 未请求不加载；可选未选不运行；必选未绑定 fail-closed；任务直选优先；agent 参数不匹配拒绝 | PASS 7/7 | `tests/UniClaw.Host.Tests/LanguageInspectionBindingTests.cs` |
| DETERMINISTIC | post-commit + Settings Coverage scenario tests | 每 capture 只产一个 finding；声明文本投影、Partial/Missing/ignore route 语义、重复 capture、facts 文件引用可复验 | PASS；Host.Tests 197/197 | `tests/UniClaw.Host.Tests/LanguageInspectionPostCommitTests.cs`; `tests/UniClaw.Host.Tests/SettingsCoverageScenarioTests.cs` |
| DETERMINISTIC | DSH task-initialization selection tests | 首个任务初始化可带选择；adapter 只取一次；后续重复选择 fail-closed；plugin response/schema 一致 | PASS；Agent.Dsh.Tests 158/158，plugin 22/22 | `tests/UniClaw.Agent.Dsh.Tests/DshOpenedHttpPeerTests.cs`; `tests/UniClaw.Agent.Dsh.Tests/ProtocolFoundationTests.cs`; `dsh/uniclaw-decision-channel/tests/plugin.test.mjs` |
| DETERMINISTIC | full solution | 0 failures，既有能力/运行时/DSH 回归不受影响 | PASS 1440/1440 | `dotnet test UniClaw.Kernel.slnx --no-restore` |

当前已验证的是 Host 内部的 binding seam、任务写死时的真实 runner 接线，以及 DSH
任务初始化中的一次性 `capabilitySelection` 传输。能力选择和动作决策仍是任务
envelope 内的两个层次；后续任务再带选择会被拒绝。首次 Agent 请求的能力 profile
上下文投影已由 CAP-015 收口，后续请求不重复注入。

## Status log

- 2026-10-06 · UNDERSTAND → RESOLVE → PERSIST · `grill-with-docs` 收口：能力集供
  `uni-agent` 选择；任务只给要求/参数；Host 负责 task-scoped binding；runner
  不得自行启用；binding wire shape 留在本 change 的实现门。
- 2026-10-06 · IMPLEMENT · 落地最小 binding seam：任务可固定
  `runtime.language-inspector`，或携带与任务参数完全匹配的 agent selection；
  必选未绑定安全停止，可选未选不产物。Host/Settings Coverage 在 Kernel
  observation commit 后投影 `text`/`content_desc`/`hint`，写出
  `language-findings.json`，facts 只引用该文件。
- 2026-10-06 · IMPLEMENT · 初版把选择错误地放在第一次
  `submit_decision` response 顶层；按所有者修正，新入口移到通用
  `AgentTaskEnvelope.task.initialization`，计划和指令共用，Host 在 payload 进入
  Kernel 前形成 task-scoped binding。协议 schema、DSH plugin、adapter、Host
  runner 均已接线。
- 2026-10-06 · REVIEW → VERIFY · Host.Tests 197/197、Agent.Dsh.Tests 158/158、
  plugin node tests 22/22、全方案 1440/1440；既有 NU1900 漏洞缓存权限警告仍
  存在。首次 Agent 请求的能力集投影转由 CAP-015 接续收口。
- 2026-10-07 · VERIFY → CLOSED · CAP-015 已完成首次 Agent 请求的能力 profile
  注入；CAP-013 的 binding、任务初始化和 runner 消费链无剩余实现项。
