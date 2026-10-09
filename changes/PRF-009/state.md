# PRF-009 — Product Skill 领域指导装配与 Session 快照

lifecycle_state: closed · disposition: none · depth: decision-heavy · base: working-tree

## Intent（WHAT/WHY）

为 UniAgent 增加首个领域级 Product Skill：`android-automotive-ui-testing`。
Skill 负责车机 Android UI 自动化测试的观察、规划、受控执行、验证、证据和安全
停止指导；现有 Task Profile → `ExecutionContract` → immutable
`ExecutionContractView` 链路继续是任务契约唯一来源。

本 Change 解决 Skill 如何进入 Product Runtime、如何由 Profile allowlist 管理、
如何在 Session/Run 初始化时固定并审计的问题，不新增第二套 Task Contract、
Kernel 规划模型或 Agent transport 字段。

## Scope

- 新增 `product/skills/android-automotive-ui-testing/manifest.yaml` 与
  `guidance.md`，采用领域级而非 Settings/车型/OEM/控件级粒度。
- 扩展 Product Profile 的 `allowedSkillRefs`，引用 Skill 的 name、revision、
  path 与 sha256，并由 Profile loader fail-closed 校验。
- 在 Product Prompt/DSH session assembly 中挂载经过校验的 Skill guidance
  scoped static section；保持每轮只追加动态 `AgentDecisionContext`。
- 在现有 `InitializationEnvelope` 中记录 Skill name、revision、sha256，遵循
  first-run-wins 与 Session/Run snapshot 语义。
- 补充 deterministic contract tests、prompt isolation/hash tests 和 envelope
  tests；同步必要的 profile/schema/hash 生成物与文档。

## Out of Scope

- 不修改 `ExecutionContract`、`ExecutionContractView`、`AgentDecisionContext`、
  `AgentDecision` union 或 `AgentTaskInitialization`。
- 不把 Skill 选择塞进当前一次性 `capabilitySelection`；该字段继续只服务
  Runtime Integration capability binding。
- 不实现 Memory System、MemoryRecall schema 或 Strategy Memory storage。
- 不迁移、删除或重定义现有 `product/tasks/*.yaml`；不创建第二套 Task Catalog。
- 不新增 Task→Skill resolver、运行中热切换、provider/model/endpoint 字段或
  Effect authority。
- 不按具体 Settings 页面、Wi-Fi、车型、OEM、固定 AVD 或测试用例拆 Skill。

## Decisions

1. **唯一任务契约链**：现有 Task Profile 由 Host loader 编译为
   `ExecutionContract`，Kernel admission 产生 immutable `ExecutionContractView`；
   Product Skill 只提供规划与执行指导。
2. **Skill 领域粒度**：首个 Skill 名为 `android-automotive-ui-testing`，覆盖
   typed UI observation、grounding、bounded exploration/planning、受控 Effect、
   post-action verification、evidence 以及安全 stop/replan。
3. **接入位置**：Skill artifact 由 Profile allowlist 管理，在 Session/Run 初始化
   时解析、校验、固定，并作为 Product Prompt scoped static section 挂载。
4. **运行时边界**：每轮输入仍是静态 Product Prompt + Skill guidance + Kernel
   投影的 `AgentDecisionContext` + 可选 Memory Recall + screenshot；Skill 文本
   不进入 Kernel context 的 canonical state。
5. **审计**：`InitializationEnvelope` 记录 skill name/revision/sha256；Session
   内 first-run-wins，运行中不热更新。
6. **协议保持封闭**：`AgentTaskInitialization` 继续只承载一次
   Runtime Integration capability selection；未来若出现 task-scoped Skill
   buyer，另立协议 change。

## Owner / Authority impact

- Product Profile/Skill artifact：Product owner，revision/hash 由 Profile loader
  校验。
- Product Prompt assembly：Product realization/DSH adapter，只负责挂载和传输。
- Task Contract：现有 Task Profile/Host loader 与 Kernel Run Model，Skill 不拥有。
- Session snapshot：Runtime/Host initialization envelope，Skill 只提供被审计的引用。
- Memory、Evidence、Effect、Run State：保持现有 owner，不因 Skill 改变。

## Acceptance

1. `android-automotive-ui-testing` Skill artifact 可被发现，manifest 与 guidance
   hash 可重算，领域范围不含具体页面/车型/OEM/坐标。
2. Product Profile 的 `allowedSkillRefs` 对未知 name、重复 name、路径越界、
   revision 非法或 sha256 不匹配 fail-closed。
3. 合法 Skill guidance 只作为 Product Prompt scoped section 挂载一次；开发
   Harness prompt/skill 不泄漏；每轮动态 context 仍由 Kernel 生成。
4. Session 首个 Run 的 `initialization.json` 记录 Skill name/revision/sha256；
   同 Session 后续 Run 不覆盖该快照。
5. 现有 Agent protocol schema/hash、Task Profile→ExecutionContract 链、
   `capabilitySelection` 一次性语义和完整测试保持通过。

## Verification

```yaml
level: CONTRACT
method: >-
  Profile/Skill loader tests、prompt assembly isolation/hash tests、
  InitializationEnvelope first-run-wins tests、现有 Agent/Host/Kernel 全解
expected: >-
  Skill 引用与 guidance 装配可重建且 fail-closed；协议和现有任务契约不漂移；
  envelope 能还原首个 Session 装配
actual: >-
  Skill artifact、Profile allowlist、DSH static section、per-turn fallback 和
  InitializationEnvelope 快照均按 acceptance 通过；场景 source hash 已重新封存，
  focused tests 与全解均全绿。
evidence: >-
  `dotnet test tests/UniClaw.Agent.Tests/UniClaw.Agent.Tests.csproj --no-restore`
  (29/29); `dotnet test tests/UniClaw.Host.Dsh.Tests/UniClaw.Host.Dsh.Tests.csproj
  --no-restore` (33/33); `node --test --test-force-exit
  dsh/uniclaw-decision-channel/tests/plugin.test.mjs` (32/32); `node --check`;
  `dotnet test UniClaw.Kernel.slnx --no-restore` (all suites green: Kernel 849,
  Simulation 188, Host 200, Agent.Dsh 159, Agent 29, Host.Dsh 33, Core 14,
  FileSystemRealization 9); `python3 tools/scenario_certify.py --change PRF-009
  --all --check` (28 files, 0 violations);
  `git diff --check`。
```

## Status log

- 2026-10-10 · GRILL → PERSIST · 所有者确认 Q31–Q34：Skill 作为静态 Product
  Prompt section 装配；Session/Run 固定单一 Skill；envelope 记录 name/revision/hash；
  不修改 ExecutionContract、AgentDecisionContext 或 AgentTaskInitialization。
- 2026-10-10 · PERSIST → PLAN · 采纳 `plans/2026-10-10-prf-009-product-skill-assembly.md`；
  派发 `WI-PRF009-001`（Skill artifact/Profile allowlist）与
  `WI-PRF009-002`（DSH section/envelope），前者为后者的 artifact 依赖。
- 2026-10-10 · PLAN → IMPLEMENT · `WI-PRF009-001` 已派发；完成后再启动依赖的
  `WI-PRF009-002`，两者均按 scope 隔离文件写入。
- 2026-10-10 · IMPLEMENT → VERIFY · 两个 WorkItem 完成：Profile/Skill loader、
  DSH scoped section/per-turn fallback、包副本 hash、InitializationEnvelope
  snapshot 与 deterministic tests 已落地；未改 Kernel、Agent protocol 或 Task
  contract。
- 2026-10-10 · VERIFY · Agent.Tests 29/29、Host.Dsh.Tests 33/33、DSH plugin
  32/32、`node --check`、`git diff --check` 与
  `scenario_certify --change PRF-009 --all --check` 通过。首次全解发现 20 个场景
  源哈希需在最新 Profile 源码后重封，按唯一认证脚本重封后复跑全解 849 Kernel、
  188 Simulation、200 Host、
  159 Agent.Dsh、29 Agent、33 Host.Dsh、14 Core、9 FileSystemRealization，
  全部通过；NU1900 仅为本地 NuGet 漏洞缓存权限警告。
- 2026-10-10 · VERIFY → CLOSED · acceptance、全解、场景认证、diff 审阅均通过；
  PRF-009 进入 closed。工作树中既有车机 gap 调研文档仅补齐仓库要求的
  `Status: DRAFT` / `Authority: NONE` 元数据，正文未纳入本 Change 提交。
