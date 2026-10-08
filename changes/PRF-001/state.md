# PRF-001 — PRF-1a：产品身份类型上移 UniClaw.Agent + ADR-0041

lifecycle_state: closed · disposition: none · depth: standard · base: working-tree

## Intent（WHAT/WHY）

落地设计 `docs/design/uniagent-profile-realization-and-management-v0.1.md`
切片 PRF-1a（/grill-with-docs 17 项裁决，Q17 拆分的纯机械半部）：把产品
身份类型从 DSH adapter 程序集上移到 UniAgent 产品程序集，使「UniAgent
Profile 是产品级定义、DSH 只是映射消费方」有编译层结构支撑；随 change 落
ADR-0041。本片**行为零变**（拆文件/双 loader 属 PRF-1b）。

## Scope

- 上移五个类型到 `src/UniClaw.Agent/Profile/`（namespace
  `UniClaw.Agent.Profile`）：`ProductProtocolVersions`、`CapabilityManifest`、
  `ProductCapabilities`、`ProductProfile`、`UniagentProdProfile`。
- transport/绑定类型留 `UniClaw.Agent.Dsh`（ProtocolStamp、Handshake*、
  DshServiceEndpoint、ModelConfiguration、UniagentProdConfiguration 等）。
- `UniClaw.Agent.Dsh.csproj` 增对 `UniClaw.Agent` 的 ProjectReference
  （单向；ADR-0008「Kernel 永不引用 Agent」不受影响）。
- 消费方（src 5 文件 + tests 5 文件）加 using 跟随。
- 落 `docs/adr/0041-uniagent-profile-product-definition-and-dsh-binding-separation.md`。
- 附带执法同步（源哈希/归属/闭包三测试是既有执法面，非范围蔓延）：
  scenario 20 块经 `tools/scenario_certify.py --change PRF-001 --all` 唯一
  合法路径重封；`src/UniClaw.Agent/README.md` 归属表 + ownership 测试映射
  增 `Profile/`；closure 测试期望闭包加 `UniClaw.Agent`。

## Out of Scope

- 不拆 profile/绑定文件、不动 loader、不增 modelRoles（PRF-1b）。
- 不改任何运行时行为、协议、schema、hash。
- 不动宿主 deployment（cordis.patch.yml）。
- 不处置并行会话的 yaml 中间态（见 Verification 附注）。

## Decisions

1. 上移目标 = `UniClaw.Agent/Profile/`（裁决 Q5；被拒：留 adapter、进 Kernel）。
2. ADR-0041 内容 = 设计文档 §0/§4 全部裁决 + 被拒项，含 Q7（不采业界
   tools[] 明细块=有意识偏离）与 Q9（revision 进程级钉扎 + revisit 触发）标注。
3. Agent 程序集 XML 文档强制：上移类型补齐公共成员注释（Agent.Dsh 原不
   强制，搬迁后按产品程序集纪律补齐，语义未变）。

## Acceptance

1. 五类型物理位于 `src/UniClaw.Agent/Profile/`，namespace `UniClaw.Agent.Profile`；
   `UniClaw.Agent.csproj` 无对 Agent.Dsh 的引用。
2. `UniClaw.Agent.Dsh.csproj` 引用 `UniClaw.Agent`；全解编译零错误、本片
   零新增警告。
3. 全解测试除 1 个已归因外部失败外全绿（行为零变可证）。
4. ADR-0041 存在于 `docs/adr/`（文件名 `\d{4}-` 合规）。

## Verification

| level | method | expected | actual | evidence |
|---|---|---|---|---|
| CONTRACT | 类型位置/命名空间/csproj 检查 | 五类型在 Profile/；Agent 无 Agent.Dsh 引用；Agent.Dsh→Agent 单向 | PASS（Profile/ 五文件；`grep -c Agent.Dsh UniClaw.Agent.csproj`=0） | `src/UniClaw.Agent/Profile/`；两 csproj |
| CONTRACT | ADR 文件名规则 | `\d{4}-` 前缀 | PASS | `docs/adr/0041-uniagent-profile-product-definition-and-dsh-binding-separation.md` |
| DETERMINISTIC | closure 测试 | 闭包={Agent,Agent.Dsh,Core,Kernel}，无禁用 transport 名 | PASS | `tests/UniClaw.Agent.Dsh.Tests/DecisionChannelClosureTests.cs` |
| DETERMINISTIC | scenario 认证重封 + 全套 | 28 文件 0 违规；Simulation 188/188 | PASS | `tools/scenario_certify.py --check`＝PASS(28,0)；UniClaw.Simulation.Tests 188/188 |
| DETERMINISTIC | ownership/docs 执法 | Profile/ 登记进维度 README；docs 头部合规 | PASS | Kernel.Tests 849/849（含 SourceReadmeOwnership、DocsMetadata） |
| SCENARIO | `dotnet test UniClaw.Kernel.slnx --no-restore` | 全解绿 | 1593 中 1592 绿；唯一失败 = `ObserverProjectionTests.Runtime_Configuration`（**外部**：并行会话把 `.dsh/profiles/uniagent-prod.yaml` baseUrl 改 3081（CAP-012 D9 注释自证），其测试断言 3080 未跟——测试注释自认「该断言追踪并行会话的 yaml 中间态，最终归属其 change」；本片未触碰 yaml/加载值） | 本表 + 全解输出 |

## Status log

- 2026-10-07 · UNDERSTAND → RESOLVE → PERSIST · 用法面核对闭合
  （src 5 + tests 5 文件，全在 Agent.Dsh 族内）；裁决全部来自设计文档
  grill 定稿，无新增未知。
- 2026-10-07 · IMPLEMENT → REVIEW → VERIFY → CLOSED · 五类型上移 +
  using 跟随 + csproj 引用；三执法面同步（scenario 重封 20 块
  --change PRF-001、README 归属 + ownership 映射、closure 期望）；ADR-0041
  落档；Agent 程序集 XML 文档补齐；全解 1592/1593 绿（1 失败归因并行会话）。
