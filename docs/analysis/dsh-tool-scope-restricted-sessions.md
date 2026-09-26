# DSH 工具作用域与受限会话参考（agt-002-b1）

> Status: `COMPLETE`（调查产物：DSH 工具可见性一手语义与受限会话正确形状）
> Authority: `NONE`（解释性参考，不是架构决策；与 ADR / baseline 冲突时以后者为准）
> Date: 2026-09-26
> 受控对象：AGT-002 review B1 —— 真实模型会话必须在受限 `uniagent-prod`
> preset 下运行，实际可见工具目录 == 冻结清单 `[submit_decision]`（机械证明）
> DSH 版本：0.1.7-rc.2（checkout HEAD `477b4f4205`）

本文只讲 **DSH 工具可见性的真实语义** 与 **本项目受限会话的正确实现形状**。
所有结论带一手引用：DSH 侧路径相对 `dk-harness` 仓库根，uni_claw 侧相对本仓
仓库根。标注 **[已核实]** = 读源码 / 官方文档 / 真实实例确认；
**[推断]** = 基于已核实事实的分析。

---

## 0. TL;DR（先读这 6 条）

1. **`deny` 列表挡不住晚注册的工具。** DSH 明确定义：deny-only 过滤器**放行**
   它没点名的、之后注册的继承工具。MCP 服务器是 boot 后异步注册的，所以
   **任何开机时刻的 deny 快照必然漏** —— 这不是时序没抓好，是语义如此。
2. **`restrict()` 在调用时就校验名字**，不认识的名字抛
   `unknown global tool`。所以 deny 快照连"预先写上将来的名字"都做不到。
3. **正路是 `restrict({ allow: [] })`**：allow 列表排除一切它没点名的继承项。
   空 allow = "不继承任何东西"，单调有效、与注册顺序无关。
4. **但 restrict 与工具不能同层。** 限制只豁免**观察者 scope 自己的层**；
   restrict 和工具同处 preset scope 时，`allow: []` 会把工具一起干掉。
5. **正确形状**：`allow: []` 放 **preset scope**；`submit_decision` 注册进
   **agent 子 scope**（`agent.ctx.tools.register`）。
6. `guard` 是执行期拒绝、**不过滤目录**，只能当第二道防线，不能替代 allow。

---

## 1. 语义基础：restriction 到底过滤什么

### 1.1 过滤面 = 继承面（全局层 + 全部祖先 scope 层）

`view()` 每次调用都重新读取活的层表，对**继承来的**每个名字算
`layers.every(layer => layer.admits(name))`：

> `packages/core/tools/src/index.ts:1187-1201`
> ```ts
> const inherited = new Map<string, ToolDefinition>(this.layers.global.tools.entries())
> for (const layer of layers) { if (layer === own) continue; /* ... */ }
> for (const [name, definition] of inherited) {
>   knownNames.add(name); restrictableNames.add(name)
>   if (layers.every(layer => layer.admits(name))) visible.set(name, definition)
> }
> ```

官方文档同义（这是权威表述，源码里 `ToolRestriction` 的 JSDoc 反而**过时**，
仍写着 "Global tool names"）：

> `docs/subsystems/tools.md:165`
> > `ToolRestriction` applies to the tools a scope inherits: the deployment-global
> > layer plus every ancestor scope on its chain. … **A deny-only filter admits
> > later unlisted inherited tools, while an allow-list excludes them.**

**[已核实]** 三条推论：

- 过滤是**活的**（每次 `view()` 重算，`admits()` 遍历活的 restrictions 迭代器
  `index.ts:757-764`），所以在限制**之后**注册的工具同样受约束。
- 继承面**包含全局层**（`index.ts:1187` 用 global 播种 `inherited`），
  所以 MCP 工具（注册进全局层，见 §3）会被 preset scope 的 restrict 挡住。
- **deny 放行未点名的晚注册项**；allow 排除之。

### 1.2 豁免面 = 观察者自己拥有的层（**不是**"全局层"）

> `packages/core/tools/src/index.ts:1202-1209`
> ```ts
> // The scope's own registrations last, shadowing an inherited name and
> // outside the filter above.
> if (own !== undefined) { for (const [name, definition] of own.tools.entries()) { /* ... */ } }
> ```

这条的设计意图是：委派运行时把子 agent 的结构化输出工具注册进**子 agent 自己的层**，
一个"允许用哪些能力"的过滤器不能把子 agent 赖以作答的机制本身剥掉。

**关键坑（本项曾被误判）**：豁免的是 **观察者（viewing scope）自己的层**，不是
"注册所在的层"。当 restrict 与工具**同处一个 scope** 时，该工具对观察者而言是
**继承**来的（经祖先链），照常被过滤。

实测（真实 `ToolRuntime`，见 §5 用例 3）：

| 形状 | 受限 agent 可见 |
|---|---|
| `submit_decision` 与 `allow: []` **同处 preset scope** | `[]` ← 工具被干掉 |
| `submit_decision` 在 **agent scope**，`allow: []` 在 preset scope | `["submit_decision"]` ← 正确 |
| `submit_decision` 在 preset scope，`allow: []` 在 **agent scope** | `[]` ← 工具是继承来的，被干掉 |

### 1.3 `restrict()` 的校验语义

> `packages/core/tools/src/index.ts:1097-1124`

- 必须**有 scope**：无 scope 抛
  `tools.restrict() requires a scoped context (agent.ctx)`。
- `allow`/`deny` 全缺省 → 抛 no-op 错。**`allow: []` 与 `deny: []` 都被接受**
  （[已核实]，空数组不是被拒的空配置 bug）。
- 名字对 **`restrictableNames`（= 继承面名字）** 校验，不认识即抛
  `unknown global tool`，所以**scope 内工具名不能写进 allow/deny**。
- 不能点名保留传输名 `run_code`。
- 多次 restrict **取交集**并累加；每次返回各自的 disposer。

---

## 2. guard：执行期单调拒绝（第二道防线）

> `packages/core/tools/src/index.ts:723-731, 1136-1142, 1144-1154`

```ts
export type ToolGuard = (execution: Readonly<ToolExecution>) => string | undefined
```

- 返回**字符串 = 拒绝理由**（该字符串作为错误面呈现）；返回 `undefined` = 放行。
- **没有 allow 语义**：任何 guard 都不能把另一个 guard 的拒绝翻回许可
  （监听顺序无法提权）。
- **不要求 scope**（与 `restrict` 不同，`guard()` 体内没有 `scopeOf` 前置断言）：
  plain ctx → 全局 guard；经 `agent.ctx` → 只作用于该 agent。
- 在**执行期**按活的层迭代求值，**与工具注册先后无关**。
- **不过滤目录**：`schemas()` / `wireSchemas()` 只读 `view().visible`，从不读
  `guards`。所以泄漏的工具**仍然出现在目录与系统提示里**，只是调用被拒。

**结论**：guard 是纵深防御，不能拿来满足"模型看不见"的要求。

---

## 3. MCP 工具注册在哪一层

- `mcp-client` 用自己的 ctx 注册，**无 scope 参数**：
  `packages/mcp/mcp-client/src/index.ts:181` → `connection.ts:172` →
  `tools.ts:150 ctx.tools.register(definition)`。层由**挂载位置**决定
  （`scopeOf(this.ctx)`）。
- 工具公开名：`mcp__<serverName>__<rawName>`（超长则截断加 sha256 前缀）。
- `apply()` 是 async，等 `connection.ready`（初次连接 + `tools/list`），
  并在收到 list-changed 通知后重同步 → **注册确实发生在 boot 之后**。
- 本项目 `dsh-mcp-manager`（**第三方插件**，非 DSH core，
  github.com/Nichts0v0/dsh-mcp-manager）在 host profile 层挂载，读
  `$DSH_HOME/mcp-servers.json`，因此其工具落在**全局层**。

### `mcp-servers.json` 的 `cwd` / `--workspace-from-cwd` 管什么

> `packages/mcp/mcp-client/src/index.ts:119-142`（Config）、`connection.ts`（构造 transport）

- `cwd`：**spawn 出来的 MCP 服务器子进程的工作目录**。
- `--workspace-from-cwd`：只是**传给该服务器自己的参数**（本项目 csharper-mcp
  用它来定位 solution）。
- 两者**都与工具可见性无关**。因此"给会话换专属空 cwd"挡不住工具泄漏
  —— 这条曾经的假设已被证伪。

---

## 4. 本项目的正确实现形状

### 4.1 组成

```
preset scope（uniagent-prod standing scope）
├── @uniclaw/dsh-uniagent-restrict      → ctx.tools.restrict({ allow: [] })
└── @uniclaw/dsh-decision-channel       → guard（第二道防线）
                                        → 不在此处注册 submit_decision

agent scope（加入了该 preset 的 Agent，是 preset scope 的子）
└── submit_decision                      ← 由 decision-channel 在握手时注册
```

### 4.2 为什么工具在 agent scope

preset 的 `plugins:` 列表里所有行**共享同一个 preset scope**：
`mountPreset()` 建 `new PresetTree(ctx)`（`mount.ts:261`）并在该 ctx 下
`tree.root.update(plugins)`；`PresetTree extends EntryTree`（`mount.ts:9`）
且**不为行创建新 scope** —— Cordis Loader 的行是 `ctx.plugin()` 的子 fiber，
不是新 scope。mount 记录 `key: scopeOf(ctx)`（`mount.ts:268`）。
Agent 通过 `bindScopeParent(key, generation.key)` 成为其子
（`agent-preset-registry/src/index.ts:242`）。

因此：若把 `submit_decision` 注册在 preset scope，它**就是** preset scope 自己
的注册，而 `allow: []` 也在同一层 → 对观察者（agent）而言它是继承项 → 被剥掉。

注册进 agent 自己的层则真正豁免。

### 4.3 为什么不写 `allow: ['submit_decision']`

因为 `restrict()` 只接受**继承面**的名字，scope-local 名字会抛
`unknown global tool`。所以清单用"空 allow + 子层豁免"表达，而不是正向点名。

### 4.4 手写代码

```js
// preset scope（@uniclaw/dsh-uniagent-restrict）
ctx.tools.restrict({ allow: [] })

// preset scope（@uniclaw/dsh-decision-channel，第二道防线）
ctx.tools.guard(exec => exec.name === 'submit_decision'
  ? undefined
  : `uniagent-prod: tool "${exec.name}" is not in the frozen capability manifest [submit_decision]`)

// 握手时，注册进 agent 自己的层
const agent = await resolveSessionAgent(ctx, dshSessionId)
state.sessionToolDisposer = agent.ctx.tools.register(submitDecisionTool)
```

握手随后**机械校验**实际目录并 fail closed：

```js
const catalog = ctx.tools.schemas(agent).map(s => s.name).sort()
if (catalog.join('\n') !== ['submit_decision'].join('\n')) → session-capability-mismatch
```

detach 时调 `sessionToolDisposer()` 释放注册。

---

## 5. 已验证行为（真实 `ToolRuntime`）

`dsh/uniclaw-restrict/tests/restriction.test.mjs`（`node --test`，加载 checkout
的 built libs，跑真实 scope 链）：

| # | 用例 | 期望 | 结果 |
|---|---|---|---|
| 1 | `allow: []` in preset + 工具在 agent；boot 后注册 MCP 工具 | `[submit_decision]` | PASS |
| 2 | 旧机制 `deny:[boot 快照]`，之后注册 MCP 工具 | MCP 工具**泄漏** | PASS（锁定回归） |
| 3 | `allow: []` 与工具**同处 preset scope** | `[]`（文档化坑） | PASS |
| 4 | agent guard 拒非清单调用、放行 `submit_decision` | 拒绝/放行正确 | PASS |
| 5 | 未加入 preset 的普通会话 | 保留完整工具面 | PASS |

`dsh/uniclaw-decision-channel/tests/plugin.test.mjs`：12/12 PASS，其中 3 条覆盖
B1（preset 创建、**agent-scope 注册**、detach 释放）。

### 真实实例端到端（决定性证据）

同一 live 实例上，两个 MCP 服务器均 `connected`：
`csharper-mcp` 8 个工具 + `cwm-roslyn-navigator` 15 个 = **23 个 MCP 工具在场**。

**旧机制**（`deny` 快照）：

```
[OLD-MECHANISM] denied boot-time snapshot: 2 entries        ← 只有 usage_*
→ handshake 200:
  {"ok":false,"error":{"code":"session-capability-mismatch",
   "message":"actual=[mcp__csharper-mcp__apply_code_action, … 共 23 个 mcp__* …, submit_decision]
              expected=[submit_decision]"}}
```

**修复后**（同条件、同 23 个 MCP 工具）：

```
[uniclaw-decision-channel] submit_decision registered into the restricted agent scope
[uniclaw-decision-channel] restricted session verified: visible tools = submit_decision
[uniclaw-decision-channel] handshake accepted
```

```
{"accepted":true, "runtimeCapabilities":["submit_decision"], "runtimePreset":"uniagent-prod",
 "dshSessionId":"session-4c2c9803-…"}
```

---

## 6. 操作纪律（踩过的坑，必须遵守）

1. **`file:` 依赖是快照拷贝，不是软链。** 改完源码**必须**重装，否则跑的还是旧代码，
   而且日志会骗人。且 pnpm 会**复用内容寻址快照**，`pnpm add file:` 可能不重新拷贝：

   ```bash
   cd ~/.dsh/profiles/web
   pnpm remove @uniclaw/dsh-uniagent-restrict
   pnpm add file:/abs/path/to/dsh/uniclaw-restrict
   grep -n "allow: \[\]" node_modules/@uniclaw/dsh-uniagent-restrict/src/index.js   # 必须眼见为实
   ```
2. **preset 目录 `~/.dsh/.agent-presets/<id>/` 是死路**（旧格式，DSH 不读）。
   preset 只能靠 profile patch 的声明行注册。
3. **作用域判别不能靠环境探测**：preset 挂载的插件里 `ctx.connection` 会沿作用域链
   解析到宿主服务。判别要用挂载行的 `config.sessionScoped` 显式声明。
4. **不要再用 deny 补刀 / 握手时枚举名字**：那是跟注册时序赛跑，且打字面上就漏。
5. **改动 preset 组成后要重启实例**：插件在 boot 期挂载。

---

## 7. 给审查者的判定清单

一个受限会话实现**合规**当且仅当：

- [ ] 限制用 **allow** 形态（或等价的"默认不继承"），**不是** deny 清单；
- [ ] 限制所在 scope 与清单工具的注册 scope **不同层**，且工具在**更内层**；
- [ ] 目录校验是**机械读取** `tools.schemas(agent)` 并与冻结清单**精确比对**，
      fail closed（不接受自报）；
- [ ] 校验发生在**每日志/每会话的真实运行**中，且在 MCP 异步注册**之后**
      （握手时刻天然满足）；
- [ ] guard 之类执行期防线**存在但不被当作目录保证**。

**反例特征**（出现即不合规）：`restrict({ deny: [...] })` 里是 boot 期枚举的名字；
依赖 `cwd` / workspace 隔离工具可见性；"可见工具"靠插件自报而非枚举校验。
