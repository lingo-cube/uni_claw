# dsh/ — @uniclaw DSH profile plugin deployment

`deploy.sh` deploys the four profile plugins:

- `uniclaw-restrict` → `@uniclaw/dsh-uniagent-restrict`
- `uniclaw-decision-channel` → `@uniclaw/dsh-decision-channel`
- `uniclaw-task-workbench` → `@uniclaw/dsh-task-workbench` (UniClaw Product
  Workspace: `/api/uniclaw-task/*` task surface plus the full-screen project →
  Task Instance → session/trace/evidence client workspace, PNL-002)
- `uniclaw-task-scope` → `@uniclaw/dsh-task-scope` (uniagent-task session
  tool-surface restriction, PNL-001 决策 6 / S2)

into the DSH web profile (`~/.dsh/profiles/web`) and verifies the installed
copies.

The dedicated Slow route also requires the profile preset row in
`agent-presets/uniagent-slow/cordis.patch.yml` to be present in the active DSH
profile as `uniclaw-slow`. That preset closes inherited tools so the provider
request cannot carry the Product `submit_decision` schema. Adding or changing
preset rows (including the workbench/task-scope mount rows) is an **Owner
manual deployment action**: `deploy.sh` only does pnpm remove/add of the
packages and never edits `cordis.patch.yml`; preset-composition changes load
only at boot (see Standing rule).

## The snapshot pitfall

- pnpm `file:` deps are **snapshot copies, not symlinks** — editing the repo
  does nothing to the profile.
- pnpm **reuses content-addressed snapshots**, so a plain `pnpm add file:...`
  may copy nothing new and the profile silently runs stale code.
- The fix is mechanical: `pnpm remove` + `pnpm add` per package, then
  **grep-verify a marker** in the installed copy, then diff every file.
  (Background: `docs/analysis/dsh-tool-scope-restricted-sessions.md` §6,
  `docs/analysis/agt-002-b1-fix-instruction.md` §4 M2 / §6.)

## Usage

```bash
# Deploy (remove+add both packages, verify markers, drift check)
dsh/deploy.sh

# Check for drift only — no mutation, exit 0 clean / 1 drift
dsh/deploy.sh --check-only

# Deploy, then print (never run) the start command for a dedicated test port
dsh/deploy.sh --port 3081

# Env overrides
DSH_PROFILE_DIR=~/.dsh/profiles/other DSH_PNPM_BIN=/path/to/pnpm dsh/deploy.sh --check-only
```

## Standing rule

Profile patch preset rows (the `@deepseek-ai/dsh-agent-preset` declaration
lines) and plugin mounts only load **at boot** — any preset-composition or
plugin change requires restarting the instance. The script only prints the
start command hint (`--port N`) for a dedicated test instance; it never starts,
restarts, or kills the owner's live 3080 instance. Live E2E must target a
dedicated port such as 3081.

## Project and conversation naming

The host mount accepts configurable values in the active DSH profile patch
(`~/.dsh/profiles/web/cordis.patch.yml`). The field contract is also kept next
to the plugin in [`uniclaw-decision-channel/README.md`](uniclaw-decision-channel/README.md),
so naming changes can be reviewed and maintained with the plugin source.
They are applied through the real DSH services: `workspaceTitle` names the
project shown in the sidebar, `sessionTitle` names the task-scoped Product
conversation, and `slowSessionTitle` names each temporary Slow conversation.
The current live profile uses:

```yaml
- id: uniclaw-decision-channel
  name: '@uniclaw/dsh-decision-channel'
  config:
    workspaceTitle: UniClaw Product Tasks
    workspaceKey: UniClaw Product Tasks
    workspaceReuse: true
    sessionTitle: 遍历设置菜单覆盖测试
    slowSessionTitle: 遍历设置菜单覆盖测试 · Slow
    autoCloseTurn: false
```

The Product model is selected from `.dsh/profiles/uniagent-prod.yaml`; the
checked-in default is `zai-coding-cn/glm-5.3-flash`, which is also present in
the local DSH model catalog.

The Product session is created with the configured workspace id and the title
is committed through `sessionController.rename`; the plugin does not edit DSH
storage files directly. If a value is omitted, the plugin uses a readable
default and keeps the same behavior for existing mounts.

For shared test work, set `workspaceReuse: true` and a stable `workspaceKey`.
The DSH workspace registry reuses that canonical directory while each Product
task keeps its own session and task-purpose title (for example, `遍历设置菜单覆盖测试`).
`autoCloseTurn` defaults to `false`, so a task does not cancel or recycle a
shared DSH turn unless the profile explicitly opts in.

To change the names, edit the `config` block in that profile row, deploy the
plugin if its source changed, and start a dedicated test service so the profile
patch is loaded. The running owner service on 3080 is never changed by this
workflow.

## Dedicated live-test service

Start the test service explicitly when live E2E is needed:

```bash
dsh/test-service.sh
```

It uses port 3081 by default, refuses port 3080, and exits if the test port is
already occupied. It does not kill or restart any process. Point the tests at
it with `UNICLAW_DSH_E2E_BASE=http://127.0.0.1:3081/`.

## Side-effect guarantee

The script never writes anything under `~/.dsh` except `node_modules` of the
four `@uniclaw` packages via pnpm remove/add; it never touches credentials,
`mcp-servers.json`, `cordis.patch.yml`, or any running instance.
