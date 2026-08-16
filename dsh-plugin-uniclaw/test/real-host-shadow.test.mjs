/**
 * REAL pinned-DSH Shadow Cognition integration test (F1–F16 real-host proof).
 *
 * Boots the REAL pinned DSH host (`boot()` from `@deepseek-ai/dsh-app-boot`,
 * real vendored cordis 4.0.1 + real loader) with THREE rows — the real
 * `@deepseek-ai/dsh-commands` registry, the real `@deepseek-ai/dsh-llm`
 * (LlmRuntime), and `dsh-plugin-uniclaw` configured with
 * `shadow.model.provider=test-shadow`. The shadow command is then executed
 * through the REAL registry end to end:
 *
 *   commands.execute(agent, '/uniclaw-shadow-analyze run-1 --focus trap ...')
 *     → plugin shadow handler → runShadowAnalysis
 *     → uniclaw service → adapter → loopback DriverHost RPC fixture
 *     → bounded context assembly → ONE real `ctx.llm.stream()` call through
 *       the REAL LlmRuntime with a REAL LlmAdapter subclass registered via
 *       `registerAdapter(['test-shadow'], fake)` → ShadowAnalysis artifact.
 *
 * The agent is a REAL detached `Session` (`Session.create(SessionId(...))`),
 * so the command response carries a TRUTHFUL DSH session identity and the
 * post-execute `session.events` view proves F15: only `command/run` +
 * `command/done` were appended — zero custom shadow events, zero persistence.
 *
 * Kernel non-mutation (F8): the fixture records every RPC method requested;
 * after the whole scenario the requested set must be a subset of the six
 * read-only methods. No run.pause/stop/abort, no action.*, no adb.*, no
 * run.events.drain, no control.support.
 *
 * The LLM seam is exercised three ways against the REAL LlmRuntime:
 *  1. registered adapter  → status success, 1 call, provider/model forwarded;
 *  2. handle.dispose()    → NO_ADAPTER finish chunk → status not-configured
 *     + model-unavailable (deterministic digest, ModelCalls 0);
 *  3. full ctx dispose + fresh boot → cache starts empty, fresh analysis
 *     (EPHEMERAL_PROCESS_LOCAL: restart means truthful loss, no fake history).
 *
 * Pinned DSH checkout: READ-ONLY. Verified HEAD and empty porcelain before
 * boot; the test never writes into it. Override the checkout with
 * `DSH_PINNED_REPO` (developer default = the local pinned checkout).
 */
import test from 'node:test';
import assert from 'node:assert/strict';
import net from 'node:net';
import { execFileSync } from 'node:child_process';
import { mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const repoRoot = join(here, '..', '..');

const PINNED_HEAD = '47f943859bef60e4160492346772ded9b24f765a';
const PIN = process.env.DSH_PINNED_REPO ?? '/Users/fran/Documents/Code/dk-harness';

const RUN_ID = 'run-1';
const SHADOW_SESSION_ID = 'session-shadow-real';
/** The six read-only wire methods the shadow slice is allowed to use. */
const READ_ONLY_METHODS = new Set([
  'ping', 'run.list', 'run.snapshot.get', 'run.events.after', 'run.trap.get', 'evidence.get',
]);
/** Wire methods that would constitute Kernel/DriverHost mutation if requested. */
const MUTATING_METHOD_PATTERN = /^(run\.(pause|stop|abort|resume|start|events\.drain|snapshot\.set)|action\.|adb\.|control\.)/;

/** Classified camelCase snapshot DTO as served by the DriverHost fixture. */
function snapshotDto() {
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

function trapDto() {
  return {
    runId: RUN_ID,
    found: true,
    trap: {
      value: {
        kind: 'StateMismatch',
        scope: 'wifi',
        expected: 'WifiConnectivity.Enabled=true',
        observed: 'WifiConnectivity.Enabled=false',
        source: 'observation:seq-1',
        evidence: ['capture:shadow:record:1'],
        lastActionDescription: 'tap switch toggle',
      },
      classification: 'directPublicProjection',
    },
    diagnostic: null,
  };
}

function evidenceDto() {
  return {
    found: true,
    ref: { locator: 'capture:shadow:record:1', kind: 'TraceFragment', runId: RUN_ID, observationSequence: 1 },
    captureSessionId: 'capture-shadow-1',
    record: { order: 1, kind: 'TraceFragment' },
    artifact: { artifactId: 'art-shadow-1', frameId: null, fileName: 'trace-1.bin', contentHash: 'sha256:abc', byteCount: 128 },
    diagnostic: null,
  };
}

function eventsPageDto() {
  return {
    runId: RUN_ID,
    events: [
      {
        eventId: 'evt-trap-1', runId: RUN_ID, sequence: 3, kind: 'TrapRaised',
        correlationId: null, causationId: null, observationSequence: 1,
        evidenceRefs: [{ locator: 'capture:shadow:record:1', kind: 'TraceFragment', runId: RUN_ID, observationSequence: 1, contentIdentity: null, maturity: 'Captured', sizeBytes: 128 }],
        payload: { trapKind: 'StateMismatch' },
      },
      {
        eventId: 'evt-failed-1', runId: RUN_ID, sequence: 5, kind: 'RunFailed',
        correlationId: null, causationId: null, observationSequence: null,
        evidenceRefs: [],
        payload: { cause: 'agent' },
      },
    ],
    nextCursor: { runId: RUN_ID, lastSequence: 5 },
    hasMore: false,
    diagnostics: [],
  };
}

/**
 * Deterministic wire-conformant DriverHost stand-in. Records every method the
 * plugin requests so the F8 no-mutation proof is an assertion over the ACTUAL
 * wire traffic, not an assumption.
 */
function startFixture() {
  const state = { connections: 0, sockets: new Set(), port: 0, server: null, methods: [] };
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
          state.methods.push(method);
          let result;
          if (method === 'ping') {
            result = { protocolVersion: 1, serviceName: 'dsh-uniclaw-driverhost' };
          } else if (method === 'run.list') {
            result = { runs: [{ runId: RUN_ID, state: 'completed' }] };
          } else if (method === 'run.snapshot.get') {
            result = snapshotDto();
          } else if (method === 'run.trap.get') {
            result = trapDto();
          } else if (method === 'run.events.after') {
            result = eventsPageDto();
          } else if (method === 'evidence.get') {
            result = evidenceDto();
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
    throw new Error(`PINNED_DSH_TEST_ENVIRONMENT_UNAVAILABLE: pinned checkout HEAD ${head} != ${PINNED_HEAD}`);
  }
  if (porcelain !== '') {
    throw new Error('PINNED_DSH_TEST_ENVIRONMENT_UNAVAILABLE: pinned checkout is not clean (porcelain non-empty)');
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

function writeComposition(tempDir, fixturePort) {
  const configPath = join(tempDir, 'cordis.yml');
  writeFileSync(configPath, [
    '# real pinned-DSH shadow cognition composition (generated at test runtime)',
    '- id: commands',
    `  name: ${join(PIN, 'packages', 'interaction', 'commands', 'lib', 'index.js')}`,
    '- id: llm',
    `  name: ${join(PIN, 'packages', 'llm', 'llm', 'lib', 'index.js')}`,
    '- id: dsh-plugin-uniclaw',
    `  name: ${join(repoRoot, 'dsh-plugin-uniclaw', 'src', 'plugin.js')}`,
    '  config:',
    '    host: 127.0.0.1',
    `    port: ${fixturePort}`,
    '    shadow:',
    '      model:',
    '        provider: test-shadow',
    '        model: fake-1',
    '',
  ].join('\n'));
  return configPath;
}

/** REAL LlmAdapter subclass: emits one deterministic text-delta + success finish. */
function makeFakeAdapter() {
  const { LlmAdapter } = awaitImportedLlm;
  class FakeAdapter extends LlmAdapter {
    constructor() {
      super();
      this.calls = 0;
      this.seenOptions = null;
      this._disposed = false;
    }

    resolveModel(provider, model, signal) {
      return Promise.resolve({ provider, id: model, name: model });
    }

    async *stream(options) {
      this.calls += 1;
      this.seenOptions = options;
      const text = JSON.stringify({
        humanSummary: 'The run completed after a trap was raised. The failure cause is a shadow hypothesis, not a Kernel fact.',
        hypotheses: [
          { claim: 'the switch tap likely missed its target', supportingRefs: ['evt-trap-1'], flaggedUncertain: true },
        ],
        uncertainties: [],
        recommendations: [{ text: 'inspect the switch state on the device' }],
      });
      yield { type: 'text-delta', index: 0, text };
      yield { type: 'finish', reason: { kind: 'success' } };
    }
  }
  return new FakeAdapter();
}

// The real LlmRuntime/LlmAdapter exports are imported lazily inside the test
// (after the pinned checkout is verified); a module-level placeholder keeps
// the adapter factory simple.
let awaitImportedLlm = null;

test('real pinned DSH host: uniclaw-shadow-analyze through real commands + real ctx.llm seam', async (t) => {
  verifyPinnedCheckout();

  const fixture = await startFixture();
  t.after(() => closeFixture(fixture));

  const tempDir = mkdtempSync(join(tmpdir(), 'dsh-uniclaw-shadow-'));
  t.after(() => {
    try {
      rmSync(tempDir, { recursive: true, force: true });
    } catch {
      // best-effort self-cleanup
    }
  });

  process.env.DSH_PLUGIN_CORDIS_PACKAGE_JSON = join(PIN, 'vendor', 'cordis', 'package.json');

  const { boot } = await import(pathToFileURL(join(PIN, 'packages', 'boot', 'app-boot', 'lib', 'index.js')).href);
  const { Session, SessionId } = await import(pathToFileURL(join(PIN, 'packages', 'core', 'session', 'lib', 'index.js')).href);
  const { LlmAdapter } = await import(pathToFileURL(join(PIN, 'packages', 'llm', 'llm', 'lib', 'index.js')).href);
  awaitImportedLlm = { LlmAdapter };

  const configPath = writeComposition(tempDir, fixture.port);

  const state = {};
  let firstAdapter = null;
  try {
    state.ctx = await boot('dsh-uniclaw-shadow-test', configPath);
  } catch (err) {
    throw new Error(`real boot failed: ${err instanceof Error ? err.message : String(err)}`);
  }
  t.after(async () => {
    await state.ctx?.fiber?.dispose();
    const adapter = state.ctx?.get?.('uniclaw')?.adapter;
    if (adapter) {
      assert.equal(adapter._disposed, true, 'adapter disposed by plugin effect disposer');
    }
  });

  await waitFor(
    () => state.ctx.get('uniclaw')?.adapter?.state === 'connected',
    'adapter handshake with the DriverHost fixture',
  );
  firstAdapter = state.ctx.get('uniclaw').adapter;

  // Register the REAL LlmRuntime adapter seam for provider test-shadow.
  const llm = state.ctx.get('llm');
  assert.ok(llm && typeof llm.registerAdapter === 'function', 'real LlmRuntime exposes registerAdapter');
  const fake = makeFakeAdapter();
  const handle = llm.registerAdapter(['test-shadow'], fake);

  const commands = state.ctx.get('commands');
  const agentSession = Session.create(SessionId(SHADOW_SESSION_ID));
  assert.equal(agentSession.id, SHADOW_SESSION_ID, 'real detached Session carries the truthful identity');

  await t.test('ShadowCommandEndToEnd: success path through real registry, real LlmRuntime, real Session', async () => {
    const executed = await commands.execute(
      { session: agentSession },
      '/uniclaw-shadow-analyze run-1 --focus trap --reason verify shadow reads only',
      new AbortController().signal,
    );
    assert.equal(executed?.result?.kind, 'success', 'shadow command returned a success CommandResult');

    const text = String(executed?.result?.text ?? '');
    assert.match(text, /shadow analysis: shadow-run-1-1/, 'first analysis id minted');
    assert.match(text, /classification: COGNITIVE_INFERENCE/, 'artifact classification surfaced');
    assert.match(text, /runId: run-1/, 'run identity surfaced');
    assert.match(text, new RegExp(`sessionId: ${SHADOW_SESSION_ID}`), 'TRUTHFUL DSH session identity surfaced (sessionId != runId)');
    assert.ok(!text.includes(`sessionId: run-1`), 'session identity is never conflated with the run id');
    assert.match(text, /model call: success \(test-shadow\/fake-1, 2 events/, 'one real model call with provider/model + bounded event count');

    assert.equal(fake.calls, 1, 'ModelCalls = 1 for the success path');
    assert.equal(fake.seenOptions?.provider, 'test-shadow', 'provider forwarded to the adapter');
    assert.equal(fake.seenOptions?.model, 'fake-1', 'model forwarded to the adapter');
    assert.ok(Array.isArray(fake.seenOptions?.messages), 'a bounded user message reached the adapter');
    const userText = fake.seenOptions.messages.find((m) => m.role === 'user')?.content?.[0]?.text ?? '';
    assert.ok(userText.includes('runState: "completed"'), 'bounded read-model context reached the model');
    assert.ok(userText.includes('TrapRaised'), 'bounded kernel-fact context reached the model');

    // Epistemic labels on the human surface (F13).
    assert.match(text, /\[kernel-fact\] TrapRaised/, 'kernel-fact observed fact labeled');
    assert.match(text, /\[derived-read-model\]/, 'derived-read-model fact labeled');
    assert.match(text, /\[shadow-inference\]/, 'hypothesis labeled non-authoritative');
    assert.match(text, /\[human-investigation\]/, 'recommendation targets human investigation only');

    // F15: the real session log carries ONLY command lifecycle events.
    const eventTypes = agentSession.events.map((e) => e.type);
    assert.deepEqual(eventTypes, ['command/run', 'command/done'], 'zero custom shadow events, zero persistence writes');

    // Bounded process-local cache holds the fresh artifact.
    const cached = state.ctx.get('shadow').cache.get('run-1');
    assert.ok(cached && cached.analysisId === 'shadow-run-1-1', 'bounded cache holds the fresh analysis');
    assert.ok(cached.classification === 'COGNITIVE_INFERENCE', 'cached artifact is classified');
  });

  await t.test('F8WireProof: only read-only methods were ever requested of DriverHost', () => {
    for (const method of fixture.methods) {
      assert.ok(READ_ONLY_METHODS.has(method), `wire method ${method} is within the frozen read-only surface`);
      assert.ok(!MUTATING_METHOD_PATTERN.test(method), `no mutating wire method (${method})`);
    }
    assert.ok(fixture.methods.includes('run.snapshot.get'), 'snapshot retrieval exercised');
    assert.ok(fixture.methods.includes('run.events.after'), 'bounded event retrieval exercised');
    assert.ok(fixture.methods.includes('run.trap.get'), 'trap retrieval exercised (trap focus)');
    assert.ok(fixture.methods.includes('evidence.get'), 'bounded evidence resolution exercised (trap focus)');
  });

  await t.test('SecondInvocation: fresh analysis id, one more model call, log grows by two lifecycle events', async () => {
    const executed = await commands.execute(
      { session: agentSession },
      '/uniclaw-shadow-analyze run-1 --focus completion',
      new AbortController().signal,
    );
    assert.equal(executed?.result?.kind, 'success', 'second shadow command succeeded');
    assert.match(String(executed?.result?.text ?? ''), /shadow analysis: shadow-run-1-2/, 'per-session monotonic analysis id');
    assert.equal(fake.calls, 2, 'one additional model call');
    const eventTypes = agentSession.events.map((e) => e.type);
    assert.deepEqual(eventTypes, ['command/run', 'command/done', 'command/run', 'command/done'], 'still only command lifecycle events');
    const cached = state.ctx.get('shadow').cache.get('run-1');
    assert.equal(cached?.analysisId, 'shadow-run-1-2', 'cache refreshed to the newest bounded artifact');
  });

  await t.test('ModelUnavailableAfterDispose: NO_ADAPTER → not-configured + model-unavailable, zero calls, digest stands in', async () => {
    // LlmRuntime.registerAdapter returns a CALLABLE disposer (invoking it
    // releases the routes; there is no .dispose() method).
    handle();
    const executed = await commands.execute(
      { session: agentSession },
      '/uniclaw-shadow-analyze run-1 --focus failure',
      new AbortController().signal,
    );
    assert.equal(executed?.result?.kind, 'success', 'command stays truthful and usable without a model');
    const text = String(executed?.result?.text ?? '');
    assert.match(text, /shadow analysis: shadow-run-1-3/, 'analysis still produced');
    assert.match(text, /model call: not-configured/, 'ModelCalls = 0 surfaced truthfully');
    assert.match(text, /model-unavailable/, 'model-unavailable uncertainty surfaced');
    assert.match(text, /runState=completed/, 'deterministic read-model digest stands in');
    assert.equal(fake.calls, 2, 'no additional model call attempted after dispose');
  });

  await t.test('RestartIsTruthfulLoss: fresh boot → empty cache → fresh analysis, no reconstruction', async () => {
    await state.ctx.fiber.dispose();
    assert.equal(firstAdapter._disposed, true, 'first-boot adapter disposed by the plugin effect disposer');
    state.ctx = null;

    // Fresh boot against the same fixture: EPHEMERAL_PROCESS_LOCAL means the
    // previous cache is gone and no history is reconstructed.
    const state2 = {};
    try {
      state2.ctx = await boot('dsh-uniclaw-shadow-test-2', configPath);
    } catch (err) {
      throw new Error(`second real boot failed: ${err instanceof Error ? err.message : String(err)}`);
    }
    t.after(async () => {
      await state2.ctx?.fiber?.dispose();
      const adapter = state2.ctx?.get?.('uniclaw')?.adapter;
      if (adapter) {
        assert.equal(adapter._disposed, true, 'second-boot adapter disposed by plugin effect disposer');
      }
    });
    await waitFor(
      () => state2.ctx.get('uniclaw')?.adapter?.state === 'connected',
      'second-boot adapter handshake',
    );

    assert.equal(state2.ctx.get('shadow').cache.get('run-1'), undefined, 'restart means truthful loss: cache empty');

    const llm2 = state2.ctx.get('llm');
    const fake2 = makeFakeAdapter();
    const handle2 = llm2.registerAdapter(['test-shadow'], fake2);

    const freshSession = Session.create(SessionId(`${SHADOW_SESSION_ID}-2`));
    const executed = await state2.ctx.get('commands').execute(
      { session: freshSession },
      '/uniclaw-shadow-analyze run-1 --focus progress',
      new AbortController().signal,
    );
    assert.equal(executed?.result?.kind, 'success', 'post-restart command succeeded');
    const text = String(executed?.result?.text ?? '');
    // The analysis-id sequence is a process-local module counter (id namespace
    // is EPHEMERAL_PROCESS_LOCAL like everything else): a fresh boot inside the
    // SAME node process continues the monotonic sequence (4 here), while a true
    // process restart would reset it. Uniqueness per session is never weakened.
    assert.match(text, /shadow analysis: shadow-run-1-4/, 'fresh analysis id minted on the restarted runtime (no reconstruction of the old one)');
    assert.match(text, new RegExp(`sessionId: ${SHADOW_SESSION_ID}-2`), 'truthful fresh session identity');
    assert.equal(fake2.calls, 1, 'fresh model call on the fresh runtime');
    assert.equal(state2.ctx.get('shadow').cache.get('run-1')?.analysisId, 'shadow-run-1-4', 'fresh artifact cached in the new process-local cache');
  });
});
