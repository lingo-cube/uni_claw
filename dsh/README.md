# dsh/ — @uniclaw DSH profile plugin deployment

`deploy.sh` deploys the two profile plugins (`uniclaw-restrict` →
`@uniclaw/dsh-uniagent-restrict`, `uniclaw-decision-channel` →
`@uniclaw/dsh-decision-channel`) into the DSH web profile
(`~/.dsh/profiles/web`) and verifies the installed copies.

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

# Deploy, then print (never run) the restart command for a specific port
dsh/deploy.sh --port 3080

# Env overrides
DSH_PROFILE_DIR=~/.dsh/profiles/other DSH_PNPM_BIN=/path/to/pnpm dsh/deploy.sh --check-only
```

## Standing rule

Profile patch preset rows (the `@deepseek-ai/dsh-agent-preset` declaration
lines) and plugin mounts only load **at boot** — any preset-composition or
plugin change requires restarting the instance. The script only prints the
restart command hint (`--port N`); restarting the owner's instance is always
a human decision.

## Side-effect guarantee

The script never writes anything under `~/.dsh` except `node_modules` of the
two `@uniclaw` packages via pnpm remove/add; it never touches credentials,
`mcp-servers.json`, `cordis.patch.yml`, or any running instance.
