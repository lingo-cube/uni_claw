#!/usr/bin/env node
/**
 * Standalone DriverHost fixture server for the live demo / control-plane.
 *
 * Listens on 127.0.0.1:<port> (default 5177) and answers the frozen
 * read-only wire methods with a realistic MULTI-TASK universe: five runs in
 * different states (completed / running / failed / trapped / queued), each
 * with its own snapshot, event stream, trap, and evidence — so the control
 * plane can render a task list, per-task workbench, and detail panel.
 *
 * Read-only by construction: the frozen baseline carries no mutating method.
 *
 * Usage: node demo/demo-driverhost-server.mjs [--port <port>]
 */
import net from 'node:net';

const PORT = (() => {
  const i = process.argv.indexOf('--port');
  if (i >= 0 && process.argv[i + 1]) {
    const n = Number(process.argv[i + 1]);
    if (Number.isInteger(n) && n > 0 && n <= 65535) return n;
    console.error(`invalid --port ${process.argv[i + 1]}`);
    process.exit(2);
  }
  return 5177;
})();

/** Deterministic universe of five tasks, keyed by runId. */
const TASKS = {
  'run-wifi-settings-001': {
    state: 'completed',
    page: 'Settings > WiFi',
    goal: 'WifiConnectivity.Enabled=true',
    device: 'emulator-5554',
    scenario: 'WiFi settings traversal',
    summary: 'goal satisfied, switch verified on-device',
    events: [
      { eventId: 'evt-run-1-seq1', sequence: 1, kind: 'RunStarted', observationSequence: 0, payload: { scenario: 'wifi-settings' }, evidenceRefs: [] },
      { eventId: 'evt-run-1-seq3', sequence: 3, kind: 'TrapRaised', observationSequence: 3, payload: { trapKind: 'StateMismatch' }, evidenceRefs: [{ locator: 'capture:demo:record:1', kind: 'TraceFragment', runId: 'run-wifi-settings-001', observationSequence: 3, maturity: 'Captured', sizeBytes: 256 }] },
      { eventId: 'evt-run-1-seq5', sequence: 5, kind: 'RunCompleted', observationSequence: null, payload: { outcome: 'goal-satisfied' }, evidenceRefs: [] },
    ],
    trap: null,
    evidence: {
      ref: { locator: 'capture:demo:record:1', kind: 'TraceFragment', runId: 'run-wifi-settings-001', observationSequence: 3 },
      captureSessionId: 'capture-demo-1',
      record: { order: 3, kind: 'TraceFragment' },
      artifact: { artifactId: 'art-demo-1', fileName: 'trace-3.bin', contentHash: 'sha256:abc', byteCount: 256 },
    },
  },
  'run-wifi-settings-002': {
    state: 'running',
    page: 'Settings > WiFi',
    goal: 'WifiConnectivity.Enabled=true',
    device: 'emulator-5556',
    scenario: 'WiFi settings traversal (retry)',
    summary: 'executing step 4/8: traverse to Advanced',
    events: [
      { eventId: 'evt-run-2-seq1', sequence: 1, kind: 'RunStarted', observationSequence: 0, payload: { scenario: 'wifi-settings' }, evidenceRefs: [] },
      { eventId: 'evt-run-2-seq2', sequence: 2, kind: 'ObservationRecorded', observationSequence: 2, payload: { page: 'Settings > WiFi' }, evidenceRefs: [] },
      { eventId: 'evt-run-2-seq4', sequence: 4, kind: 'StepAdvanced', observationSequence: 4, payload: { step: 4, total: 8 }, evidenceRefs: [] },
    ],
    trap: null,
    evidence: null,
  },
  'run-bluetooth-pairing-001': {
    state: 'failed',
    page: 'Settings > Bluetooth',
    goal: 'Bluetooth.PairedDevice=true',
    device: 'emulator-5554',
    scenario: 'Bluetooth pairing flow',
    summary: 'pairing failed at discovery step',
    events: [
      { eventId: 'evt-run-3-seq1', sequence: 1, kind: 'RunStarted', observationSequence: 0, payload: { scenario: 'bluetooth-pairing' }, evidenceRefs: [] },
      { eventId: 'evt-run-3-seq2', sequence: 2, kind: 'ObservationRecorded', observationSequence: 2, payload: { page: 'Settings > Bluetooth' }, evidenceRefs: [] },
      { eventId: 'evt-run-3-seq3', sequence: 3, kind: 'RunFailed', observationSequence: null, payload: { cause: 'discovery-timeout' }, evidenceRefs: [] },
    ],
    trap: null,
    evidence: null,
  },
  'run-notifications-001': {
    state: 'trapped',
    page: 'Settings > Notifications',
    goal: 'Notifications.DND=true',
    device: 'emulator-5556',
    scenario: 'Do-Not-Disturb enable',
    summary: 'StateMismatch: expected true, observed false',
    events: [
      { eventId: 'evt-run-4-seq1', sequence: 1, kind: 'RunStarted', observationSequence: 0, payload: { scenario: 'notifications' }, evidenceRefs: [] },
      { eventId: 'evt-run-4-seq3', sequence: 3, kind: 'TrapRaised', observationSequence: 3, payload: { trapKind: 'StateMismatch' }, evidenceRefs: [{ locator: 'capture:demo:record:2', kind: 'TraceFragment', runId: 'run-notifications-001', observationSequence: 3, maturity: 'Captured', sizeBytes: 512 }] },
    ],
    trap: {
      value: {
        kind: 'StateMismatch',
        scope: 'notifications',
        expected: 'Notifications.DND=true',
        observed: 'Notifications.DND=false',
        source: 'observation:seq-3',
        evidence: ['capture:demo:record:2'],
        lastActionDescription: 'toggle DND switch',
      },
      classification: 'directPublicProjection',
    },
    evidence: {
      ref: { locator: 'capture:demo:record:2', kind: 'TraceFragment', runId: 'run-notifications-001', observationSequence: 3 },
      captureSessionId: 'capture-demo-2',
      record: { order: 3, kind: 'TraceFragment' },
      artifact: { artifactId: 'art-demo-2', fileName: 'trace-2.bin', contentHash: 'sha256:def', byteCount: 512 },
    },
  },
  'run-display-001': {
    state: 'queued',
    page: null,
    goal: 'Display.Brightness=50',
    device: 'emulator-5554',
    scenario: 'Brightness adjustment',
    summary: 'waiting for device slot',
    events: [
      { eventId: 'evt-run-5-seq1', sequence: 1, kind: 'RunStarted', observationSequence: 0, payload: { scenario: 'brightness' }, evidenceRefs: [] },
    ],
    trap: null,
    evidence: null,
  },
};


function snapshotDto(runId) {
  const t = TASKS[runId];
  return {
    runId,
    runState: { value: t.state, classification: 'directPublicProjection', truthSource: 'Agent.State (public read model)', isPartial: false },
    currentSemanticPage: t.page === null
      ? { value: null, classification: 'notCurrentlyAvailable', truthSource: null, isPartial: false }
      : { value: t.page, classification: 'directPublicProjection', truthSource: 'Container.SemanticPage', isPartial: false },
    activeTrap: t.trap === null
      ? { value: null, classification: 'directPublicProjection', truthSource: 'Agent.LastTrap (public read model)', isPartial: false }
      : { value: t.trap.value, classification: 'directPublicProjection', truthSource: 'Agent.LastTrap (public read model)', isPartial: false },
    currentGoal: { value: t.goal, classification: 'derivedReadModel', truthSource: 'RunSemanticGoal (derived read model)', isPartial: false },
    lastDecision: { value: t.state === 'queued' ? null : 'bind(' + (t.scenario.split(' ')[0].toLowerCase()) + ')', classification: 'directPublicProjection', truthSource: 'Agent.LastDecision', isPartial: false },
    lastAction: { value: t.state === 'queued' ? null : 'tap(primary-control)', classification: 'directPublicProjection', truthSource: 'Agent.LastAction', isPartial: false },
    recoveryState: { value: null, classification: 'notCurrentlyAvailable', truthSource: null, isPartial: false },
    latestGoalEvidence: { value: t.evidence?.ref?.locator ?? null, classification: 'directPublicProjection', truthSource: 'GoalEvidenceCatalog', isPartial: false },
    currentObservationSequence: { value: t.events.length, classification: 'directPublicProjection', truthSource: 'Agent.ObservationSequence', isPartial: false },
    currentContainerSummary: { value: t.page === null ? null : t.page + ', controls bound', classification: 'derivedReadModel', truthSource: 'Container.Bindings (derived)', isPartial: false },
    bindingsSummary: { value: t.page === null ? [] : [{ bindingId: 'b-1', semanticTarget: 'primary-control', state: 'bound' }], classification: 'directPublicProjection', truthSource: 'Container.Bindings', isPartial: false },
    stateBeliefsSummary: { value: t.page === null ? [] : [{ belief: t.goal.split('=')[0], value: t.goal.split('=')[1] ?? '', confidence: t.state === 'failed' ? 'contradicted' : 'verified' }], classification: 'directPublicProjection', truthSource: 'StateBeliefReducer', isPartial: false },
    diagnostics: t.history
      ? [`executedAt: ${t.history.startedAt}`, `durationMs: ${t.history.durationMs}`, `outcome: ${t.history.note}`]
      : [],
  };
}

function trapDto(runId) {
  const t = TASKS[runId];
  if (t.trap === null) {
    return { runId, found: false, trap: null, diagnostic: null };
  }
  return { runId, found: true, trap: { value: t.trap.value, classification: 'directPublicProjection' }, diagnostic: null };
}

function evidenceDto(runId) {
  const t = TASKS[runId];
  if (t.evidence === null) {
    return { found: false, diagnostic: 'no evidence for ' + runId };
  }
  return { found: true, ref: t.evidence.ref, captureSessionId: t.evidence.captureSessionId, record: t.evidence.record, artifact: t.evidence.artifact, diagnostic: null };
}

function eventsPageDto(runId, cursor) {
  const t = TASKS[runId];
  const all = t.events;
  // Cursor semantics (frozen run.events.after): return events with
  // sequence > cursor (cursor = last seen sequence; exclusive).
  const filtered = cursor !== undefined && cursor !== null
    ? all.filter((e) => e.sequence > cursor)
    : all;
  return {
    runId,
    events: filtered,
    nextCursor: { runId, lastSequence: all.length > 0 ? all[all.length - 1].sequence : 0 },
    hasMore: false,
    diagnostics: [],
  };
}

const server = net.createServer((socket) => {
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
      console.log(`[driverhost fixture] <- ${method} ${params?.runId ?? ''}`.trim());
      let result;
      if (method === 'ping') {
        result = { protocolVersion: 1, serviceName: 'dsh-uniclaw-driverhost' };
      } else if (method === 'run.list') {
        result = { runIds: ORDER };
      } else if (method === 'run.snapshot.get') {
        const runId = params?.runId ?? ORDER[0];
        result = TASKS[runId] ? snapshotDto(runId) : { error: { code: 'run_not_found', message: `no run ${runId}` } };
      } else if (method === 'run.trap.get') {
        const runId = params?.runId ?? ORDER[0];
        result = TASKS[runId] ? trapDto(runId) : { error: { code: 'run_not_found', message: `no run ${runId}` } };
      } else if (method === 'run.events.after') {
        const runId = params?.runId ?? ORDER[0];
        result = TASKS[runId] ? eventsPageDto(runId, params?.cursor) : { error: { code: 'run_not_found', message: `no run ${runId}` } };
      } else if (method === 'evidence.get') {
        const runId = params?.ref?.runId ?? ORDER[0];
        result = TASKS[runId] ? evidenceDto(runId) : { error: { code: 'run_not_found', message: `no run ${runId}` } };
      } else if (method === 'control.support') {
        result = { supported: [], deferred: ['start', 'pause', 'resume', 'stop', 'abort'] };
      } else {
        result = { error: { code: 'unknown_method', message: `no ${method}` } };
      }
      socket.write(`${JSON.stringify({ id, result })}\n`);
    }
  });
});

server.on('error', (err) => {
  console.error(`driverhost fixture error: ${err.message}`);
  process.exit(1);
});

server.listen(PORT, '127.0.0.1', () => {
  console.log(`[dsh-uniclaw driverhost fixture] listening on 127.0.0.1:${PORT} (frozen baseline, read-only)`);
  console.log(`[dsh-uniclaw driverhost fixture] tasks: ${ORDER.join(', ')}`);
});

/* ------------------------------------------------------------------ *
 * Historical task universe (demo data) — 8 completed/failed/trapped
 * runs with execution timestamps and durations, appended after the live
 * tasks so the control plane can show a history section.
 * ------------------------------------------------------------------ */

const DAY = 86400000;
const NOW = Date.now();
const HISTORY_SPECS = [
  { key: 'run-wifi-settings-101', state: 'completed', page: 'Settings > WiFi', goal: 'WifiConnectivity.Enabled=true', device: 'emulator-5554', scenario: 'WiFi settings traversal', ageMs: 1 * DAY, durMs: 42000, note: 'pass · switch verified' },
  { key: 'run-wifi-settings-102', state: 'failed', page: 'Settings > WiFi', goal: 'WifiConnectivity.Enabled=true', device: 'emulator-5556', scenario: 'WiFi settings traversal', ageMs: 2 * DAY, durMs: 93000, note: 'fail · toggle not found' },
  { key: 'run-bluetooth-pairing-101', state: 'completed', page: 'Settings > Bluetooth', goal: 'Bluetooth.PairedDevice=true', device: 'emulator-5554', scenario: 'Bluetooth pairing flow', ageMs: 3 * DAY, durMs: 68000, note: 'pass · paired OK' },
  { key: 'run-bluetooth-pairing-102', state: 'trapped', page: 'Settings > Bluetooth', goal: 'Bluetooth.PairedDevice=true', device: 'emulator-5556', scenario: 'Bluetooth pairing flow', ageMs: 4 * DAY, durMs: 121000, note: 'trap · pairing dialog mismatch' },
  { key: 'run-notifications-101', state: 'completed', page: 'Settings > Notifications', goal: 'Notifications.DND=true', device: 'emulator-5554', scenario: 'Do-Not-Disturb enable', ageMs: 5 * DAY, durMs: 31000, note: 'pass · DND enabled' },
  { key: 'run-display-101', state: 'completed', page: 'Settings > Display', goal: 'Display.Brightness=50', device: 'emulator-5554', scenario: 'Brightness adjustment', ageMs: 6 * DAY, durMs: 24000, note: 'pass · brightness set' },
  { key: 'run-display-102', state: 'failed', page: 'Settings > Display', goal: 'Display.Brightness=50', device: 'emulator-5556', scenario: 'Brightness adjustment', ageMs: 7 * DAY, durMs: 77000, note: 'fail · slider unreachable' },
  { key: 'run-display-103', state: 'completed', page: 'Settings > Display', goal: 'Display.Brightness=75', device: 'emulator-5554', scenario: 'Brightness raise', ageMs: 8 * DAY, durMs: 28000, note: 'pass · brightness raised' },
];

/** Merge the historical universe into TASKS (created after the live five). */
function attachHistory() {
  for (const spec of HISTORY_SPECS) {
    const startedAt = new Date(NOW - spec.ageMs);
    const finishedAt = new Date(startedAt.getTime() + spec.durMs);
    const fmt = (d) => d.toISOString().replace('T', ' ').slice(0, 19);
    TASKS[spec.key] = {
      state: spec.state,
      page: spec.page,
      goal: spec.goal,
      device: spec.device,
      scenario: spec.scenario,
      summary: spec.note,
      history: {
        startedAt: fmt(startedAt),
        finishedAt: fmt(finishedAt),
        durationMs: spec.durMs,
        note: spec.note,
      },
      events: [
        { eventId: `${spec.key}-seq1`, sequence: 1, kind: 'RunStarted', observationSequence: 0, payload: { scenario: spec.scenario }, evidenceRefs: [] },
        ...(spec.state === 'trapped'
          ? [{ eventId: `${spec.key}-seq3`, sequence: 3, kind: 'TrapRaised', observationSequence: 3, payload: { trapKind: 'StateMismatch' }, evidenceRefs: [{ locator: `capture:history:${spec.key}`, kind: 'TraceFragment', runId: spec.key, observationSequence: 3, maturity: 'Captured', sizeBytes: 384 }] }]
          : []),
        ...(spec.state === 'failed'
          ? [{ eventId: `${spec.key}-seq4`, sequence: 4, kind: 'RunFailed', observationSequence: null, payload: { cause: spec.note.split(' · ')[1] ?? 'failure' }, evidenceRefs: [] }]
          : []),
        ...(spec.state === 'completed'
          ? [{ eventId: `${spec.key}-seq5`, sequence: 5, kind: 'RunCompleted', observationSequence: null, payload: { outcome: 'goal-satisfied' }, evidenceRefs: [] }]
          : []),
      ],
      trap: spec.state === 'trapped' ? {
        value: {
          kind: 'StateMismatch',
          scope: spec.scenario.toLowerCase().split(' ')[0],
          expected: spec.goal,
          observed: `${spec.goal.split('=')[0]}=false`,
          source: 'observation:seq-3',
          evidence: [`capture:history:${spec.key}`],
          lastActionDescription: 'tap primary control',
        },
        classification: 'directPublicProjection',
      } : null,
      evidence: spec.state === 'trapped' ? {
        ref: { locator: `capture:history:${spec.key}`, kind: 'TraceFragment', runId: spec.key, observationSequence: 3 },
        captureSessionId: `capture-history-${spec.key}`,
        record: { order: 3, kind: 'TraceFragment' },
        artifact: { artifactId: `art-${spec.key}`, fileName: `trace-${spec.key}.bin`, contentHash: 'sha256:hist', byteCount: 384 },
      } : null,
    };
  }
}

attachHistory();

/** Task order: live tasks first (TASKS insertion order), then history. */
const ORDER = Object.keys(TASKS);
