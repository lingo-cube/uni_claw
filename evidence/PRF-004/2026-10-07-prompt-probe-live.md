# PRF-004 产品会话隔离探针 — live evidence

日期：2026-10-07 · 实例：dedicated test service 127.0.0.1:3082（dsh/test-service.sh，
dk-harness CLI + ~/.dsh/profiles/web 共享 profile）· 插件：@uniclaw/dsh-decision-channel
含 prompt manifest revision 1（PRF-003 后部署）

## 探针机制

uniagent-prod preset 行（sessionScoped）在 mount 时调用 `systemPrompt.assemble({})`
（scope-filtered dispatch），记录装配 inventory（section/context/tool 名单，不含正文），
经 HOST 行只读路由 `/api/uniclaw-agent/prompt-probe` 与 panel snapshot 暴露。
常驻断言：`tests/UniClaw.Agent.Dsh.Tests/PromptProbeE2eTests`（env 门控
UNICLAW_DSH_E2E_BASE；allowlist={uniagent-prod:product-prompt}，当前为泄漏 sentinel）。

## 观测结果（两次 boot，逐字摘自实例日志）

Boot 1（section 注册走 `ctx.get('systemPrompt').section()`）：

```text
[uniclaw-decision-channel] prompt probe recorded:
  sections=[harness:identity, deployment:persona-prefix, mcp-resource-servers,
            harness:source, app:web-surface, deployment:persona-suffix]
  contexts=[sandbox:policy, approval:policy] tools=18
```

Boot 2（注册改走 `ctx.inject(['systemPrompt'], …)` 作用域模式，server-context.ts 先例）：

```text
[uniclaw-decision-channel] prompt probe recorded:
  sections=[harness:identity, deployment:persona-prefix, mcp-resource-servers,
            ui:deliverable-file-references, harness:source, app:web-surface,
            deployment:persona-suffix]
  contexts=[sandbox:policy, approval:policy] tools=18
```

（`ui:deliverable-file-references` 仅在 Boot 2 出现——异步注册的段随时机漂移，
证明静态快照枚举本身也会低估。）

## 结论（机械证据，非推断）

1. **开发面 section 泄漏进产品 preset 作用域**：harness 身份、宿主 persona
   前后缀、MCP servers instructions、harness source、web app 面段（+异步出现的
   deliverable-file-references）。这些是 deployment 全局层注册，preset 子作用域
   无抑制机制（restrict 只作用于工具面）——**preset/restrict 层无法移除**，
   修复路径属部署形态/上游能力决策。
2. **附带面**：18 个工具 schema 进入 prompt 装配（产品会话可执行面被 restrict
   冻结为 1——prompt 宣传面 ≠ 可执行面，模型可能尝试不可用工具）；contexts
   含 sandbox/approval policy。
3. **PRF-003 的 product-prompt section 未出现在探针作用域装配中**（两种注册
   模式皆未出现）。未决根因：探针装配发生在插件自身作用域，与 agent 会话
   作用域的层拓扑关系未定；section 是否真正抵达会话需会话侧 ground truth
   （session log 的 system/message 事件，见后续项）验证。
4. 探针作用域 inventory ≠ 会话最终 system prompt；本 evidence 的准确表述是
   「preset 行插件作用域可见的装配面」——它足以证明 1（全局段无差别可达），
   不足以证明或否证 3 的会话侧效果。

## 后续项（转所有者裁决 / 后续 change）

- **D1 泄漏修复路径**：(a) 产品专用 DSH profile/实例（不挂开发插件行，config
  级，无上游改动，test-service.sh 加 profile 选项）；(b) 上游 DSH 增 scoped
  section 抑制能力；(c) 暂时接受并在 PRF-005 envelope 记录实际装配面。推荐 (a)。
- **D2 会话侧 ground truth**：用 session log system/message（request/header
  @persistenceReserved 注释确认 system prompt 落日志）验证 product-prompt
  section 是否抵达会话；D1 落地后在新 profile 下复测（inventory 会变）。
- **D3 工具宣传面**：18 schemas vs restrict 冻结的 1——是否需要在产品 profile
  收敛 prompt 内工具清单（DSH toolOrder/tools provider 的 scope 行为核实）。
- 过渡期可用的合法路径：`promptMount: 'per-turn'`（静态文本每轮随 turn 传输，
  PRF-003 语义完整，不依赖 section 装配）。

## 复现

```bash
DSH_PROFILE_DIR="$HOME/.dsh/profiles/web" bash dsh/deploy.sh
DSH_TEST_PORT=3082 dsh/test-service.sh          # 前台或后台
UNICLAW_DSH_E2E_BASE=http://127.0.0.1:3082/ \
  dotnet test tests/UniClaw.Agent.Dsh.Tests --filter PromptProbeE2eTests \
  --logger "console;verbosity=detailed"          # sentinel：当前 RED（泄漏在场）
# 实例日志行：grep "prompt probe recorded"
```

---

# 附录（2026-10-08）：D1a 落地结果

裁决：所有者选 D1(a)——产品专用 DSH profile。

## 产品 profile（~/.dsh/profiles/uniclaw-product）

- bundles：dsh-base + dsh-web-app（与 web 同基座）。
- 关闭面：`agent-instructions`（disabled）、`system-prompt`
  （includeHarnessIdentity/includeRuntimeContext=false，persona 前后缀清空）、
  `web-runtime`（surfaceContext=false）、`ui-deliverables`/`workspace-changes`
  （disabled）。
- 产品行：decision-channel（host+preset，**promptMount: per-turn**，见下）+
  restrict preset + 模型路由（deepseek-official/opencode-go/zai 三 choice）。
- 启动：`DSH_TEST_PORT=3082 DSH_TEST_PROFILE=uniclaw-product dsh/test-service.sh`
  （CLI 语法：子命令即 profile 名）。

## 复测结果（机械证据）

```text
prompt probe recorded: sections=[] contexts=[] tools=1
```

对比共享 profile 的 6-7 开发段 + 2 policy contexts + 18 工具 schema：
**全部消除**（harness:identity/source、persona、mcp instructions、web 面、
deliverable 引用、sandbox/approval contexts、工具宣传面 18→1）。
Sentinel（PromptProbeE2eTests）对产品 profile **GREEN**；对共享 web profile
保持 RED（泄漏在场文档）——同一条 sentinel 两种部署形态各有其义。
真实 consult E2E（deepseek 官方往返）通过：per-turn 静态纪律随每轮送达。

## 残留与后续

1. **scoped section 挂载根因未解**：ctx.inject 注册的
   uniagent-prod:product-prompt 在两种 profile 下都未进 preset 作用域装配
   （cordis 作用域拓扑问题）。过渡方案 per-turn 已 live 验证等价送达
   （PRF-003 语义完整）；根因查明并切回 section 属后续小项（token 收益）。
2. persona/mcp 段在装配名单仍出现但为空文本——探针已按「渲染期丢弃空段」
   过滤，名单=模型实际可见。
3. owner 的 web profile（开发实例）插件快照同步更新为带探针版本（deploy.sh
   例行）；其行为不受影响（默认 section 模式 + 探针只读）。
