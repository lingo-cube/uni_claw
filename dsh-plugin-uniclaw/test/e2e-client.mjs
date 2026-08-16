#!/usr/bin/env node
/**
 * End-to-end client exercised by the .NET DriverHost E2E test
 * (DriverHostPluginE2ETests). Drives the real transport boundary from the
 * Node side: connect to the DriverHost listener, then walk the read-only
 * surface with the same frozen assertions as the plugin itself.
 *
 * Usage: node test/e2e-client.mjs --host 127.0.0.1 --port <port> --runId run-1
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
    else if (token === '--runId') args.runId = argv[++i];
  }
  return args;
}

function fail(message) {
  console.log(`E2E_FAIL: ${message}`);
  process.exit(1);
}

const { host = '127.0.0.1', port, runId } = parseArgs(process.argv.slice(2));
if (!Number.isInteger(port) || !runId) fail('usage: --host <h> --port <port> --runId <runId>');

const adapter = new UniClawAdapter({ host, port, maxAttempts: 3, backoffMs: 50 });

try {
  await adapter.ensureConnected();
  console.log('E2E_CONNECT_OK');

  const ping = await adapter.ping();
  if (ping.service !== 'dsh-uniclaw-driverhost') fail(`ping.service=${ping.service}`);
  if (ping.protocolVersion !== 1) fail(`ping.protocolVersion=${ping.protocolVersion}`);
  if (ping.baselineChange !== 'dsh-uniclaw-control-plane-protocol-baseline') fail(`ping.baselineChange=${ping.baselineChange}`);
  console.log('E2E_PING_OK');

  const snapshot = await adapter.getRunSnapshot(runId);
  if (snapshot?.runState?.value !== 'completed') fail(`runState.value=${snapshot?.runState?.value}`);
  if (snapshot?.runState?.classification !== 'directPublicProjection') fail(`runState.classification=${snapshot?.runState?.classification}`);
  console.log('E2E_SNAPSHOT_OK');

  const page = await adapter.getRuntimeEvents(runId);
  const events = page?.events ?? [];
  if (events.length === 0) fail('runtime events empty');
  if (!events[0].eventId.startsWith(`evt-${runId}-`)) fail(`eventId=${events[0].eventId}`);
  if (events[0].sequence !== 1) fail(`sequence=${events[0].sequence}`);
  console.log('E2E_EVENTS_OK');

  const resolution = await adapter.getEvidence({ locator: 'capture:session-e2e:record:1', runId });
  if (resolution?.found !== true) fail(`evidence found=${resolution?.found}`);
  if (resolution?.record?.order !== 1) fail(`record.order=${resolution?.record?.order}`);
  console.log('E2E_EVIDENCE_OK');

  const support = await adapter.controlSupport('pause');
  if (support?.reason !== 'DEFERRED_NO_KERNEL_CONTROL_BUYER') fail(`control.support reason=${support?.reason}`);
  console.log('E2E_CONTROL_OK');

  console.log('E2E_ALL_OK');
  adapter.dispose();
  process.exit(0);
} catch (err) {
  fail(err?.message ?? String(err));
}
