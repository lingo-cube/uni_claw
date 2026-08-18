/**
 * Deterministic DSH commands over the read-only UniClaw surface (PLUG-F7).
 * Every handler is a pure read: it formats the classified snapshot / trap /
 * evidence / run list from the wire DTOs and returns a CommandResult. No
 * command ever dispatches an action or changes Kernel state — control
 * operations are audited and deferred (control.support).
 *
 * The fifth command, `uniclaw-shadow-analyze`, is the Shadow Cognition
 * inspection surface (design §11): zero-model dispatch (the handler runs
 * directly against the receiving agent — the command is never sent to the
 * model), truthful session identity, deterministic read-only retrieval,
 * optional one `ctx.llm` call, and a structured-text `ShadowAnalysis`
 * response. It writes ZERO session events.
 */
'use strict';

import { FOCUS_VALUES } from './shadow/analysis.js';
import { runShadowAnalysis } from './shadow/index.js';

const NAME_PATTERN = /^[a-z][a-z0-9_-]*$/;

/** Format one classified field as `value (classification — truth source)`. */
function formatField(label, field) {
  const value = field && field.value !== null && field.value !== undefined
    ? JSON.stringify(field.value)
    : 'N/A';
  const classification = field?.classification ?? 'unknown';
  const truthSource = field?.truthSource ? ` — ${field.truthSource}` : '';
  return `${label}: ${value} (${classification}${truthSource})`;
}

/** Format a snapshot wire DTO into a stable, human-readable block. */
export function formatSnapshot(snapshot) {
  if (!snapshot || typeof snapshot !== 'object') {
    return 'no snapshot data';
  }
  const lines = [
    `runId: ${snapshot.runId}`,
    formatField('runState', snapshot.runState),
    formatField('currentSemanticPage', snapshot.currentSemanticPage),
    formatField('activeTrap', snapshot.activeTrap),
    formatField('currentGoal', snapshot.currentGoal),
    formatField('lastDecision', snapshot.lastDecision),
    formatField('lastAction', snapshot.lastAction),
    formatField('recoveryState', snapshot.recoveryState),
    formatField('latestGoalEvidence', snapshot.latestGoalEvidence),
    formatField('currentObservationSequence', snapshot.currentObservationSequence),
    formatField('currentContainerSummary', snapshot.currentContainerSummary),
    formatField('bindingsSummary', snapshot.bindingsSummary),
    formatField('stateBeliefsSummary', snapshot.stateBeliefsSummary),
  ];
  if (Array.isArray(snapshot.diagnostics) && snapshot.diagnostics.length > 0) {
    lines.push(`diagnostics: ${snapshot.diagnostics.join('; ')}`);
  }
  return lines.join('\n');
}

function extractRunId(rawInput) {
  return rawInput.trim();
}

/** Build the deterministic command definitions bound to one adapter.
 *
 * Without `shadowContext` the four control-plane commands are returned
 * (back-compat shape). With `shadowContext` the fifth command,
 * `uniclaw-shadow-analyze`, is appended. `shadowContext` =
 * `{ config, facade, getLlm, cache }` (built by the plugin; the facade is the
 * narrowed read-only retrieval surface — no adapter internals reach the
 * command).
 */
export function buildCommands(adapter, shadowContext) {
  const commands = [
    {
      name: 'uniclaw-inspect-run',
      description: 'Inspect the classified read-only snapshot of one UniClaw run.',
      input: { hint: '<runId>' },
      recordInput: true,
      handler: async (invocation) => {
        const runId = extractRunId(invocation.rawInput);
        if (!runId) {
          return { kind: 'error', text: 'usage: uniclaw-inspect-run <runId>' };
        }
        try {
          const snapshot = await adapter.getRunSnapshot(runId);
          return { kind: 'success', text: formatSnapshot(snapshot) };
        } catch (err) {
          return { kind: 'error', text: errorText(err) };
        }
      },
    },
    {
      name: 'uniclaw-inspect-trap',
      description: 'Inspect the classified active trap of one UniClaw run.',
      input: { hint: '<runId>' },
      recordInput: true,
      handler: async (invocation) => {
        const runId = extractRunId(invocation.rawInput);
        if (!runId) {
          return { kind: 'error', text: 'usage: uniclaw-inspect-trap <runId>' };
        }
        try {
          const result = await adapter.getTrap(runId);
          const lines = [`runId: ${result.runId}`, `found: ${result.found}`];
          if (result.trap?.value) {
            lines.push(`kind: ${result.trap.value.kind}`);
            lines.push(`scope: ${result.trap.value.scope}`);
            lines.push(`expected: ${result.trap.value.expected ?? 'N/A'}`);
            lines.push(`observed: ${result.trap.value.observed ?? 'N/A'}`);
            lines.push(`source: ${result.trap.value.source}`);
            lines.push(`evidence: ${result.trap.value.evidence}`);
            if (result.trap.value.lastActionDescription) {
              lines.push(`lastAction: ${result.trap.value.lastActionDescription}`);
            }
            lines.push(`classification: ${result.trap.classification}`);
          }
          if (result.diagnostic) lines.push(`diagnostic: ${result.diagnostic}`);
          return { kind: 'success', text: lines.join('\n') };
        } catch (err) {
          return { kind: 'error', text: errorText(err) };
        }
      },
    },
    {
      name: 'uniclaw-evidence-open',
      description: 'Open one logical evidence ref (metadata only; resolution by logical locator only).',
      input: { hint: '<locator> <runId>' },
      recordInput: true,
      handler: async (invocation) => {
        const parts = invocation.rawInput.trim().split(/\s+/);
        const [locator, runId] = parts;
        if (!locator || !runId) {
          return { kind: 'error', text: 'usage: uniclaw-evidence-open <locator> <runId>' };
        }
        try {
          const resolution = await adapter.getEvidence({ locator, runId });
          const lines = [`found: ${resolution.found}`];
          if (resolution.found) {
            if (resolution.captureSessionId) lines.push(`captureSessionId: ${resolution.captureSessionId}`);
            if (resolution.record) {
              lines.push(`record.order: ${resolution.record.order}`);
              lines.push(`record.kind: ${resolution.record.kind}`);
              lines.push(`record.sequenceNumber: ${resolution.record.sequenceNumber}`);
              if (resolution.record.actionId) lines.push(`record.actionId: ${resolution.record.actionId}`);
            }
            if (resolution.artifact) {
              lines.push(`artifact.artifactId: ${resolution.artifact.artifactId}`);
              lines.push(`artifact.byteCount: ${resolution.artifact.byteCount}`);
            }
            if (resolution.ref) {
              lines.push(`ref.kind: ${resolution.ref.kind}`);
              lines.push(`ref.maturity: ${resolution.ref.maturity}`);
            }
          }
          if (resolution.diagnostic) lines.push(`diagnostic: ${resolution.diagnostic}`);
          return { kind: 'success', text: lines.join('\n') };
        } catch (err) {
          return { kind: 'error', text: errorText(err) };
        }
      },
    },
    {
      name: 'uniclaw-runs-list',
      description: 'List run ids registered with the DriverHost read surface.',
      handler: async () => {
        try {
          const result = await adapter.listRuns();
          const runs = Array.isArray(result?.runIds) ? result.runIds : [];
          return { kind: 'success', text: runs.length === 0 ? '(no runs registered)' : runs.join('\n') };
        } catch (err) {
          return { kind: 'error', text: errorText(err) };
        }
      },
    },
    {
      name: 'uniclaw-events-after',
      description: 'Read classified RuntimeEvent pages for one run (frozen run.events.after wire; zero-model).',
      input: { hint: '<runId> [--cursor <n>]' },
      recordInput: true,
      handler: async (invocation) => {
        const parsed = parseEventsAfterInvocation(invocation.rawInput);
        if (parsed.error) return { kind: 'error', text: parsed.error };
        try {
          const page = await adapter.getRuntimeEvents(parsed.runId, parsed.cursor);
          if (page?.error) {
            return { kind: 'error', text: `DriverHost error [${page.error.code}]: ${page.error.message}` };
          }
          return { kind: 'success', text: formatEventsPage(page, parsed.runId, parsed.cursor) };
        } catch (err) {
          return { kind: 'error', text: errorText(err) };
        }
      },
    },
    {
      name: 'uniclaw-run-goal',
      description: 'Start a UniClaw Runtime.Agent semantic run asynchronously (additive run.start; deterministic control, no inference calls; returns runId immediately; observe via uniclaw-events-after / uniclaw-inspect-run / uniclaw-inspect-trap).',
      input: { hint: '<json> ({"goal":{"objectIdentity","stateDimension","desiredValue"},"objects":[...],"capabilities":[...],"device":"serial:<id>"})' },
      recordInput: true,
      handler: async (invocation) => {
        const parsed = parseRunGoalInvocation(invocation.rawInput);
        if (parsed.error) return { kind: 'error', text: parsed.error };
        try {
          const accepted = await adapter.runStart(parsed.request);
          if (!accepted || accepted.accepted !== true || typeof accepted.runId !== 'string' || accepted.runId.length === 0) {
            return { kind: 'error', text: 'DriverHost did not accept the run (no runId returned).' };
          }
          // No automatic follow-up: no polling, no shadow cognition, no semantic
          // actions, no device translation, no inference call.
          return { kind: 'success', text: `runId: ${accepted.runId}\nrunState: ${accepted.runState ?? 'unknown'}` };
        } catch (err) {
          return { kind: 'error', text: errorText(err) };
        }
      },
    },
  ];

  if (shadowContext) {
    commands.push(buildShadowCommand(shadowContext));
  }

  return commands;
}

/**
 * Parse `uniclaw-events-after` input deterministically:
 * `<runId> [--cursor <n>]`. runId is required; `--cursor` is an optional
 * positive integer (sequence after which events are requested). Unknown or
 * duplicate flags are rejected.
 */
export function parseEventsAfterInvocation(rawInput) {
  const trimmed = typeof rawInput === 'string' ? rawInput.trim() : '';
  if (!trimmed) {
    return { error: 'usage: uniclaw-events-after <runId> [--cursor <n>]' };
  }
  const tokens = trimmed.split(/\s+/);
  const runId = tokens[0];
  const rest = tokens.slice(1);
  let cursor;
  const seen = new Set();
  let i = 0;
  while (i < rest.length) {
    const token = rest[i];
    if (token === '--cursor') {
      if (seen.has('cursor')) return { error: 'duplicate --cursor flag' };
      const value = rest[i + 1];
      if (value === undefined || !/^\d+$/.test(value)) {
        return { error: 'usage: --cursor requires a non-negative integer' };
      }
      cursor = Number(value);
      seen.add('cursor');
      i += 2;
    } else {
      return { error: `unknown argument "${token}"` };
    }
  }
  return { runId, cursor };
}

/**
 * Parse `uniclaw-run-goal` input deterministically: a single JSON object
 * { goal, objects, capabilities, device }. Command-layer syntax validation
 * ONLY — semantic validation (unknown object/device, busy device) happens at
 * the DriverHost and surfaces as a typed request_rejected RPC error.
 */
export function parseRunGoalInvocation(rawInput) {
  const trimmed = typeof rawInput === 'string' ? rawInput.trim() : '';
  if (!trimmed) {
    return { error: 'usage: uniclaw-run-goal <json> ({"goal":{"objectIdentity","stateDimension","desiredValue"},"objects":[...],"capabilities":[...],"device":"serial:<id>"})' };
  }
  let parsed;
  try {
    parsed = JSON.parse(trimmed);
  } catch (err) {
    return { error: `invalid JSON: ${err.message}` };
  }
  if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) {
    return { error: 'request must be a JSON object' };
  }

  const goal = parsed.goal;
  if (!goal || typeof goal !== 'object'
      || typeof goal.objectIdentity !== 'string' || goal.objectIdentity.length === 0
      || typeof goal.stateDimension !== 'string' || goal.stateDimension.length === 0
      || typeof goal.desiredValue !== 'boolean') {
    return { error: 'goal requires { objectIdentity: string, stateDimension: string, desiredValue: boolean }' };
  }

  if (!Array.isArray(parsed.objects) || parsed.objects.length === 0) {
    return { error: 'objects requires a non-empty array of { identity, category, stateDimensions: string[] }' };
  }
  for (const obj of parsed.objects) {
    if (!obj || typeof obj !== 'object'
        || typeof obj.identity !== 'string' || obj.identity.length === 0
        || typeof obj.category !== 'string' || obj.category.length === 0
        || !Array.isArray(obj.stateDimensions)
        || obj.stateDimensions.some((d) => typeof d !== 'string')) {
      return { error: 'each object requires { identity: string, category: string, stateDimensions: string[] }' };
    }
  }

  if (!Array.isArray(parsed.capabilities) || parsed.capabilities.length === 0) {
    return { error: 'capabilities requires a non-empty array of { name, applicableToCategory, stateDimension }' };
  }
  for (const cap of parsed.capabilities) {
    if (!cap || typeof cap !== 'object'
        || typeof cap.name !== 'string' || cap.name.length === 0
        || typeof cap.applicableToCategory !== 'string' || cap.applicableToCategory.length === 0
        || typeof cap.stateDimension !== 'string' || cap.stateDimension.length === 0) {
      return { error: 'each capability requires { name: string, applicableToCategory: string, stateDimension: string }' };
    }
  }

  if (typeof parsed.device !== 'string' || parsed.device.trim().length === 0) {
    return { error: 'device requires a string selector (e.g. "serial:<adb-serial>")' };
  }

  return { request: { goal, objects: parsed.objects, capabilities: parsed.capabilities, device: parsed.device.trim() } };
}

/**
 * Format one RuntimeEvent page (frozen run.events.after DTO) into a stable,
 * human-readable block (design D2). One line per event:
 *   event: <eventId> [<kind>] seq=<sequence> obs=<observationSequence|null> payload=<json> refs=<count>
 * plus a trailing cursor line so callers can continue incrementally.
 */
export function formatEventsPage(page, runId, cursor) {
  const events = Array.isArray(page?.events) ? page.events : [];
  const lines = [`runId: ${runId}`];
  if (cursor !== undefined) lines.push(`cursor: ${cursor}`);
  for (const event of events) {
    const parts = [`event: ${event.eventId}`, `[${event.kind}]`, `seq=${event.sequence}`];
    if (event.observationSequence !== null && event.observationSequence !== undefined) {
      parts.push(`obs=${event.observationSequence}`);
    }
    if (event.payload !== undefined && event.payload !== null) {
      parts.push(`payload=${JSON.stringify(event.payload)}`);
    }
    if (Array.isArray(event.evidenceRefs) && event.evidenceRefs.length > 0) {
      parts.push(`refs=${event.evidenceRefs.map((r) => r.locator ?? r.kind ?? '?').join(',')}`);
    }
    lines.push(parts.join(' '));
  }
  if (events.length === 0) {
    lines.push('(no events)');
  }
  const next = page?.nextCursor?.lastSequence;
  if (next !== undefined && next !== null) {
    lines.push(`nextCursor: ${next}`);
    lines.push(`hasMore: ${page.hasMore === true}`);
  }
  return lines.join('\n');
}

/**
 * Parse `uniclaw-shadow-analyze` input deterministically:
 * `<runId> [--focus <value>] [--reason <text>]`. Unknown or duplicate flags
 * are rejected; `--reason` consumes the remainder of the line.
 */
export function parseShadowInvocation(rawInput) {
  const trimmed = typeof rawInput === 'string' ? rawInput.trim() : '';
  if (!trimmed) {
    return { error: 'usage: uniclaw-shadow-analyze <runId> [--focus <value>] [--reason <text>]' };
  }
  const tokens = trimmed.split(/\s+/);
  const runId = tokens[0];
  const rest = tokens.slice(1);
  let focus = 'general';
  let reason;
  const seen = new Set();
  let i = 0;
  while (i < rest.length) {
    const token = rest[i];
    if (token === '--focus') {
      if (seen.has('focus')) return { error: 'duplicate --focus flag' };
      const value = rest[i + 1];
      if (value === undefined || value.startsWith('--')) {
        return { error: 'usage: --focus requires a value (general|trap|failure|completion|progress|blocked)' };
      }
      if (!FOCUS_VALUES.includes(value)) {
        return { error: `unknown focus "${value}" (allowed: ${FOCUS_VALUES.join(', ')})` };
      }
      focus = value;
      seen.add('focus');
      i += 2;
    } else if (token === '--reason') {
      if (seen.has('reason')) return { error: 'duplicate --reason flag' };
      const value = rest.slice(i + 1).join(' ').trim();
      if (!value) return { error: 'usage: --reason requires text' };
      reason = value;
      seen.add('reason');
      break;
    } else {
      return { error: `unknown argument "${token}"` };
    }
  }
  return { runId, focus, reason };
}

/** Structured-text rendering of one ShadowAnalysis (authoritative surface). */
export function formatShadowAnalysis(analysis) {
  const call = analysis?.modelCall;
  const lines = [
    `shadow analysis: ${analysis.analysisId}`,
    `classification: ${analysis.classification}`,
    `runId: ${analysis.runId}`,
    `sessionId: ${analysis.sessionId}`,
    `trigger: ${analysis.trigger}`,
    `focus: ${analysis.focus}`,
    `model call: ${call?.status ?? 'n/a'}${call?.provider ? ` (${call.provider}/${call.model ?? '?'}, ${call.inputEventCount} events, ${call.contextChars} chars)` : ''}`,
    `humanSummary: ${analysis.humanSummary}`,
  ];
  if (Array.isArray(analysis.observedFacts) && analysis.observedFacts.length > 0) {
    lines.push('observedFacts:');
    for (const entry of analysis.observedFacts) {
      const ref = entry.ref ? ` (${entry.ref.kind}: ${entry.ref.eventId ?? entry.ref.field ?? entry.ref.locator ?? ''})` : '';
      lines.push(`  [${entry.classification}] ${entry.claim}${ref}`);
    }
  }
  if (Array.isArray(analysis.hypotheses) && analysis.hypotheses.length > 0) {
    lines.push('hypotheses:');
    for (const entry of analysis.hypotheses) {
      const flag = entry.flaggedUncertain ? ' (uncertain)' : '';
      const refs = entry.supportingRefs?.length ? ` refs=${entry.supportingRefs.join(',')}` : '';
      lines.push(`  [${entry.classification}] ${entry.claim}${refs}${flag}`);
    }
  }
  if (Array.isArray(analysis.uncertainties) && analysis.uncertainties.length > 0) {
    lines.push('uncertainties:');
    for (const entry of analysis.uncertainties) {
      lines.push(`  ${entry.topic}: ${entry.reason}`);
    }
  }
  if (Array.isArray(analysis.recommendations) && analysis.recommendations.length > 0) {
    lines.push('recommendations:');
    for (const entry of analysis.recommendations) {
      lines.push(`  [${entry.target}] ${entry.text}`);
    }
  }
  return lines.join('\n');
}

function buildShadowCommand(shadowContext) {
  return {
    name: 'uniclaw-shadow-analyze',
    description: 'Produce a bounded ShadowAnalysis (COGNITIVE_INFERENCE) for one UniClaw run; read-only, zero session writes.',
    input: { hint: '<runId> [--focus <value>] [--reason <text>]' },
    recordInput: true,
    handler: async (invocation) => {
      const parsed = parseShadowInvocation(invocation.rawInput);
      if (parsed.error) return { kind: 'error', text: parsed.error };
      if (shadowContext.config && shadowContext.config.enabled === false) {
        return { kind: 'error', text: 'shadow cognition is disabled by configuration (shadow.enabled=false)' };
      }
      // Truthful session identity only — never invented (design §12).
      const sessionId = invocation.agent?.session?.id;
      if (typeof sessionId !== 'string' || sessionId.length === 0) {
        return {
          kind: 'error',
          text: 'shadow analysis requires a truthful DSH session identity (invocation.agent.session.id); none is available — refusing to invent one',
        };
      }
      try {
        const analysis = await runShadowAnalysis({
          facade: shadowContext.facade,
          llm: typeof shadowContext.getLlm === 'function' ? shadowContext.getLlm() : null,
          config: shadowContext.config ?? {},
          cache: shadowContext.cache,
          request: {
            runId: parsed.runId,
            sessionId,
            focus: parsed.focus,
            reason: parsed.reason,
            signal: invocation.signal ?? null,
          },
        });
        return { kind: 'success', text: formatShadowAnalysis(analysis) };
      } catch (err) {
        // Fail-open relative to the Kernel (§13): the Kernel is never
        // touched; the shadow-level failure is reported as command text.
        return { kind: 'error', text: `shadow analysis failed: ${err?.message ?? String(err)}` };
      }
    },
  };
}

function errorText(err) {
  if (err && err.name === 'UniClawRpcError') {
    return `DriverHost error [${err.code}]: ${err.message}`;
  }
  return `error: ${err?.message ?? String(err)}`;
}

export { NAME_PATTERN };
