/**
 * Shadow cognition tests (dsh-shadow-cognition gate) — unit level with a fake
 * read facade and a plain fake llm. Covers the frozen V1 semantics and the
 * F1–F16 falsifiers at unit granularity; the REAL pinned-DSH integration
 * (real boot, real LlmRuntime + FakeAdapter, truthful session identity,
 * zero appended session events) lives in real-host-shadow.test.mjs.
 *
 * Frozen semantics exercised here:
 *   - artifact classified COGNITIVE_INFERENCE, trigger human.request
 *   - 0-or-1 model calls; zero tools; bounded context; bounded output
 *   - missing data → missing-data; unresolvable refs → unresolved-evidence-ref;
 *     timeout → model-timeout; error → model-error; no config → model-unavailable
 *   - observedFacts ∈ {kernel-fact, derived-read-model};
 *     hypotheses all shadow-inference; recommendations target human-investigation
 *   - cache EPHEMERAL_PROCESS_LOCAL: restart → truthful loss; never authoritative
 *   - zero session writes; nothing Kernel-mutating anywhere in the artifact
 */
import test from 'node:test';
import assert from 'node:assert/strict';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const srcDir = join(here, '..', 'src');

const {
  CLASSIFICATION,
  TRIGGER,
  FOCUS_VALUES,
  UNCERTAINTY_REASONS,
  MODEL_CALL_STATUSES,
  FACT_CLASSIFICATIONS,
  HYPOTHESIS_CLASSIFICATION,
  RECOMMENDATION_TARGET,
  MAX_MODEL_OUTPUT_CHARS,
  MAX_FACTS,
  MAX_HYPOTHESES,
  MAX_UNCERTAINTIES,
  MAX_RECOMMENDATIONS,
  createShadowAnalysis,
  parseModelOutput,
  fact,
  hypothesis,
  uncertainty,
  recommendation,
} = await import(join(srcDir, 'shadow', 'analysis.js'));
const {
  assembleContext,
  buildBoundedText,
  DEFAULT_LIMITS,
  SNAPSHOT_FIELDS,
  selectEvidenceRefs,
} = await import(join(srcDir, 'shadow', 'context.js'));
const {
  invokeOneShotModel,
  SYSTEM_PROMPT,
  DEFAULT_TIMEOUT_MS,
  MAX_ACCUMULATED_OUTPUT_CHARS,
} = await import(join(srcDir, 'shadow', 'model.js'));
const { createShadowCache, DEFAULT_CACHE_MAX_ENTRIES } = await import(join(srcDir, 'shadow', 'cache.js'));
const {
  DEFAULT_SHADOW_CONFIG,
  validateShadowConfig,
  resolveShadowConfig,
  runShadowAnalysis,
} = await import(join(srcDir, 'shadow', 'index.js'));

const RUN_ID = 'run-1';

// ---------- fixtures ----------

function snapshot(overrides = {}) {
  return {
    runId: RUN_ID,
    runState: { value: 'completed', classification: 'directPublicProjection', truthSource: 'Agent.State (public read model)', isPartial: false },
    currentSemanticPage: { value: null, classification: 'notCurrentlyAvailable', truthSource: null, isPartial: false },
    activeTrap: { value: null, classification: 'notCurrentlyAvailable', truthSource: null, isPartial: false },
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
    ...overrides,
  };
}

function evt(eventId, sequence, kind, extra = {}) {
  return {
    eventId,
    runId: RUN_ID,
    sequence,
    kind,
    correlationId: null,
    causationId: null,
    observationSequence: null,
    evidenceRefs: [],
    payload: {},
    ...extra,
  };
}

function baseEvents() {
  return [
    evt('evt-1', 1, 'RunStarted'),
    evt('evt-2', 2, 'TrapRaised', {
      observationSequence: 1,
      evidenceRefs: [{ locator: 'capture:shadow:record:1', kind: 'TraceFragment', runId: RUN_ID, observationSequence: 1, contentIdentity: null, maturity: 'Captured', sizeBytes: 128 }],
      payload: { trapKind: 'StateMismatch' },
    }),
    evt('evt-3', 3, 'RunFailed', { payload: { cause: 'agent' } }),
  ];
}

function facade(overrides = {}) {
  const calls = { snapshot: 0, events: 0, trap: 0, evidence: 0 };
  const base = {
    getRunSnapshot: async () => snapshot(),
    getRuntimeEvents: async () => ({ runId: RUN_ID, events: baseEvents(), nextCursor: null, hasMore: false, diagnostics: [] }),
    getTrap: async () => ({ runId: RUN_ID, found: false, trap: null, diagnostic: null }),
    getEvidence: async () => ({ found: false, diagnostic: 'no evidence catalog registered' }),
  };
  const f = {};
  for (const [key, counter] of [
    ['getRunSnapshot', 'snapshot'],
    ['getRuntimeEvents', 'events'],
    ['getTrap', 'trap'],
    ['getEvidence', 'evidence'],
  ]) {
    const impl = overrides[key] ?? base[key];
    f[key] = async (...args) => {
      calls[counter] += 1;
      return impl(...args);
    };
  }
  return { calls, ...f };
}

/** Plain fake llm honoring options.signal (unit level; real seam in real-host-shadow). */
function fakeLlm({ chunks, onOptions } = {}) {
  let calls = 0;
  const seenOptions = [];
  return {
    get calls() { return calls; },
    get seenOptions() { return seenOptions; },
    stream: async function* (options) {
      calls += 1;
      seenOptions.push(options);
      const seq = chunks ?? [
        { type: 'text-delta', index: 0, text: '{"humanSummary":"the run completed","hypotheses":[{"claim":"the switch likely missed","supportingRefs":["evt-2"],"flaggedUncertain":true}],"uncertainties":[],"recommendations":[{"text":"inspect the switch state"}]}' },
        { type: 'finish', reason: { kind: 'success' } },
      ];
      for (const chunk of seq) yield chunk;
    },
  };
}

function config(overrides = {}) {
  return {
    ...DEFAULT_SHADOW_CONFIG,
    model: { provider: 'test-shadow', model: 'fake-1' },
    ...overrides,
  };
}

function request(overrides = {}) {
  return { runId: RUN_ID, sessionId: 'session-unit-1', focus: 'general', reason: undefined, ...overrides };
}

async function run(fixtureOverrides, requestOverrides, configOverrides) {
  const f = facade(fixtureOverrides?.facade);
  const llm = fixtureOverrides?.llm;
  const cache = createShadowCache();
  const artifact = await runShadowAnalysis({
    facade: f,
    llm: llm === undefined ? fakeLlm() : llm,
    config: config(configOverrides),
    cache,
    request: request(requestOverrides),
  });
  return { artifact, facade: f, cache, llm };
}

// ---------- vocab / schema (analysis.js) ----------

test('frozen vocab constants are exact', () => {
  assert.equal(CLASSIFICATION, 'COGNITIVE_INFERENCE');
  assert.equal(TRIGGER, 'human.request');
  assert.deepEqual(FOCUS_VALUES, ['general', 'trap', 'failure', 'completion', 'progress', 'blocked']);
  assert.deepEqual(UNCERTAINTY_REASONS, ['missing-data', 'stale-data', 'unresolved-evidence-ref', 'context-assembly-failed', 'model-unavailable', 'model-timeout', 'model-error']);
  assert.deepEqual(MODEL_CALL_STATUSES, ['success', 'error', 'timeout', 'aborted', 'not-configured']);
  assert.deepEqual(FACT_CLASSIFICATIONS, ['kernel-fact', 'derived-read-model']);
  assert.equal(HYPOTHESIS_CLASSIFICATION, 'shadow-inference');
  assert.equal(RECOMMENDATION_TARGET, 'human-investigation');
  assert.equal(MAX_MODEL_OUTPUT_CHARS, 16000);
  assert.equal(MAX_FACTS, 64);
  assert.equal(MAX_HYPOTHESES, 5);
  assert.equal(MAX_UNCERTAINTIES, 12);
  assert.equal(MAX_RECOMMENDATIONS, 5);
});

test('createShadowAnalysis builds and deep-freezes the full frozen schema', () => {
  const requestedAt = Date.now();
  const artifact = createShadowAnalysis({
    analysisId: 'shadow-run-1-1',
    runId: RUN_ID,
    sessionId: 'session-unit-2',
    focus: 'trap',
    requestedAt,
    completedAt: requestedAt + 10,
    classification: CLASSIFICATION,
    evidenceRefs: [{ locator: 'capture:shadow:record:1', kind: 'TraceFragment', maturity: 'Captured', sizeBytes: 128, runId: RUN_ID }],
    observedFacts: [fact('runState: "completed"', 'derived-read-model', { kind: 'RunSnapshot', field: 'runState' })],
    hypotheses: [hypothesis('h1', ['evt-2'], true)],
    uncertainties: [uncertainty('snapshot field activeTrap', 'missing-data')],
    recommendations: [recommendation('inspect')],
    humanSummary: 'digest',
    model: { provider: 'p', model: 'm' },
    modelCall: {
      trigger: 'human.request',
      evidenceRefs: ['capture:shadow:record:1'],
      inputEventCount: 3,
      contextChars: 120,
      provider: 'p',
      model: 'm',
      status: 'success',
      startedAt: requestedAt,
      finishedAt: requestedAt + 5,
      error: null,
    },
  });
  assert.equal(artifact.analysisId, 'shadow-run-1-1');
  assert.equal(artifact.classification, 'COGNITIVE_INFERENCE');
  assert.equal(artifact.trigger, 'human.request');
  assert.equal(typeof artifact.requestedAt, 'number');
  assert.equal(typeof artifact.completedAt, 'number');
  assert.ok(Number.isInteger(artifact.requestedAt) && Number.isInteger(artifact.completedAt));
  assert.equal(artifact.sessionId, 'session-unit-2');
  assert.equal(artifact.model.provider, 'p');
  assert.equal(artifact.modelCall.status, 'success');
  assert.ok(Object.isFrozen(artifact));
  assert.ok(Object.isFrozen(artifact.observedFacts[0]));
  assert.ok(Object.isFrozen(artifact.hypotheses[0]));
  assert.ok(Object.isFrozen(artifact.modelCall));
  assert.ok(Object.isFrozen(artifact.evidenceRefs[0]));
});

test('parseModelOutput accepts raw and fenced JSON; fails closed on garbage', () => {
  const valid = { humanSummary: 's', hypotheses: [{ claim: 'h', supportingRefs: ['a'] }], uncertainties: [{ topic: 't', reason: 'missing-data' }], recommendations: [{ text: 'r' }] };
  assert.equal(parseModelOutput(JSON.stringify(valid)).ok, true);
  assert.equal(parseModelOutput(`\`\`\`json\n${JSON.stringify(valid)}\n\`\`\``).ok, true);
  const nonJson = parseModelOutput('not json at all');
  assert.equal(nonJson.ok, false);
  assert.match(nonJson.error, /not valid JSON/);
  const noSummary = parseModelOutput(JSON.stringify({ humanSummary: '' }));
  assert.equal(noSummary.ok, false);
  assert.match(noSummary.error, /lacks humanSummary/);
  // per-field acceptance: invalid entries dropped, never invented
  const filtered = parseModelOutput(JSON.stringify({
    humanSummary: 's',
    hypotheses: [{ claim: 42 }, { claim: 'ok' }, null],
    uncertainties: [{ topic: 'x', reason: 'bogus-reason' }, { topic: 'y', reason: 'model-error' }],
    recommendations: [{ text: '' }, { text: 'ok' }],
  }));
  assert.equal(filtered.ok, true);
  assert.deepEqual(filtered.hypotheses.map((h) => h.claim), ['ok']);
  assert.deepEqual(filtered.uncertainties, [{ topic: 'y', reason: 'model-error' }]);
  assert.deepEqual(filtered.recommendations.map((r) => r.text), ['ok']);
  assert.equal(filtered.hypotheses[0].classification, 'shadow-inference');
  assert.equal(filtered.recommendations[0].target, 'human-investigation');
  // input is bounded before parsing
  const big = 'x'.repeat(MAX_MODEL_OUTPUT_CHARS + 500);
  assert.ok(parseModelOutput(big).ok === false);
});

// ---------- cache.js ----------

test('cache is bounded, disposable, and never authoritative (EPHEMERAL_PROCESS_LOCAL)', () => {
  assert.equal(DEFAULT_CACHE_MAX_ENTRIES, 20);
  const cache = createShadowCache({ maxEntries: 3 });
  assert.equal(cache.size, 0);
  cache.set('r1', { runId: 'r1' });
  cache.set('r2', { runId: 'r2' });
  cache.set('r3', { runId: 'r3' });
  cache.set('r4', { runId: 'r4' });
  assert.equal(cache.size, 3, 'insertion-order eviction keeps the newest 3');
  assert.equal(cache.get('r1'), undefined);
  assert.equal(cache.get('r4').runId, 'r4');
  cache.delete('r4');
  assert.equal(cache.get('r4'), undefined);
  assert.equal(cache.size, 2);
  cache.clear();
  assert.equal(cache.size, 0);
  // set never throws even for hostile values
  assert.doesNotThrow(() => cache.set('x', undefined));
});

test('F7: restart means truthful loss — a fresh cache has no history', () => {
  const first = createShadowCache();
  first.set(RUN_ID, { analysisId: 'shadow-run-1-1' });
  const second = createShadowCache(); // process restart equivalent
  assert.equal(second.get(RUN_ID), undefined, 'no fabricated history across restart');
  assert.equal(first.get(RUN_ID).analysisId, 'shadow-run-1-1');
});

// ---------- context.js ----------

test('assembleContext is bounded and deterministic (F11)', async () => {
  const many = [];
  for (let i = 0; i < 300; i += 1) many.push(evt(`evt-${i}`, i + 1, 'RunEvent'));
  many.push(evt('evt-trap', 301, 'TrapRaised', { observationSequence: 10 }));
  const f = facade({
    getRuntimeEvents: async () => ({ runId: RUN_ID, events: many, nextCursor: null, hasMore: false, diagnostics: [] }),
  });
  const context = await assembleContext({ facade: f, limits: DEFAULT_LIMITS, request: { runId: RUN_ID, focus: 'general' } });
  assert.ok(context.eventCount <= DEFAULT_LIMITS.maxEvents, `eventCount ${context.eventCount} <= ${DEFAULT_LIMITS.maxEvents}`);
  assert.equal(context.eventCount, DEFAULT_LIMITS.maxEvents, 'recent window is the last maxEvents by sequence');
  assert.ok(context.contextChars <= DEFAULT_LIMITS.maxContextChars, `contextChars ${context.contextChars} <= ${DEFAULT_LIMITS.maxContextChars}`);
  // deterministic: same input → identical text
  const second = await assembleContext({ facade: f, limits: DEFAULT_LIMITS, request: { runId: RUN_ID, focus: 'general' } });
  assert.equal(second.text, context.text);
  // priority anchor survives the trim (TrapRaised is causal/terminal)
  assert.match(context.text, /TrapRaised/);
  // oldest non-priority events were dropped
  assert.ok(!context.text.includes('evt-0'), 'oldest event trimmed');
});

test('assembleContext honors an extreme maxContextChars cap (hard deterministic slice)', async () => {
  const f = facade();
  const context = await assembleContext({ facade: f, limits: { ...DEFAULT_LIMITS, maxContextChars: 400 }, request: { runId: RUN_ID, focus: 'general' } });
  assert.ok(context.contextChars <= 400, `hard-sliced to ${context.contextChars} <= 400`);
});

test('snapshot fields with values become derived-read-model facts; missing ones become missing-data (F3, F9)', async () => {
  const f = facade();
  const context = await assembleContext({ facade: f, limits: DEFAULT_LIMITS, request: { runId: RUN_ID, focus: 'general' } });
  const runStateFact = context.facts.find((entry) => entry.claim.startsWith('runState:'));
  assert.ok(runStateFact, 'runState fact present');
  assert.equal(runStateFact.classification, 'derived-read-model');
  assert.equal(runStateFact.ref.kind, 'RunSnapshot');
  assert.equal(runStateFact.ref.field, 'runState');
  assert.ok(runStateFact.ref.truthSource);
  // F9: latestGoalEvidence is null in the read surface → uncertainty, never a fact
  const goalFact = context.facts.find((entry) => entry.claim.startsWith('latestGoalEvidence:'));
  assert.equal(goalFact, undefined, 'no GoalEvidence fact invented from missing data');
  const goalUncertainty = context.uncertainties.find((entry) => entry.topic === 'snapshot field latestGoalEvidence');
  assert.equal(goalUncertainty.reason, 'missing-data');
});

test('priority events become kernel-fact observed facts (F1/F2 citation surface)', async () => {
  const f = facade();
  const context = await assembleContext({ facade: f, limits: DEFAULT_LIMITS, request: { runId: RUN_ID, focus: 'general' } });
  const trapFact = context.facts.find((entry) => entry.claim.includes('TrapRaised'));
  const failedFact = context.facts.find((entry) => entry.claim.includes('RunFailed'));
  assert.equal(trapFact.classification, 'kernel-fact');
  assert.equal(trapFact.ref.kind, 'RuntimeEvent');
  assert.equal(trapFact.ref.eventId, 'evt-2');
  assert.equal(failedFact.classification, 'kernel-fact');
  // F2: the RunFailed *cause* is never asserted as a kernel fact
  assert.equal(context.facts.find((entry) => entry.claim.includes('cause')), undefined);
});

test('trap focus lazily resolves evidence; unresolvable refs yield unresolved-evidence-ref (F1, F4)', async () => {
  const f = facade({
    getTrap: async () => ({
      runId: RUN_ID,
      found: true,
      trap: {
        value: { kind: 'StateMismatch', scope: 'wifi', expected: true, observed: false, source: 'capability-probe', evidence: 'capture:shadow:record:1', lastActionDescription: 'probe wifi' },
        classification: 'directPublicProjection',
      },
      diagnostic: null,
    }),
    getEvidence: async () => ({ found: false, diagnostic: 'record missing from catalog' }),
  });
  const context = await assembleContext({ facade: f, limits: DEFAULT_LIMITS, request: { runId: RUN_ID, focus: 'trap' } });
  assert.equal(f.calls.trap, 1, 'trap fetched exactly once on trap focus');
  assert.equal(f.calls.evidence, 1, 'evidence resolved lazily on trap focus');
  const trapFact = context.facts.find((entry) => entry.claim.startsWith('trap:'));
  assert.equal(trapFact.classification, 'derived-read-model');
  assert.equal(trapFact.ref.kind, 'TrapDetail');
  assert.match(context.text, /--- trap ---/);
  const unresolved = context.uncertainties.find((entry) => entry.topic.startsWith('evidence capture:shadow:record:1'));
  assert.equal(unresolved.reason, 'unresolved-evidence-ref');
  assert.ok(context.text.length > 0, 'analysis remains usable despite unresolved refs');
});

test('non-trap focus never touches trap or evidence (lazy, F1)', async () => {
  const f = facade();
  const context = await assembleContext({ facade: f, limits: DEFAULT_LIMITS, request: { runId: RUN_ID, focus: 'general' } });
  assert.equal(f.calls.trap, 0);
  assert.equal(f.calls.evidence, 0);
  assert.equal(context.trap, null);
  assert.equal(context.selectedRefs.length, 1, 'logical locators still selected from the window');
});

test('evidence refs over evidenceBytesPerRef are flagged unresolved (F4 bound)', async () => {
  const f = facade({
    getTrap: async () => ({ runId: RUN_ID, found: false, trap: null, diagnostic: null }),
    getEvidence: async () => ({ found: true, ref: null, record: null, artifact: { artifactId: 'a1', frameId: 'f1', fileName: 'trace.bin', contentHash: 'h', byteCount: 99999 }, diagnostic: null }),
  });
  const context = await assembleContext({ facade: f, limits: DEFAULT_LIMITS, request: { runId: RUN_ID, focus: 'trap' } });
  assert.equal(f.calls.evidence, 1);
  const flagged = context.uncertainties.find((entry) => entry.topic.startsWith('evidence capture:shadow:record:1'));
  assert.equal(flagged.reason, 'unresolved-evidence-ref');
  assert.match(flagged.topic, /99999 bytes/);
});

test('assembly failure yields context-assembly-failed uncertainties, not a crash', async () => {
  const f = facade({
    getRunSnapshot: async () => { throw new Error('snapshot read failed'); },
    getRuntimeEvents: async () => { throw new Error('events read failed'); },
  });
  const context = await assembleContext({ facade: f, limits: DEFAULT_LIMITS, request: { runId: RUN_ID, focus: 'general' } });
  const reasons = context.uncertainties.map((u) => u.reason);
  assert.ok(reasons.includes('context-assembly-failed'));
  assert.ok(context.text.length > 0, 'deterministic text still produced');
});

test('selectEvidenceRefs dedupes by locator and caps at maxEvidenceRefs', () => {
  const events = [
    evt('a', 1, 'RunEvent', { evidenceRefs: [{ locator: 'L1' }, { locator: 'L2' }] }),
    evt('b', 2, 'RunEvent', { evidenceRefs: [{ locator: 'L1' }, { locator: 'L3' }] }),
  ];
  const refs = selectEvidenceRefs(events, 2);
  assert.deepEqual(refs.map((r) => r.locator), ['L1', 'L2']);
});

// ---------- model.js ----------

test('invokeOneShotModel: success path accumulates text and passes the frozen options shape', async () => {
  const llm = fakeLlm({
    chunks: [
      { type: 'text-delta', index: 0, text: 'hello ' },
      { type: 'text-delta', index: 0, text: 'world' },
      { type: 'finish', reason: { kind: 'success' } },
    ],
  });
  const result = await invokeOneShotModel({ llm, provider: 'p', model: 'm', userText: 'u', signal: new AbortController().signal, timeoutMs: 100 });
  assert.equal(result.status, 'success');
  assert.equal(result.uncertainty, null);
  assert.equal(result.text, 'hello world');
  const options = llm.seenOptions[0];
  assert.equal(options.provider, 'p');
  assert.equal(options.model, 'm');
  assert.equal(typeof options.system, 'string');
  assert.equal(options.purpose, undefined, 'purpose unset');
  assert.ok(Array.isArray(options.messages));
  assert.equal(options.messages[0].role, 'user');
  assert.deepEqual(options.messages[0].content, [{ type: 'text', text: 'u' }]);
  assert.ok(options.signal instanceof AbortSignal);
  assert.ok(!('tools' in options), 'no tools exposed to the model (frozen)');
});

test('invokeOneShotModel: no llm or no provider/model → not-configured + model-unavailable, zero calls', async () => {
  const result = await invokeOneShotModel({ llm: null, provider: null, model: null, userText: 'u' });
  assert.equal(result.status, 'not-configured');
  assert.equal(result.uncertainty, 'model-unavailable');
  // timestamps are recorded even on the unavailable path (deterministic audit)
  assert.equal(typeof result.startedAt, 'number');
  assert.equal(typeof result.finishedAt, 'number');
  assert.ok(result.finishedAt >= result.startedAt);
});

test('invokeOneShotModel: NO_ADAPTER finish chunk → not-configured + model-unavailable', async () => {
  const llm = fakeLlm({ chunks: [{ type: 'finish', reason: { kind: 'error', failure: { code: 'NO_ADAPTER', message: 'no adapter registered for provider "x"' } } }] });
  const result = await invokeOneShotModel({ llm, provider: 'x', model: 'm', userText: 'u' });
  assert.equal(result.status, 'not-configured');
  assert.equal(result.uncertainty, 'model-unavailable');
  assert.equal(result.text, null);
});

test('invokeOneShotModel: error finish chunk → error + model-error (F6)', async () => {
  const llm = fakeLlm({ chunks: [{ type: 'finish', reason: { kind: 'error', failure: { code: 'E', message: 'provider exploded' } } }] });
  const result = await invokeOneShotModel({ llm, provider: 'p', model: 'm', userText: 'u' });
  assert.equal(result.status, 'error');
  assert.equal(result.uncertainty, 'model-error');
  assert.match(result.error, /provider exploded/);
});

test('invokeOneShotModel: thrown iteration error → error + model-error', async () => {
  const llm = { stream: async function* () { throw new Error('boom'); } };
  const result = await invokeOneShotModel({ llm, provider: 'p', model: 'm', userText: 'u' });
  assert.equal(result.status, 'error');
  assert.equal(result.uncertainty, 'model-error');
  assert.match(result.error, /boom/);
});

test('invokeOneShotModel: F5 timeout → model-timeout with a deterministic ref-d timer', async () => {
  const llm = {
    stream: async function* (options) {
      await new Promise((_, reject) => {
        options.signal.addEventListener('abort', () => reject(new Error('aborted by timeout')));
      });
    },
  };
  const started = Date.now();
  const result = await invokeOneShotModel({ llm, provider: 'p', model: 'm', userText: 'u', signal: new AbortController().signal, timeoutMs: 60 });
  assert.equal(result.status, 'timeout');
  assert.equal(result.uncertainty, 'model-timeout');
  assert.ok(Date.now() - started < 5000, 'timeout actually fired (ref\'d timer keeps the loop alive)');
  assert.equal(result.text, null);
});

test('invokeOneShotModel: caller abort → aborted (status-only)', async () => {
  const controller = new AbortController();
  const llm = {
    stream: async function* (options) {
      await new Promise((_, reject) => {
        options.signal.addEventListener('abort', () => reject(new Error('caller aborted')));
      });
    },
  };
  setTimeout(() => controller.abort(), 30);
  const result = await invokeOneShotModel({ llm, provider: 'p', model: 'm', userText: 'u', signal: controller.signal, timeoutMs: 60000 });
  assert.equal(result.status, 'aborted');
  assert.equal(result.uncertainty, null, 'aborted may carry status only');
});

test('invokeOneShotModel: empty success text fails closed; output accumulation is bounded', async () => {
  const empty = await invokeOneShotModel({ llm: fakeLlm({ chunks: [{ type: 'finish', reason: { kind: 'success' } }] }), provider: 'p', model: 'm', userText: 'u' });
  assert.equal(empty.status, 'error');
  assert.equal(empty.uncertainty, 'model-error');
  assert.match(empty.error, /no output/);

  const huge = 'x'.repeat(MAX_ACCUMULATED_OUTPUT_CHARS + 1000);
  const llm = fakeLlm({ chunks: [{ type: 'text-delta', index: 0, text: huge }, { type: 'finish', reason: { kind: 'success' } }] });
  const result = await invokeOneShotModel({ llm, provider: 'p', model: 'm', userText: 'u' });
  assert.equal(result.status, 'success');
  assert.ok(result.text.length <= MAX_ACCUMULATED_OUTPUT_CHARS, `bounded to ${result.text.length} <= ${MAX_ACCUMULATED_OUTPUT_CHARS}`);
  assert.equal(llm.calls, 1);
});

test('SYSTEM_PROMPT encodes the frozen epistemic contract', () => {
  assert.equal(typeof SYSTEM_PROMPT, 'string', 'prompt is exported as a single joined string');
  assert.match(SYSTEM_PROMPT, /Shadow Cognition analyst/);
  assert.match(SYSTEM_PROMPT, /kernel facts/);
  assert.match(SYSTEM_PROMPT, /derived-read-model/);
  assert.match(SYSTEM_PROMPT, /shadow-inference/);
  assert.match(SYSTEM_PROMPT, /human-investigation/);
  assert.match(SYSTEM_PROMPT, /Kernel GoalEvidence/);
  assert.match(SYSTEM_PROMPT, /missing-data|model-unavailable|model-timeout|model-error/);
  assert.match(SYSTEM_PROMPT, /no tools/);
});

// ---------- index.js (config + orchestration) ----------

test('validateShadowConfig: autoTriggers reserved and MUST be []', () => {
  assert.throws(() => validateShadowConfig({ autoTriggers: ['run-failed'] }), /reserved and MUST be \[\] in V1/);
  assert.deepEqual(validateShadowConfig({ autoTriggers: [] }), { autoTriggers: [] });
  assert.throws(() => validateShadowConfig({ maxEvents: 0 }), /positive integer/);
  assert.throws(() => validateShadowConfig({ maxEvents: 1.5 }), /positive integer/);
  assert.throws(() => validateShadowConfig({ enabled: 'yes' }), /boolean/);
  assert.throws(() => validateShadowConfig({ model: { provider: '' } }), /non-empty string/);
  assert.deepEqual(validateShadowConfig({ enabled: false, model: { provider: 'p', model: 'm' } }), {
    enabled: false,
    model: { provider: 'p', model: 'm' },
  });
});

test('resolveShadowConfig merges validated keys over frozen defaults', () => {
  const cfg = resolveShadowConfig(validateShadowConfig({ model: { provider: 'p', model: 'm' }, maxEvents: 50 }));
  assert.equal(cfg.enabled, true);
  assert.equal(cfg.maxEvents, 50);
  assert.equal(cfg.maxContextChars, 80000);
  assert.equal(cfg.model.provider, 'p');
  assert.deepEqual(cfg.autoTriggers, []);
  assert.equal(cfg.visual.enabled, false, 'F12: visual default disabled');
  assert.equal(cfg.timeoutMs, DEFAULT_TIMEOUT_MS);
});

test('runShadowAnalysis requires a truthful sessionId (never invented)', async () => {
  await assert.rejects(runShadowAnalysis({ facade: facade(), llm: null, config: config(), cache: createShadowCache(), request: { runId: RUN_ID } }), /truthful DSH sessionId/);
});

test('F14: artifact sessionId and runId are distinct identities', async () => {
  const { artifact } = await run({}, { sessionId: 'session-unit-9' });
  assert.equal(artifact.runId, RUN_ID);
  assert.equal(artifact.sessionId, 'session-unit-9');
  assert.notEqual(artifact.sessionId, artifact.runId);
});

test('F1: trap focus yields a bounded artifact with trap detail, TrapRaised citation, and lazy evidence', async () => {
  const f = facade({
    getTrap: async () => ({
      runId: RUN_ID,
      found: true,
      trap: {
        value: { kind: 'StateMismatch', scope: 'wifi', expected: true, observed: false, source: 'capability-probe', evidence: 'capture:shadow:record:1', lastActionDescription: 'probe wifi' },
        classification: 'directPublicProjection',
      },
      diagnostic: null,
    }),
    getEvidence: async () => ({ found: true, ref: null, record: { order: 7, kind: 'TraceFragment', sequenceNumber: 1, frameId: 'f1', actionId: 'a1', resultOutcome: 'mismatch', info: null }, artifact: { artifactId: 'a1', frameId: 'f1', fileName: 't.bin', contentHash: 'h', byteCount: 128 }, diagnostic: null }),
  });
  const { artifact } = await run({ facade: f }, { focus: 'trap' });
  assert.equal(artifact.classification, 'COGNITIVE_INFERENCE');
  assert.equal(artifact.trigger, 'human.request');
  assert.equal(artifact.focus, 'trap');
  assert.ok(artifact.observedFacts.find((entry) => entry.claim.startsWith('trap:')));
  assert.ok(artifact.observedFacts.find((entry) => entry.claim.includes('TrapRaised') && entry.ref.eventId === 'evt-2'));
  assert.ok(artifact.observedFacts.find((entry) => entry.claim.startsWith('evidence capture:shadow:record:1')));
  assert.equal(artifact.evidenceRefs[0].locator, 'capture:shadow:record:1');
  assert.equal(f.calls.evidence, 1);
  assert.equal(artifact.modelCall.inputEventCount, 3);
  assert.equal(artifact.modelCall.evidenceRefs.length, 1);
});

test('F2: model hypotheses stay shadow-inference; Kernel facts stay kernel-fact; no cause assertion', async () => {
  const { artifact } = await run({}, { focus: 'general' });
  for (const entry of artifact.observedFacts) {
    assert.ok(FACT_CLASSIFICATIONS.includes(entry.classification), `fact ${entry.claim} ∈ kernel-fact|derived-read-model`);
  }
  for (const entry of artifact.hypotheses) {
    assert.equal(entry.classification, 'shadow-inference');
  }
  for (const entry of artifact.recommendations) {
    assert.equal(entry.target, 'human-investigation');
  }
  assert.equal(artifact.hypotheses[0].claim, 'the switch likely missed');
  assert.deepEqual(artifact.hypotheses[0].supportingRefs, ['evt-2']);
  assert.equal(artifact.hypotheses[0].flaggedUncertain, true);
  assert.equal(artifact.observedFacts.find((entry) => entry.claim.includes('cause')), undefined);
});

test('F3: missing snapshot fields surface as missing-data uncertainties', async () => {
  const { artifact } = await run({});
  const missing = artifact.uncertainties.filter((u) => u.reason === 'missing-data');
  assert.ok(missing.length >= 1);
  assert.ok(missing.find((u) => u.topic === 'snapshot field latestGoalEvidence'));
  assert.ok(missing.find((u) => u.topic === 'snapshot field bindingsSummary'));
});

test('F4: unresolvable evidence ref leaves the analysis usable', async () => {
  const f = facade({
    getTrap: async () => ({ runId: RUN_ID, found: true, trap: { value: { kind: 'StateMismatch', scope: 'wifi', expected: true, observed: false, source: 'capability-probe', evidence: 'capture:shadow:record:1', lastActionDescription: 'probe' }, classification: 'directPublicProjection' }, diagnostic: null }),
    getEvidence: async () => ({ found: false, diagnostic: 'catalog miss' }),
  });
  const { artifact } = await run({ facade: f }, { focus: 'trap' });
  assert.equal(artifact.classification, 'COGNITIVE_INFERENCE');
  assert.ok(artifact.uncertainties.find((u) => u.reason === 'unresolved-evidence-ref'));
  assert.ok(artifact.humanSummary.length > 0);
});

test('F5: timeout surfaces model-timeout; model calls never exceed one', async () => {
  const hanging = {
    stream: async function* (options) {
      await new Promise((_, reject) => {
        options.signal.addEventListener('abort', () => reject(new Error('timed out')));
      });
    },
  };
  const { artifact } = await run({ llm: hanging }, {}, { timeoutMs: 60 });
  assert.equal(artifact.modelCall.status, 'timeout');
  assert.ok(artifact.uncertainties.find((u) => u.reason === 'model-timeout'));
  assert.equal(artifact.humanSummary.includes('Model cognition timeout'), true);
  assert.equal(artifact.modelCall.inputEventCount, 3);
});

test('F6: model error surfaces model-error and a deterministic digest remains', async () => {
  const broken = { stream: async function* () { throw new Error('provider exploded'); } };
  const { artifact } = await run({ llm: broken });
  assert.equal(artifact.modelCall.status, 'error');
  assert.ok(artifact.uncertainties.find((u) => u.reason === 'model-error'));
  assert.equal(artifact.modelCall.error, 'provider exploded');
  assert.ok(artifact.humanSummary.length > 0);
});

test('model-unavailable path: no llm → not-configured, zero model calls, deterministic digest', async () => {
  const { artifact } = await run({ llm: null }, {}, { model: { provider: null, model: null } });
  assert.equal(artifact.modelCall.status, 'not-configured');
  assert.equal(artifact.modelCall.provider, null);
  assert.ok(artifact.uncertainties.find((u) => u.reason === 'model-unavailable'));
  assert.match(artifact.humanSummary, /Model cognition not-configured/);
  assert.match(artifact.humanSummary, /runState=completed/);
  assert.match(artifact.humanSummary, /3 bounded runtime events/);
  assert.equal(artifact.classification, 'COGNITIVE_INFERENCE');
  // deterministic recommendations from current uncertainties (capped at 5)
  assert.ok(artifact.recommendations.length > 0);
  assert.ok(artifact.recommendations.every((r) => r.target === 'human-investigation'));
});

test('malformed model output fails closed into model-error (never invents)', async () => {
  const bad = fakeLlm({ chunks: [{ type: 'text-delta', index: 0, text: 'not json' }, { type: 'finish', reason: { kind: 'success' } }] });
  const { artifact } = await run({ llm: bad });
  assert.equal(artifact.modelCall.status, 'error');
  assert.ok(artifact.uncertainties.find((u) => u.reason === 'model-error'));
  assert.ok(artifact.humanSummary.length > 0, 'deterministic digest stands in');
  assert.deepEqual(artifact.hypotheses, []);
});

test('F8: model output is text-only — no execution/action surface exists in the artifact', async () => {
  const tap = fakeLlm({
    chunks: [{
      type: 'text-delta', index: 0, text: '{"humanSummary":"tap the wifi switch to verify","hypotheses":[{"claim":"tapping may help","supportingRefs":[]}],"uncertainties":[],"recommendations":[{"text":"tap the switch"}]}',
    }, { type: 'finish', reason: { kind: 'success' } }],
  });
  const { artifact } = await run({ llm: tap });
  assert.equal(artifact.humanSummary, 'tap the wifi switch to verify');
  assert.equal(artifact.recommendations[0].target, 'human-investigation', 'recommendations are for humans, never Kernel actions');
  // the artifact schema carries no execution/action/adb/device field
  for (const forbidden of ['executionProposal', 'actionPlan', 'deviceAction', 'adbCommand', 'approval', 'authorization', 'plannerState', 'confidence', 'severity']) {
    assert.equal(forbidden in artifact, false, `artifact must not carry ${forbidden}`);
    for (const hypothesisEntry of artifact.hypotheses) {
      assert.equal(forbidden in hypothesisEntry, false);
    }
  }
  // F12: zero visual inputs in the model context
  const options = tap.seenOptions[0];
  assert.ok(options.messages[0].content.every((block) => block.type === 'text'), 'only text blocks reach the model');
});

test('F13: observed facts and hypotheses never share a classification bucket', async () => {
  const { artifact } = await run({});
  const factClasses = new Set(artifact.observedFacts.map((f) => f.classification));
  assert.ok([...factClasses].every((c) => FACT_CLASSIFICATIONS.includes(c)));
  const hypothesisClasses = new Set(artifact.hypotheses.map((h) => h.classification));
  assert.deepEqual([...hypothesisClasses], ['shadow-inference']);
});

test('F11: overflow analysis stays bounded end-to-end and deterministic', async () => {
  const many = [];
  for (let i = 0; i < 300; i += 1) many.push(evt(`evt-${i}`, i + 1, 'RunEvent'));
  const f = facade({ getRuntimeEvents: async () => ({ runId: RUN_ID, events: many, nextCursor: null, hasMore: false, diagnostics: [] }) });
  const first = await runShadowAnalysis({ facade: f, llm: fakeLlm(), config: config(), cache: createShadowCache(), request: request() });
  const second = await runShadowAnalysis({ facade: f, llm: fakeLlm(), config: config(), cache: createShadowCache(), request: request() });
  assert.ok(first.modelCall.inputEventCount <= DEFAULT_LIMITS.maxEvents);
  assert.ok(first.modelCall.contextChars <= DEFAULT_LIMITS.maxContextChars);
  assert.equal(second.modelCall.contextChars, first.modelCall.contextChars, 'deterministic across runs');
});

test('F15: the analysis writes zero session events (no append surface touched)', async () => {
  const appendSpy = { calls: 0, append() { this.calls += 1; return {}; } };
  const f = facade();
  const artifact = await runShadowAnalysis({ facade: f, llm: fakeLlm(), config: config(), cache: createShadowCache(), request: request() });
  assert.equal(appendSpy.calls, 0, 'no session write anywhere');
  assert.ok(artifact.analysisId.startsWith(`shadow-${RUN_ID}-`));
  // each analysis is a fresh artifact; the cache holds the last one only
  assert.equal(createShadowCache().get(RUN_ID), undefined);
});

test('analysis ids are unique per human request; cache retains the latest per runId', async () => {
  const f = facade();
  const cache = createShadowCache();
  const a1 = await runShadowAnalysis({ facade: f, llm: fakeLlm(), config: config(), cache, request: request({ sessionId: 'session-unit-3' }) });
  const a2 = await runShadowAnalysis({ facade: f, llm: fakeLlm(), config: config(), cache, request: request({ sessionId: 'session-unit-3' }) });
  assert.notEqual(a1.analysisId, a2.analysisId);
  assert.equal(cache.get(RUN_ID).analysisId, a2.analysisId);
});

test('F16/zero-Kernel-write: artifact contains no Kernel-mutating or durable-persistence fields', () => {
  const artifact = createShadowAnalysis({
    analysisId: 'shadow-run-1-x',
    runId: RUN_ID,
    sessionId: 's',
    focus: 'general',
    requestedAt: 1,
    completedAt: 2,
    classification: CLASSIFICATION,
    evidenceRefs: [],
    observedFacts: [],
    hypotheses: [],
    uncertainties: [],
    recommendations: [],
    humanSummary: 's',
    model: { provider: null, model: null },
    modelCall: { trigger: 'human.request', evidenceRefs: [], inputEventCount: 0, contextChars: 0, provider: null, model: null, status: 'not-configured', startedAt: null, finishedAt: null, error: null },
  });
  const allowed = new Set([
    'analysisId', 'runId', 'sessionId', 'trigger', 'focus', 'requestedAt', 'completedAt', 'classification',
    'evidenceRefs', 'observedFacts', 'hypotheses', 'uncertainties', 'recommendations', 'humanSummary',
    'model', 'modelCall',
  ]);
  assert.deepEqual(Object.keys(artifact).sort(), [...allowed].sort());
});
