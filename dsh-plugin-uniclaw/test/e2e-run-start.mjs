#!/usr/bin/env node
/**
 * Cross-process run.start E2E client (dsh-runtime-agent-subagent-run-entry §32).
 * Drives the REAL transport boundary from the Node side: connect to a REAL
 * DriverHost server wired with the RunExecutionCoordinator + a deterministic
 * (scripted) environment factory, call run.start, receive the DriverHost-owned
 * runId immediately, then observe the SAME run exclusively through the existing
 * read-only surfaces until the REAL Runtime.Agent semantic entry completes.
 *
 * Usage: node test/e2e-run-start.mjs --host 127.0.0.1 --port <port>
 * Prints E2E_* markers; exits 0 only when every step passed.
 */
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const { UniClawAdapter } = await import(join(here, '..', 'src', 'adapter.js'));

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

  const ping = await adapter.ping();
  if (ping.service !== 'dsh-uniclaw-driverhost') fail(`ping.service=${ping.service}`);
  console.log('E2E_PING_OK');

  // run.start → RunAccepted(runId) immediately (never blocks on execution).
  const accepted = await adapter.runStart(REQUEST);
  if (!accepted || accepted.accepted !== true) fail(`run.start not accepted: ${JSON.stringify(accepted)}`);
  if (typeof accepted.runId !== 'string' || accepted.runId.length === 0) fail(`missing runId: ${JSON.stringify(accepted)}`);
  const runId = accepted.runId;
  console.log(`E2E_RUN_START_ACCEPTED runId=${runId} runState=${accepted.runState}`);

  // Immediate visibility: the returned runId is legitimate on the EXISTING surfaces.
  const runs = await adapter.listRuns();
  if (!Array.isArray(runs?.runIds) || !runs.runIds.includes(runId)) fail(`run.list does not contain ${runId}: ${JSON.stringify(runs)}`);
  console.log('E2E_RUN_LIST_OK');

  const snapshotNow = await adapter.getRunSnapshot(runId);
  // Truthful accepted/live state: the run is REGISTERED (directPublicProjection,
  // never the "unknown run" read). The value is a truthful Agent state at read
  // time (idle/initializing/running, or completed if the deterministic scripted
  // run already finished) — never fabricated, never "run does not exist".
  const acceptedStates = ['idle', 'initializing', 'running', 'completed', 'failed'];
  if (snapshotNow?.runState?.classification !== 'directPublicProjection') fail('snapshot not a direct public projection');
  if (!acceptedStates.includes(snapshotNow?.runState?.value)) fail(`immediate snapshot runState=${snapshotNow?.runState?.value}`);
  console.log('E2E_IMMEDIATE_SNAPSHOT_OK');

  const eventsNow = await adapter.getRuntimeEvents(runId);
  if (!eventsNow || !Array.isArray(eventsNow.events)) fail('events surface not readable immediately');
  console.log('E2E_IMMEDIATE_EVENTS_OK');

  // Existing surfaces only: poll run.snapshot.get until the REAL Agent completes.
  let terminal = null;
  for (let attempt = 0; attempt < 200; attempt += 1) {
    const snap = await adapter.getRunSnapshot(runId);
    const state = snap?.runState?.value;
    if (state === 'completed' || state === 'failed') {
      terminal = snap;
      break;
    }
    await sleep(25);
  }
  if (!terminal) fail(`run did not reach a terminal state (runId=${runId})`);
  if (terminal.runState.value !== 'completed') fail(`terminal runState=${terminal.runState.value} (expected completed)`);
  console.log('E2E_SNAPSHOT_COMPLETED_OK');

  // Existing event surface carries Kernel truth: RunCompleted must be present.
  const page = await adapter.getRuntimeEvents(runId);
  const events = page?.events ?? [];
  const kinds = events.map((e) => e.kind);
  if (!kinds.includes('RunCompleted')) fail(`RunCompleted missing from events: ${kinds.join(',')}`);
  if (!events.every((e) => e.runId === runId)) fail('events carry a foreign runId');
  console.log('E2E_EVENTS_COMPLETED_OK');

  // No DSH-synthesized completion: the completed truth came from the Kernel path.
  console.log(`E2E_RUN_START_OK runId=${runId}`);
  process.exit(0);
} catch (err) {
  fail(`unexpected: ${err?.message ?? String(err)}`);
}
