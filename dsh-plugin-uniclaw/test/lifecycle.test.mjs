/**
 * Lifecycle tests for dsh-plugin-uniclaw (PLUG-F1/F3/F4/F6/F7/F17 gates).
 * Runs against the REAL vendored cordis fork (4.0.1) loaded standalone: the
 * plugin is applied via ctx.plugin() and its lifecycle hooks are asserted
 * against the fork's actual semantics (async activation, session/created as
 * the attach event, ctx.effect cleanup).
 */
import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, readdirSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const repoRoot = join(here, '..', '..');
const srcDir = join(here, '..', 'src');
const moduleDir = join(here, '..');

// Load the vendored cordis fork standalone (probe-verified: new Context() works).
// Preferred: repo-installed fork; fallback: the pinned dk-harness vendor tree.
const DEFAULT_VENDORED_CORDIS = join(repoRoot, 'node_modules', '@deepseek-ai', 'cordis', 'lib', 'index.js');
const DSH_VENDOR_CORDIS = '/Users/fran/Documents/Code/dk-harness/vendor/cordis/lib/index.js';
const { existsSync } = await import('node:fs');
const cordisPath = process.env.DSH_TEST_CORDIS_PATH
  || (existsSync(DEFAULT_VENDORED_CORDIS) ? DEFAULT_VENDORED_CORDIS : DSH_VENDOR_CORDIS);
const { Context } = await import(cordisPath);

// Point the plugin's version guard at the vendored fork manifest.
process.env.DSH_PLUGIN_CORDIS_PACKAGE_JSON = join(dirname(cordisPath), '..', 'package.json');

const { default: plugin, DSH_BASELINE, DSH_VERSION, CORDIS_REQUIRED_VERSION, readCordisVersion, assertCordisVersion } =
  await import(join(srcDir, 'plugin.js'));
const { UniClawAdapter } = await import(join(srcDir, 'adapter.js'));

const FAST_CONFIG = { port: 1, maxAttempts: 1, backoffMs: 1 }; // nothing listens on port 1

function fakeCommands() {
  const registered = [];
  return {
    registered,
    register(def) {
      registered.push(def);
      return () => {
        const i = registered.indexOf(def);
        if (i >= 0) registered.splice(i, 1);
      };
    },
  };
}

/** Apply the plugin and wait for the fiber to reach active state. */
async function applyPlugin(ctx, config = FAST_CONFIG) {
  const fiber = ctx.plugin(plugin, config);
  await fiber.await();
  return fiber;
}

test('plugin metadata pins the frozen baseline', () => {
  assert.equal(DSH_BASELINE, '47f943859bef60e4160492346772ded9b24f765a');
  assert.equal(DSH_VERSION, '0.1.0-rc.5');
  assert.equal(CORDIS_REQUIRED_VERSION, '4.0.1');
  assert.equal(plugin.name, 'dsh-plugin-uniclaw');
  assert.equal(typeof plugin.apply, 'function');
});

test('version guard reads the vendored fork and accepts 4.0.1', () => {
  const version = readCordisVersion();
  assert.equal(version, '4.0.1');
  assert.equal(assertCordisVersion(version), version);
});

test('version guard refuses activation on an unpinned version', () => {
  assert.throws(() => assertCordisVersion('9.9.9'), /refusing activation/);
});

test('activation registers the uniclaw service and seven deterministic commands', async () => {
  const ctx = new Context();
  const commands = fakeCommands();
  ctx.provide('commands', commands);

  const fiber = await applyPlugin(ctx);
  try {
    const service = ctx.get('uniclaw');
    assert.ok(service, 'uniclaw service must be provided');
    assert.equal(typeof service.ping, 'function');
    assert.equal(typeof service.getRunSnapshot, 'function');
    assert.equal(typeof service.getTrap, 'function');
    assert.equal(typeof service.getEvidence, 'function');
    assert.equal(typeof service.listRuns, 'function');
    assert.equal(typeof service.controlSupport, 'function');

    // shadow service: bounded process-local cache surface (EPHEMERAL_PROCESS_LOCAL)
    const shadow = ctx.get('shadow');
    assert.ok(shadow, 'shadow service must be provided');
    assert.equal(typeof shadow.analyze, 'function');
    assert.equal(typeof shadow.cache.get, 'function');
    assert.equal(typeof shadow.cache.size, 'function');
    assert.equal(typeof shadow.cache.clear, 'function');

    assert.equal(commands.registered.length, 7);
    const names = commands.registered.map((d) => d.name);
    assert.deepEqual([...names].sort(), ['uniclaw-events-after', 'uniclaw-evidence-open', 'uniclaw-inspect-run', 'uniclaw-inspect-trap', 'uniclaw-run-goal', 'uniclaw-runs-list', 'uniclaw-shadow-analyze']);
    for (const def of commands.registered) {
      assert.match(def.name, /^[a-z][a-z0-9_-]*$/);
      assert.ok(typeof def.description === 'string' && def.description.length > 0);
      assert.equal(typeof def.handler, 'function');
    }
  } finally {
    await fiber.dispose();
  }
});

test('session/created triggers a connection attempt', async () => {
  const ctx = new Context();
  ctx.provide('commands', fakeCommands());

  const realEnsure = UniClawAdapter.prototype.ensureConnected;
  let attempts = 0;
  UniClawAdapter.prototype.ensureConnected = function (...args) {
    attempts += 1;
    return realEnsure.apply(this, args);
  };
  try {
    const fiber = await applyPlugin(ctx);
    const before = attempts;
    // DSH emits session/created with the session as its single argument.
    ctx.events.emit('session/created', { id: 's1' });
    await new Promise((resolve) => setTimeout(resolve, 30));
    assert.ok(attempts > before, 'session/created must trigger ensureConnected');
    await fiber.dispose();
  } finally {
    UniClawAdapter.prototype.ensureConnected = realEnsure;
  }
});

test('session/event firehose subscription delivers (subject, event)', async () => {
  const ctx = new Context();
  ctx.provide('commands', fakeCommands());
  const fiber = await applyPlugin(ctx);
  try {
    const received = [];
    // Register an additional observer on the same bus to capture the dispatch.
    ctx.on('session/event', (subject, event) => received.push({ subject, event }));
    ctx.events.emit('session/event', { id: 's1' }, { type: 'user/message' });
    assert.equal(received.length, 1);
    assert.equal(received[0].subject.id, 's1');
    assert.equal(received[0].event.type, 'user/message');
  } finally {
    await fiber.dispose();
  }
});

test('dispose unregisters commands and disposes the adapter', async () => {
  const ctx = new Context();
  const commands = fakeCommands();
  ctx.provide('commands', commands);

  const realDispose = UniClawAdapter.prototype.dispose;
  let adapterDisposed = 0;
  UniClawAdapter.prototype.dispose = function (...args) {
    adapterDisposed += 1;
    return realDispose.apply(this, args);
  };
  try {
    const fiber = await applyPlugin(ctx);
    assert.equal(commands.registered.length, 7);
    await fiber.dispose();
    assert.equal(commands.registered.length, 0, 'dispose must unregister all commands');
    assert.equal(adapterDisposed, 1, 'dispose must dispose the adapter');
  } finally {
    UniClawAdapter.prototype.dispose = realDispose;
  }
});

test('F17: control-plane source is free of inference-service references', () => {
  // The shadow slice references llm/model by design (the optional ctx.llm
  // seam); the control plane must stay inference-free.
  const forbidden = /\b(llm|vlm|model)\b/i;
  const offenders = [];
  for (const file of ['adapter.js', 'protocol.js']) {
    const text = readFileSync(join(srcDir, file), 'utf8');
    if (forbidden.test(text)) offenders.push(file);
  }
  assert.deepEqual(offenders, [], 'control-plane modules must not reference llm/vlm/model');
});

test('F17: plugin package declares no runtime dependencies beyond the cordis peer', () => {
  const manifest = JSON.parse(readFileSync(join(moduleDir, 'package.json'), 'utf8'));
  assert.equal(manifest.peerDependencies['@deepseek-ai/cordis'], '4.0.1');
  assert.ok(!manifest.dependencies || Object.keys(manifest.dependencies).length === 0);
});
