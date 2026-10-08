# PRF-004 — PRF-3：产品会话隔离探针

lifecycle_state: closed · disposition: none · depth: standard · base: working-tree

## Intent（WHAT/WHY）

设计文档切片 PRF-3（Q12 裁决）：机械证明（而非假设）uniagent-prod 产品会话
不携带开发 Harness prompt 面；产出 = evidence 归档 + 常驻回归测试双份。

## Scope

- 探针：preset 行 mount 时 `systemPrompt.assemble({})` 记录 inventory
  （section/context/tool 名单）；HOST 行只读路由
  `/api/uniclaw-agent/prompt-probe` + panelSnapshot 字段暴露。
- 顺带修复（探针发现）：PRF-003 的 section 注册自 `ctx.get().section()` 改为
  `ctx.inject(['systemPrompt'], …)` 作用域模式（server-context.ts 先例）。
- 常驻断言：`PromptProbeE2eTests`（env 门控，allowlist=
  {uniagent-prod:product-prompt}，当前为泄漏 sentinel）。
- live evidence 两轮（boot×2）归档 `evidence/PRF-004/`。

## Out of Scope

- 泄漏的部署级修复（D1 决策项，见下）。
- 会话侧 system message ground truth（D2）。
- 工具宣传面收敛（D3）。

## Decisions

1. 探针记名单不记正文（prompt 文本不出路由）。
2. 发现即修的仅限插件层（ctx.inject 模式）；部署级泄漏不在本片修。

## Acceptance

1. 探针 inventory 记录 + 路由暴露 + panel 字段（done，plugin 测试覆盖）。
2. live evidence 归档（done，两轮 boot）。
3. 常驻 sentinel（done；当前对现部署 RED = 泄漏在场的机械文档，无 env 时跳过）。
4. 「泄漏项为零或修复后复验」→ **泄漏在场且 preset 层不可修**（机械证据：
   6-7 个开发面 section + 18 工具 schema 进装配）→ 转所有者裁决 D1-D3，
   本 change 停在 implemented，不虚报 CLOSED。

## Verification

| level | method | expected | actual | evidence |
|---|---|---|---|---|
| DETERMINISTIC | plugin tests（node --test） | 含探针/section 用例全过 | PASS；29/29 | `dsh/uniclaw-decision-channel/tests/plugin.test.mjs` |
| ENVIRONMENT | live 探针（3082 专用实例，boot×2） | inventory 机械记录 | DONE；6-7 开发段+18 工具 schema+2 policy contexts 在场；product 段未见于插件作用域装配 | `evidence/PRF-004/2026-10-07-prompt-probe-live.md` |
| SCENARIO | sentinel E2E（env 门控） | 无 env 跳过、套件绿 | PASS（默认全解绿）；对产品 profile（3082）**GREEN**：sections=[] | `tests/UniClaw.Agent.Dsh.Tests/PromptProbeE2eTests.cs`；`evidence/PRF-004/` 附录 |
| ENVIRONMENT | 产品 profile live（handshake+consult E2E） | 链路通、静态纪律送达 | PASS（consult 真实 deepseek 往返 3s；handshake 绿） | 同上 |

## 裁决与处置（2026-10-08 所有者：D1 按推荐 (a)）

- **D1 → 已落地**：产品专用 profile `~/.dsh/profiles/uniclaw-product`
  （无开发面）+ `DSH_TEST_PROFILE` 支持；复测 sections=[]/contexts=[]/
  tools=1，sentinel GREEN，真实 consult E2E 通过（见 evidence 附录）。
- **D2 → per-turn 等价已验证**：scoped section 注册未进装配的根因
  （cordis 作用域拓扑）留作后续小项；产品 profile 已设
  `promptMount: per-turn`，静态纪律每轮送达，PRF-003 语义完整。
- **D3 → 随 D1a 自动解决**：产品 profile 无全局开发工具注册，prompt
  装配 tools=1（submit_decision），宣传面=可执行面。
- **遗留 follow-up（不阻塞）**：section 挂载根因查明后从 per-ton 切回
  section（token 收益）；方法与条件见 evidence 附录 §残留。

## Status log

- 2026-10-07 · UNDERSTAND → PERSIST · 探针面选定（SystemPrompt.assemble 公开
  + scope-filtered）；E2E env 门控先例确认。
- 2026-10-07 · IMPLEMENT · preset 探针 + 路由 + panel 字段 + sentinel + mock
  扩展（inject/systemPrompt.assemble）；部署坑：环境 DSH_PROFILE_DIR 指向
  desktop 导致 web 未更新（grep 验证后定向重部署）。
- 2026-10-07 · VERIFY（部分） · live 两轮取得机械证据：泄漏在场（evidence
  归档）；PRF-003 section 注册缺陷发现并修（ctx.inject 模式，29/29 绿）；
  「泄漏为零」验收不满足且 preset 层不可修 → 停 implemented，D1-D3 转所有者。
- 2026-10-08 · D1a IMPLEMENT → REVIEW → VERIFY → CLOSED · 产品 profile
  落地（开发面全关）+ DSH_TEST_PROFILE 支持；探针与挂载模式解耦并过滤
  空段；复测 sections=[]/tools=1，sentinel GREEN；真实 consult E2E 通过
  （per-turn 静态纪律送达）；全解 1468 绿。修复路上三个事实入档：CLI
  子命令=profile 名；Assert.True 消息参数急切求值坑；DSH_PROFILE_DIR
  环境残留导致部署跑偏（grep 验证拦截）。
