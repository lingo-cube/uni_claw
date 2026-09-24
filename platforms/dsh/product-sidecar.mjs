#!/usr/bin/env node

// AGT-002 headless sidecar skeleton. It is intentionally a line-delimited
// JSON-RPC process with one approved Product capability. A real provider can be
// attached behind consult without adding a Product tool or authority surface.
import fs from 'node:fs';
import readline from 'node:readline';

const protocolVersion = 'uniclaw.agent.protocol.v1';
const schemaVersion = 'uniclaw.agent.schema.v1';
const schemaHash = process.env.UNICLAW_SCHEMA_HASH ?? '';
const capabilities = ['submit_decision'];
const sessionId = process.env.DSH_SESSION_ID ?? `dsh-sidecar-${process.pid}`;
const replay = process.env.DSH_REPLAY_FILE && fs.existsSync(process.env.DSH_REPLAY_FILE)
  ? JSON.parse(fs.readFileSync(process.env.DSH_REPLAY_FILE, 'utf8')) : {};

const output = (id, result, error) => {
  const body = { jsonrpc: '2.0', id };
  if (error) body.error = { code: -32000, message: error };
  else body.result = result;
  process.stdout.write(`${JSON.stringify(body)}\n`);
};

const rl = readline.createInterface({ input: process.stdin, crlfDelay: Infinity });
rl.on('line', (line) => {
  let request;
  try { request = JSON.parse(line); } catch { return; }
  const { id, method, params = {} } = request;
  if (method === 'initialize') {
    const accepted = params.protocol?.protocolVersion === protocolVersion
      && params.protocol?.schemaVersion === schemaVersion
      && (!schemaHash || params.protocol?.schemaHash === schemaHash)
      && JSON.stringify(params.expectedCapabilities?.capabilities ?? [])
        === JSON.stringify(capabilities);
    output(id, {
      accepted,
      protocol: { protocolVersion, schemaVersion, schemaHash },
      reportedCapabilities: { capabilities },
      dshSessionId: sessionId,
      failureReason: accepted ? null : 'sidecar-handshake-mismatch',
    });
    return;
  }
  if (method === 'abort_current_turn') {
    output(id, {});
    return;
  }
  if (method === 'consult') {
    const decision = replay[params.context?.decisionId];
    output(id, {
      requestId: params.requestId,
      generation: params.generation,
      decision: decision ?? null,
      error: decision ? null : 'provider-not-configured',
    });
    return;
  }
  output(id, null, 'method-not-allowed');
});
