# AI Coding Agent Routing

> **Human-readable explanation only.** The canonical executable routing truth is `.ai/model-routing.yaml`.
> This file must not contradict the YAML. If they differ, the YAML wins.
>
> Shared routing map for every compatible AI Coder Host.
> Keep project rules in `AGENTS.md`; keep executor-specific invocation mechanics in thin Host adapters.
> Model aliases and provider/fallback chains are configured in `.ai/model-routing.yaml`.

## Principles

1. Roles are portable; tools are adapters.
2. `AGENTS.md` is the project entrypoint for every Host.
3. `.codex/` and `.dsh/` may bind portable roles to Host mechanisms; they cannot redefine role semantics. Other Hosts consume `AGENTS.md`, `.ai/`, and `.agents/skills/` directly when supported.
4. A role may run on a stronger tier than configured, but must not silently downgrade below its tier.
5. Runtime implementation only starts from an approved Scenario Contract / OpenSpec SHALL / task. Ambiguous semantics route to design first.

## Development Lane Routing

The `project-leader` selects the lane from repository truth:

- `SEMANTIC_DISCOVERY` for new or ambiguous semantics, Reality Pressure, CP/RM
  admission, ownership/authority changes, safety semantics, dependency changes,
  or invariant challenges.
- `CAPABILITY_DELIVERY_FAST` for an accepted capability whose work remains
  inside the approved semantic envelope and current architecture invariants.

Parallel workers are allowed for repository inspection, test analysis,
implementation-local research, validation, and documentation reconciliation.
Parallel discovery is not parallel semantic commitment: only the Project Leader
commits the next canonical state, and workers cannot own final semantic,
architecture, ownership, authority, or invariant decisions.

For one explicitly selected pressure, `SEMANTIC_DISCOVERY_AUTOPILOT` lets the
Project Leader auto-continue evidence research → Reality Model extraction →
independent validation → condition repair → admission → capability-gap analysis
→ candidate generation → Architecture Fit. Workers perform bounded evidence,
fixture, minimization, validation, and repair work. Human relay is required only
at the material boundaries defined in `.ai/development-protocol.md`.

Fast Lane workers return evidence/results to the Project Leader. The Project
Leader owns diagnose → repair → validate → continue and stops only at a
canonical Hard Gate or terminal validated state.

Meaningful run/failure evidence should become the smallest feasible replayable
asset, preferring L2 short-chain integration and L3 recorded-reality replay.
Workers may construct and cluster assets; only the Project Leader commits corpus
promotion and evidence-pulled next-capability priority. Static roadmap order is
guidance, not automatic priority.

## Model Tiers

| Tier | Responsibility | Typical work | Avoid |
|------|----------------|--------------|-------|
| `leader` | Top-level coordination and final responsibility | Scope control, OpenSpec lifecycle, task dispatch, merge judgment, user-facing decisions | Leaf coding and bulk file reading |
| `expert` | Decision-dense investigation | Cross-module refactor, deep failure diagnosis, architecture tradeoff | Mechanical coding that has no design decision |
| `standard` | Contract-driven production work | Runtime coding, scenario design, validation, focused tests | Owning top-level phase decisions alone |
| `fast` | Read-only retrieval and compression | File search, log extraction, symbol lookup, artifact summarization | Writing files or making architecture decisions |

## Portable Role Map

| Portable role | Tier | Host realization | Main output |
|---------------|------|------------------|-------------|
| `PROJECT_LEADER_MODEL` | `leader` | Main Leader session using an allowed binding | Plan, semantic/admission commitment, architecture-prior falsification, corpus/priority decision, gate judgment |
| `EXECUTION_WORKER_MODEL` | `fast`/`standard` | Inline or bounded Worker using an allowed binding | Evidence, minimization, fixtures/assets, implementation, test, diagnosis, repair, validation |
| `project-leader` | `leader` | Main session | Plan, dispatch, final decision |
| `phase-evolution-controller` | `standard` | Inline controlled planning role | Next Action |
| `scenario-architect` | `expert` | Inline or bounded expert role | Scenario Contract, Fake World, vocabulary, invariant check |
| `runtime-coder` | `standard` | `module-worker` + `development` | Code/test changes for one approved task |
| `runtime-validator` | `standard` | `verifier` + `verification` | PASS / CONDITIONAL_PASS / FAIL |
| `openspec-researcher` | `fast` | Read-only researcher adapter when available | Structured facts with file/line evidence |
| `openspec-coder` | `standard` | `module-worker` + approved WorkItem | Code/test changes for one scoped task |
| `openspec-refactorer` | `expert` | High-reasoning bounded investigation | Root cause, options, targeted change |

**Provider-neutral principle:** `PROJECT_LEADER_MODEL` and `EXECUTION_WORKER_MODEL` are canonical logical roles. Each provider maps them to its own concrete model identifiers per `.ai/model-routing.yaml`. Development protocols reference the logical roles; never hardcode provider-specific model names.

## Reusable Profile Layer

通用组合定义在 `.ai/profiles/`，UniFlow 工作流见 `.ai/workflows/uniflow-coding-workflow.md`：

```text
AgentProfile = RoleProfile + ExecutionProfile + Optional ModuleProfile
AgentInvocation = AgentProfile + ModelBinding + ModuleContext + WorkItem
```

- 稳定 RoleProfile 只有 `coding-leader` 与 `module-worker`；现有 portable roles 继续作为协议/路由名称，不复制成新的职责 Profile。
- `development`、`test-authoring`、`verification`、`semantic-analysis`、`tool-only` 是 ExecutionProfile。
- ModuleProfile 按真实稳定边界合并为 `runtime-core`、`runtime-integration`、`semantic-capability`、`engineering-governance`。
- `.ai/model-routing.yaml` 是独立 ModelBinding 来源；Profile 不硬编码 provider/model。
- `.codex/agents/module-worker.toml`、`test-author.toml`、`verifier.toml`、`semantic-analyzer.toml` 是 Codex adapter，不是 Profile 真相源。
- 一个 WorkItem 只能指定一个主要 ModuleProfile 与一个 `worker_owner`，不得 fanout 给多个 Worker。

### UniFlow 入口

- 统一调用格式：`执行 UniFlow：<任务内容>`；语义明确的“按 UniFlow 执行”等价。
- Codex 与 DSH 只在识别该触发约定后按需加载工作流、匹配的 Profile、WorkItem Schema
  与任务相关上下文；不得默认加载全部 Profile、OpenSpec 或历史 Decisions。
- `UniFlow` 不选择固定模型或固定 Worker；Leader 仍依据任务形态和
  `.ai/model-routing.yaml` 完成路由。
- Codex 使用项目 custom agent adapter；DSH 使用自身可用执行能力，但两者消费同一
  Profile、WorkItem 和约束优先级，不建立平台专属副本。

## Dispatch Rules

| Request shape | Route |
|---------------|-------|
| `openspec propose ...` | `project-leader` loads `.ai/skills/openspec-propose/SKILL.md` as playbook, then uses `scenario-architect` / `openspec-researcher` as needed. |
| `openspec explore ...` | Read-only exploration. Prefer `openspec-researcher` for broad retrieval; do not implement production code. |
| `openspec apply ...` | `project-leader` reads change artifacts, dispatches `runtime-coder` / `openspec-coder` for approved tasks, then `runtime-validator`. |
| Runtime scenario or semantic design | `scenario-architect` before coding. |
| Approved atomic Runtime task | `module-worker` + `development`; keep one dispatch scoped to one WorkItem and one owner. |
| Test authoring only | `test-author` + `test-authoring`; production source is forbidden. |
| Verification only | `verifier` + `verification`; source files are read-only. |
| Semantic evidence / Fact / consumer-boundary analysis | `semantic-analyzer` + `semantic-analysis`; analysis only. |
| Phase or vertical slice completion claim | `runtime-validator`; validation is independent of coder claims. |
| Cross-module design failure or deep diagnosis | `openspec-refactorer` on `expert`. |
| Bulk read-only context gathering | `openspec-researcher` on `fast`. |
| Failure minimization / L2-L3 asset construction | `EXECUTION_WORKER_MODEL`; Project Leader decides promotion. |

Human-facing Gate communication is compressed to Goal, discovery/change,
architecture impact, material trade-off, and exact decision. Detailed governance
artifacts remain repository-facing and are not duplicated in routing adapters.

## Host Adapter Notes

A Host may use a registered custom worker or execute the portable role inline. When a named adapter or configured model is unavailable, record `ROUTING_CAPABILITY_LIMIT`; run inline only when the tier's fallback policy permits it:

1. State the role being used.
2. Load the same required portable repository documents any Host must load.
3. Respect the role's write boundary, especially read-only validator/researcher boundaries.
4. Use MCP-first C# symbol lookup when a C# definition/reference/diagnostic is needed.
5. Report whether the role ran inline or through a concrete Host adapter.

## Change Control

- Add or rename a role here first.
- Update `.ai/model-routing.yaml` with its tier binding.
- Update a Host adapter only when invocation mechanics require it; never copy portable role semantics into it.
- Update `AGENTS.md` only to change the shared entrypoint map.
