/**
 * AssistanceBridge + DeterministicAssistanceConsumer tests
 * (dsh-assistance-provider-adapter A3/A3b): provider-agnostic bridge flow
 * (poll → normalize → consumer → translate → resolve), duplicate requestId
 * suppression, reconnect-safe polling, consumer port replaceability, whitelist
 * normalization, and the ZERO hard llm/model dependency static guard on
 * src/assistance/.
 */
import test from 'node:test';
import assert from 'node:assert/strict';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { readdirSync, readFileSync, statSync } from 'node:fs';

const here = dirname(fileURLToPath(import.meta.url));
const srcDir = join(here, '..', 'src');

const { AssistanceBridge } = await import(join(srcDir, 'assistance', 'bridge.js'));
const { DeterministicAssistanceConsumer } = await import(join(srcDir, 'assistance', 'consumer.js'));
const { resolveAssistanceBridge } = await import(join(srcDir, 'plugin.js'));

function pendingRequest(overrides = {}) {
  return {
    requestId: 'assist-run-1-1',
    runId: 'run-1',
    semanticPage: 'Settings',
    beliefState: 'Contradicted',
    worldVersion: 7,
    observation: { sequence: 2, foregroundApplication: 'settings', elementCount: 2, elementTexts: ['Wi‑Fi', ''] },
    ...overrides,
  };
}

function mockAdapter(overrides = {}) {
  const calls = { pending: 0, resolve: [] };
  return {
    getState: () => 'connected',
    assistancePending: async () => {
      calls.pending += 1;
      return { requests: [pendingRequest()] };
    },
    assistanceResolve: async (params) => {
      calls.resolve.push(params);
      return { resolved: true, diagnostic: null };
    },
    ...overrides,
    _calls: calls,
  };
}

test('bridge: poll → normalize → consumer → translate → resolve (full flow)', async () => {
  const adapter = mockAdapter();
  const consumed = [];
  const bridge = new AssistanceBridge({
    adapter,
    consumer: {
      resolve: (request) => {
        consumed.push(request);
        return { recommendation: 're-observe', reason: 'consumer says re-observe' };
      },
    },
  });

  await bridge.pollOnce();

  assert.equal(adapter._calls.pending, 1, 'polled assistance.pending');
  assert.equal(consumed.length, 1, 'consumer invoked exactly once');
  assert.equal(consumed[0].requestId, 'assist-run-1-1');
  assert.equal(consumed[0].semanticPage, 'Settings');
  assert.equal(consumed[0].beliefState, 'Contradicted');
  assert.equal(consumed[0].worldVersion, 7);
  assert.equal(consumed[0].observation.elementCount, 2);

  assert.equal(adapter._calls.resolve.length, 1, 'resolved exactly once');
  assert.deepEqual(adapter._calls.resolve[0], {
    requestId: 'assist-run-1-1',
    worldVersion: 7,
    recommendation: 're-observe',
    additionalEvidence: null,
    reason: 'consumer says re-observe',
  });
  assert.equal(bridge.stats.resolved, 1);
});

test('bridge: duplicate requestId is suppressed across polls', async () => {
  let pending = true;
  const adapter = mockAdapter({
    assistancePending: async () => ({ requests: pending ? [pendingRequest()] : [] }),
  });
  const bridge = new AssistanceBridge({ adapter, consumer: new DeterministicAssistanceConsumer() });

  await bridge.pollOnce();
  await bridge.pollOnce(); // same pending request again

  assert.equal(adapter._calls.resolve.length, 1, 'same request resolved only once');
  pending = false;
  await bridge.pollOnce(); // now empty
  assert.equal(adapter._calls.resolve.length, 1);
});

test('bridge: reconnect-safe — skips polling while disconnected, retries later', async () => {
  let state = 'disconnected';
  const adapter = mockAdapter({
    getState: () => state,
    assistancePending: async () => ({ requests: [pendingRequest()] }),
  });
  const bridge = new AssistanceBridge({ adapter, consumer: new DeterministicAssistanceConsumer() });

  await bridge.pollOnce(); // disconnected → skip
  assert.equal(adapter._calls.resolve.length, 0, 'no resolve while disconnected');

  state = 'connected';
  await bridge.pollOnce(); // connected → resolves
  assert.equal(adapter._calls.resolve.length, 1, 'resolves after reconnect');
});

test('bridge: consumer port is replaceable (stub consumer)', async () => {
  const adapter = mockAdapter();
  const stubConsumer = {
    resolve: () => ({ recommendation: 'rebind', reason: 'stub consumer' }),
  };
  const bridge = new AssistanceBridge({ adapter, consumer: stubConsumer });
  await bridge.pollOnce();

  assert.equal(adapter._calls.resolve[0].recommendation, 'rebind');
  assert.equal(bridge.consumer, stubConsumer, 'consumer instance is the injected one');
});

test('consumer: deterministic mapping — Settings → re-observe, other page → abandon', () => {
  const consumer = new DeterministicAssistanceConsumer();
  const onSettings = consumer.resolve({ requestId: 'x', semanticPage: 'Settings' });
  assert.equal(onSettings.recommendation, 're-observe');

  const other = consumer.resolve({ requestId: 'x', semanticPage: 'OtherPage' });
  assert.equal(other.recommendation, null);
});

test('consumer: un-whitelisted recommendation is suppressed to abandon', () => {
  const consumer = new DeterministicAssistanceConsumer({
    responder: () => ({ recommendation: 'not-a-real-recommendation' }),
  });
  const result = consumer.resolve({ requestId: 'x', semanticPage: 'Settings' });
  assert.equal(result.recommendation, null);
  assert.match(result.reason, /suppressed/);
});

test('consumer: injectable responder and malformed-input safety', () => {
  const consumer = new DeterministicAssistanceConsumer({
    responder: () => ({ recommendation: 'dismiss-obstruction', reason: 'custom' }),
  });
  assert.equal(consumer.resolve({ requestId: 'x', semanticPage: 'S' }).recommendation, 'dismiss-obstruction');
  assert.equal(consumer.resolve(null).recommendation, null);
});

test('STATIC GUARD (bridge purity): AssistanceBridge has zero llm/model dependency', () => {
  // §11: ONLY the bridge must be free of llm/model seams. The consumer files MAY
  // depend on the LLM seam (that is their role); the bridge must not.
  const bridgeSource = readFileSync(join(srcDir, 'assistance', 'bridge.js'), 'utf8');
  const forbidden = /dsh-llm|@deepseek-ai\/llm|ctx\.get\(['"]llm|modelProvider|openai|deepseek-ai\/model|model:|prompt|system:/i;
  assert.ok(!forbidden.test(bridgeSource), 'AssistanceBridge references no llm/model/prompt seam');
});

test('STATIC GUARD (consumer purity): llm-consumer never emits execution/authority tokens', () => {
  // T12: the consumer must not expose DeviceAction, goal completion, or
  // belief/binding mutation vocabulary.
  const consumerSource = readFileSync(join(srcDir, 'assistance', 'llm-consumer.js'), 'utf8');
  const forbidden = /\bDeviceAction\b|\bGoalSatisfied\b|\bElementIndex\b|\bbelief mutation\b|\bbinding mutation\b/i;
  assert.ok(!forbidden.test(consumerSource), 'llm-consumer carries no execution/authority vocabulary');
});

test('GRADUATION: consumer is OPT-IN — no configured consumer ⇒ no bridge ⇒ bounded fail-closed', () => {
  const adapter = mockAdapter();
  // Default production config: NO assistance consumer ⇒ NO bridge. A DriverHost
  // consult then simply times out (bounded) and the Agent fails closed — the
  // deterministic fixture behavior NEVER silently affects normal production.
  assert.equal(resolveAssistanceBridge(adapter, {}), null);
  assert.equal(resolveAssistanceBridge(adapter, undefined), null);
  assert.equal(resolveAssistanceBridge(adapter, { assistance: {} }), null);
  assert.equal(resolveAssistanceBridge(adapter, { assistance: { consumer: 'unknown-kind' } }), null);
});

test('GRADUATION: deterministic consumer requires an explicit test/demo profile config', () => {
  const adapter = mockAdapter();
  const bridge = resolveAssistanceBridge(adapter, { assistance: { consumer: 'deterministic' } });
  assert.ok(bridge instanceof AssistanceBridge, 'explicit deterministic config builds the bridge');
  assert.ok(bridge.consumer instanceof DeterministicAssistanceConsumer);
  bridge.dispose();
});
