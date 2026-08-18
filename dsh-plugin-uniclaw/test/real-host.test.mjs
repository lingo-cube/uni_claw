/**
 * REAL pinned-DSH host regression test — durable protection for the
 * command-registration race (PLUG-F7 / graduation-review §16 gate).
 *
 * Regression protected:
 *   missing `inject: ['commands']` causes the real parallel loader to activate
 *   UniClaw before the commands service exists, producing an empty/missing
 *   UniClaw command registry result.
 *
 * The test boots the REAL pinned DSH host (`boot()` from
 * `@deepseek-ai/dsh-app-boot` driving the REAL vendored
 * `@deepseek-ai/cordis-plugin-loader` and `@deepseek-ai/cordis` 4.0.1) against
 * a leaf cordis.yml whose TWO rows — the REAL `@deepseek-ai/dsh-commands`
 * registry and `dsh-plugin-uniclaw` — are separate loader entries. The loader
 * activates entries in parallel, so the ONLY thing that keeps the UniClaw
 * `apply` from racing the registry initialization is the plugin's native
 * Cordis dependency declaration `inject: ['commands']`, which defers
 * activation until the commands service exists. No fake registry, no manual
 * CommandRuntime, no pre-registration, no ordering tricks, no sleeps, no
 * polling for registry appearance: the assertions below read the ACTUAL
 * registry view (`list()` / `find()`) after the real loader settles, and one
 * command is executed through the real registry end to end.
 *
 * Zero-model: no agent/model turn is created; `commands.execute()` runs the
 * registered handler directly (dsh-commands semantics), reaching the uniclaw
 * service → adapter → loopback DriverHost RPC fixture. CommandModelCalls =
 * LlmCalls = VlmCalls = 0 (see also the F17 source scan in lifecycle.test.mjs).
 *
 * Pinned DSH checkout: READ-ONLY. Verified HEAD and empty porcelain before
 * boot; the test never writes into it. Override the checkout with
 * `DSH_PINNED_REPO` (developer default = the local pinned checkout).
 */
import test from 'node:test';
import assert from 'node:assert/strict';
import net from 'node:net';
import { execFileSync } from 'node:child_process';
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const repoRoot = join(here, '..', '..');

const PINNED_HEAD = '47f943859bef60e4160492346772ded9b24f765a';
const PINNED_CORDIS_VERSION = '4.0.1';
const PIN = process.env.DSH_PINNED_REPO ?? '/Users/fran/Documents/Code/dk-harness';

const EXPECTED_COMMANDS = [
  'uniclaw-events-after',
  'uniclaw-evidence-open',
  'uniclaw-inspect-run',
  'uniclaw-inspect-trap',
  'uniclaw-run-goal',
  'uniclaw-runs-list',
  'uniclaw-shadow-analyze',
];
const MUTATING_NAMES = ['start', 'pause', 'resume', 'stop', 'abort'];
const SERVICE_METHODS = [
  'ping', 'listRuns', 'getRunSnapshot', 'getTrap', 'getRuntimeEvents',
  'drainRunEvents', 'getEvidence', 'controlSupport',
];

/** Smallest deterministic DriverHost stand-in: wire-conformant loopback server. */
function startFixture() {
  const state = { connections: 0, sockets: new Set(), port: 0, server: null };
  return new Promise((resolve, reject) => {
    const server = net.createServer((socket) => {
      state.connections += 1;
      state.sockets.add(socket);
      socket.on('close', () => state.sockets.delete(socket));
      socket.setEncoding('utf8');
      let buffer = '';
      socket.on('data', (chunk) => {
        buffer += chunk;
        let newlineIndex;
        while ((newlineIndex = buffer.indexOf('\n')) >= 0) {
          const line = buffer.slice(0, newlineIndex);
          buffer = buffer.slice(newlineIndex + 1);
          if (!line.trim()) continue;
          let msg;
          try {
            msg = JSON.parse(line);
          } catch {
            continue;
          }
          const { id, method, params } = msg;
          let result;
          if (method === 'ping') {
            result = { protocolVersion: 1, serviceName: 'dsh-uniclaw-driverhost' };
          } else if (method === 'run.list') {
            result = { runs: [{ runId: 'run-smoke', state: 'completed' }] };
          } else if (method === 'run.snapshot.get') {
            result = {
              runId: params?.runId ?? 'run-smoke',
              state: 'completed',
              classification: 'directPublicProjection',
              summary: 'smoke run',
            };
          } else if (method === 'run.start') {
            if (params?.device !== 'serial:smoke-1') {
              result = { error: { code: 'request_rejected', message: `device ${params?.device} is not supported` } };
            } else {
              result = { accepted: true, runId: 'run-smoke-2', runState: 'Idle' };
            }
          } else {
            result = { error: { code: 'unknown_method', message: `no ${method}` } };
          }
          socket.write(`${JSON.stringify({ id, result })}\n`);
        }
      });
    });
    server.on('error', reject);
    server.listen(0, '127.0.0.1', () => {
      state.server = server;
      state.port = server.address().port;
      resolve(state);
    });
  });
}

function closeFixture(state) {
  for (const socket of state.sockets) {
    try {
      socket.destroy();
    } catch {
      // best-effort
    }
  }
  state.sockets.clear();
  if (state.server) {
    state.server.close();
    state.server = null;
  }
}

/**
 * Verify the pinned DSH checkout before real-host execution. Throws with the
 * explicit classification PINNED_DSH_TEST_ENVIRONMENT_UNAVAILABLE when the
 * checkout is missing, on the wrong commit, or dirty — the test never
 * silently replaces the pinned stack with mocks.
 */
function verifyPinnedCheckout() {
  let head;
  let porcelain;
  try {
    head = execFileSync('git', ['-C', PIN, 'rev-parse', 'HEAD'], { encoding: 'utf8' }).trim();
    porcelain = execFileSync('git', ['-C', PIN, 'status', '--porcelain'], { encoding: 'utf8' }).trim();
  } catch (err) {
    throw new Error(
      `PINNED_DSH_TEST_ENVIRONMENT_UNAVAILABLE: cannot inspect pinned checkout at ${PIN}: ${err instanceof Error ? err.message : String(err)}`,
    );
  }
  if (head !== PINNED_HEAD) {
    throw new Error(
      `PINNED_DSH_TEST_ENVIRONMENT_UNAVAILABLE: pinned checkout HEAD ${head} != ${PINNED_HEAD}`,
    );
  }
  if (porcelain !== '') {
    throw new Error(
      `PINNED_DSH_TEST_ENVIRONMENT_UNAVAILABLE: pinned checkout is not clean (porcelain non-empty)`,
    );
  }
}

async function waitFor(predicate, label, timeoutMs = 4000) {
  const deadline = Date.now() + timeoutMs;
  while (!predicate()) {
    if (Date.now() > deadline) {
      throw new Error(`timed out waiting for ${label}`);
    }
    await new Promise((resolve) => setTimeout(resolve, 25));
  }
}

test('real pinned DSH host: command-registration race regression (inject dependency)', async (t) => {
  verifyPinnedCheckout();

  // Deterministic wire-conformant DriverHost fixture on an ephemeral port,
  // started BEFORE boot so the plugin's activation connect succeeds.
  const fixture = await startFixture();
  t.after(() => closeFixture(fixture));

  // Leaf composition: TWO separate loader entries so the real parallel
  // activation behavior is exercised. Absolute `name:` paths resolve through
  // the real loader; no bareModuleBaseUrl, no node_modules symlinks, no
  // mocks. The fixture port is bound before the config is written.
  const tempDir = mkdtempSync(join(tmpdir(), 'dsh-uniclaw-realhost-'));
  t.after(() => {
    try {
      rmSync(tempDir, { recursive: true, force: true });
    } catch {
      // best-effort self-cleanup
    }
  });
  const configPath = join(tempDir, 'cordis.yml');
  writeFileSync(configPath, [
    '# durable real-host regression composition (generated at test runtime)',
    '- id: commands',
    `  name: ${join(PIN, 'packages', 'interaction', 'commands', 'lib', 'index.js')}`,
    '- id: dsh-plugin-uniclaw',
    `  name: ${join(repoRoot, 'dsh-plugin-uniclaw', 'src', 'plugin.js')}`,
    '  config:',
    '    host: 127.0.0.1',
    `    port: ${fixture.port}`,
    '',
  ].join('\n'));

  // Point the plugin's activation version guard at the pinned vendored fork.
  process.env.DSH_PLUGIN_CORDIS_PACKAGE_JSON = join(PIN, 'vendor', 'cordis', 'package.json');

  const { boot } = await import(pathToFileURL(join(PIN, 'packages', 'boot', 'app-boot', 'lib', 'index.js')).href);

  const state = {};
  try {
    state.ctx = await boot('dsh-uniclaw-realhost-test', configPath);
  } catch (err) {
    throw new Error(`real boot failed: ${err instanceof Error ? err.message : String(err)}`);
  }
  t.after(async () => {
    await state.ctx?.fiber?.dispose();
    const adapter = state.ctx?.get?.('uniclaw')?.adapter;
    if (adapter) {
      assert.equal(adapter._disposed, true, 'adapter disposed by plugin effect disposer');
      assert.equal(adapter.state, 'disconnected', 'adapter connection state disconnected after dispose');
    }
  });

  // The plugin connects fire-and-forget at activation; the handshake settles
  // a moment after the loader reports the tree settled. Wait once for the
  // real connection state (event-driven, bounded) so the connection-bound
  // assertions below are deterministic. Registry assertions never wait: the
  // loader's own `await()` already settled inject-deferred activation.
  await waitFor(
    () => state.ctx.get('uniclaw')?.adapter?.state === 'connected',
    'adapter handshake with the DriverHost fixture',
  );

  await t.test('RealBoot: pinned DSH host boots with real loader and pinned cordis', () => {
    assert.ok(state.ctx, 'boot() resolved a real cordis Context');
    const loader = state.ctx.get('loader');
    assert.ok(loader && typeof loader.await === 'function', 'real cordis-plugin-loader present');
    const manifest = JSON.parse(readFileSync(join(PIN, 'vendor', 'cordis', 'package.json'), 'utf8'));
    assert.equal(manifest.version, PINNED_CORDIS_VERSION, 'pinned vendored cordis fork version');
    const service = state.ctx.get('uniclaw');
    assert.ok(service, 'UniclawPluginActivated: uniclaw service provided after inject-deferred activation');
    for (const method of SERVICE_METHODS) {
      assert.equal(typeof service[method], 'function', `uniclaw service exposes ${method}`);
    }
  });

  await t.test('CommandsDependencyResolvedViaInject: plugin descriptor declares commands injection', async () => {
    const { default: plugin } = await import(pathToFileURL(join(repoRoot, 'dsh-plugin-uniclaw', 'src', 'plugin.js')).href);
    assert.deepEqual(plugin.inject, ['commands'], 'loader-consumed inject declaration on the plugin descriptor');
  });

  await t.test('RealCommandsService: commands service is the real @deepseek-ai/dsh-commands registry', () => {
    const commands = state.ctx.get('commands');
    for (const method of ['register', 'list', 'find', 'execute']) {
      assert.equal(typeof commands?.[method], 'function', `real registry exposes ${method}`);
    }
  });

  await t.test('ActualRegistryInspected: real registry view carries exactly the seven commands', () => {
    const commands = state.ctx.get('commands');
    const stubAgent = { session: { append: () => ({}) } };
    const listed = [...(commands.list(stubAgent) ?? [])].map((entry) => entry.name);
    assert.deepEqual([...listed].sort(), [...EXPECTED_COMMANDS].sort(), 'registry list() view matches the seven commands');
    assert.equal(listed.length, 7, 'RegisteredCommandCount = 7');
    for (const name of EXPECTED_COMMANDS) {
      assert.ok(commands.find(stubAgent, name), `registry find() resolves ${name}`);
    }
  });

  await t.test('MutatingCommandsRegistered: start/pause/resume/stop/abort are NOT registered', () => {
    const commands = state.ctx.get('commands');
    const stubAgent = { session: { append: () => ({}) } };
    for (const name of MUTATING_NAMES) {
      assert.equal(commands.find(stubAgent, name), undefined, `mutating command ${name} absent from registry`);
    }
  });

  await t.test('RealCommandInvocation: uniclaw-inspect-run executes through the real registry end to end', async () => {
    const commands = state.ctx.get('commands');
    const stubAgent = { session: { append: () => ({}) } };
    const executed = await commands.execute(
      stubAgent,
      '/uniclaw-inspect-run run-smoke',
      new AbortController().signal,
    );
    assert.equal(executed?.result?.kind, 'success', 'handler returned a success CommandResult');
    assert.ok(
      String(executed?.result?.text ?? '').includes('runId: run-smoke'),
      'formatted classified snapshot returned through registry → handler → uniclaw service → adapter → DriverHost RPC',
    );
  });

  await t.test('RealCommandInvocation: uniclaw-run-goal executes through the real registry end to end (run.start → runId)', async () => {
    const commands = state.ctx.get('commands');
    const stubAgent = { session: { append: () => ({}) } };
    const request = JSON.stringify({
      goal: { objectIdentity: 'WifiConnectivity', stateDimension: 'Enabled', desiredValue: true },
      objects: [{ identity: 'WifiConnectivity', category: 'ConnectivitySetting', stateDimensions: ['Enabled'] }],
      capabilities: [{ name: 'SetEnabled', applicableToCategory: 'ConnectivitySetting', stateDimension: 'Enabled' }],
      device: 'serial:smoke-1',
    });
    const executed = await commands.execute(
      stubAgent,
      `/uniclaw-run-goal ${request}`,
      new AbortController().signal,
    );
    assert.equal(executed?.result?.kind, 'success', 'run-goal handler returned a success CommandResult');
    assert.ok(
      String(executed?.result?.text ?? '').includes('runId: run-smoke-2'),
      'formatted runId returned through registry → handler → adapter.runStart → DriverHost RPC',
    );
  });

  await t.test('SessionLifecycle: session/created subscription executes on the real host', async () => {
    const adapter = state.ctx.get('uniclaw').adapter;
    assert.ok(fixture.connections >= 1, 'activation connect reached the fixture');
    for (const socket of fixture.sockets) {
      try {
        socket.destroy();
      } catch {
        // best-effort
      }
    }
    await waitFor(() => adapter.state === 'disconnected', 'adapter to observe the dropped connection');
    state.ctx.emit('session/created', {});
    await waitFor(
      () => fixture.connections >= 2,
      'session/created subscription to drive a fresh DriverHost connection',
    );
    assert.ok(fixture.connections >= 2, 'session/created subscription executed (new connection accepted)');
  });
});
