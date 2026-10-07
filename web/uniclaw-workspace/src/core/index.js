'use strict';

const ERROR_CODES = Object.freeze([
  'unavailable',
  'uncorrelated',
  'stale',
  'permission-denied',
  'not-found',
  'timeout'
]);

function queryError(code, message, source, options = {}) {
  if (!ERROR_CODES.includes(code)) throw new Error(`Unknown QueryError code: ${code}`);
  return Object.freeze({
    schemaVersion: 'uniclaw.workspace.query-error.v1',
    contractVersion: 'uniclaw.workspace.contract.v1',
    code,
    message,
    retryable: options.retryable ?? ['unavailable', 'timeout', 'stale'].includes(code),
    source,
    ...(options.correlationId ? { correlationId: options.correlationId } : {})
  });
}

function failure(error, envelope = {}) {
  return { ok: false, error, ...envelopeMetadata(envelope), ...(envelope.capability !== undefined ? { capability: envelope.capability } : {}), ...(envelope.source !== undefined ? { source: envelope.source } : {}) };
}

function success(data, envelope = {}) {
  return {
    ok: true,
    data,
    ...(envelope.snapshotId ? { snapshotId: envelope.snapshotId } : {}),
    ...(envelope.revision ? { revision: envelope.revision } : {}),
    ...(envelope.observedAt ? { observedAt: envelope.observedAt } : {})
  };
}

function resultError(result) {
  return result && result.ok === false ? result.error : undefined;
}

function failureEnvelope(result, error) {
  return {
    errors: [error],
    ...(result && result.capability !== undefined ? { capability: result.capability } : {}),
    ...(result && result.source !== undefined ? { source: result.source } : (error && error.source !== undefined ? { source: error.source } : {})),
    ...(result ? envelopeMetadata(result) : {})
  };
}

function envelopeMetadata(result) {
  return Object.fromEntries(['snapshotId', 'revision', 'observedAt']
    .filter((key) => result[key] !== undefined)
    .map((key) => [key, result[key]]));
}

function sourceMeta(record) {
  return {
    source: record.source,
    authority: record.authority,
    ...(record.correlationId ? { correlationId: record.correlationId } : {}),
    ...(record.productSessionId ? { productSessionId: record.productSessionId } : {})
  };
}

function normalizePage(result, itemKey) {
  if (!result || result.ok === false) {
    return { items: [], errors: [resultError(result) || queryError('unavailable', 'Capability returned no result', 'unknown')] };
  }
  const data = result.data || {};
  if (!data || typeof data !== 'object' || !Object.prototype.hasOwnProperty.call(data, itemKey) || !Array.isArray(data[itemKey])) {
    return { items: [], errors: [queryError('unavailable', `Capability response is missing ${itemKey}`, 'workspace')] };
  }
  const items = data[itemKey];
  const errors = Array.isArray(data.errors) ? data.errors : [];
  return {
    items,
    errors,
    ...(data.context ? { context: data.context } : {}),
    ...(data.launchDefaults && typeof data.launchDefaults === 'object' ? { launchDefaults: data.launchDefaults } : {}),
    ...(data.localConfig && typeof data.localConfig === 'object' ? { localConfig: data.localConfig } : {}),
    ...(data.nextCursor ? { nextCursor: data.nextCursor } : {})
  };
}

class WorkspaceQueryCore {
  constructor(capabilities) {
    const required = ['TaskQuery', 'SessionQuery', 'TraceQuery', 'EvidenceQuery', 'DetailQuery'];
    for (const name of required) {
      if (!capabilities || !capabilities[name]) throw new TypeError(`Missing capability: ${name}`);
    }
    this.capabilities = capabilities;
    this.latest = new Map();
  }

  async listProjects(request = {}) {
    return this.#page('projects', () => this.capabilities.TaskQuery.listProjects(request), 'projects');
  }

  async listTaskInstances(projectId, request = {}) {
    return this.#page(`tasks:${projectId}`, () => this.capabilities.TaskQuery.listTaskInstances({ ...request, projectId }), 'taskInstances');
  }

  async getSession(productSessionId) {
    const result = await this.#read(`session:${productSessionId}`, () => this.capabilities.SessionQuery.getSession({ productSessionId }));
    if (!result.ok) return { status: 'error', session: null, ...failureEnvelope(result, result.error) };
    if (!result.data || typeof result.data !== 'object' || Array.isArray(result.data)) {
      return { status: 'error', session: null, errors: [queryError('unavailable', 'Session payload is malformed', 'workspace')] };
    }
    return { status: 'ready', session: result.data, errors: [], ...this.#envelope(result) };
  }

  async getTimeline(productSessionId, request = {}) {
    const result = await this.#read(`timeline:${productSessionId}`, () => this.capabilities.SessionQuery.getTimeline({ ...request, productSessionId }));
    if (!result.ok) return { status: 'error', items: [], ...failureEnvelope(result, result.error) };
    const data = result.data || {};
    const items = Array.isArray(data) ? data : (Array.isArray(data.items) ? data.items : (Array.isArray(data.timeline) ? data.timeline : undefined));
    if (!items) return { status: 'error', items: [], errors: [queryError('unavailable', 'Timeline payload is missing an items array', 'workspace')] };
    const errors = Array.isArray(data.errors) ? data.errors : [];
    return { status: errors.length ? 'partial' : 'ready', items, errors, ...this.#envelope(result) };
  }

  async getTraces(productSessionId, request = {}) {
    return this.#page(`traces:${productSessionId}:${request.source || 'all'}`, () => this.capabilities.TraceQuery.getTraces({ ...request, productSessionId }), 'traces');
  }

  async getEvidence(productSessionId, request = {}) {
    return this.#page(`evidence:${productSessionId}`, () => this.capabilities.EvidenceQuery.getEvidence({ ...request, productSessionId }), 'evidence');
  }

  async launchTask(request = {}) {
    const capability = this.capabilities.TaskCommand;
    if (!capability || typeof capability.launchTask !== 'function') return { status: 'error', errors: [queryError('unavailable', '当前 Host 未提供任务发起能力', 'TaskCommand')] };
    let result;
    try { result = await capability.launchTask(request); } catch (error) { return { status: 'error', errors: [queryError('unavailable', error instanceof Error ? error.message : '任务发起失败', 'TaskCommand')] }; }
    if (!result || result.ok === false) {
      const error = result?.error || queryError('unavailable', '任务发起返回无效结果', 'TaskCommand');
      return { status: error.partial !== undefined ? 'partial' : 'error', data: error.partial || null, errors: [error], ...this.#envelope(result || {}) };
    }
    return { status: 'ready', data: result.data || {}, errors: [], ...this.#envelope(result) };
  }

  // PNL-008 / ADR-0039：ToolInvoke 可选能力（与 TaskCommand 同款优雅降级）。
  async listTools() {
    const capability = this.capabilities.ToolInvoke;
    if (!capability || typeof capability.listTools !== 'function') return { status: 'error', items: [], errors: [queryError('unavailable', '当前 Host 未提供工具调用能力', 'ToolInvoke')] };
    let result;
    try { result = await capability.listTools(); } catch (error) { return { status: 'error', items: [], errors: [queryError('unavailable', error instanceof Error ? error.message : '工具列表获取失败', 'ToolInvoke')] }; }
    if (!result || result.ok === false) {
      const error = result?.error || queryError('unavailable', '工具列表返回无效结果', 'ToolInvoke');
      return { status: 'error', items: [], errors: [error], ...this.#envelope(result || {}) };
    }
    const items = Array.isArray(result.data?.tools) ? result.data.tools : [];
    return { status: 'ready', items, errors: [], ...this.#envelope(result) };
  }

  async invokeTool(name, request = {}) {
    const capability = this.capabilities.ToolInvoke;
    if (!capability || typeof capability.invokeTool !== 'function') return { status: 'error', errors: [queryError('unavailable', '当前 Host 未提供工具调用能力', 'ToolInvoke')] };
    let result;
    try { result = await capability.invokeTool(name, request); } catch (error) { return { status: 'error', errors: [queryError('unavailable', error instanceof Error ? error.message : '工具调用失败', 'ToolInvoke')] }; }
    if (!result || result.ok === false) {
      const error = result?.error || queryError('unavailable', '工具调用返回无效结果', 'ToolInvoke');
      return { status: 'error', errors: [error], ...this.#envelope(result || {}) };
    }
    return { status: 'ready', data: result.data || {}, errors: [], ...this.#envelope(result) };
  }

  async resolveDetail(detailRef, request = {}) {
    if (!detailRef || typeof detailRef.refId !== 'string') {
      return { status: 'error', errors: [queryError('not-found', 'DetailRef is required', 'workspace')] };
    }
    const result = await this.#read(`detail:${detailRef.source}:${detailRef.refId}`, () => this.capabilities.DetailQuery.resolveDetail({ ...request, detailRef }));
    return result.ok
      ? { status: 'ready', detail: result.data, errors: [], ...this.#envelope(result) }
      : { status: 'error', ...failureEnvelope(result, result.error) };
  }

  #envelope(result) { return envelopeMetadata(result); }

  async #page(key, invoke, itemKey) {
    const result = await this.#read(key, invoke);
    if (!result.ok) return { status: 'error', items: [], ...failureEnvelope(result, result.error) };
    const page = normalizePage(result, itemKey);
    return {
      status: page.errors.length ? 'partial' : 'ready',
      ...page,
      ...this.#envelope(result)
    };
  }

  async #read(key, invoke) {
    let result;
    try {
      result = await invoke();
    } catch (error) {
      return failure(queryError('unavailable', error instanceof Error ? error.message : 'Capability rejected', 'workspace'));
    }
    if (!result || typeof result !== 'object' || typeof result.ok !== 'boolean') {
      return failure(queryError('unavailable', 'Capability response must declare ok', 'workspace'));
    }
    if (result.ok === false) return failure(resultError(result) || queryError('unavailable', 'Capability returned no error', 'unknown'), result);
    if (!hasSnapshotMetadata(result)) {
      return failure(queryError('unavailable', 'Capability response has no snapshot metadata', 'workspace'));
    }
    const incoming = { ...result, data: result.data };
    const previous = this.latest.get(key);
    if (previous && isOlder(incoming, previous)) {
      return failure(queryError('stale', 'Response revision is older than the current result', 'workspace', { retryable: true }), incoming);
    }
    this.latest.set(key, incoming);
    return incoming;
  }
}

function hasSnapshotMetadata(result) {
  return ['snapshotId', 'revision', 'observedAt'].some((key) => result[key] !== undefined && result[key] !== null && result[key] !== '');
}

function revisionValue(result) {
  if (typeof result.revision === 'number') return result.revision;
  if (typeof result.revision === 'string' && /^\d+$/.test(result.revision)) return Number(result.revision);
  return undefined;
}

function isOlder(incoming, previous) {
  const next = revisionValue(incoming);
  const prior = revisionValue(previous);
  return next !== undefined && prior !== undefined && next < prior;
}

module.exports = {
  ERROR_CODES,
  WorkspaceQueryCore,
  queryError,
  success,
  failure,
  sourceMeta
};
