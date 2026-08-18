#!/usr/bin/env node
/**
 * dsh-plugin-uniclaw — LIVE control-plane demo.
 *
 * Boots the REAL pinned DSH host (boot() from @deepseek-ai/dsh-app-boot,
 * real vendored cordis 4.0.1 + real loader) with the REAL plugin and a
 * wire-conformant DriverHost fixture serving a realistic wifi-settings run,
 * then executes every registered command through the REAL command registry
 * and prints the live transcript.
 *
 * This is a demo, not a test: it prints the actual command outputs so a human
 * can SEE the DSH → plugin → adapter → DriverHost control-plane chain work
 * end to end, including the Shadow Cognition slice.
 *
 * Pinned DSH checkout: READ-ONLY. Verified HEAD and empty porcelain before
 * boot; the demo never writes into it. Override the checkout with
 * `DSH_PINNED_REPO` (developer default = the local pinned checkout).
 *
 * Usage:  node demo/demo-control-plane.mjs
 */
import net from 'node:net';
import { execFileSync } from 'node:child_process';
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const repoRoot = join(here, '..', '..');

const PINNED_HEAD = '47f943859bef60e4160492346772ded9b24f765a';
const PIN = process.env.DSH_PINNED_REPO ?? '/Users/fran/Documents/Code/dk-harness';

const RUN_ID = 'run-wifi-settings-001';
const SHADOW_SESSION_ID = 'session-demo-001';

/* ------------------------------------------------------------------ *
 * Wire-conformant DriverHost fixture: one realistic wifi-settings run.
 * Speaks the frozen baseline DTOs only (no wire invention).
 * ------------------------------------------------------------------ */
function snapshotDto() {
  return {
    runId: RUN_ID,
    runState: { value: 'completed', classification: 'directPublicProjection', truthSource: 'Agent.State (public read model)', isPartial: false },
    currentSemanticPage: { value: 'Settings > WiFi', classification: 'directPublicProjection', truthSource: 'Container.SemanticPage', isPartial: false },
    activeTrap: { value: null, classification: 'directPublicProjection', truthSource: 'Agent.LastTrap (public read model)', isPartial: false },
    currentGoal: { value: 'WifiConnectivity.Enabled=true', classification: 'derivedReadModel', truthSource: 'RunSemanticGoal (derived read model)', isPartial: false },
    lastDecision: { value: 'bind(toggle-switch)', classification: 'directPublicProjection', truthSource: 'Agent.LastDecision', isPartial: false },
    lastAction: { value: 'tap(toggle-switch)', classification: 'directPublicProjection', truthSource: 'Agent.LastAction', isPartial: false },
    recoveryState: { value: null, classification: 'notCurrentlyAvailable', truthSource: null, isPartial: false },
    latestGoalEvidence: { value: 'capture:demo:record:1', classification: 'directPublicProjection', truthSource: 'GoalEvidenceCatalog', isPartial: false },
    currentObservationSequence: { value: 3, classification: 'directPublicProjection', truthSource: 'Agent.ObservationSequence', isPartial: false },
    currentContainerSummary: { value: 'WiFi settings page, 1 toggle control bound', classification: 'derivedReadModel', truthSource: 'Container.Bindings (derived)', isPartial: false },
    bindingsSummary: { value: [{ bindingId: 'b-1', semanticTarget: 'toggle-switch', state: 'bound' }], classification: 'directPublicProjection', truthSource: 'Container.Bindings', isPartial: false },
    stateBeliefsSummary: { value: [{ belief: 'WifiConnectivity.Enabled', value: 'true', confidence: 'verified' }], classification: 'directPublicProjection', truthSource: 'StateBeliefReducer', isPartial: false },
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
        source: 'observation:seq-3',
        evidence: ['capture:demo:record:1'],
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
    ref: { locator: 'capture:demo:record:1', kind: 'TraceFragment', runId: RUN_ID, observationSequence: 3 },
    captureSessionId: 'capture-demo-1',
    record: { order: 3, kind: 'TraceFragment' },
    artifact: { artifactId: 'art-demo-1', frameId: null, fileName: 'trace-3.bin', contentHash: 'sha256:abc', byteCount: 256 },
    diagnostic: null,
  };
}

function eventsPageDto() {
  return {
    runId: RUN_ID,
    events: [
      {
        eventId: 'evt-trap-1', runId: RUN_ID, sequence: 3, kind: 'TrapRaised',
        correlationId: null, causationId: null, observationSequence: 3,
        evidenceRefs: [{ locator: 'capture:demo:record:1', kind: 'TraceFragment', runId: RUN_ID, observationSequence: 3, contentIdentity: null, maturity: 'Captured', sizeBytes: 256 }],
        payload: { trapKind: 'StateMismatch' },
      },
      {
        eventId: 'evt-completed-1', runId: RUN_ID, sequence: 5, kind: 'RunCompleted',
        correlationId: null, causationId: null, observationSequence: null,
        evidenceRefs: [],
        payload: { outcome: 'goal-satisfied' },
      },
    ],
    nextCursor: { runId: RUN_ID, lastSequence: 5 },
    hasMore: false,
    diagnostics: [],
  };
}

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
            result = { runIds: [RUN_ID] };
          } else if (method === 'run.snapshot.get') {
            result = snapshotDto();
          } else if (method === 'run.trap.get') {
            result = trapDto();
          } else if (method === 'run.events.after') {
            result = eventsPageDto();
          } else if (method === 'evidence.get') {
            result = evidenceDto();
          } else if (method === 'control.support') {
            result = { supported: [], deferred: ['start', 'pause', 'resume', 'stop', 'abort'] };
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

function verifyPinnedCheckout() {
  let head;
  let porcelain;
  try {
    head = execFileSync('git', ['-C', PIN, 'rev-parse', 'HEAD'], { encoding: 'utf8' }).trim();
    porcelain = execFileSync('git', ['-C', PIN, 'status', '--porcelain'], { encoding: 'utf8' }).trim();
  } catch (err) {
    throw new Error(`cannot inspect pinned checkout at ${PIN}: ${err instanceof Error ? err.message : String(err)}`);
  }
  if (head !== PINNED_HEAD) {
    throw new Error(`pinned checkout HEAD ${head} != ${PINNED_HEAD}`);
  }
  if (porcelain !== '') {
    throw new Error('pinned checkout is not clean (porcelain non-empty)');
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

/** REAL LlmAdapter subclass: emits one deterministic text-delta + success finish. */
function makeFakeAdapter(awaitImportedLlm) {
  const { LlmAdapter } = awaitImportedLlm;
  class FakeAdapter extends LlmAdapter {
    constructor() {
      super();
      this.calls = 0;
    }
    resolveModel(provider, model, signal) {
      return Promise.resolve({ provider, id: model, name: model });
    }
    async *stream(options) {
      this.calls += 1;
      const text = JSON.stringify({
        humanSummary: 'The wifi run completed after a trap was raised and recovered. Switch state verified on-device.',
        observedFacts: [
          { claim: 'WiFi toggle ended bound and verified', classification: 'kernel-fact' },
        ],
        hypotheses: [
          { claim: 'the first tap may have missed the switch target', supportingRefs: ['evt-trap-1'], flaggedUncertain: true },
        ],
        uncertainties: [
          { topic: 'tap precision', reason: 'no raw-pixel confidence evidence in the bounded window' },
        ],
        recommendations: [
          { text: 'inspect the switch state on the device', target: 'human-investigation' },
        ],
      });
      yield { type: 'text-delta', index: 0, text };
      yield { type: 'finish', reason: { kind: 'success' } };
    }
  }
  return new FakeAdapter();
}

/* ------------------------------------------------------------------ */

async function main() {
  console.log('═══════════════════════════════════════════════════════════════');
  console.log('  dsh-plugin-uniclaw — DSH → UniClaw control-plane LIVE demo');
  console.log(`  pinned DSH: ${PINNED_HEAD} (0.1.0-rc.5) | cordis 4.0.1 (vendored fork)`);
  console.log('═══════════════════════════════════════════════════════════════\n');

  verifyPinnedCheckout();
  console.log('• pinned DSH checkout verified (HEAD + clean porcelain)\n');

  const fixture = await startFixture();
  console.log(`• DriverHost fixture listening on 127.0.0.1:${fixture.port} (frozen baseline DTOs)`);

  const tempDir = mkdtempSync(join(tmpdir(), 'dsh-uniclaw-demo-'));
  const configPath = join(tempDir, 'cordis.yml');
  writeFileSync(configPath, [
    '# demo composition: real commands + real llm + dsh-plugin-uniclaw',
    '- id: commands',
    `  name: ${join(PIN, 'packages', 'interaction', 'commands', 'lib', 'index.js')}`,
    '- id: llm',
    `  name: ${join(PIN, 'packages', 'llm', 'llm', 'lib', 'index.js')}`,
    '- id: dsh-plugin-uniclaw',
    `  name: ${join(repoRoot, 'dsh-plugin-uniclaw', 'src', 'plugin.js')}`,
    '  config:',
    '    host: 127.0.0.1',
    `    port: ${fixture.port}`,
    '    shadow:',
    '      model:',
    '        provider: demo-shadow',
    '        model: demo-1',
    '',
  ].join('\n'));

  process.env.DSH_PLUGIN_CORDIS_PACKAGE_JSON = join(PIN, 'vendor', 'cordis', 'package.json');

  const { boot } = await import(pathToFileURL(join(PIN, 'packages', 'boot', 'app-boot', 'lib', 'index.js')).href);
  const { Session, SessionId } = await import(pathToFileURL(join(PIN, 'packages', 'core', 'session', 'lib', 'index.js')).href);
  const { LlmAdapter } = await import(pathToFileURL(join(PIN, 'packages', 'llm', 'llm', 'lib', 'index.js')).href);

  const ctx = await boot('dsh-uniclaw-demo', configPath);
  console.log('• REAL pinned DSH host booted (real loader, real vendored cordis)');
  console.log('• dsh-plugin-uniclaw activated, inject: [commands] satisfied\n');

  await waitFor(() => ctx.get('uniclaw')?.adapter?.state === 'connected', 'adapter handshake');
  console.log('• adapter connected to the DriverHost fixture\n');

  // Optional real ctx.llm seam for the shadow command.
  const llm = ctx.get('llm');
  const fake = makeFakeAdapter({ LlmAdapter });
  const handle = llm.registerAdapter(['demo-shadow'], fake);

  const commands = ctx.get('commands');
  const agentSession = Session.create(SessionId(SHADOW_SESSION_ID));

  const invocations = [
    ['/uniclaw-runs-list', 'list registered runs'],
    ['/uniclaw-inspect-run run-wifi-settings-001', 'classified read-only snapshot'],
    ['/uniclaw-inspect-trap run-wifi-settings-001', 'classified active trap'],
    ['/uniclaw-evidence-open capture:demo:record:1 run-wifi-settings-001', 'logical evidence ref (metadata only)'],
    ['/uniclaw-shadow-analyze run-wifi-settings-001 --focus trap --reason demo live', 'Shadow Cognition (COGNITIVE_INFERENCE)'],
  ];

  for (const [rawInput, label] of invocations) {
    console.log(`───────────────────────────────────────────────────────────────`);
    console.log(`$ ${rawInput}`);
    console.log(`  (${label})`);
    console.log('───────────────────────────────────────────────────────────────');
    const executed = await commands.execute(
      { session: agentSession },
      rawInput,
      new AbortController().signal,
    );
    const text = String(executed?.result?.text ?? '(no text)');
    console.log(text);
    console.log(`\n  → kind: ${executed?.result?.kind ?? 'n/a'}\n`);
  }

  const wireLine = fixture.methods.join(', ');
  console.log('═══════════════════════════════════════════════════════════════');
  console.log('  wire methods actually requested of DriverHost (read-only):');
  console.log(`  ${wireLine}`);
  console.log('  model calls: ' + fake.calls + ' (single one-shot ctx.llm call in the shadow command)');
  console.log('  session events written: ' + agentSession.events.map((e) => e.type).join(', '));
  console.log('═══════════════════════════════════════════════════════════════\n');

  handle?.();
  await ctx.fiber.dispose();
  closeFixture(fixture);
  try {
    rmSync(tempDir, { recursive: true, force: true });
  } catch {
    // best-effort self-cleanup
  }
  console.log('• clean teardown: plugin effect disposer ran, temp config removed');
}

main().catch((err) => {
  console.error('demo failed:', err instanceof Error ? err.message : String(err));
  process.exitCode = 1;
});
