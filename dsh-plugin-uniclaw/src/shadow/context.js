/**
 * Deterministic bounded context assembly (design §8, D3).
 *
 * Retrieval FIRST (zero-model), then one bounded context:
 *  1. latest `RunSnapshot` — always, exactly one in context (no history dumps);
 *  2. bounded recent `RuntimeEvent` window — most recent `maxEvents` by
 *     sequence (the wire carries no event timestamps, so the deterministic
 *     recent-window bound is count-based, never age-fabricated);
 *  3. trap detail — only when `focus: 'trap'`;
 *  4. `EvidenceRef`s — referenced as bounded logical locators; resolved LAZILY
 *     only when the analysis buyer requires content (trap focus), capped by
 *     `maxEvidenceRefs` and `evidenceBytesPerRef`, metadata only — content is
 *     never embedded into the artifact or the model context.
 *
 * All caps are enforced deterministically; trimming drops oldest non-priority
 * events first, then shortens lines, then hard-slices at the boundary. The
 * final context NEVER exceeds `maxContextChars`.
 */
'use strict';

import { fact, uncertainty } from './analysis.js';

/** Frozen default limits (design §8; config defaults in tasks.md Slice 1). */
export const DEFAULT_LIMITS = Object.freeze({
  maxEvents: 200,
  maxContextChars: 80000,
  maxEvidenceRefs: 8,
  evidenceBytesPerRef: 8192,
});

/** Snapshot field labels rendered in a stable order (wire DTO field names). */
export const SNAPSHOT_FIELDS = Object.freeze([
  'runState',
  'currentSemanticPage',
  'activeTrap',
  'currentGoal',
  'lastDecision',
  'lastAction',
  'recoveryState',
  'latestGoalEvidence',
  'currentObservationSequence',
  'currentContainerSummary',
  'bindingsSummary',
  'stateBeliefsSummary',
]);

/** RuntimeEvent kinds that are causal/terminal anchors — never trimmed away. */
const PRIORITY_EVENT_KINDS = new Set([
  'TrapRaised',
  'RunFailed',
  'RunCompleted',
  'RecoveryStarted',
  'GoalEvidenceProduced',
]);

/** Per-line bounds used by the deterministic trimmer. */
const EVENT_LINE_MAX_CHARS = 240;
const EVENT_PAYLOAD_MAX_CHARS = 500;
const REASON_MAX_CHARS = 2000;

function normalizeLimits(limits) {
  return {
    maxEvents: Number.isInteger(limits?.maxEvents) && limits.maxEvents > 0 ? limits.maxEvents : DEFAULT_LIMITS.maxEvents,
    maxContextChars: Number.isInteger(limits?.maxContextChars) && limits.maxContextChars > 0 ? limits.maxContextChars : DEFAULT_LIMITS.maxContextChars,
    maxEvidenceRefs: Number.isInteger(limits?.maxEvidenceRefs) && limits.maxEvidenceRefs > 0 ? limits.maxEvidenceRefs : DEFAULT_LIMITS.maxEvidenceRefs,
    evidenceBytesPerRef: Number.isInteger(limits?.evidenceBytesPerRef) && limits.evidenceBytesPerRef > 0 ? limits.evidenceBytesPerRef : DEFAULT_LIMITS.evidenceBytesPerRef,
  };
}

function stableJson(value) {
  if (value === null || value === undefined) return 'null';
  try {
    const text = JSON.stringify(value);
    return text === undefined ? String(value) : text;
  } catch {
    return '[unserializable]';
  }
}

function refLocator(ref) {
  return typeof ref?.locator === 'string' ? ref.locator : null;
}

/** Dedupe bounded logical EvidenceRefs from the event window (locator-keyed). */
export function selectEvidenceRefs(events, maxEvidenceRefs) {
  const seen = new Set();
  const refs = [];
  for (const event of events ?? []) {
    const eventRefs = Array.isArray(event?.evidenceRefs) ? event.evidenceRefs : [];
    for (const ref of eventRefs) {
      const locator = refLocator(ref);
      if (locator === null) continue;
      if (seen.has(locator)) continue;
      seen.add(locator);
      refs.push({
        locator,
        kind: typeof ref.kind === 'string' ? ref.kind : null,
        maturity: typeof ref.maturity === 'string' ? ref.maturity : null,
        sizeBytes: Number.isInteger(ref.sizeBytes) ? ref.sizeBytes : null,
        runId: typeof ref.runId === 'string' ? ref.runId : null,
      });
      if (refs.length >= maxEvidenceRefs) return refs;
    }
  }
  return refs;
}

/**
 * Deterministically build observed facts from the read surface. Never invents:
 * a field with no value yields a `missing-data` uncertainty instead of a fact.
 */
export function buildFacts({ snapshot, events, trap, resolved, focus }) {
  const facts = [];
  const uncertainties = [];
  const unknownRun = Boolean(snapshot && Array.isArray(snapshot.diagnostics) && snapshot.diagnostics.length > 0);

  if (snapshot && typeof snapshot === 'object' && !unknownRun) {
    for (const label of SNAPSHOT_FIELDS) {
      const field = snapshot[label];
      const hasValue = field && field.value !== null && field.value !== undefined;
      if (hasValue) {
        facts.push(fact(
          `${label}: ${stableJson(field.value)}`,
          'derived-read-model',
          { kind: 'RunSnapshot', field: label, truthSource: typeof field.truthSource === 'string' ? field.truthSource : null },
        ));
      } else {
        uncertainties.push(uncertainty(`snapshot field ${label}`, 'missing-data'));
      }
    }
  }

  if (focus === 'trap' && trap && typeof trap === 'object' && trap.found === true && trap.trap?.value) {
    const value = trap.trap.value;
    const parts = [];
    if (typeof value.kind === 'string') parts.push(`kind=${value.kind}`);
    if (typeof value.scope === 'string') parts.push(`scope=${value.scope}`);
    if (value.expected !== null && value.expected !== undefined) parts.push(`expected=${stableJson(value.expected)}`);
    if (value.observed !== null && value.observed !== undefined) parts.push(`observed=${stableJson(value.observed)}`);
    if (typeof value.source === 'string') parts.push(`source=${value.source}`);
    if (typeof value.evidence === 'string') parts.push(`evidence=${value.evidence}`);
    facts.push(fact(
      `trap: ${parts.join(' ')}`,
      'derived-read-model',
      { kind: 'TrapDetail', runId: trap.runId ?? null },
    ));
  }

  if (focus === 'trap' && Array.isArray(resolved)) {
    for (const entry of resolved) {
      const resolution = entry.resolution;
      const record = resolution?.record;
      facts.push(fact(
        `evidence ${entry.ref.locator}: ${record ? `record ${record.order}/${record.kind}` : 'resolved'}`,
        'derived-read-model',
        { kind: 'EvidenceRef', locator: entry.ref.locator },
      ));
    }
  }

  // Key runtime events: terminal + causal anchors + the most recent event,
  // deduped by eventId, bounded.
  const selected = new Map();
  for (const event of events ?? []) {
    if (event && typeof event.eventId === 'string' && PRIORITY_EVENT_KINDS.has(event.kind)) {
      selected.set(event.eventId, event);
    }
  }
  if (events.length > 0 && events[events.length - 1]?.eventId) {
    const last = events[events.length - 1];
    if (!selected.has(last.eventId)) selected.set(last.eventId, last);
  }
  let budget = MAX_FACTS_FROM_EVENTS;
  for (const event of selected.values()) {
    if (budget <= 0) break;
    facts.push(fact(
      `${event.kind} @seq ${Number.isInteger(event.sequence) ? event.sequence : '?'}`,
      'kernel-fact',
      { kind: 'RuntimeEvent', eventId: event.eventId, sequence: Number.isInteger(event.sequence) ? event.sequence : null },
    ));
    budget -= 1;
  }

  return { facts, uncertainties };
}

/** Deterministic cap on kernel-fact event entries inside one artifact. */
const MAX_FACTS_FROM_EVENTS = 12;

function formatEventLine(event) {
  const kind = typeof event.kind === 'string' ? event.kind : 'Unknown';
  const seq = Number.isInteger(event.sequence) ? event.sequence : '?';
  const id = typeof event.eventId === 'string' ? event.eventId : '?';
  let line = `[${seq}] ${kind} ${id}`;
  const locators = selectEvidenceRefs([event], 8).map((r) => r.locator);
  if (locators.length > 0) line += ` refs=${locators.join(',')}`;
  const payload = event.payload === null || event.payload === undefined ? null : stableJson(event.payload);
  if (payload !== null && payload !== 'null') {
    const boundedPayload = payload.length > EVENT_PAYLOAD_MAX_CHARS ? `${payload.slice(0, EVENT_PAYLOAD_MAX_CHARS)}…` : payload;
    line += ` payload=${boundedPayload}`;
  }
  if (line.length > EVENT_LINE_MAX_CHARS) {
    line = `${line.slice(0, EVENT_LINE_MAX_CHARS)}…`;
  }
  return line;
}

function formatSnapshotBlock(snapshot) {
  if (!snapshot || typeof snapshot !== 'object') return '  (no snapshot data)';
  const lines = [`  runId: ${snapshot.runId}`];
  for (const label of SNAPSHOT_FIELDS) {
    const field = snapshot[label];
    const value = field && field.value !== null && field.value !== undefined ? stableJson(field.value) : 'N/A';
    const classification = typeof field?.classification === 'string' ? field.classification : 'unknown';
    const source = typeof field?.truthSource === 'string' ? ` truthSource=${field.truthSource}` : '';
    lines.push(`  ${label}: ${value} (${classification}${source})`);
  }
  if (Array.isArray(snapshot.diagnostics) && snapshot.diagnostics.length > 0) {
    lines.push(`  diagnostics: ${snapshot.diagnostics.join('; ')}`);
  }
  return lines.join('\n');
}

/**
 * Assemble the deterministic bounded context for one analysis.
 *
 * @param {object} params
 * @param {object} params.facade   narrowed read-only retrieval facade
 * @param {object} params.limits   resolved caps
 * @param {object} params.request  { runId, focus, reason }
 * @returns {Promise<object>} context record with text, eventCount, contextChars,
 *   snapshot, events, trap, selectedRefs, resolved, facts, uncertainties, notes
 */
export async function assembleContext({ facade, limits, request }) {
  const caps = normalizeLimits(limits);
  const { runId, focus } = request;
  const reason = typeof request.reason === 'string' ? request.reason.trim() : '';
  const uncertainties = [];
  const notes = [];

  // 1. Latest snapshot — always (exactly one; no history dumps).
  let snapshot = null;
  try {
    snapshot = await facade.getRunSnapshot(runId);
  } catch (err) {
    notes.push({ step: 'snapshot', error: err?.message ?? String(err) });
    uncertainties.push(uncertainty('run snapshot', 'context-assembly-failed'));
  }

  // 2. Bounded recent RuntimeEvent window (most recent maxEvents by sequence;
  //    the wire has no timestamps, so the deterministic recent bound is count).
  let events = [];
  try {
    const page = await facade.getRuntimeEvents(runId, null);
    const all = Array.isArray(page?.events) ? page.events : [];
    events = all.slice(-caps.maxEvents);
  } catch (err) {
    notes.push({ step: 'events', error: err?.message ?? String(err) });
    uncertainties.push(uncertainty('runtime events', 'context-assembly-failed'));
  }

  // 3. Trap detail — only on trap focus.
  let trap = null;
  if (focus === 'trap') {
    try {
      trap = await facade.getTrap(runId);
    } catch (err) {
      notes.push({ step: 'trap', error: err?.message ?? String(err) });
      uncertainties.push(uncertainty('trap detail', 'context-assembly-failed'));
    }
  }

  // 4. Logical EvidenceRefs from the window — bounded locators.
  const selectedRefs = selectEvidenceRefs(events, caps.maxEvidenceRefs);

  // 5. LAZY resolution — only when the buyer requires content (trap focus),
  //    capped by maxEvidenceRefs / evidenceBytesPerRef, metadata only.
  const resolved = [];
  if (focus === 'trap' && selectedRefs.length > 0) {
    for (const ref of selectedRefs) {
      try {
        const resolution = await facade.getEvidence({ locator: ref.locator, runId });
        if (resolution && resolution.found === true) {
          const byteCount = Number.isInteger(resolution?.artifact?.byteCount)
            ? resolution.artifact.byteCount
            : null;
          if (byteCount !== null && byteCount > caps.evidenceBytesPerRef) {
            uncertainties.push(uncertainty(`evidence ${ref.locator} (${byteCount} bytes > ${caps.evidenceBytesPerRef})`, 'unresolved-evidence-ref'));
            continue;
          }
          resolved.push({ ref, resolution });
          if (resolved.length >= caps.maxEvidenceRefs) break;
        } else {
          uncertainties.push(uncertainty(`evidence ${ref.locator}`, 'unresolved-evidence-ref'));
        }
      } catch (err) {
        notes.push({ step: 'evidence', locator: ref.locator, error: err?.message ?? String(err) });
        uncertainties.push(uncertainty(`evidence ${ref.locator}`, 'unresolved-evidence-ref'));
      }
    }
  }

  // Deterministic observed facts from the read surface.
  const built = buildFacts({ snapshot, events, trap, resolved, focus });
  const facts = built.facts;
  for (const u of built.uncertainties) uncertainties.push(u);

  // Bounded, deterministic context text.
  const text = buildBoundedText({ snapshot, events, trap, selectedRefs, resolved, focus, reason, caps });
  const contextChars = text.length;

  return {
    snapshot,
    events,
    trap,
    selectedRefs,
    resolved,
    facts,
    uncertainties,
    notes,
    text,
    eventCount: events.length,
    contextChars,
  };
}

function buildBoundedText({ snapshot, events, trap, selectedRefs, resolved, focus, reason, caps }) {
  const boundedReason = reason ? (reason.length > REASON_MAX_CHARS ? `${reason.slice(0, REASON_MAX_CHARS)}…` : reason) : null;
  const header = [
    `shadow analysis context for run ${snapshot?.runId ?? '(unknown run)'}`,
    `focus: ${focus}`,
    `trigger: human.request`,
  ];
  if (boundedReason) header.push(`reason: ${boundedReason}`);

  const render = (workingEvents) => {
    const parts = [
      header.join('\n'),
      `--- snapshot ---`,
      formatSnapshotBlock(snapshot),
    ];
    if (focus === 'trap' && trap && typeof trap === 'object' && trap.found === true && trap.trap?.value) {
      parts.push(`--- trap ---`, formatTrapBlock(trap));
    }
    parts.push(`--- runtime events (last ${workingEvents.length} of window) ---`);
    if (workingEvents.length === 0) parts.push('  (no events in window)');
    for (const event of workingEvents) parts.push(`  ${formatEventLine(event)}`);
    if (selectedRefs.length > 0) {
      parts.push(`--- evidence refs (${selectedRefs.length}) ---`);
      for (const ref of selectedRefs) {
        parts.push(`  ${ref.locator} (${ref.kind ?? 'unknown-kind'}, maturity ${ref.maturity ?? 'unknown'}, ${ref.sizeBytes ?? '?'} bytes)`);
      }
    }
    if (resolved.length > 0) {
      parts.push(`--- resolved evidence metadata (${resolved.length}) ---`);
      for (const entry of resolved) {
        const record = entry.resolution?.record;
        const artifact = entry.resolution?.artifact;
        parts.push(`  ${entry.ref.locator}: record ${record?.order ?? '?'}/${record?.kind ?? '?'} artifact ${artifact?.byteCount ?? '?'} bytes`);
      }
    }
    return parts.join('\n');
  };

  // Deterministic trimming: drop oldest non-priority events first, then
  // hard-slice. The final text NEVER exceeds the cap.
  let text = render(events);
  let workingEvents = events.slice();
  while (text.length > caps.maxContextChars && workingEvents.length > 0) {
    const removable = workingEvents.findIndex((event) => !PRIORITY_EVENT_KINDS.has(event?.kind));
    if (removable === -1) break;
    workingEvents.splice(removable, 1);
    text = render(workingEvents);
  }
  if (text.length > caps.maxContextChars) {
    text = text.slice(0, caps.maxContextChars);
  }
  return text;
}

function formatTrapBlock(trap) {
  const value = trap.trap?.value ?? {};
  const lines = [`  kind: ${value.kind}`, `  scope: ${value.scope}`];
  if (value.expected !== null && value.expected !== undefined) lines.push(`  expected: ${stableJson(value.expected)}`);
  if (value.observed !== null && value.observed !== undefined) lines.push(`  observed: ${stableJson(value.observed)}`);
  lines.push(`  source: ${value.source}`);
  if (value.evidence) lines.push(`  evidence: ${value.evidence}`);
  if (value.lastActionDescription) lines.push(`  lastAction: ${value.lastActionDescription}`);
  lines.push(`  classification: ${trap.trap.classification}`);
  return lines.join('\n');
}
