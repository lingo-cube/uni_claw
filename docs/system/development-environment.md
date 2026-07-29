# Development environment

This page is the stable local-development baseline for Codex, Claude Code, MCP
navigation, the Android Emulator boundary, and token-efficient project work.

This guidance is intentionally conservative: keep the repository trusted and
fast, keep network and GUI actions explicit, and keep destructive operations
behind prompts.

## Current baseline

- Codex project config lives in `.codex/config.toml`.
- Claude-compatible MCP config lives in `.mcp.json`.
- The runtime baseline is .NET 10 LTS (`net10.0`) with `global.json`
  selecting SDK `10.0.100` and rolling forward within the latest 10.0 feature
  band.
- C# semantic navigation is provided to Codex by `csharper-mcp`.
- `cwm-roslyn-navigator` remains in `.mcp.json` for Claude-oriented workflows,
  but is disabled in Codex because the installed 0.7.0 server does not complete
  the current Codex `tools/list` handshake reliably.
- `scripts/android-emulator.sh doctor` checks a running emulator only.
  `scripts/android-emulator.sh start` is the only project command that should
  start the emulator.
- `scripts/dev-doctor.sh` is the fast local entry point for environment checks.

After changing `.codex/config.toml`, open a new Codex task or restart Codex
Desktop before expecting new MCP tools to appear in tool discovery. Existing
tasks can keep a cached tool registry.

## Daily commands

Use the lightweight checks first:

```bash
scripts/dev-doctor.sh
codex mcp list
dotnet build src/UniClaw.Core.sln
```

Use opt-in checks when they match the work:

```bash
scripts/dev-doctor.sh --build
scripts/dev-doctor.sh --test
scripts/dev-doctor.sh --emulator
scripts/dev-doctor.sh --codex
```

With an already-running emulator, Host checks are explicit:

```bash
dotnet run --project src/UniClaw.Host/UniClaw.Host.csproj -- \
  doctor --device emulator-5554 --provider mock --model deterministic-ui

dotnet run --project src/UniClaw.Host/UniClaw.Host.csproj -- \
  analyze --device emulator-5554 --provider mock --model deterministic-ui

dotnet run --project src/UniClaw.Host/UniClaw.Host.csproj -- \
  run --scenario scenarios/android-settings/locate-one-item.v1.json \
  --device emulator-5554 --provider mock --model deterministic-ui \
  --output artifacts/runs
```

`run` never creates/downloads an AVD. Generated evidence is isolated under
`artifacts/runs/<scenario-id>/<run-id>/` and ignored by Git.

`--emulator` never starts an AVD. It calls `scripts/android-emulator.sh doctor`
and expects the selected emulator to already be running.

`--build` uses `dotnet build -nr:false -m:1 -v:minimal -p:NuGetAudit=false`
to avoid MSBuild node reuse surprises and NuGet vulnerability-feed network
noise in scripted agent runs. CI can run a normal audited restore/build on a
networked worker.

`--test` uses the same MSBuild stabilization flags. In Codex, VSTest may need a
scoped sandbox escalation because it opens a local communication socket.

## Sandbox and network triage

Treat sandbox failures as a diagnostic layer, not as the final explanation.

1. If a network command fails inside Codex with DNS or connection errors, rerun
   the same command with a scoped escalation when the command is necessary.
2. If the escalated command succeeds, the issue was the Codex network sandbox.
   Keep future approvals scoped to the command prefix.
3. If the escalated command still times out or cannot reach the service, the
   issue is outside the sandbox: VPN, proxy, firewall, DNS, custom CA, or the
   upstream endpoint.
4. Prefer reusable approval prefixes for normal work such as `git fetch`,
   `git pull --ff-only`, `dotnet test`, `dotnet build`, `codex doctor`, or
   emulator commands. Do not use `danger-full-access` as the default.
5. GUI and emulator startup should remain explicit. Project tests and health
   checks should use `doctor`, not `start`, unless the task is specifically to
   launch a device.

The current observed pattern on 2026-07-29 was:

- sandboxed `codex doctor`: DNS failures for external endpoints;
- escalated `codex doctor`: DNS resolved, but ChatGPT/OpenAI docs reachability
  timed out;
- conclusion: there is a real host/network reachability issue in addition to
  the expected Codex sandbox restriction.

## MCP rules

For C# definitions, references, implementations, callers, hierarchy, and
diagnostics, use Roslyn MCP first. Do not use grep/find to locate C# symbols.
Read only the returned file range after the semantic query.

Use `rg` for documentation, OpenSpec artifacts, logs, config, exact-string
audits, and other non-C# bulk retrieval.

For Codex:

- keep absolute solution paths in `.codex/config.toml`;
- keep read-only navigation tools auto-approved;
- keep mutating refactor tools, especially `apply_code_action`, on prompt;
- keep incompatible MCP servers disabled instead of letting them slow startup.

For Claude-compatible tooling:

- keep `.mcp.json` small and portable;
- avoid copying Codex-only approval semantics into `.mcp.json`;
- update `.claude/MCP-QUERY.md` when the semantic-navigation rule changes.

## Android Emulator

Use a pinned, visible, lightweight AVD for local development:

```text
AVD: uniclaw-lite-api35
System image: API 35, default, x86_64 on Intel macOS
```

Use `google_apis` only when the target APK requires Google Play Services. Keep
the choice explicit through `UNICLAW_AVD_NAME`; the project should not download
or create AVDs implicitly during tests.

Recommended flow:

```bash
scripts/android-emulator.sh start
scripts/android-emulator.sh doctor
scripts/android-emulator.sh stop
```

For automated checks, prefer:

```bash
scripts/dev-doctor.sh --emulator
```

That command validates ADB, boot completion, screenshot capture, and
UIAutomator XML without starting a new emulator.

## Token efficiency

- Use MCP semantic navigation before reading C# files.
- Read the smallest relevant file ranges after locating a symbol.
- Use `rg --files` and targeted `rg -n` for docs/config instead of broad reads.
- Keep OpenSpec for requirement/proposal/apply/archive work; for direct tooling
  fixes, state that the work did not use OpenSpec.
- Prefer focused `dotnet test --filter ...` for narrow changes, then
  `dotnet build src/UniClaw.Core.sln`; run the full suite for cross-cutting
  changes.
- Keep stable decisions in `AGENTS.md`, `.claude/MCP-QUERY.md`, and this page
  so future agents do not rediscover the same constraints.

## Suggested next hardening

- Add a CI job or local preflight that runs `scripts/dev-doctor.sh --build`.
- Add a separate device-integration test category only after a target APK and
  package name are selected.
- Re-test `cwm-roslyn-navigator` after upgrading it; enable it in Codex only if
  it completes `tools/list` reliably.
- If Codex reachability keeps timing out outside the sandbox, fix the host
  network path first: VPN/proxy/firewall/DNS/custom CA.
