/**
 * Adapter tests (PLUG-F2/F4/F5/F9/F10/F13/F14/F16 gates).
 * An in-process node:net fake DriverHost serves newline-delimited JSON-RPC,
 * exercising the real wire contract through the adapter.
 */
import test from 'node:test';
import assert from 'node:assert/strict';
import net from 'node:net';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const srcDir = join(here, '..', 'src');

const { UniClawAdapter } = await import(join(srcDir, 'adapter.js'));
const { UniClawRpcError, ERROR_CODES } = await import(join(srcDir, 'protocol.js'));

const RUN_ID = 'run-1';

/** Sentinel: the fake server must not write any response for this request. */
const NO_RESPONSE = Symbol('NO_RESPONSE');

/** Minimal fake DriverHost: line-framed JSON-RPC over TCP. */
function startFakeServer(handler) {
  return new Promise((resolve, reject) => {
    const server = net.createServer((socket) => {
      socket.setEncoding('utf8');
      let buffer = '';
      socket.on('data', (chunk) => {
        buffer += chunk;
        let idx;
        while ((idx = buffer.indexOf('\n')) >= 0) {
          const line = buffer.slice(0, idx);
          buffer = buffer.slice(idx + 1);
          if (!line) continue;
          let req;
          try {
            req = JSON.parse(line);
          } catch {
            socket.write(JSON.stringify({ jsonrpc: '2.0', id: null, error: { code: 'bad_request', message: 'malformed' } }) + '\n');
            continue;
          }
          let result = null;
          let error = null;
          try {
            result = handler(req) ?? null;
          } catch (err) {
            error = { code: err.code || 'internal_error', message: err.message };
          }
          if (result === NO_RESPONSE) continue;
          socket.write(JSON.stringify(error
            ? { jsonrpc: '2.0', id: req.id, error }
            : { jsonrpc: '2.0', id: req.id, result }) + '\n');
        }
      });
      socket.on('error', () => {});
    });
    server.listen(0, '127.0.0.1', () => resolve(server));
    server.on('error', reject);
  });
}

function freePort(server) {
  return server.address().port;
}

function snapshotResult() {
  return {
    runId: RUN_ID,
    runState: { value: 'completed', classification: 'directPublicProjection', truthSource: 'Agent.State (public read model)', isPartial: false },
    currentSemanticPage: { value: null, classification: 'notCurrentlyAvailable', truthSource: null, isPartial: false },
    activeTrap: { value: null, classification: 'directPublicProjection', truthSource: 'Agent.LastTrap (public read model)', isPartial: false },
    currentGoal: { value: 'WifiConnectivity.Enabled=true', classification: 'derivedReadModel', truthSource: 'RunSemanticGoal (derived read model)', isPartial: false },
    lastDecision: { value: null, classification: 'notCurrentlyAvailable', truthSource: null, isPartial: false },
    lastAction: { value: null, classification: 'notCurrentlyAvailable', truthSource: null, isPartial: false },
    recoveryState: { value: null, classification: 'notCurrentlyAvailable', truthSource: null, isPartial: false },
    latestGoalEvidence: { value: null, classification: 'notCurrentlyAvailable', truthSource: null, isPartial: false },
    currentObservationSequence: { value: null, classification: 'notCurrentlyAvailable', truthSource: null, isPartial: false },
    currentContainerSummary: { value: null, classification: 'notCurrentlyAvailable', truthSource: null, isPartial: false },
    bindingsSummary: { value: null, classification: 'notCurrentlyAvailable', truthSource: null, isPartial: false },
    stateBeliefsSummary: { value: null, classification: 'notCurrentlyAvailable', truthSource: null, isPartial: false },
    diagnostics: [],
  };
}

function eventsPage(cursor) {
  const start = cursor ? cursor.lastSequence + 1 : 1;
  const events = [];
  for (let i = start; i <= 3; i += 1) {
    events.push({
      eventId: `evt-${RUN_ID}-${i}`,
      runId: RUN_ID,
      sequence: i,
      kind: 'Navigation/StepCompleted',
      correlationId: null,
      causationId: null,
      observationSequence: i,
      evidenceRefs: [],
      payload: null,
    });
  }
  return { events, nextCursor: { runId: RUN_ID, lastSequence: 3 }, hasMore: false, diagnostic: null };
}

function defaultHandler(req) {
  switch (req.method) {
    case 'ping':
      return { service: 'dsh-uniclaw-driverhost', protocolVersion: 1, baselineChange: 'dsh-uniclaw-control-plane-protocol-baseline' };
    case 'run.list':
      return { runIds: [RUN_ID] };
    case 'run.snapshot.get':
      return snapshotResult();
    case 'run.trap.get':
      return { runId: req.params.runId, found: false, trap: null, diagnostic: null };
    case 'run.events.after':
      return eventsPage(req.params?.cursor);
    case 'run.events.drain':
      return eventsPage(req.params?.cursor);
    case 'evidence.get':
      return { found: false, diagnostic: 'no evidence catalog registered' };
    case 'control.support':
      return { operation: req.params.operation, supported: false, reason: 'DEFERRED_NO_KERNEL_CONTROL_BUYER', evidence: ['audit'], readOnly: false };
    default:
      throw Object.assign(new Error(`unknown method ${req.method}`), { code: 'unknown_method' });
  }
}

async function withServer(t, handler = defaultHandler, fn) {
  const server = await startFakeServer(handler);
  const adapter = new UniClawAdapter({ port: freePort(server), maxAttempts: 2, backoffMs: 10 });
  try {
    await fn(server, adapter);
  } finally {
    adapter.dispose();
    await new Promise((resolve) => server.close(resolve));
  }
}

test('F4: connection lifecycle is observable', async (t) => {
  await withServer(t, undefined, async (server, adapter) => {
    const states = [];
    adapter.onConnectionChange = (s) => states.push(s);
    await adapter.ensureConnected();
    assert.equal(adapter.getState(), 'connected');
    adapter.disconnect();
    assert.equal(adapter.getState(), 'disconnected');
    assert.ok(states.includes('connecting') && states.includes('connected') && states.includes('disconnected'));
  });
});

test('ping returns the frozen service identity', async (t) => {
  await withServer(t, undefined, async (server, adapter) => {
    await adapter.ensureConnected();
    const ping = await adapter.ping();
    assert.equal(ping.service, 'dsh-uniclaw-driverhost');
    assert.equal(ping.protocolVersion, 1);
    assert.equal(ping.baselineChange, 'dsh-uniclaw-control-plane-protocol-baseline');
  });
});

test('snapshot classification survives the wire (F8/F9)', async (t) => {
  await withServer(t, undefined, async (server, adapter) => {
    await adapter.ensureConnected();
    const snapshot = await adapter.getRunSnapshot(RUN_ID);
    assert.equal(snapshot.runState.classification, 'directPublicProjection');
    assert.equal(snapshot.currentGoal.classification, 'derivedReadModel');
    assert.equal(snapshot.lastDecision.classification, 'notCurrentlyAvailable');
    assert.equal(snapshot.lastDecision.value, null);
    assert.equal(snapshot.currentGoal.value, 'WifiConnectivity.Enabled=true');
  });
});

test('cursor semantics: only newer events are returned (F10)', async (t) => {
  await withServer(t, undefined, async (server, adapter) => {
    await adapter.ensureConnected();
    const first = await adapter.getRuntimeEvents(RUN_ID);
    assert.equal(first.events.length, 3);
    assert.equal(first.events[0].sequence, 1);
    const second = await adapter.getRuntimeEvents(RUN_ID, { runId: RUN_ID, lastSequence: 1 });
    assert.equal(second.events.length, 2);
    assert.equal(second.events[0].sequence, 2);
    assert.equal(second.nextCursor.lastSequence, 3);
  });
});

test('event identity is stable across the wire (F11)', async (t) => {
  await withServer(t, undefined, async (server, adapter) => {
    await adapter.ensureConnected();
    const page = await adapter.getRuntimeEvents(RUN_ID);
    const first = page.events[0];
    assert.equal(first.eventId, `evt-${RUN_ID}-1`);
    assert.equal(first.sequence, 1);
    assert.equal(first.observationSequence, 1);
    assert.equal(first.runId, RUN_ID);
  });
});

test('evidence resolution reports no-catalog without fabrication (F12)', async (t) => {
  await withServer(t, undefined, async (server, adapter) => {
    await adapter.ensureConnected();
    const resolution = await adapter.getEvidence({ locator: 'capture:session-e2e:record:1', runId: RUN_ID });
    assert.equal(resolution.found, false);
    assert.ok(resolution.diagnostic);
  });
});

test('unknown run yields an unknown snapshot shape (F13)', async (t) => {
  await withServer(t, (req) => {
    if (req.method === 'run.snapshot.get') {
      return {
        runId: req.params.runId,
        runState: { value: null, classification: 'notCurrentlyAvailable', truthSource: null, isPartial: false },
        currentSemanticPage: { value: null, classification: 'notCurrentlyAvailable', truthSource: null, isPartial: false },
        activeTrap: { value: null, classification: 'notCurrentlyAvailable', truthSource: null, isPartial: false },
        currentGoal: { value: null, classification: 'notCurrentlyAvailable', truthSource: null, isPartial: false },
        lastDecision: { value: null, classification: 'notCurrentlyAvailable', truthSource: null, isPartial: false },
        lastAction: { value: null, classification: 'notCurrentlyAvailable', truthSource: null, isPartial: false },
        recoveryState: { value: null, classification: 'notCurrentlyAvailable', truthSource: null, isPartial: false },
        latestGoalEvidence: { value: null, classification: 'notCurrentlyAvailable', truthSource: null, isPartial: false },
        currentObservationSequence: { value: null, classification: 'notCurrentlyAvailable', truthSource: null, isPartial: false },
        currentContainerSummary: { value: null, classification: 'notCurrentlyAvailable', truthSource: null, isPartial: false },
        bindingsSummary: { value: null, classification: 'notCurrentlyAvailable', truthSource: null, isPartial: false },
        stateBeliefsSummary: { value: null, classification: 'notCurrentlyAvailable', truthSource: null, isPartial: false },
        diagnostics: ['run not registered'],
      };
    }
    return defaultHandler(req);
  }, async (server, adapter) => {
    await adapter.ensureConnected();
    const snapshot = await adapter.getRunSnapshot('run-unknown');
    assert.equal(snapshot.runState.classification, 'notCurrentlyAvailable');
    assert.equal(snapshot.runState.value, null);
    assert.ok(snapshot.diagnostics.includes('run not registered'));
  });
});

test('typed protocol errors surface as UniClawRpcError (F14)', async (t) => {
  await withServer(t, undefined, async (server, adapter) => {
    await adapter.ensureConnected();
    const err = await adapter.controlSupport('no-such-op').then(
      () => null,
      (e) => e,
    );
    // default handler returns a result for any operation; force a server error instead
  });
  // Second scenario: server rejects with a typed error.
  await withServer(t, (req) => {
    throw Object.assign(new Error('boom'), { code: 'internal_error' });
  }, async (server, adapter) => {
    await adapter.ensureConnected();
    await assert.rejects(adapter.ping(), (err) => {
      assert.ok(err instanceof UniClawRpcError);
      assert.equal(err.code, 'internal_error');
      return true;
    });
  });
});

test('connection refused maps to typed DRIVERHOST_DISCONNECTED (F5)', async () => {
  // Find a port with nothing listening.
  const server = await startFakeServer(() => null);
  const port = freePort(server);
  await new Promise((resolve) => server.close(resolve));

  const adapter = new UniClawAdapter({ port, maxAttempts: 2, backoffMs: 10 });
  try {
    await assert.rejects(adapter.ensureConnected(), (err) => {
      assert.ok(err instanceof UniClawRpcError);
      assert.equal(err.code, ERROR_CODES.DRIVERHOST_DISCONNECTED);
      return true;
    });
    assert.equal(adapter.getState(), 'error');
  } finally {
    adapter.dispose();
  }
});

test('requests while disconnected reject with DRIVERHOST_DISCONNECTED', async () => {
  const adapter = new UniClawAdapter({ port: 1, maxAttempts: 1, backoffMs: 5 });
  try {
    await assert.rejects(adapter.ping(), (err) => {
      assert.ok(err instanceof UniClawRpcError);
      assert.equal(err.code, ERROR_CODES.DRIVERHOST_DISCONNECTED);
      return true;
    });
  } finally {
    adapter.dispose();
  }
});

test('F16: reconnect returns a fresh full page, nothing cached across connections', async (t) => {
  const server = await startFakeServer((req) => {
    if (req.method === 'run.events.after' && req.params?.cursor) {
      // a cursor from a previous connection must never be honored: server has
      // no memory across connections, so only a fresh full page is possible
      return { events: [], nextCursor: { runId: RUN_ID, lastSequence: 0 }, hasMore: false, diagnostic: null };
    }
    return defaultHandler(req);
  });
  const adapter = new UniClawAdapter({ port: freePort(server), maxAttempts: 2, backoffMs: 10 });
  try {
    await adapter.ensureConnected();
    const firstSocket = adapter._socket;
    const first = await adapter.getRuntimeEvents(RUN_ID);
    assert.equal(first.events.length, 3);
    adapter.disconnect();
    await adapter.ensureConnected();
    assert.notEqual(adapter._socket, firstSocket, 'reconnect must establish a new socket (fresh state)');
    assert.equal(adapter.getState(), 'connected');
    const second = await adapter.getRuntimeEvents(RUN_ID);
    assert.equal(second.events.length, 3, 'fresh connection must return the full page (no cross-connection cursor state)');
  } finally {
    adapter.dispose();
    await new Promise((resolve) => server.close(resolve));
  }
});

test('timeout maps to DRIVERHOST_DISCONNECTED and closes the socket', async (t) => {
  const server = await startFakeServer(() => NO_RESPONSE); // never responds
  const adapter = new UniClawAdapter({ port: freePort(server), timeoutMs: 50, maxAttempts: 1, backoffMs: 5 });
  try {
    await adapter.ensureConnected();
    await assert.rejects(adapter.ping(), (err) => {
      assert.ok(err instanceof UniClawRpcError);
      assert.equal(err.code, ERROR_CODES.DRIVERHOST_DISCONNECTED);
      return true;
    });
    assert.notEqual(adapter.getState(), 'connected');
  } finally {
    adapter.dispose();
    await new Promise((resolve) => server.close(resolve));
  }
});
