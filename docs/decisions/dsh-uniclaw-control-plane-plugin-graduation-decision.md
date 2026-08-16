# DSH UniClaw Control-Plane Plugin — Graduation Decision Record

> Status: GRADUATED (INDEPENDENT REVIEW) | Decision: `PROJECT_LEADER_DSH_UNICLAW_CONTROL_PLANE_PLUGIN_GRADUATION_REVIEW_V3` | Date: 2026-08-15
> Maturity: `DSH_UNICLAW_CONTROL_PLANE_PLUGIN_INTEGRATED`
> Scope: DSH→UniClaw control-plane plugin integration graduation only — NOT an authorization for Shadow/Advisory/Blocking cognition, mutating Kernel controls, persistent EvidenceRef resolution, custom durable session events, or DSH physical authority.
> Change artifacts: `openspec/changes/dsh-uniclaw-control-plane-plugin-implementation/` (archived same day).

## Decision

`GRADUATED` — the real pinned DSH host → native plugin loader → native commands dependency
injection → UniClaw plugin → native commands registry → deterministic read/inspect commands →
UniClaw service → adapter → DriverHost integration is integrated and durably regression-protected.
Kernel authority is unchanged.

## Review History

1. **Graduation V1 → `REPAIR_REQUIRED`**: under the real parallel loader, UniClaw commands did not
   reliably register because the plugin lacked the native Cordis dependency declaration
   `inject: ['commands']`.
2. **Production repair (bounded)**: `dsh-plugin-uniclaw/src/plugin.js` gained
   `inject: ['commands']` on the default export — the loader then defers UniClaw activation until
   the commands service exists. (Only production change in the whole sequence; verified as the sole
   file at/after its mtime.)
3. **Graduation V2 → `REPAIR_REQUIRED`**: the real-host proof existed only in `/tmp` and was not
   protected by a permanent repository regression test (§16 DurableRealHostRegressionTest = FAIL).
4. **Test-only repair**: `dsh-plugin-uniclaw/test/real-host.test.mjs` permanently exercises the
   real pinned host and catches the original missing-inject race.
5. **Graduation V3 (this review)**: final narrow post-repair review — durable test truly PASS, no
   production/authority/architecture delta introduced by the final repair.

## Root Cause of the Original Defect

`ctx.get('commands')` was read inside `apply` while the commands plugin and dsh-plugin-uniclaw
activated in parallel. Without an `inject` declaration the UniClaw `apply` could run before the
commands service existed, silently skipping registration (empty UniClaw command registry).

## Durable Regression Protection

- **Path**: `dsh-plugin-uniclaw/test/real-host.test.mjs`
- **Included in the normal suite**: YES — auto-discovered by `node --test "test/*.test.mjs"`;
  `npm test` = 41/41 (33 prior + 8 new), standalone 8/8 × 4 consecutive runs, deterministic.
- **Real stack**: `boot()` from `@deepseek-ai/dsh-app-boot` → vendored `cordis-plugin-loader` →
  `@deepseek-ai/cordis` 4.0.1 → real `@deepseek-ai/dsh-commands` → real `dsh-plugin-uniclaw`, all
  from the pinned READ-ONLY DSH checkout `47f943859bef60e4160492346772ded9b24f765a`
  (DSH `0.1.0-rc.5`); the test verifies the pin (HEAD + porcelain) before boot and never writes
  into it.
- **Parallel loader property**: leaf cordis.yml carries TWO separate loader entries (commands +
  UniClaw); ordering is resolved ONLY by `inject: ['commands']` + the real loader's `await()`.
  No fakeCommands, no manual pre-provide, no sleep, no manual ordering, no registry polling.
- **Old behavior fails (evidence)**: isolated /tmp reproduction of the pre-repair implementation
  (missing inject) under the same real parallel loader produced an EMPTY registry — all four
  commands missing — 3/3 deterministic runs (`REGRESSION_CAPTURED`). The durable test's
  `ActualRegistryInspected` assertion would therefore fail on the old implementation.
- **Actual registry assertions**: `list()`/`find()` on the real registry — exact set
  `[uniclaw-evidence-open, uniclaw-inspect-run, uniclaw-inspect-trap, uniclaw-runs-list]`,
  count 4; `start/pause/resume/stop/abort` NOT registered.
- **Real command invocation**: `/uniclaw-inspect-run run-smoke` executed through the actual
  registry → UniClaw handler → uniclaw service → adapter → deterministic wire-conformant loopback
  DriverHost RPC fixture; result `{kind:'success', text: 'runId: run-smoke …'}`. Zero-model:
  CommandModelCalls = LlmCalls = VlmCalls = 0 (no agent/model turn; F17 static scan covers it).
- **Fixture boundary**: the fixture represents only the already-independently-verified RPC peer;
  cross-process real DriverHost behavior remains separately protected by
  `DriverHostPluginE2ETests`.
- **Lifecycle/disposal**: session/created subscription proven (connection drop → reconnect);
  `ctx.fiber.dispose()` clean; adapter `_disposed = true`, state `disconnected`; no hanging
  handles; temp config self-cleaned.

## Validation (fresh runs this review)

- Node suite: `npm test` in `dsh-plugin-uniclaw/` — **41/41 PASS** (real-host.test.mjs included).
- Real pinned-host test standalone: **8/8 PASS**.
- Cross-process E2E + DriverHost/plugin targeted + PluginIntegrationGuardTests + architecture
  guards: **PASS** (included in full suite; filtered DriverHost+Architecture run 89/89 PASS).
- `dotnet build src/UniClaw.Runtime.sln`: **0 errors, 0 warnings**.
- `dotnet test src/UniClaw.Runtime.sln`: **1052 passed / 4 failed** — attribution independently
  established, all known non-blocking environmental:
  - `PF01_ProcessRunner_TimeoutKillsShortLivedChildWithoutShellInterpolation` — timing flake
    (passed in other runs this session; classified flake, not green-by-rerun).
  - `CORR_HOST03 / CORR_HOST04 / CORR_HOST09` — Vision pipeline identity drift
    (`Identity mismatch (PIPELINE)`, `VisionServiceHost.cs:266`, live deployment drift).
- `scripts/check-consistency.sh`: **ALL PASS**.
- `openspec validate dsh-uniclaw-control-plane-plugin-implementation --strict --no-interactive`:
  **PASS** (valid; re-run at archive).

## Zero-Delta Confirmation (final repair)

- Final repair (since V2) changed ONLY `dsh-plugin-uniclaw/test/real-host.test.mjs` and the
  OpenSpec task record in `openspec/changes/dsh-uniclaw-control-plane-plugin-implementation/tasks.md`.
- Production tree scan for files newer than the repair moment (`2026-08-15 17:41:44`):
  NONE outside `dsh-plugin-uniclaw/src/plugin.js` (which IS the V1 inject repair itself).
- RuntimeModified / RuntimeAgentModified / RuntimeSemanticModelChanged / NewRuntimeSemanticEmitters /
  ProtocolChangedByFinalRepair / TransportChangedByFinalRepair / ParallelProtocolInvented /
  DirectDSHPhysicalAuthority / KernelTruthDependsOnDSHState: all NO.
- GoalEvidenceAuthority: `KERNEL_ONLY`. CustomDurableSessionEvents: NONE.
- Pinned DSH checkout verified unmodified: HEAD `47f943859bef60e4160492346772ded9b24f765a`,
  `git status --porcelain` = 0 lines, before and after all validation.

## Transport Role

ONE loopback TCP newline JSON-RPC (127.0.0.1:5177) between the DSH plugin (client) and
UniClaw DriverHost (listener); read-only methods ping/run.list/run.snapshot.get/run.trap.get/
run.events.after/run.events.drain/evidence.get/control.support. Unchanged by the final repair.

## Remaining Limitations (explicitly out of scope)

- Mutating Kernel controls (start/pause/resume/stop/abort): intentionally NOT registered;
  control operations audit via `control.support` and report deferred when the Kernel has no buyer.
- Shadow / Advisory / Blocking cognition: not implemented.
- Persistent EvidenceRef resolution: not implemented (logical locators only).
- Custom durable session events: none introduced.
- DSH physical authority: none — the plugin is a read-only control-plane consumer.
- Real DriverHost is launched only by the cross-process e2e suite; the durable regression test uses
  the deterministic wire fixture (acceptable per the review boundary).

## Next Change

`dsh-shadow-cognition` — to be proposed/implemented separately, NOT inside this graduation review.
