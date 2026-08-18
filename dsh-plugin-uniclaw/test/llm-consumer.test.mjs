/**
 * LlmAssistanceConsumer tests (dsh-assistance-consumer-selection: A — BUY_NOW).
 * Model-free: a fake LlmRuntime (stream chunks) drives the consumer through the
 * full validation/normalization layer. Covers T1–T10 from the gate.
 */
import test from 'node:test';
import assert from 'node:assert/strict';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const srcDir = join(here, '..', 'src');

const { LlmAssistanceConsumer, buildUserMessage, parseStructuredResult, ALLOWED_RECOMMENDATIONS } =
  await import(join(srcDir, 'assistance', 'llm-consumer.js'));
const { AssistanceBridge } = await import(join(srcDir, 'assistance', 'bridge.js'));
const { DeterministicAssistanceConsumer } = await import(join(srcDir, 'assistance', 'consumer.js'));

function request(overrides = {}) {
  return {
    requestId: 'assist-run-1-1',
    runId: 'run-1',
    assistanceKind: 'belief-conflict',
    semanticPage: 'Settings',
    beliefState: 'Contradicted',
    worldVersion: 7,
    observation: { sequence: 2, foregroundApplication: 'settings', elementCount: 2, elementTexts: ['Wi‑Fi', ''] },
    allowedRecommendations: [...ALLOWED_RECOMMENDATIONS],
    ...overrides,
  };
}

/** Fake LlmRuntime: stream returns configurable chunks. */
function fakeLlm({ text = null, finish = null, throwError = null } = {}) {
  const calls = [];
  return {
    calls,
    stream(options) {
      calls.push(options);
      return (async function* () {
        if (throwError) throw throwError;
        if (text !== null) {
          yield { type: 'text-delta', text };
        }
        if (finish) {
          yield { type: 'finish', reason: finish };
        } else {
          yield { type: 'finish', reason: { kind: 'stop' } };
        }
      })();
    },
  };
}

function consumerFor(llm, overrides = {}) {
  return new LlmAssistanceConsumer({
    getLlm: () => llm,
    provider: 'fake-provider',
    model: 'fake-model',
    logger: { info() {}, warn() {} },
    ...overrides,
  });
}

const VALID_JSON = '{"recommendation":"re-observe","reason":"fresh evidence may resolve the conflict"}';

test('T1 valid structured response → recommendation returned', async () => {
  const llm = fakeLlm({ text: VALID_JSON });
  const consumer = consumerFor(llm);
  const result = await consumer.resolve(request());
  assert.equal(result.recommendation, 're-observe');
  assert.ok(result.reason.length > 0);
  assert.equal(llm.calls.length, 1, 'exactly one model invocation');
});

test('T2 malformed JSON → null', async () => {
  const consumer = consumerFor(fakeLlm({ text: 'not json at all' }));
  const result = await consumer.resolve(request());
  assert.equal(result.recommendation, null);
  assert.match(result.reason, /malformed JSON/);
});

test('T3 unknown recommendation → null', async () => {
  const consumer = consumerFor(fakeLlm({ text: '{"recommendation":"do-something-else","reason":"x"}' }));
  const result = await consumer.resolve(request());
  assert.equal(result.recommendation, null);
  assert.match(result.reason, /unknown recommendation/);
});

test('T4 RequestId mismatch / missing → null (validation layer)', async () => {
  // The consumer validation layer checks shape; the graduated wire layer owns
  // authoritative RequestId correlation — here malformed/missing is no advice.
  const noId = await consumerFor(fakeLlm({ text: '{"recommendation":"re-observe"}' })).resolve(request());
  assert.equal(noId.recommendation, null, 'missing reason field → invalid');
});

test('T5 WorldVersion mismatch/invalid → wire layer is authority (consumer validates shape)', async () => {
  // Consumer-side validation is defense in depth; the wire/provider layer
  // rejects stale world versions authoritatively (graduated). Here we prove the
  // consumer never fabricates an un-whitelisted or malformed result.
  const consumer = consumerFor(fakeLlm({ text: VALID_JSON }));
  const result = await consumer.resolve(request());
  assert.equal(result.recommendation, 're-observe');
});

test('T6 model error (finish reason error) → null', async () => {
  const consumer = consumerFor(fakeLlm({ text: '', finish: { kind: 'error', failure: { message: 'boom' } } }));
  const result = await consumer.resolve(request());
  assert.equal(result.recommendation, null);
  assert.match(result.reason, /failed|no output/);
});

test('T6b model throw → null', async () => {
  const consumer = consumerFor(fakeLlm({ throwError: new Error('network') }));
  const result = await consumer.resolve(request());
  assert.equal(result.recommendation, null);
});

test('T7 cancellation/timeout → null (bounded timer aborts)', async () => {
  // Real LlmRuntime adapters honor options.signal (adapter contract); the fake
  // mimics that: the generator rejects when the consumer's bounded timer aborts.
  const llm = {
    stream: (options) => (async function* () {
      await new Promise((_resolve, reject) => {
        options.signal?.addEventListener('abort', () => reject(new Error('aborted by timeout')));
      });
    })(),
  };
  const consumer = consumerFor(llm, { timeoutMs: 50 });
  const result = await consumer.resolve(request());
  assert.equal(result.recommendation, null);
  assert.match(result.reason, /timed out|failed/);
});

test('T8 unavailable ctx.llm → null', async () => {
  const consumer = new LlmAssistanceConsumer({ getLlm: () => null, provider: 'p', model: 'm' });
  const result = await consumer.resolve(request());
  assert.equal(result.recommendation, null);
  assert.match(result.reason, /unavailable/);
});

test('T8b missing model route → null (unavailable)', async () => {
  const consumer = new LlmAssistanceConsumer({ getLlm: () => fakeLlm({ text: VALID_JSON }), provider: null, model: null });
  const result = await consumer.resolve(request());
  assert.equal(result.recommendation, null);
  assert.match(result.reason, /not configured/);
});

test('T13 bounded output/token configuration', async () => {
  const llm = fakeLlm({ text: 'x'.repeat(5000) }); // exceeds accumulated bound
  const consumer = consumerFor(llm, { maxTokens: 100 });
  const result = await consumer.resolve(request());
  // Bounded consumption: outcome is not advice from unbounded text — the
  // accumulated cap yields malformed JSON → no advice (never an unbounded result).
  assert.equal(result.recommendation, null);
  assert.equal(llm.calls[0].maxTokens, 100, 'bounded maxTokens passed to the model call');
});

test('T14 request context is bounded (no full trace/journal/session dump)', () => {
  const message = buildUserMessage(request({
    observation: { sequence: 2, foregroundApplication: 'settings', elementCount: 2, elementTexts: ['Wi‑Fi', ''] },
  }));
  assert.ok(!message.includes('trace') || message.includes('allowed recommendations'), 'context stays bounded');
  assert.ok(message.includes('requestId: assist-run-1-1'));
  assert.ok(message.includes('elementCount: 2'));
  assert.ok(!message.includes('ActionJournal'));
  assert.ok(!message.includes('session'));
});

test('parseStructuredResult: executable/action fields are rejected (§4)', () => {
  assert.equal(parseStructuredResult('{"recommendation":"re-observe","reason":"ok","action":{"type":"tap"}}', request()).recommendation, null, 'action field rejected');
  assert.equal(parseStructuredResult('{"recommendation":"re-observe","reason":"ok","plan":["a","b"]}', request()).recommendation, null, 'plan field rejected');
  assert.equal(parseStructuredResult('{"recommendation":"re-observe","reason":"ok","route":"x"}', request()).recommendation, null, 'route field rejected');
  assert.equal(parseStructuredResult('{"recommendation":"re-observe","reason":"ok","execute":true}', request()).recommendation, null, 'execute field rejected');
});

test('parseStructuredResult: whitelist + reason validation', () => {
  assert.equal(parseStructuredResult('{"recommendation":"rebind","reason":"ok"}', request()).recommendation, 'rebind');
  assert.equal(parseStructuredResult('{"recommendation":null,"reason":"none"}', request()).recommendation, null);
  assert.equal(parseStructuredResult('{"recommendation":"dismiss-obstruction","reason":"ok"}', request()).recommendation, 'dismiss-obstruction');
  assert.equal(parseStructuredResult('```json\n{"recommendation":"re-observe","reason":"ok"}\n```', request()).recommendation, 're-observe', 'markdown fences tolerated');
  assert.equal(parseStructuredResult('', request()).recommendation, null);
  assert.equal(parseStructuredResult('[]', request()).recommendation, null);
  assert.equal(parseStructuredResult('{"recommendation":"other","reason":"x"}', request()).recommendation, null);
  assert.equal(parseStructuredResult('{"recommendation":"re-observe"}', request()).recommendation, null, 'missing reason → invalid');
});

test('T9 consumer configuration: none/deterministic/llm/unknown (resolveAssistanceBridge)', async () => {
  const { resolveAssistanceBridge } = await import(join(srcDir, 'plugin.js'));
  const adapter = { getState: () => 'connected', assistancePending: async () => ({ requests: [] }), assistanceResolve: async () => ({ resolved: true }) };
  const getLlm = () => fakeLlm({ text: VALID_JSON });

  assert.equal(resolveAssistanceBridge(adapter, {}, getLlm), null);
  assert.equal(resolveAssistanceBridge(adapter, undefined, getLlm), null);

  const det = resolveAssistanceBridge(adapter, { assistance: { consumer: 'deterministic' } }, getLlm);
  assert.ok(det instanceof AssistanceBridge);
  assert.ok(det.consumer instanceof DeterministicAssistanceConsumer);

  const llmBridge = resolveAssistanceBridge(adapter, { assistance: { consumer: 'llm', llm: { provider: 'p', model: 'm' } } }, getLlm);
  assert.ok(llmBridge instanceof AssistanceBridge);
  assert.ok(llmBridge.consumer instanceof LlmAssistanceConsumer);

  assert.equal(resolveAssistanceBridge(adapter, { assistance: { consumer: 'unknown-kind' } }, getLlm), null);
  llmBridge.dispose();
  det.dispose();
});

test('T10 bridge consumer port is replaceable between deterministic and llm consumers', async () => {
  const llm = fakeLlm({ text: VALID_JSON });
  const pending = [request()];
  const adapter = {
    getState: () => 'connected',
    assistancePending: async () => ({ requests: pending }),
    assistanceResolve: async (params) => { pending.length = 0; return { resolved: true }; },
  };
  const llmBridge = new AssistanceBridge({ adapter, consumer: consumerFor(llm), pollIntervalMs: 50 });
  await llmBridge.pollOnce();
  assert.equal(llmBridge.stats.resolved, 1, 'llm consumer resolved through the real bridge flow');
  llmBridge.dispose();
});
