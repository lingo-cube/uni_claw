/**
 * Commands tests (PLUG-F7 gate): the four deterministic commands format
 * classified read-only data and never call any inference service. Handlers
 * are invoked with the dsh-commands CommandInvocation shape.
 */
import test from 'node:test';
import assert from 'node:assert/strict';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const srcDir = join(here, '..', 'src');

const { buildCommands, formatSnapshot, parseShadowInvocation, formatShadowAnalysis } = await import(join(srcDir, 'commands.js'));
const { UniClawRpcError, ERROR_CODES } = await import(join(srcDir, 'protocol.js'));

const RUN_ID = 'run-1';

function snapshot() {
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

function invocation(rawInput, agent) {
  return { commandId: 'test-cmd', agent: agent ?? {}, rawInput, signal: new AbortController().signal };
}

function mockAdapter(overrides) {
  return {
    getRunSnapshot: async () => snapshot(),
    getTrap: async () => ({ runId: RUN_ID, found: false, trap: null, diagnostic: null }),
    getEvidence: async () => ({ found: false, diagnostic: 'no evidence catalog registered' }),
    listRuns: async () => ({ runIds: [RUN_ID] }),
    controlSupport: async () => ({ operation: 'pause', supported: false, reason: 'DEFERRED_NO_KERNEL_CONTROL_BUYER', evidence: ['audit'], readOnly: false }),
    ...overrides,
  };
}

/** Wire-shaped bounded event page (no timestamps; complete remainder). */
function eventPage(events) {
  return { runId: RUN_ID, events, nextCursor: { runId: RUN_ID, lastSequence: events.length }, hasMore: false, diagnostics: [] };
}

function trapEvent() {
  return {
    eventId: 'evt-trap-1',
    runId: RUN_ID,
    sequence: 3,
    kind: 'TrapRaised',
    correlationId: null,
    causationId: null,
    observationSequence: null,
    evidenceRefs: [{ locator: 'capture:shadow:record:1', kind: 'TraceFragment', runId: RUN_ID, observationSequence: 1, contentIdentity: null, maturity: 'Captured', sizeBytes: 128, locator: 'capture:shadow:record:1' }],
    payload: { trapKind: 'StateMismatch' },
  };
}

function failedRunEvent() {
  return {
    eventId: 'evt-failed-1',
    runId: RUN_ID,
    sequence: 5,
    kind: 'RunFailed',
    correlationId: null,
    causationId: null,
    observationSequence: null,
    evidenceRefs: [],
    payload: { cause: 'agent' },
  };
}

/** A plain-object fake llm (unit level; the real LlmRuntime seam is exercised by real-host-shadow). */
function fakeLlm({ text } = {}) {
  let calls = 0;
  return {
    get calls() { return calls; },
    stream: async function* (options) {
      calls += 1;
      if (text !== undefined) yield { type: 'text-delta', index: 0, text };
      yield { type: 'finish', reason: { kind: 'success' } };
    },
  };
}

/** A shadow command context: narrowed read-only facade + fake llm + config + cache. */
function shadowContextFixture(overrides) {
  const config = {
    enabled: true,
    model: { provider: 'test-shadow', model: 'fake-1' },
    autoTriggers: [],
    maxEvents: 200,
    maxContextChars: 80000,
    maxEvidenceRefs: 8,
    evidenceBytesPerRef: 8192,
    timeoutMs: 60000,
    visual: { enabled: false },
  };
  const adapter = mockAdapter({
    getRuntimeEvents: async () => eventPage([trapEvent(), failedRunEvent()]),
    ...(overrides?.adapter ?? {}),
  });
  const facade = {
    getRunSnapshot: (runId) => adapter.getRunSnapshot(runId),
    getRuntimeEvents: (runId) => adapter.getRuntimeEvents(runId, null),
    getTrap: (runId) => adapter.getTrap(runId),
    getEvidence: (ref) => adapter.getEvidence(ref),
  };
  const cache = { map: new Map(), set(runId, analysis) { this.map.set(runId, analysis); }, get(runId) { return this.map.get(runId); } };
  const llm = overrides?.llm === undefined ? fakeLlm({ text: '{"humanSummary":"the run completed","hypotheses":[{"claim":"the tap likely missed","supportingRefs":["evt-failed-1"],"flaggedUncertain":true}],"uncertainties":[],"recommendations":[{"text":"inspect the switch state"}]}' }) : overrides.llm;
  return {
    config,
    facade,
    llm,
    getLlm: () => llm,
    cache,
    calls: { reads: 0 },
  };
}

function byName(commands, name) {
  const def = commands.find((d) => d.name === name);
  assert.ok(def, `command ${name} must exist`);
  return def;
}

test('registers exactly the four read-only commands without shadow context (back-compat shape)', () => {
  const commands = buildCommands(mockAdapter());
  assert.deepEqual(commands.map((c) => c.name).sort(), ['uniclaw-evidence-open', 'uniclaw-inspect-run', 'uniclaw-inspect-trap', 'uniclaw-runs-list']);
  for (const def of commands) {
    assert.match(def.name, /^[a-z][a-z0-9_-]*$/);
    assert.ok(typeof def.description === 'string' && def.description.length > 0);
    assert.equal(typeof def.handler, 'function');
  }
});

test('registers exactly the five commands with shadow context', () => {
  const commands = buildCommands(mockAdapter(), shadowContextFixture());
  assert.deepEqual(commands.map((c) => c.name).sort(), [
    'uniclaw-evidence-open',
    'uniclaw-inspect-run',
    'uniclaw-inspect-trap',
    'uniclaw-runs-list',
    'uniclaw-shadow-analyze',
  ]);
  const shadow = byName(commands, 'uniclaw-shadow-analyze');
  assert.equal(shadow.recordInput, true);
  assert.match(shadow.description, /COGNITIVE_INFERENCE/);
});

test('inspect.run formats a classified snapshot', async () => {
  const def = byName(buildCommands(mockAdapter()), 'uniclaw-inspect-run');
  const result = await def.handler(invocation(` ${RUN_ID}`));
  assert.equal(result.kind, 'success');
  assert.match(result.text, new RegExp(`runId: ${RUN_ID}`));
  assert.match(result.text, /runState: "completed" \(directPublicProjection — Agent\.State/);
  assert.match(result.text, /currentGoal: "WifiConnectivity\.Enabled=true" \(derivedReadModel — RunSemanticGoal/);
  assert.match(result.text, /lastDecision: N\/A \(notCurrentlyAvailable\)/);
});

test('inspect.run rejects a missing run id', async () => {
  const def = byName(buildCommands(mockAdapter()), 'uniclaw-inspect-run');
  const result = await def.handler(invocation(' '));
  assert.equal(result.kind, 'error');
  assert.match(result.text, /usage: uniclaw-inspect-run/);
});

test('inspect.trap reports found=false for a trap-less run', async () => {
  const def = byName(buildCommands(mockAdapter()), 'uniclaw-inspect-trap');
  const result = await def.handler(invocation(` ${RUN_ID}`));
  assert.equal(result.kind, 'success');
  assert.match(result.text, /found: false/);
});

test('inspect.trap formats a found trap', async () => {
  const trap = {
    runId: RUN_ID,
    found: true,
    trap: {
      value: {
        kind: 'StateMismatch',
        scope: 'Agent',
        expected: 3,
        observed: 7,
        source: 'agent',
        evidence: 'observed=false expected=true',
        lastActionDescription: 'SetSwitch(1, true)',
      },
      classification: 'directPublicProjection',
    },
    diagnostic: null,
  };
  const def = byName(buildCommands(mockAdapter({ getTrap: async () => trap })), 'uniclaw-inspect-trap');
  const result = await def.handler(invocation(` ${RUN_ID}`));
  assert.equal(result.kind, 'success');
  assert.match(result.text, /kind: StateMismatch/);
  assert.match(result.text, /expected: 3/);
  assert.match(result.text, /observed: 7/);
  assert.match(result.text, /lastAction: SetSwitch\(1, true\)/);
});

test('evidence.open reports no-catalog without fabrication', async () => {
  const def = byName(buildCommands(mockAdapter()), 'uniclaw-evidence-open');
  const result = await def.handler(invocation(' capture:session-e2e:record:1 run-1'));
  assert.equal(result.kind, 'success');
  assert.match(result.text, /found: false/);
  assert.match(result.text, /diagnostic: no evidence catalog registered/);
});

test('evidence.open formats a resolved logical record', async () => {
  const resolution = {
    found: true,
    ref: { kind: 'TraceFragment', maturity: 'Captured' },
    captureSessionId: 'session-e2e',
    record: { order: 1, kind: 'Observation', sequenceNumber: 7, actionId: null },
    artifact: null,
    diagnostic: null,
  };
  const def = byName(buildCommands(mockAdapter({ getEvidence: async () => resolution })), 'uniclaw-evidence-open');
  const result = await def.handler(invocation(' capture:session-e2e:record:1 run-1'));
  assert.equal(result.kind, 'success');
  assert.match(result.text, /found: true/);
  assert.match(result.text, /record.order: 1/);
  assert.match(result.text, /record.sequenceNumber: 7/);
});

test('runs.list formats the registered run ids', async () => {
  const def = byName(buildCommands(mockAdapter()), 'uniclaw-runs-list');
  const result = await def.handler(invocation(''));
  assert.equal(result.kind, 'success');
  assert.match(result.text, new RegExp(`^${RUN_ID}$`));
});

test('runs.list handles the empty list', async () => {
  const def = byName(buildCommands(mockAdapter({ listRuns: async () => ({ runIds: [] }) })), 'uniclaw-runs-list');
  const result = await def.handler(invocation(''));
  assert.equal(result.kind, 'success');
  assert.match(result.text, /no runs registered/);
});

test('driverhost errors surface as typed messages', async () => {
  const disconnected = new UniClawRpcError(ERROR_CODES.DRIVERHOST_DISCONNECTED, 'not connected to DriverHost');
  const def = byName(buildCommands(mockAdapter({ getRunSnapshot: async () => { throw disconnected; } })), 'uniclaw-inspect-run');
  const result = await def.handler(invocation(` ${RUN_ID}`));
  assert.equal(result.kind, 'error');
  assert.match(result.text, /DriverHost error \[driverhost_disconnected\]: not connected to DriverHost/);
});

test('control operations are never dispatched by commands (read-only surface)', () => {
  const adapter = mockAdapter();
  const names = buildCommands(adapter).map((c) => c.name);
  // The command surface exposes no start/pause/resume/stop/abort handler.
  for (const control of ['start', 'pause', 'resume', 'stop', 'abort']) {
    assert.ok(!names.includes(control), `command ${control} must not exist`);
  }
});

test('formatSnapshot handles null input', () => {
  assert.equal(formatSnapshot(null), 'no snapshot data');
});

// ---- uniclaw-shadow-analyze ----

test('shadow.parse rejects malformed invocations deterministically', () => {
  assert.match(parseShadowInvocation('').error, /usage: uniclaw-shadow-analyze/);
  assert.match(parseShadowInvocation('run-1 --bogus x').error, /unknown argument "--bogus"/);
  assert.match(parseShadowInvocation('run-1 --focus nope').error, /unknown focus "nope"/);
  assert.match(parseShadowInvocation('run-1 --focus --reason x').error, /--focus requires a value/);
  assert.match(parseShadowInvocation('run-1 --focus trap --focus general').error, /duplicate --focus/);
  assert.match(parseShadowInvocation('run-1 --reason').error, /--reason requires text/);
  const parsed = parseShadowInvocation('run-9 --focus trap --reason what happened here');
  assert.equal(parsed.runId, 'run-9');
  assert.equal(parsed.focus, 'trap');
  assert.equal(parsed.reason, 'what happened here');
  // --reason consumes the remainder of the line (free text may contain '--')
  const remainder = parseShadowInvocation('run-9 --reason saw --something --weird');
  assert.equal(remainder.reason, 'saw --something --weird');
});

test('shadow refuses to invent a session identity', async () => {
  const def = byName(buildCommands(mockAdapter(), shadowContextFixture()), 'uniclaw-shadow-analyze');
  const result = await def.handler(invocation(` ${RUN_ID}`, { session: undefined }));
  assert.equal(result.kind, 'error');
  assert.match(result.text, /refusing to invent one/);
});

test('shadow honors the disabled config', async () => {
  const ctx = shadowContextFixture();
  ctx.config.enabled = false;
  const def = byName(buildCommands(mockAdapter(), ctx), 'uniclaw-shadow-analyze');
  const result = await def.handler(invocation(` ${RUN_ID}`, { session: { id: 'session-shadow-1' } }));
  assert.equal(result.kind, 'error');
  assert.match(result.text, /disabled by configuration/);
});

test('shadow produces a bounded COGNITIVE_INFERENCE analysis with distinct identities', async () => {
  const ctx = shadowContextFixture();
  const def = byName(buildCommands(mockAdapter(), ctx), 'uniclaw-shadow-analyze');
  const result = await def.handler(invocation(` ${RUN_ID} --focus trap --reason why`, { session: { id: 'session-shadow-1' } }));
  assert.equal(result.kind, 'success');
  assert.match(result.text, /classification: COGNITIVE_INFERENCE/);
  assert.match(result.text, new RegExp(`runId: ${RUN_ID}`));
  assert.match(result.text, /sessionId: session-shadow-1/);
  assert.match(result.text, /trigger: human\.request/);
  assert.match(result.text, /focus: trap/);
  assert.match(result.text, /model call: success \(test-shadow\/fake-1/);
  assert.match(result.text, /humanSummary: the run completed/);
  // epistemic labels: kernel fact cites the TrapRaised event, hypothesis is shadow-inference
  assert.match(result.text, /\[kernel-fact\] TrapRaised @seq 3 \(RuntimeEvent: evt-trap-1\)/);
  assert.match(result.text, /\[derived-read-model\] runState: "completed"/);
  assert.match(result.text, /\[shadow-inference\] the tap likely missed/);
  assert.match(result.text, /\[human-investigation\] inspect the switch state/);
  assert.match(result.text, /uncertainties:/);
  // F14: runId and sessionId are separate and never equal
  assert.notEqual(RUN_ID, 'session-shadow-1');
  // zero-model dispatch never sent the command to the model
  assert.equal(ctx.llm.calls, 1);
});

test('shadow degrades to a deterministic digest when the model is unavailable (ModelCalls=0)', async () => {
  const ctx = shadowContextFixture();
  ctx.llm = null;
  ctx.getLlm = () => null;
  const def = byName(buildCommands(mockAdapter(), ctx), 'uniclaw-shadow-analyze');
  const result = await def.handler(invocation(` ${RUN_ID}`, { session: { id: 'session-shadow-2' } }));
  assert.equal(result.kind, 'success');
  assert.match(result.text, /model call: not-configured/);
  assert.match(result.text, /uncertainties:/);
  assert.match(result.text, /model call: model-unavailable/);
  assert.match(result.text, /Model cognition not-configured/);
  assert.match(result.text, /classification: COGNITIVE_INFERENCE/);
});

test('shadow formats a model-timeout artifact truthfully', async () => {
  const ctx = shadowContextFixture();
  ctx.config.timeoutMs = 50;
  ctx.llm = {
    stream: async function* (options) {
      await new Promise((_, reject) => {
        options.signal.addEventListener('abort', () => reject(new Error('aborted by timeout')));
      });
    },
  };
  ctx.getLlm = () => ctx.llm;
  const def = byName(buildCommands(mockAdapter(), ctx), 'uniclaw-shadow-analyze');
  const result = await def.handler(invocation(` ${RUN_ID}`, { session: { id: 'session-shadow-3' } }));
  assert.equal(result.kind, 'success');
  assert.match(result.text, /model call: timeout/);
  assert.match(result.text, /model-timeout/);
});

test('shadow errors surface as shadow-level failures, never Kernel failures', async () => {
  const ctx = shadowContextFixture();
  ctx.llm = {
    stream: async function* () {
      throw new Error('provider exploded');
    },
  };
  ctx.getLlm = () => ctx.llm;
  const def = byName(buildCommands(mockAdapter(), ctx), 'uniclaw-shadow-analyze');
  const result = await def.handler(invocation(` ${RUN_ID}`, { session: { id: 'session-shadow-4' } }));
  assert.equal(result.kind, 'success');
  assert.match(result.text, /model call: error/);
  assert.match(result.text, /model-error/);
});

test('shadow command response exposes the bounded artifact fields', async () => {
  const ctx = shadowContextFixture();
  const def = byName(buildCommands(mockAdapter(), ctx), 'uniclaw-shadow-analyze');
  const result = await def.handler(invocation(` ${RUN_ID}`, { session: { id: 'session-shadow-5' } }));
  assert.equal(result.kind, 'success');
  assert.match(result.text, /^shadow analysis: shadow-run-1-\d+$/m);
  assert.match(result.text, /model call: success \(test-shadow\/fake-1, 2 events, \d+ chars\)/);
  // bounded-cache write: the run's analysis is retained (convenience only)
  assert.ok(ctx.cache.get(RUN_ID));
  assert.equal(ctx.cache.get(RUN_ID).runId, RUN_ID);
  assert.equal(ctx.cache.get(RUN_ID).classification, 'COGNITIVE_INFERENCE');
});

test('shadow runs twice produce fresh analyses and never dedupe human requests', async () => {
  const ctx = shadowContextFixture();
  const def = byName(buildCommands(mockAdapter(), ctx), 'uniclaw-shadow-analyze');
  const first = await def.handler(invocation(` ${RUN_ID}`, { session: { id: 'session-shadow-6' } }));
  const second = await def.handler(invocation(` ${RUN_ID}`, { session: { id: 'session-shadow-6' } }));
  assert.equal(first.kind, 'success');
  assert.equal(second.kind, 'success');
  const id1 = /shadow analysis: (shadow-run-1-\d+)/.exec(first.text)[1];
  const id2 = /shadow analysis: (shadow-run-1-\d+)/.exec(second.text)[1];
  assert.notEqual(id1, id2);
  assert.equal(ctx.llm.calls, 2);
});
