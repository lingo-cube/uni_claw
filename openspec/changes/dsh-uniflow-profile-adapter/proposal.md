# Proposal: dsh-uniflow-profile-adapter

## Buyer

`AGENTS.md` §4「UniFlow 按需触发」 declares that DSH consumes the same Profile /
WorkItem / routing semantics as Codex: "DSH 使用自身可用的执行/委派能力消费同一
Profile 与 WorkItem，不复制另一套工作流或约束"。`.ai/workflows/codex-coding-workflow.md`
§10「DSH 边界」 leaves the concrete DSH adapter unspecified. This change supplies it.

## Gap

DSH sessions operating on this repository currently have no mechanism to:

- load and version-validate the UniClaw Profile Core (`.ai/profiles/*.json`);
- compose RoleProfile + ExecutionProfile + ModuleProfile with upstream-identical
  merge priority and conflict semantics;
- bind logical roles to DSH providers/models (GLM-5.2 leader primary/fallback,
  DeepSeek-V4 worker) without leaking model IDs into the profiles;
- route, dispatch, and gate WorkItem/WorkResult per `.ai/schemas/*.schema.json`.

## What changes (engineering-governance module only)

1. `tools/dsh_profile_adapter.py` — DSH Profile Source / Loader / Adapter /
   Model Binding / Worker Router / ModuleContext Loader / Profile Cache /
   LeaderCheckpoint / Work Envelope / WorkResult Gate / minimal event log.
   Delegates all Profile semantics to `tools/agent_profile_validator.py` as the
   single upstream authority; adds only DSH runtime fields via outer envelope.
2. `.dsh/profile-adapter/profile-source.yaml` — pinned Profile Source config and
   model bindings (zai glm-5.2 leader primary, opencode-go glm-5.2 fallback,
   opencode-go deepseek-v4-flash worker, tool-only none). 绑定 provider 名以
   真实 DSH Host 注册路由为准（本部署注册 `opencode-go`）。
3. `tests/AgentWorkflow/test_dsh_profile_adapter.py` — 30-case gate suite.
4. `.dsh/profile-adapter/README.md` — usage doc + minimal goal→worker→accept
   example.

## Non-goals

- No second WorkItem/WorkResult schema; no edits to `.ai/profiles/`,
  `.ai/schemas/`, or `.ai/model-routing.yaml`.
- No new Agent Runtime, no Data Plane / Fact Store / Event Sourcing.
- No model IDs inside generic profiles; no open-ended fanout.

## Human Gate

Large change (new boundary inside `engineering-governance`). Approved directly by
the repository owner in the DSH session that commissioned this work
(readiness gate evidence: `agent_profile_validator.py validate` →
`AGENT_WORKFLOW_VALIDATION_PASS` at revision `eac69ee`).
