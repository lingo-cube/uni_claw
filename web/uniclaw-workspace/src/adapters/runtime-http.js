'use strict';

const { queryError } = require('../core');

const SOURCE = 'uniclaw-runtime';
const CONTRACT = 'uniclaw.workspace.contract.v1';
const CAPABILITY_SCHEMA = 'uniclaw.workspace.capability.v1';

/**
 * Host-neutral adapter for the Runtime Run HTTP contract. It depends only on
 * fetch and logical Runtime endpoints; DSH, ego-lite and standalone hosts can
 * all provide their own fetch/detail resolver without changing the UI core.
 */
function createRuntimeHttpCapabilities({ baseUrl, fetchImpl = globalThis.fetch, headers = {}, detailResolver } = {}) {
  if (typeof baseUrl !== 'string' || baseUrl.trim().length === 0) throw new TypeError('baseUrl is required');
  if (typeof fetchImpl !== 'function') throw new TypeError('fetchImpl is required');
  const root = baseUrl.replace(/\/+$/, '');

  const request = async (path, capability, init = {}) => {
    let response;
    let payload;
    try {
      response = await fetchImpl(`${root}${path}`, {
        ...init,
        headers: { accept: 'application/json', ...headers, ...(init.headers || {}) },
      });
      payload = await response.json();
    } catch (error) {
      return failure(capability, queryError('unavailable', error instanceof Error ? error.message : 'Runtime HTTP request failed', SOURCE));
    }
    const meta = {
      capability,
      source: SOURCE,
      ...(payload && payload.revision !== undefined ? { revision: payload.revision } : {}),
      ...(payload && payload.observedAt ? { observedAt: payload.observedAt } : {}),
      ...(payload && payload.run?.runId ? { snapshotId: payload.run.runId } : {}),
    };
    if (!response.ok || !payload || payload.ok !== true) {
      const error = payload?.error || queryError(httpErrorCode(response?.status), `Runtime HTTP returned ${response?.status || 'an invalid response'}`, SOURCE, { retryable: response?.status >= 500 });
      return { ok: false, error: normalizeError(error), ...meta };
    }
    return { ok: true, data: payload, ...meta };
  };

  const listRuns = (requestOptions = {}) => {
    const params = new URLSearchParams();
    if (requestOptions.status) params.set('status', requestOptions.status);
    if (requestOptions.productSessionId) params.set('productSessionId', requestOptions.productSessionId);
    if (requestOptions.cursor) params.set('cursor', requestOptions.cursor);
    if (requestOptions.limit) params.set('limit', String(requestOptions.limit));
    const query = params.toString();
    return request(`/api/uniclaw-runtime/runs${query ? `?${query}` : ''}`, 'TaskQuery');
  };

  const getRun = (runId) => request(`/api/uniclaw-runtime/runs/${encodeURIComponent(runId)}`, 'SessionQuery');
  const getEvents = (runId, options = {}, capability = 'SessionQuery') => {
    const params = new URLSearchParams();
    if (options.source) params.set('source', options.source);
    if (options.cursor) params.set('cursor', options.cursor);
    if (options.limit) params.set('limit', String(options.limit));
    const query = params.toString();
    return request(`/api/uniclaw-runtime/runs/${encodeURIComponent(runId)}/events${query ? `?${query}` : ''}`, capability);
  };

  const findRunForSession = async (productSessionId) => {
    const result = await listRuns({ productSessionId, limit: 100 });
    if (!result.ok) return result;
    const run = (result.data.runs || [])[0];
    return run ? { ...result, data: { ...result.data, run } } : failure('SessionQuery', queryError('not-found', 'Product session not found', SOURCE, { retryable: false }));
  };

  const listProjects = async (requestOptions = {}) => {
    const result = await listRuns(requestOptions);
    if (!result.ok) return result;
    const groups = new Map();
    for (const run of result.data.runs || []) {
      const projectId = logicalId(run.projectRef) || 'default';
      if (!groups.has(projectId)) groups.set(projectId, { projectId, name: run.projectRef?.label || projectId, instances: [] });
      groups.get(projectId).instances.push(toTaskInstance(run, projectId));
    }
    return { ...result, data: { ...result.data, projects: [...groups.values()] } };
  };

  const listTaskInstances = async ({ projectId, ...requestOptions } = {}) => {
    const result = await listRuns(requestOptions);
    if (!result.ok) return result;
    const taskInstances = (result.data.runs || [])
      .filter((run) => !projectId || logicalId(run.projectRef) === projectId)
      .map((run) => toTaskInstance(run, logicalId(run.projectRef) || projectId || 'default'));
    return { ...result, data: { ...result.data, taskInstances } };
  };

  const getSession = async ({ productSessionId }) => {
    const result = await findRunForSession(productSessionId);
    if (!result.ok) return result;
    return { ...result, data: toSession(result.data.run) };
  };

  const getTimeline = async ({ productSessionId, ...options }) => {
    const result = await findRunForSession(productSessionId);
    if (!result.ok) return result;
    const events = await getEvents(result.data.run.runId, options, 'SessionQuery');
    if (!events.ok) return events;
    return { ...events, data: { ...events.data, items: (events.data.events || []).map(toTimelineItem) } };
  };

  const getTraces = async ({ productSessionId, source, ...options }) => {
    const result = await findRunForSession(productSessionId);
    if (!result.ok) return result;
    const events = await getEvents(result.data.run.runId, { ...options, ...(source ? { source } : {}) }, 'TraceQuery');
    if (!events.ok) return events;
    return { ...events, data: { ...events.data, traces: (events.data.events || []).map(toTrace) } };
  };

  const getEvidence = async ({ productSessionId }) => {
    const result = await findRunForSession(productSessionId);
    if (!result.ok) return result;
    return { ...result, data: { evidence: (result.data.run.artifacts || []).filter((item) => item.kind === 'evidence' || item.kind === 'facts').map((item) => toEvidence(item, result.data.run)) } };
  };

  const resolveDetail = async ({ productSessionId, detailRef }) => {
    if (typeof detailResolver !== 'function') return failure('DetailQuery', queryError('unavailable', 'Runtime detail resolver is not configured', SOURCE));
    try {
      const value = await detailResolver({ productSessionId, detailRef });
      return { ok: true, data: value, capability: 'DetailQuery', source: SOURCE, observedAt: new Date().toISOString() };
    } catch (error) {
      return failure('DetailQuery', queryError('unavailable', error instanceof Error ? error.message : 'Runtime detail resolver failed', SOURCE));
    }
  };

  const launchTask = async (requestOptions = {}) => {
    const body = {
      productSessionId: requestOptions.productSessionId,
      taskInstanceId: requestOptions.taskInstanceId,
      launchId: requestOptions.launchId || requestOptions.launchRequestId,
      idempotencyKey: requestOptions.idempotencyKey,
      correlationId: requestOptions.correlationId,
      projectRef: requestOptions.projectRef,
      testSetRef: requestOptions.testSetRef,
      taskRef: requestOptions.taskRef,
      environmentIntent: requestOptions.environmentIntent,
    };
    return request('/api/uniclaw-runtime/runs', 'TaskCommand', { method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify(body) });
  };

  return Object.freeze({
    TaskQuery: Object.freeze({ listProjects, listTaskInstances }),
    SessionQuery: Object.freeze({ getSession, getTimeline }),
    TraceQuery: Object.freeze({ getTraces }),
    EvidenceQuery: Object.freeze({ getEvidence }),
    DetailQuery: Object.freeze({ resolveDetail }),
    TaskCommand: Object.freeze({ launchTask }),
  });
}

function toTaskInstance(run, projectId) {
  return {
    id: run.taskInstanceId,
    taskInstanceId: run.taskInstanceId,
    projectId,
    productSessionId: run.productSessionId,
    sessionId: run.hostSessionRef?.sessionId,
    runId: run.runId,
    title: run.taskRef?.label || run.taskRef?.id || run.taskInstanceId,
    status: run.status,
    phase: run.phase,
    outcome: run.outcome,
    source: SOURCE,
    authority: 'uniclaw-runtime',
    correlationId: run.correlationId,
    projectRef: run.projectRef,
    testSetRef: run.testSetRef,
    taskRef: run.taskRef,
    environment: run.environment,
  };
}

function toSession(run) {
  return {
    productSessionId: run.productSessionId,
    runId: run.runId,
    taskInstanceId: run.taskInstanceId,
    source: SOURCE,
    authority: 'uniclaw-runtime',
    correlationId: run.correlationId,
    status: run.status,
    phase: run.phase,
    outcome: run.outcome,
    reason: run.reason,
    hostSessionRef: run.hostSessionRef,
    projectRef: run.projectRef,
    testSetRef: run.testSetRef,
    taskRef: run.taskRef,
    environment: run.environment,
    revision: run.revision,
    observedAt: run.observedAt,
  };
}

function toTimelineItem(event) {
  return { id: event.eventId, kind: event.eventType, summary: event.summary, source: event.source, authority: event.authority, occurredAt: event.occurredAt, runId: event.runId, detailRef: event.payloadRef ? { refId: event.payloadRef, source: event.source } : undefined };
}

function toTrace(event) {
  return { id: event.eventId, definition: event.eventType, summary: event.summary, source: event.source, authority: event.authority, occurredAt: event.occurredAt, sequence: event.sequence, detailRef: event.payloadRef ? { refId: event.payloadRef, source: event.source } : undefined };
}

function toEvidence(artifact, run) {
  return { id: artifact.artifactId, title: artifact.kind, source: SOURCE, authority: 'uniclaw-runtime', productSessionId: run.productSessionId, detailRef: artifact.detailEndpoint ? { refId: artifact.detailEndpoint, source: SOURCE } : undefined };
}

function logicalId(value) {
  return typeof value === 'string' ? value : value && typeof value.id === 'string' ? value.id : null;
}

function httpErrorCode(status) {
  if (status === 404) return 'not-found';
  if (status === 408 || status === 429 || status >= 500) return 'timeout';
  if (status === 401 || status === 403) return 'permission-denied';
  return 'unavailable';
}

function normalizeError(error) {
  if (error && error.schemaVersion && error.contractVersion) return error;
  const code = ['unavailable', 'uncorrelated', 'stale', 'permission-denied', 'not-found', 'timeout'].includes(error?.code)
    ? error.code
    : error?.code === 'runtime-run-not-found' ? 'not-found' : 'unavailable';
  return queryError(code, error?.message || 'Runtime request failed', SOURCE, { retryable: error?.retryable });
}

function failure(capability, error) {
  return { ok: false, error, capability, source: SOURCE, observedAt: new Date().toISOString() };
}

module.exports = { createRuntimeHttpCapabilities };
