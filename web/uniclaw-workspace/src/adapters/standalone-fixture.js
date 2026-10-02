'use strict';

const { queryError } = require('../core');

const SOURCE = 'standalone-fixture';
const CONTRACT = 'uniclaw.workspace.contract.v1';
const SCHEMA = 'uniclaw.workspace.capability.v1';

function createStandaloneFixtureCapabilities({ fixture, now = () => new Date().toISOString(), revision = 1 } = {}) {
  if (!fixture || typeof fixture !== 'object') throw new TypeError('fixture is required');
  let requestNumber = 0;
  const observedAt = () => {
    const value = typeof now === 'function' ? now() : now;
    return new Date(value).toISOString();
  };
  const envelope = (capability, request, ok, dataOrError) => ({
    schemaVersion: SCHEMA,
    contractVersion: CONTRACT,
    capability,
    capabilityVersion: 'v1',
    requestId: `${SOURCE}-${++requestNumber}`,
    ok,
    ...(ok ? { data: clone(dataOrError) } : { error: dataOrError }),
    revision,
    observedAt: observedAt(),
    ...(request && request.productSessionId ? { productSessionId: request.productSessionId } : {})
  });
  const fail = (capability, request, code, message, options = {}) => envelope(
    capability,
    request,
    false,
    queryError(code, message, SOURCE, options)
  );
  const stale = (capability, request) => {
    const requested = request && Number(request.revision);
    return Number.isFinite(requested) && requested > Number(revision)
      ? fail(capability, request, 'stale', 'Fixture revision is older than requested revision', { retryable: true })
      : null;
  };
  const session = (id) => (Array.isArray(fixture.sessions) ? fixture.sessions : []).find((item) => item.productSessionId === id);
  const task = (id) => (Array.isArray(fixture.tasks) ? fixture.tasks : []).find((item) => item.productSessionId === id);
  const guardSession = (capability, request) => {
    const old = stale(capability, request);
    if (old) return old;
    if (!request || typeof request.productSessionId !== 'string') return fail(capability, request, 'not-found', 'ProductSessionId is required', { retryable: false });
    if (!session(request.productSessionId) || !task(request.productSessionId)) return fail(capability, request, 'not-found', 'Product session not found', { retryable: false });
    return null;
  };
  const listProjects = (request = {}) => {
    const old = stale('TaskQuery', request);
    if (old) return old;
    return envelope('TaskQuery', request, true, { projects: clone(fixture.projects || []) });
  };
  const listTaskInstances = (request = {}) => {
    const old = stale('TaskQuery', request);
    if (old) return old;
    const items = (fixture.tasks || []).filter((item) => !request.projectId || item.projectId === request.projectId);
    return envelope('TaskQuery', request, true, { taskInstances: clone(items) });
  };
  const getSession = (request = {}) => {
    const error = guardSession('SessionQuery', request);
    return error || envelope('SessionQuery', request, true, session(request.productSessionId));
  };
  const getTimeline = (request = {}) => {
    const error = guardSession('SessionQuery', request);
    return error || envelope('SessionQuery', request, true, { items: clone((fixture.timeline || []).filter((item) => item.productSessionId === request.productSessionId)) });
  };
  const getTraces = (request = {}) => {
    const error = guardSession('TraceQuery', request);
    return error || envelope('TraceQuery', request, true, { traces: clone((fixture.traces || []).filter((item) => item.productSessionId === request.productSessionId && (!request.source || item.source === request.source))) });
  };
  const getEvidence = (request = {}) => {
    const error = guardSession('EvidenceQuery', request);
    return error || envelope('EvidenceQuery', request, true, { evidence: clone((fixture.evidence || []).filter((item) => item.productSessionId === request.productSessionId)) });
  };
  const resolveDetail = (request = {}) => {
    const old = stale('DetailQuery', request);
    if (old) return old;
    const ref = request.detailRef;
    if (!ref || typeof ref.refId !== 'string' || !ref.source) return fail('DetailQuery', request, 'not-found', 'DetailRef is required', { retryable: false });
    if ((fixture.deniedDetailRefs || []).includes(ref.refId)) return fail('DetailQuery', request, 'permission-denied', 'Detail access denied', { retryable: false });
    const detail = (fixture.details || {})[`${ref.source}:${ref.refId}`];
    if (!detail) return fail('DetailQuery', request, 'not-found', 'Detail ref not found', { retryable: false });
    return envelope('DetailQuery', request, true, detail);
  };
  return Object.freeze({
    TaskQuery: Object.freeze({ listProjects, listTaskInstances }),
    SessionQuery: Object.freeze({ getSession, getTimeline }),
    TraceQuery: Object.freeze({ getTraces }),
    EvidenceQuery: Object.freeze({ getEvidence }),
    DetailQuery: Object.freeze({ resolveDetail })
  });
}

function clone(value) {
  return value === undefined ? value : JSON.parse(JSON.stringify(value));
}

module.exports = { createStandaloneFixtureCapabilities };
