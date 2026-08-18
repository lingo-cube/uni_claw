#!/usr/bin/env node
/**
 * Cross-process Assistance E2E client (dsh-assistance-provider-adapter A4).
 * Drives the REAL transport boundary: connects to a REAL DriverHost server wired
 * with the AssistanceWireProvider + a deterministic (scripted) environment
 * factory, starts a run whose Agent hits a Contradicted adjudication, runs the
 * REAL plugin-side AssistanceBridge + DeterministicAssistanceConsumer, and
 * observes the advice resolve → re-observe → SAME goal → completion.
 *
 * MODEL-FREE: the consumer is deterministic; no real model is invoked.
 *
 * Usage: node test/e2e-assistance.mjs --host 127.0.0.1 --port <port>
 * Prints E2E_* markers; exits 0 only when every step passed.
 */
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const { UniClawAdapter } = await import(join(here, '..', 'src', 'adapter.js'));
const { AssistanceBridge } = await import(join(here, '..', 'src', 'assistance', 'bridge.js'));
const { DeterministicAssistanceConsumer } = await import(join(here, '..', 'src', 'assistance', 'consumer.js'));

function parseArgs(argv) {
  const args = {};
  for (let i = 0; i < argv.length; i += 1) {
    const token = argv[i];
    if (token === '--host') args.host = argv[++i];
    else if (token === '--port') args.port = Number(argv[++i]);
  }
  return args;
}

function fail(message) {
  console.log(`E2E_FAIL: ${message}`);
  process.exit(1);
}

const REQUEST = {
  goal: { objectIdentity: 'WifiConnectivity', stateDimension: 'Enabled', desiredValue: true },
  objects: [{ identity: 'WifiConnectivity', category: 'ConnectivitySetting', stateDimensions: ['Enabled'] }],
  capabilities: [{ name: 'SetEnabled', applicableToCategory: 'ConnectivitySetting', stateDimension: 'Enabled' }],
  device: 'serial:test-1',
};

const { host = '127.0.0.1', port } = parseArgs(process.argv.slice(2));
if (!Number.isInteger(port)) fail('usage: --host <h> --port <port>');

const adapter = new UniClawAdapter({ host, port, maxAttempts: 3, backoffMs: 50 });

function sleep(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

try {
  await adapter.ensureConnected();
  console.log('E2E_CONNECT_OK');

  // Run the REAL plugin-side bridge with the DETERMINISTIC consumer (model-free).
  const bridge = new AssistanceBridge({
    adapter,
    consumer: new DeterministicAssistanceConsumer(),
    pollIntervalMs: 50,
  });
  bridge.start();

  const accepted = await adapter.runStart(REQUEST);
  if (!accepted || accepted.accepted !== true) fail(`run.start not accepted: ${JSON.stringify(accepted)}`);
  const runId = accepted.runId;
  console.log(`E2E_RUN_START_ACCEPTED runId=${runId}`);

  // Poll snapshot until terminal. The Agent's Contradicted adjudication consults
  // the wire provider; the bridge polls assistance.pending, the deterministic
  // consumer returns "re-observe", the bridge resolves, the Agent re-observes
  // (external world transitions via the scripted environment), and the SAME goal
  // continues to completion.
  let terminal = null;
  let sawAssistanceResolve = false;
  for (let attempt = 0; attempt < 400; attempt += 1) {
    const snap = await adapter.getRunSnapshot(runId);
    const state = snap?.runState?.value;
    if (bridge.stats.resolved > 0) sawAssistanceResolve = true;
    if (state === 'completed' || state === 'failed') {
      terminal = snap;
      break;
    }
    await sleep(25);
  }

  bridge.dispose();

  if (!terminal) fail(`run did not reach a terminal state (runId=${runId})`);
  if (terminal.runState.value !== 'completed') fail(`terminal runState=${terminal.runState.value} (expected completed)`);
  if (!sawAssistanceResolve) fail('assistance bridge never resolved a consult (Agent never consulted)');
  console.log('E2E_ASSISTANCE_RESOLVED_OK');

  const page = await adapter.getRuntimeEvents(runId);
  const kinds = (page?.events ?? []).map((e) => e.kind);
  if (!kinds.includes('RunCompleted')) fail(`RunCompleted missing: ${kinds.join(',')}`);
  console.log('E2E_EVENTS_COMPLETED_OK');

  console.log(`E2E_ASSISTANCE_OK runId=${runId} resolves=${bridge.stats.resolved}`);
  process.exit(0);
} catch (err) {
  fail(`unexpected: ${err?.message ?? String(err)}`);
}
