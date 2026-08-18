/**
 * dsh-plugin-uniclaw — DeepSeek Harness → UniClaw control-plane adapter.
 *
 * Responsibilities (bounded vertical slice, per the frozen protocol baseline):
 *  - DSH plugin lifecycle: activation guard, session/event subscription,
 *    deterministic command registration, dispose cleanup.
 *  - DriverHost connection boundary: loopback TCP JSON-RPC client, bounded
 *    reconnect, fresh-state guarantee.
 *  - Read-only consumption: classified snapshots, runtime events, traps, and
 *    logical evidence refs. NO Kernel-mutating authority exists here.
 *  - Deterministic human control seam: control operations are audited via
 *    control.support and reported as deferred when the Kernel has no buyer.
 *
 * This module never calls out to any inference service, never dispatches an
 * action, and never fabricates Kernel facts.
 */
import { createRequire } from 'node:module';
import { readFileSync } from 'node:fs';
import { UniClawAdapter } from './adapter.js';
import { buildCommands } from './commands.js';
import { createShadowCache, resolveShadowConfig, runShadowAnalysis, validateShadowConfig } from './shadow/index.js';
import { AssistanceBridge } from './assistance/bridge.js';
import { DeterministicAssistanceConsumer } from './assistance/consumer.js';
import { LlmAssistanceConsumer } from './assistance/llm-consumer.js';

/** Pinned DSH baseline the plugin is built against (READ-ONLY checkout). */
export const DSH_BASELINE = '47f943859bef60e4160492346772ded9b24f765a';

/** DSH version the baseline belongs to. */
export const DSH_VERSION = '0.1.0-rc.5';

/** Exact vendored cordis fork version the plugin requires. */
export const CORDIS_REQUIRED_VERSION = '4.0.1';

const require = createRequire(import.meta.url);

/**
 * Locate the cordis package.json: resolve the fork as installed, falling back
 * to an explicit env-provided path (DSH_PLUGIN_CORDIS_PACKAGE_JSON) for
 * runtimes where the fork is not on the plugin's resolution chain.
 */
export function resolveCordisPackageJsonPath() {
  try {
    return require.resolve('@deepseek-ai/cordis/package.json');
  } catch {
    const explicit = process.env.DSH_PLUGIN_CORDIS_PACKAGE_JSON;
    if (explicit) {
      return explicit;
    }
    throw new Error(
      'cannot resolve @deepseek-ai/cordis package.json; set DSH_PLUGIN_CORDIS_PACKAGE_JSON to the vendored fork path',
    );
  }
}

/** Read the resolved cordis version. */
export function readCordisVersion() {
  const packagePath = resolveCordisPackageJsonPath();
  const manifest = JSON.parse(readFileSync(packagePath, 'utf8'));
  if (!manifest || typeof manifest.version !== 'string') {
    throw new Error(`invalid cordis manifest at ${packagePath}: missing version`);
  }
  return manifest.version;
}

/** Refuse activation when the loaded cordis fork is not the pinned version. */
export function assertCordisVersion(version) {
  if (version !== CORDIS_REQUIRED_VERSION) {
    throw new Error(
      `dsh-plugin-uniclaw requires @deepseek-ai/cordis ${CORDIS_REQUIRED_VERSION} but loaded ${version}; refusing activation`,
    );
  }
  return version;
}

/**
 * Assistance consumer selection (composition policy — dsh-assistance-provider-adapter
 * + dsh-assistance-consumer-selection): OPT-IN only.
 *   · no configured consumer            ⇒ null ⇒ NO bridge ⇒ bounded fail-closed
 *     (a DriverHost consult times out and the Agent fails closed);
 *   · 'deterministic'                   ⇒ DeterministicAssistanceConsumer
 *     (explicit test/demo profile);
 *   · 'llm'                             ⇒ LlmAssistanceConsumer (REAL L1 consumer)
 *     behind the optional ctx.llm seam; provider/model come from
 *     config.assistance.llm (COMPOSITION_POLICY).
 *   · unknown value                     ⇒ null (resolve to unavailable).
 * Fixture-specific semantics never silently affect normal production; no hidden
 * intelligence substitution (never llm → deterministic fallback).
 */
export function resolveAssistanceBridge(adapter, config, getLlm) {
  const consumerKind = config?.assistance?.consumer;
  if (consumerKind === 'deterministic') {
    return new AssistanceBridge({ adapter, consumer: new DeterministicAssistanceConsumer() });
  }
  if (consumerKind === 'llm') {
    const llmConfig = config?.assistance?.llm ?? {};
    const consumer = new LlmAssistanceConsumer({
      getLlm: typeof getLlm === 'function' ? getLlm : () => null,
      provider: typeof llmConfig.provider === 'string' ? llmConfig.provider : null,
      model: typeof llmConfig.model === 'string' ? llmConfig.model : null,
    });
    return new AssistanceBridge({ adapter, consumer });
  }
  return null;
}

/** Build the service surface exposed to the cordis bus. */
function buildService(adapter) {
  return Object.freeze({
    adapter,
    ping: () => adapter.ping(),
    listRuns: () => adapter.listRuns(),
    getRunSnapshot: (runId) => adapter.getRunSnapshot(runId),
    getTrap: (runId) => adapter.getTrap(runId),
    getRuntimeEvents: (runId, cursor) => adapter.getRuntimeEvents(runId, cursor),
    drainRunEvents: (runId) => adapter.drainRunEvents(runId),
    getEvidence: (evidenceRef) => adapter.getEvidence(evidenceRef),
    controlSupport: (operation) => adapter.controlSupport(operation),
  });
}

function validateConfig(config) {
  const { host, port, timeoutMs, maxAttempts, backoffMs, shadow } = config ?? {};
  const validated = {};
  if (host !== undefined) validated.host = host;
  if (port !== undefined) {
    if (!Number.isInteger(port) || port <= 0) {
      throw new TypeError('dsh-plugin-uniclaw requires a positive integer port');
    }
    validated.port = port;
  }
  if (timeoutMs !== undefined) validated.timeoutMs = timeoutMs;
  if (maxAttempts !== undefined) validated.maxAttempts = maxAttempts;
  if (backoffMs !== undefined) validated.backoffMs = backoffMs;
  if (shadow !== undefined) validated.shadow = validateShadowConfig(shadow);
  return validated;
}

/**
 * Narrowed read-only retrieval facade for the shadow module (design §14.2):
 * snapshot / bounded events / trap / evidence resolution only. No adapter
 * internals, no control surface, no run listing beyond the read surface.
 */
function buildShadowFacade(service) {
  return Object.freeze({
    getRunSnapshot: (runId) => service.getRunSnapshot(runId),
    getRuntimeEvents: (runId) => service.getRuntimeEvents(runId, null),
    getTrap: (runId) => service.getTrap(runId),
    getEvidence: (evidenceRef) => service.getEvidence(evidenceRef),
  });
}

export default {
  name: 'dsh-plugin-uniclaw',

  // Deterministic dependency declaration (real DSH consumer convention): the
  // loader defers activation until the commands service exists, so command
  // registration below can never be silently skipped by parallel entry
  // activation racing the registry initialization.
  inject: ['commands'],

  apply(ctx, config) {
    // Activation guard: never activate against an unpinned cordis fork.
    assertCordisVersion(readCordisVersion());

    const adapter = new UniClawAdapter(validateConfig(config));
    const disposers = [];

    // Source-compatible service registration: 'uniclaw' on the cordis bus.
    const service = buildService(adapter);
    ctx.provide('uniclaw', service);

    // Shadow Cognition (V1 — EPHEMERAL_PROCESS_LOCAL, design §9.2):
    // - narrowed read-only facade (no adapter internals, §14.2);
    // - optional `ctx.llm` seam: read via ctx.get('llm') only — NOT injected,
    //   so activation never depends on an inference service (§10);
    // - bounded process-local cache (convenience only, never authoritative);
    // - minimal `shadow` service providing the same bounded analyze surface.
    // NOTE: validateShadowConfig directly (not validateConfig, whose whitelist
    // is the adapter's host/port surface and would drop shadow.model).
    const shadowConfig = resolveShadowConfig(validateShadowConfig(config?.shadow));
    const facade = buildShadowFacade(service);
    const cache = createShadowCache();
    const getLlm = () => ctx.get('llm');
    const shadowContext = { config: shadowConfig, facade, getLlm, cache };
    ctx.provide('shadow', Object.freeze({
      analyze: (request) => runShadowAnalysis({
        facade,
        llm: getLlm(),
        config: shadowConfig,
        cache,
        request,
      }),
      cache: Object.freeze({
        get: (runId) => cache.get(runId),
        size: () => cache.size,
        clear: () => cache.clear(),
      }),
    }));

    // Deterministic command registration (zero inference calls).
    const commands = ctx.get('commands');
    if (commands && typeof commands.register === 'function') {
      for (const definition of buildCommands(adapter, shadowContext)) {
        try {
          const dispose = commands.register(definition);
          if (typeof dispose === 'function') disposers.push(dispose);
        } catch (err) {
          ctx.emit('uniclaw/connection', { state: 'commands-registration-error', message: err?.message });
        }
      }
    }

    // DSH lifecycle consumption: connect exactly once per entered session.
    // `session/created` is the cordis attach event (DSH emits it with the
    // session as its single argument); `session/event` is the firehose of
    // session-log appends (subject, event). Connection resilience follows
    // session liveness with a bounded backoff guard, so a missing DriverHost
    // can never cause unbounded retry churn.
    ctx.on('session/created', () => {
      adapter.ensureConnected().catch(() => {});
    });

    const FIREHOSE_RECONNECT_BACKOFF_MS = 2000;
    let firehoseBackoffUntil = 0;
    ctx.on('session/event', () => {
      if (adapter.getState() !== 'connected' && Date.now() >= firehoseBackoffUntil) {
        firehoseBackoffUntil = Date.now() + FIREHOSE_RECONNECT_BACKOFF_MS;
        adapter.ensureConnected().catch(() => {});
      }
    });

    // Plugin-owned live events: connection state observability only.
    adapter.onConnectionChange = (state) => ctx.emit('uniclaw/connection', { state });

    // Assistance bridge (dsh-assistance-provider-adapter): provider-agnostic
    // protocol translator + injectable Harness consumer. The consumer is OPT-IN
    // by composition policy (resolveAssistanceBridge): no configured consumer ⇒
    // no bridge ⇒ bounded fail-closed; 'deterministic' ⇒ explicit test/demo
    // profile. Fixture-specific semantics never silently affect production.
    const bridge = resolveAssistanceBridge(adapter, config, () => ctx.get('llm'));
    if (bridge) bridge.start();

    // Canonical cleanup hook for this cordis fork: the effect's returned
    // disposer runs when the plugin fiber unloads.
    ctx.effect(() => () => {
      bridge?.dispose();
      for (const dispose of disposers) {
        try {
          dispose();
        } catch {
          // best-effort command unregistration
        }
      }
      cache.clear();
      adapter.dispose();
    });

    // Non-blocking first connection attempt; activation never fails on transport.
    adapter.ensureConnected().catch(() => {});
  },
};
