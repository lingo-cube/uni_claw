# 指令：按参考文档修复受限会话的工具面泄漏（AGT-002 B1）

> Status: `CANDIDATE`（未裁决的修复指令草案；结论须经 change 落权威目录）
> Authority: `NONE`（执行指令，不是架构决策；与 ADR / baseline 冲突时以后者为准）
> Date: 2026-09-26

## 0. 你的任务一句话

把 `uniagent-prod` 受限会话的"挡住 MCP 工具"实现，从当前的
**握手时动态 deny 补刀** 改成 **`restrict({ allow: [] })` + 工具注册进 agent scope**，
并给出机械证明。

**唯一参考文档**：`docs/analysis/dsh-tool-scope-restricted-sessions.md`

**先读它，再动手。** 里面的每条结论都带 `路径:行号` 或实测记录；遇到与你的直觉
冲突的地方，以文档为准（文档里标了哪个直觉是错的以及为什么）。

---

## 1. 你要修的是什么（现状与症状）

**症状**：宿主 DSH 上配了 MCP 服务器（csharper-mcp、cwm-roslyn-navigator），
它们的工具**泄漏进**了本该只看得见 `submit_decision` 的受限产品会话。

**实测证据**（参考文档 §5）：23 个 MCP 工具在场时，受限会话的可见目录是

```
actual=[mcp__csharper-mcp__apply_code_action, … 共 23 个 mcp__* …, submit_decision]
expected=[submit_decision]
```

**当前实现为什么错**（别只是"调参"，是语义错）：

1. `@uniclaw/dsh-uniagent-restrict` 用 `deny: [<boot 时刻枚举到的全局工具名>]`。
   DSH 定义 deny-only 过滤器**放行**未点名的、之后注册的继承工具
   （`docs/subsystems/tools.md:165`）→ MCP 晚注册 → 必然漏。
2. `restrict()` 在调用处校验名字，不认识就抛 `unknown global tool`
   （`packages/core/tools/src/index.ts:1114-1118`）→ 连"预先写上将来的名字"都做不到。
3. 于是 `uniclaw-decision-channel` 的握手逻辑里加了一段"发现多余工具就用留存的
   preset ctx 补一次 deny"（`state.restrictCtx`）→ 跟注册时序赛跑。

---

## 2. 目标形状（照抄即可）

```
preset scope（uniagent-prod standing scope）
├── @uniclaw/dsh-uniagent-restrict   → ctx.tools.restrict({ allow: [] })
└── @uniclaw/dsh-decision-channel    → ctx.tools.guard(…)   ← 第二道防线
                                      （不在此处注册 submit_decision）

agent scope（加入该 preset 的 Agent，是 preset scope 的子）
└── submit_decision                   ← 握手时由 decision-channel 注册
```

**三条硬约束**（每条都有实测支撑，违反任一即失败）：

| 约束 | 为什么 |
|---|---|
| 用 **allow** 形态，不用 deny 清单 | 只有 allow 排除"未点名的晚注册项" |
| 限制与工具**不同层**，工具在**更内层**（agent scope） | 限制只豁免**观察者自己的层**；同层时工具是"继承"来的，会被一起剥掉（实测目录变空） |
| 清单不能写成 `allow: ['submit_decision']` | `restrict()` 只认继承面名字，scope-local 名字抛 `unknown global tool` |

---

## 3. 具体改动清单

### 3.1 `dsh/uniclaw-restrict/src/index.js`

把 boot 快照 deny 换成：

```js
ctx.tools.restrict({ allow: [] })
```

删掉枚举 `ctx.tools.schemas()` 再 deny 的那段逻辑。保留 `inject = ['tools']`。

### 3.2 `dsh/uniclaw-decision-channel/src/index.js`

1. **把工具定义提前构造**成一个变量（`submitDecisionTool`），因为注册点变了。
2. **`sessionScoped` 分支**：不要再在此处 `ctx.tools.register(...)`（会被 preset 的
   `allow: []` 剥掉）。改为把定义发布到模块级 `state.sessionTool`，并注册 guard：

   ```js
   state.sessionTool = submitDecisionTool
   ctx.tools.guard(exec => exec.name === 'submit_decision'
     ? undefined
     : `uniagent-prod: tool "${exec.name}" is not in the frozen capability manifest [submit_decision]`)
   ```

3. **host 模式**：`ctx.tools.register(submitDecisionTool)`（保持原行为，供非 preset 会话）。
4. **握手时**（`controller.create` 成功后、目录校验前）：把工具注册进 **agent 自己的层**：

   ```js
   const agent = await resolveSessionAgent(ctx, dshSessionId)   // found.agent ?? found
   state.sessionToolDisposer = agent.ctx.tools.register(state.sessionTool)
   ```

   若 `agent.ctx.tools` 不可用 → fail closed（返回 `session-capability-binding-failed`）。
5. **删掉** `state.restrictCtx` 及其补刀分支 —— 目录校验改为**纯检查**，不做修补。
6. **detach** 时调 `state.sessionToolDisposer()` 并置 `null`（释放注册）。
7. **不要**把 `cwd` / workspace 当成可见性隔离手段（参考文档 §3 已证伪）；
   专用 workspace 可以留着当"不读写操作者仓库"的隔离，但注释要写对。

### 3.3 测试

- `dsh/uniclaw-decision-channel/tests/plugin.test.mjs`：mock 需要补
  `tools.guard` 和 `resolveAgent` 返回带 `agent.ctx.tools.register` 的对象；
  新增断言：**工具注册进了 agent scope**（不是 host scope）、detach 释放注册。
  注意：插件有模块级 `state`，测试间会串；需要时先调一次 detach 复位。
- 新增 `dsh/uniclaw-restrict/tests/restriction.test.mjs`：跑**真实 ToolRuntime**
  （加载 checkout 的 built libs），至少覆盖：
  1. `allow: []` in preset + 工具在 agent → boot 前后注册 MCP 工具，目录恒为 `[submit_decision]`；
  2. **回归**：旧 deny 快照机制下 MCP 工具确实泄漏（锁定"为什么不能回去"）；
  3. **回归**：同层（restrict 与工具都在 preset）→ 目录为空（锁定 §1.2 的坑）；
  4. guard 拒非清单调用、放行 `submit_decision`；
  5. 未加入 preset 的普通会话保留完整工具面。

---

## 4. 验收标准（四元组，缺一不可）

> 完成 = method / expected / actual / evidence 全部可判定，不接受"写完了"。

**M1 — 单元/机制层**

```bash
node --test dsh/uniclaw-decision-channel/tests/plugin.test.mjs
node --test dsh/uniclaw-restrict/tests/restriction.test.mjs
```

expected：全绿（参考实现为 12/12 与 5/5）。

**M2 — 真实实例端到端（决定性）**

1. 确认宿主 profile 里的插件是**当前源码**（`file:` 是快照拷贝，必须重装且**眼见为实**）：

   ```bash
   cd ~/.dsh/profiles/web
   pnpm remove @uniclaw/dsh-uniagent-restrict && pnpm add file:<abs>/dsh/uniclaw-restrict
   pnpm remove @uniclaw/dsh-decision-channel && pnpm add file:<abs>/dsh/uniclaw-decision-channel
   grep -n "allow: \[\]" node_modules/@uniclaw/dsh-uniagent-restrict/src/index.js   # 必须命中
   ```

2. 起一个独立实例（不要动所有者正在用的实例）：

   ```bash
   cd <dk-harness> && node apps/cli/lib/bin.js web --no-open --port <port>
   ```

3. 确认 **MCP 服务器真的连上并注册了工具**（否则这轮证明无意义）：

   ```bash
   curl -s "http://127.0.0.1:<port>/mcp-manager/api/servers"
   # 期望：status=connected，toolCount 合计 > 0（参考实测 8 + 15 = 23）
   ```

4. 发真实握手（cookie 认证，用 `~/.dsh/.credentials.yaml` 里
   `client-connection/browser-session` 的 secret 按 DSH 的
   `authority` 绑定规则自行签名；或直接用仓内 .NET e2e 测试夹具）。

   expected（日志三连 + 响应）：

   ```
   [uniclaw-decision-channel] submit_decision registered into the restricted agent scope
   [uniclaw-decision-channel] restricted session verified: visible tools = submit_decision
   [uniclaw-decision-channel] handshake accepted
   ```

   ```json
   {"accepted":true,"runtimeCapabilities":["submit_decision"],"runtimePreset":"uniagent-prod"}
   ```

   **关键**：这必须是在 MCP 工具已在场（step 3 已确认）的前提下拿到的。

**M3 — 反证（证明你的修复真的在起作用）**

临时把 restrict 换回旧 deny 快照机制，重装，同条件再握手：

expected：`session-capability-mismatch`，actual 里列出那 23 个 `mcp__*`。
**拿到这个反证后把源码改回修复版并重装。** 没有反证 = 没有证明"修复有效"，
只证明了"当前恰好通过"。

**M4 — 证据落盘**

把 M2/M3 的实际输出追加进 `evidence/`（沿用 `evidence/agt-002-*.md` 的格式），
并在 `changes/AGT-002/state.md` 记一行。**不要**把 cookie / secret 写进证据。

---

## 5. 明确不要做的事

- **不要**去外部问"DSH 有没有 per-preset MCP 排除机制"。参考文档 §1/§3 已给出
  代码级答案：没有也不需要，工具走全局层，preset scope 的 allow 列表就能挡。
- **不要**用 `deny` 补刀、握手枚举名字、或任何形式的"时序赛跑"。
- **不要**靠 `mcp-servers.json` 的 `cwd`、会话 `cwd`、专用 workspace 来隔离工具
  可见性（§3 已证伪）。
- **不要**把 `guard` 当作目录保证 —— 它不过滤目录，泄漏的工具仍在系统提示里。
- **不要**顺手清理无关代码。

---

## 6. 已知陷阱速查

| 陷阱 | 表现 | 处理 |
|---|---|---|
| pnpm `file:` 是快照拷贝 | 改了源码行为不变、日志骗人 | 每次改完**重装**，并 grep 安装目录确认 |
| pnpm 复用内容寻址快照 | `pnpm add file:` 不重新拷贝 | `pnpm remove` 再 `pnpm add`（必要时 `--force`） |
| 模块级 `state` 跨测试串 | 测试单跑过、连跑挂 | 测试前先 detach 复位 |
| preset 目录是死的 | `~/.dsh/.agent-presets/` 没人读 | preset 只靠 profile patch 声明行 |
| 作用域判别靠环境探测 | 沿链解析到宿主服务，判别失效 | 用挂载行 `config.sessionScoped` 显式声明 |
| 改 preset 组成不重启 | 还是旧组合 | 插件 boot 期挂载，必须重启实例 |

**顺带**：`dsh/agent-presets/uniagent-prod/agent.cordis.yml` 与 `preset.yml` 是
死路方案的残留（DSH 不读该目录）。真正的 preset 声明在
`~/.dsh/profiles/web/cordis.patch.yml` 的 `@deepseek-ai/dsh-agent-preset` 行。
要么删掉这两个文件，要么在文件头明确写"仅供参考，DSH 不读取"—— 否则下一个人
会以为改这里有用。

---

## 7. 交付时请回答

1. M1/M2/M3 的实际输出（M3 必须附反证）。
2. 你最终改动的文件清单与关键 diff。
3. 参考文档里有没有与你的实测**不符**的地方？如有，给出反例（这是唯一允许
   偏离文档的情形，且必须举证）。
