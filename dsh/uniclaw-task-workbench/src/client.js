window.__ModuleLoader__.load({id:"@uniclaw/dsh-task-workbench",factory:(externalRequire)=>{const cache=Object.create(null);const modules={
  "src/browser/entry.js": (require, module, exports) => {
'use strict';
const { WorkspaceQueryCore } = require('../core');
const { createWorkspaceApp } = require('../app/create-workspace-app');
const WORKSPACE_STYLE_ID = 'uniclaw-workspace-styles';
// A persisted DSH session may require a cold decompression/read on first access.
// Keep the browser seam bounded, while allowing the real product session to load.
const DSH_CAPABILITY_TIMEOUT_MS = 30000;

function ensureWorkspaceStyles(container, styleText) {
  if (typeof styleText !== 'string' || styleText.length === 0) return;
  const ownerDocument = container && container.ownerDocument
    || (typeof document !== 'undefined' ? document : null);
  if (!ownerDocument || typeof ownerDocument.createElement !== 'function') return;
  if (typeof ownerDocument.getElementById === 'function' && ownerDocument.getElementById(WORKSPACE_STYLE_ID)) return;
  const style = ownerDocument.createElement('style');
  style.id = WORKSPACE_STYLE_ID;
  style.setAttribute('data-uniclaw-workspace', 'true');
  style.textContent = styleText;
  (ownerDocument.head || container).appendChild(style);
}
function envelope(value, capability) {
  if (value && typeof value === 'object' && typeof value.ok === 'boolean') {
    // Typert may wrap the host result once more as `{ ok, data }`. Unwrap the
    // plugin's `{ success, ... }` payload here while retaining the shared
    // capability contract and adding the observation marker required by the
    // renderer's stale-read guard.
    const hostValue = value.value && typeof value.value === 'object' ? value.value : value.data;
    if (value.ok === true && hostValue && typeof hostValue === 'object' && hostValue.success === true) {
      const hostData = hostValue.data && typeof hostValue.data === 'object' ? hostValue.data : hostValue;
      return { ...value, data: hostData, observedAt: value.observedAt || new Date().toISOString(), capability, source: 'dsh' };
    }
    if (value.ok === true && hostValue && typeof hostValue === 'object' && hostValue.success === false) {
      return envelope(hostValue, capability);
    }
    if (value.ok === false && value.error?.code) return value;
    return value.observedAt || value.snapshotId || value.revision !== undefined
      ? value
      : { ...value, observedAt: new Date().toISOString(), capability, source: 'dsh' };
  }
  // DSH panel methods use the host plugin's `{ success, ... }` result shape.
  // Normalize that adapter shape at the browser seam so the shared frontend
  // never needs to know whether it is running inside DSH or another host.
  if (value && typeof value === 'object' && value.success === true) {
    const data = value.data && typeof value.data === 'object' ? value.data : value;
    return {
      ok: true,
      data,
      observedAt: new Date().toISOString(),
      capability,
      source: 'dsh',
    };
  }
  if (value && typeof value === 'object' && value.success === false) {
    return {
      ok: false,
      error: {
        schemaVersion: 'uniclaw.workspace.query-error.v1',
        contractVersion: 'uniclaw.workspace.contract.v1',
        code: value.error?.code || 'unavailable',
        message: value.error?.message || 'DSH panel request failed',
        retryable: false,
        source: capability,
      },
      capability,
      source: 'dsh',
      observedAt: new Date().toISOString(),
    };
  }
  return { ok: false, error: { schemaVersion: 'uniclaw.workspace.query-error.v1', contractVersion: 'uniclaw.workspace.contract.v1', code: 'unavailable', message: 'Malformed DSH response', retryable: false, source: capability }, capability, source: 'dsh' };
}
function createCapabilities(remote) {
  if (!remote || typeof remote.workspace !== 'function' || typeof remote.session !== 'function' || typeof remote.artifact !== 'function') throw new TypeError('remote workspace/session/artifact methods are required');
  const sessionByProduct = new Map();
  const call = (method, args, capability) => Promise.race([
    Promise.resolve().then(() => remote[method](...args)).then((value) => envelope(value, capability)),
    new Promise((resolve) => setTimeout(() => resolve({
      ok: false,
      error: {
        schemaVersion: 'uniclaw.workspace.query-error.v1',
        contractVersion: 'uniclaw.workspace.contract.v1',
        code: 'timeout',
        message: `DSH ${method} capability timed out`,
        retryable: true,
        source: capability,
      },
      capability,
      source: 'dsh',
      observedAt: new Date().toISOString(),
    }), DSH_CAPABILITY_TIMEOUT_MS)),
  ]);
  const rememberSessionMappings = (projects) => {
    for (const project of (projects || [])) {
      for (const instance of (project.instances || [])) {
        if (instance.productSessionId && instance.sessionId) sessionByProduct.set(instance.productSessionId, instance.sessionId);
      }
    }
  };
  const listProjects = () => call('workspace', [], 'TaskQuery').then((r) => {
    if (r.ok) rememberSessionMappings(r.data.projects);
    return r;
  });
  return {
    TaskQuery: {
      listProjects,
      listTaskInstances: ({ projectId } = {}) => listProjects().then((r) => {
        if (!r.ok) return r;
        const project = (r.data.projects || []).find((item) => item.projectId === projectId);
        if (!project) {
          return {
            ...r,
            ok: false,
            error: {
              schemaVersion: 'uniclaw.workspace.query-error.v1',
              contractVersion: 'uniclaw.workspace.contract.v1',
              code: 'not-found',
              message: `Project ${projectId} was not found`,
              retryable: false,
              source: 'TaskQuery',
            },
          };
        }
        return { ...r, data: { taskInstances: project.instances || [], errors: r.data.errors || [] } };
      }),
    },
    SessionQuery: { getSession: ({ productSessionId }) => call('session', [sessionByProduct.get(productSessionId) || productSessionId], 'SessionQuery').then((r) => r.ok ? { ...r, data: r.data.session || r.data } : r), getTimeline: ({ productSessionId }) => call('session', [sessionByProduct.get(productSessionId) || productSessionId], 'SessionQuery').then((r) => r.ok ? { ...r, data: r.data.conversation || [] } : r) },
    TraceQuery: { getTraces: ({ productSessionId }) => call('session', [sessionByProduct.get(productSessionId) || productSessionId], 'TraceQuery').then((r) => {
      if (!r.ok) return r;
      const detailRef = { refId: 'trace.json', source: 'dsh', productSessionId };
      let detailAttached = false;
      const traces = [
        ...(r.data.dshTrace || []).map((item) => ({ ...item, source: 'dsh' })),
        ...(r.data.uniclawTrace || []).map((item) => {
          const projected = { ...item, source: 'uniclaw' };
          if (!detailAttached) {
            detailAttached = true;
            projected.detailRef = detailRef;
          }
          return projected;
        }),
        ...(r.data.uniflowTrace || []).map((item) => ({ ...item, source: 'uniflow' })),
      ];
      return { ...r, data: { traces } };
    }) },
    EvidenceQuery: { getEvidence: ({ productSessionId }) => call('session', [sessionByProduct.get(productSessionId) || productSessionId], 'EvidenceQuery').then((r) => {
      if (!r.ok) return r;
      const evidence = (r.data.evidence || []).map((item) => {
        if (item && typeof item === 'object') return item;
        const refId = String(item || '').split('/').pop() || 'artifact';
        return { title: refId, source: 'UniClaw runtime artifact', detailRef: { refId, source: 'dsh', productSessionId } };
      });
      return { ...r, data: { evidence } };
    }) },
    DetailQuery: { resolveDetail: ({ productSessionId, detailRef }) => call('artifact', [sessionByProduct.get(productSessionId) || productSessionId, detailRef.refId || detailRef], 'DetailQuery') }
  };
}
function createDshWorkspaceBrowserBridge({ remote, container, render, viewOptions, styleText } = {}) {
  if (!container || typeof container.appendChild !== 'function') throw new TypeError('container is required');
  let eventsBound = false;
  const app = createWorkspaceApp({ queryCore: new WorkspaceQueryCore(createCapabilities(remote)), render, viewOptions, mount: ({ html }) => {
    container.innerHTML = html;
    if (eventsBound || typeof container.addEventListener !== 'function') return;
    eventsBound = true;
    const controller = app.getController();
    container.addEventListener('click', (event) => {
      const target = event.target && typeof event.target.closest === 'function'
        ? event.target.closest('[data-workspace-action]') : null;
      if (!target || !container.contains(target)) return;
      const action = target.getAttribute('data-workspace-action');
      event.preventDefault();
      if (action === 'refresh') {
        void controller.refresh();
      } else if (action === 'select-pane') {
        controller.selectPane(target.getAttribute('data-pane-tab'));
      } else if (action === 'select-project') {
        void controller.selectProject(target.getAttribute('data-project-id'));
      } else if (action === 'select-task') {
        const productSessionId = target.getAttribute('data-product-session-id');
        void controller.selectTaskInstance(productSessionId).then(() => Promise.all([
          controller.loadSession(productSessionId),
          controller.loadTimeline(productSessionId),
          controller.loadTraces(productSessionId),
          controller.loadEvidence(productSessionId),
        ]));
      } else if (action === 'resolve-detail') {
        void controller.resolveDetail({ refId: target.getAttribute('data-detail-ref'), source: 'dsh', detailType: 'text' });
      }
    });
  } });
  return Object.freeze({ start: () => { ensureWorkspaceStyles(container, styleText); return app.start(); }, stop: () => app.stop(), getApp: () => app });
}
module.exports = { createDshWorkspaceBrowserBridge, createCapabilities, ensureWorkspaceStyles, envelope };

  },
  "src/core/index.js": (require, module, exports) => {
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

  },
  "src/app/create-workspace-app.js": (require, module, exports) => {
'use strict';

const { createWorkspaceController } = require('../features/workspace-controller');
const { createWorkspaceViewModel } = require('../features/workspace-view-model');
const { renderWorkspaceHtml } = require('../ui/render-workspace-html');

function createWorkspaceApp({ queryCore, controller, render = renderWorkspaceHtml, mount, viewOptions } = {}) {
  if (!queryCore && !controller) throw new TypeError('queryCore or controller is required');
  if (typeof mount !== 'function') throw new TypeError('mount is required');
  if (typeof render !== 'function') throw new TypeError('render must be a function');

  const workspaceController = controller || createWorkspaceController({ queryCore });
  if (!workspaceController || typeof workspaceController.subscribe !== 'function' || typeof workspaceController.getState !== 'function') {
    throw new TypeError('controller must expose subscribe and getState');
  }
  if (typeof workspaceController.loadProjects !== 'function') throw new TypeError('controller must expose loadProjects');

  let active = false;
  let unsubscribe = null;
  let startPromise = null;
  let lastError = null;
  let lifecycle = 0;

  const publish = (state, generation, onError) => {
    if (!active || generation !== lifecycle) return;
    try {
      const viewModel = createWorkspaceViewModel(state, viewOptions);
      const html = render(viewModel, viewOptions);
      mount({ html, viewModel, state });
      onError(null);
    } catch (error) {
      onError(error);
    }
  };

  function start() {
    if (startPromise) return startPromise;
    const generation = ++lifecycle;
    let runError = null;
    active = true;
    unsubscribe = workspaceController.subscribe((state) => publish(state, generation, (error) => { runError = error; lastError = error; }));
    publish(workspaceController.getState(), generation, (error) => { runError = error; lastError = error; });
    const promise = Promise.resolve().then(() => workspaceController.loadProjects()).then(() => {
      if (runError) throw runError;
      return workspaceController.getState();
    }).catch((error) => {
      if (generation === lifecycle) lastError = error;
      throw error;
    });
    startPromise = promise;
    return promise;
  }

  function stop() {
    active = false;
    lifecycle += 1;
    if (unsubscribe) unsubscribe();
    unsubscribe = null;
    startPromise = null;
  }

  return Object.freeze({
    start,
    stop,
    getController: () => workspaceController,
    getState: () => workspaceController.getState(),
    getError: () => lastError
  });
}

module.exports = { createWorkspaceApp };

  },
  "src/features/workspace-controller.js": (require, module, exports) => {
'use strict';

function createWorkspaceController({ queryCore } = {}) {
  if (!queryCore || typeof queryCore !== 'object') throw new TypeError('queryCore is required');
  const state = {
    projects: idlePage('projects'),
    selection: { projectId: null, productSessionId: null },
    ui: { activePane: 'trace' },
    session: idleData('session'),
    timeline: idlePage('timeline'),
    traces: idlePage('traces'),
    evidence: idlePage('evidence'),
    detail: idleData('detail')
  };
  const tokens = new Map();
  let listeners = [];

  const publish = () => {
    const snapshot = clone(state);
    listeners.forEach((listener) => listener(snapshot));
    return snapshot;
  };
  const action = (name, task) => {
    const token = (tokens.get(name) || 0) + 1;
    tokens.set(name, token);
    return task(token);
  };
  const current = (name, token) => tokens.get(name) === token;
  const selected = () => state.selection.productSessionId;
  const setLoading = (key) => { state[key] = { ...state[key], status: 'loading', errors: [] }; publish(); };
  const applyPage = (key, result, token) => {
    if (!current(key, token)) return publish();
    state[key] = { ...result, status: result.status || (result.errors?.length ? 'partial' : 'ready') };
    return publish();
  };
  const applyData = (key, result, token) => {
    if (!current(key, token)) return publish();
    state[key] = {
      ...result,
      ...(key === 'detail' && !Object.prototype.hasOwnProperty.call(result, 'detail') ? { detail: null } : {}),
      status: result.status || (result.errors?.length ? 'error' : 'ready')
    };
    return publish();
  };

  async function loadProjects() {
    return action('projects', async (token) => {
      setLoading('projects');
      return applyPage('projects', await queryCore.listProjects(), token);
    });
  }
  async function selectProject(projectId) {
    state.selection = { projectId: projectId ?? null, productSessionId: null, taskInstances: { status: 'loading', items: [], errors: [] } };
    resetSelectionData();
    const token = (tokens.get('selection') || 0) + 1;
    tokens.set('selection', token);
    publish();
    const tasks = await queryCore.listTaskInstances(projectId);
    if (!current('selection', token)) return publish();
    state.selection = {
      ...state.selection,
      taskInstances: {
        ...tasks,
        items: Array.isArray(tasks.items) ? tasks.items : [],
        errors: Array.isArray(tasks.errors) ? tasks.errors : [],
        status: tasks.status || (tasks.errors?.length ? 'partial' : 'ready')
      }
    };
    return publish();
  }
  async function selectTaskInstance(productSessionId) {
    if (typeof productSessionId !== 'string' || !productSessionId) throw new TypeError('productSessionId is required');
    const task = state.projects.items
      ?.flatMap((project) => project.instances || project.taskInstances || project.tasks || [])
      .find((item) => item.productSessionId === productSessionId) || null;
    state.selection = {
      ...state.selection,
      productSessionId,
      taskInstances: task
        ? { status: 'ready', items: [task], errors: [] }
        : (state.selection.taskInstances || { status: 'idle', items: [], errors: [] })
    };
    resetSelectionData();
    publish();
    return publish();
  }
  async function loadSession(productSessionId = selected()) {
    return readSelected('session', productSessionId, () => queryCore.getSession(productSessionId), applyData);
  }
  async function loadTimeline(productSessionId = selected()) {
    return readSelected('timeline', productSessionId, () => queryCore.getTimeline(productSessionId), applyPage);
  }
  async function loadTraces(productSessionId = selected(), request = {}) {
    return readSelected('traces', productSessionId, () => queryCore.getTraces(productSessionId, request), applyPage);
  }
  async function loadEvidence(productSessionId = selected()) {
    return readSelected('evidence', productSessionId, () => queryCore.getEvidence(productSessionId), applyPage);
  }
  async function resolveDetail(detailRef) {
    const productSessionId = selected();
    if (typeof productSessionId !== 'string' || !productSessionId) {
      state.detail = { status: 'error', detail: null, errors: [{ code: 'not-found', message: 'ProductSessionId is required', retryable: false, source: 'workspace' }] };
      return publish();
    }
    const token = (tokens.get('detail') || 0) + 1;
    tokens.set('detail', token);
    state.ui.activePane = 'detail';
    state.detail = { status: 'loading', detail: null, errors: [] };
    publish();
    const result = await queryCore.resolveDetail(detailRef, { productSessionId });
    if (selected() !== productSessionId) return publish();
    return applyData('detail', result, token);
  }
  function selectPane(pane) {
    const allowed = ['trace', 'evidence', 'detail'];
    state.ui.activePane = allowed.includes(pane) ? pane : 'trace';
    return publish();
  }
  async function refresh() {
    const productSessionId = selected();
    const work = [loadProjects()];
    if (productSessionId) work.push(loadSession(productSessionId), loadTimeline(productSessionId), loadTraces(productSessionId), loadEvidence(productSessionId));
    await Promise.all(work);
    return publish();
  }
  function subscribe(listener) {
    if (typeof listener !== 'function') throw new TypeError('listener is required');
    listeners = listeners.concat(listener);
    return () => { listeners = listeners.filter((item) => item !== listener); };
  }
  return Object.freeze({
    loadProjects, selectProject, selectTaskInstance, loadSession, loadTimeline,
    loadTraces, loadEvidence, resolveDetail, selectPane, refresh, subscribe,
    getState: () => clone(state)
  });

  async function readSelected(key, productSessionId, invoke, apply) {
    if (typeof productSessionId !== 'string' || !productSessionId) {
      state[key] = { status: 'error', ...(key === 'session' ? { session: null } : { items: [] }), errors: [{ code: 'not-found', message: 'ProductSessionId is required', retryable: false, source: 'workspace' }] };
      return publish();
    }
    const token = (tokens.get(key) || 0) + 1;
    tokens.set(key, token);
    setLoading(key);
    const result = await invoke();
    if (selected() !== productSessionId) return publish();
    return apply(key, result, token);
  }
  function resetSelectionData() {
    for (const key of ['session', 'timeline', 'traces', 'evidence', 'detail']) {
      tokens.set(key, (tokens.get(key) || 0) + 1);
      state[key] = key === 'session' ? idleData(key) : idlePage(key);
    }
    state.detail = idleData('detail');
  }
}

function idlePage(key) { return { status: 'idle', items: [], errors: [], section: key }; }
function idleData(key) { return { status: 'idle', [key === 'session' ? 'session' : 'detail']: null, errors: [] }; }
function clone(value) { return JSON.parse(JSON.stringify(value)); }

module.exports = { createWorkspaceController };

  },
  "src/features/workspace-view-model.js": (require, module, exports) => {
'use strict';

// Renderer-neutral projection for the Workspace read model. This module only
// shapes data; it never performs I/O and never depends on a transport.
function createWorkspaceViewModel(input, options = {}) {
  const state = clone(input || {});
  const selectedId = state.selection && state.selection.productSessionId;
  const projectsState = page(state.projects);
  const taskState = state.selection && state.selection.taskInstances
    ? page(state.selection.taskInstances)
    : page({ status: 'idle', items: [], errors: [] });
  const tasks = taskState.items;
  const selectedTask = tasks.find((task) => task.productSessionId === selectedId) || null;
  const session = state.session && state.session.session;
  const metadata = mergeMetadata(selectedTask, session);
  const notices = collectNotices(state, selectedTask);

  return {
    navigation: {
      status: projectsState.status === 'ready' && taskState.status === 'ready' ? 'ready' : combineStatus(projectsState, taskState),
      projects: projectsState.items.map((project) => ({
        projectId: project.projectId || project.id,
        name: project.name || project.title || project.projectId || project.id,
        status: project.status || 'ready',
        tasks: (project.instances || project.taskInstances || project.tasks || []).map(taskCard)
      })),
      selectedProjectId: state.selection && state.selection.projectId || null,
      selectedProductSessionId: selectedId || null,
      taskInstances: tasks.map(taskCard),
      errors: [...projectsState.errors, ...taskState.errors]
    },
    taskHeader: {
      status: combineStatus(state.session, selectedTask ? { status: 'ready' } : { status: selectedId ? 'partial' : 'idle' }),
      productSessionId: selectedId || null,
      title: (selectedTask && (selectedTask.title || selectedTask.name || selectedTask.task && selectedTask.task.title)) || (session && (session.title || session.name)) || options.emptyTitle || '未选择任务',
      projectId: state.selection && state.selection.projectId || (selectedTask && selectedTask.projectId) || null,
      correlationStatus: selectedTask && selectedTask.correlationStatus || (selectedId ? 'correlated' : 'unselected'),
      source: options.agentLabel || 'Uni-Agent',
      origin: selectedTask && selectedTask.source || (session && session.source) || null,
      authority: selectedTask && selectedTask.authority || (session && session.authority) || null,
      metadata,
      errors: errorsOf(state.session)
    },
    conversationTimeline: timelinePane(state.timeline, session),
    tracePane: groupedPane(state.traces, 'traces', (item) => item.source || 'unknown'),
    evidencePane: evidencePane(state.evidence),
    executionPane: executionPane((state.session && state.session.session) || null),
    metadataPane: { status: state.session && state.session.status || 'idle', items: metadata, errors: errorsOf(state.session) },
    activePane: state.ui && state.ui.activePane || 'trace',
    detailActions: detailActions(state, selectedId),
    notices,
    status: overallStatus(state, notices)
  };
}

function taskCard(task) {
  return {
    id: task.id || task.instanceId || task.productSessionId || null,
    productSessionId: task.productSessionId || null,
    projectId: task.projectId || null,
    title: task.title || task.name || task.task && task.task.title || task.productSessionId || '未命名任务',
    status: task.status || 'unknown',
    correlationStatus: task.correlationStatus || (task.productSessionId ? 'correlated' : 'uncorrelated'),
    source: task.source || null,
    authority: task.authority || null,
    observed: Boolean(task.observed),
    errors: clone(task.errors || [])
  };
}

function timelinePane(value, session = null) {
  const pane = page(value);
  const groups = { request: [], decision: [], result: [] };
  pane.items.forEach((item) => {
    const kind = normalizeKind(item.kind || item.type || item.stage || item.eventType);
    groups[kind].push({
      id: item.id || item.eventId || null,
      kind,
      summary: item.summary || item.title || item.label || item.content || item.message || '',
      source: item.source || null,
      authority: item.authority || null,
      correlationId: item.correlationId || null,
      detailRef: item.detailRef || null,
      detailAvailable: Boolean(item.detailRef),
      raw: clone(item)
    });
  });
  const rounds = conversationRounds(session && session.conversationGroups, groups);
  const runStages = Array.isArray(session && session.runStages)
    ? session.runStages.map(normalizeConversationStage).filter(Boolean)
    : [];
  return {
    status: pane.status,
    groups,
    rounds,
    runStages,
    mode: rounds.length > 0 ? 'agent-conversation' : 'timeline',
    errors: pane.errors,
    snapshotId: pane.snapshotId,
    revision: pane.revision,
    observedAt: pane.observedAt
  };
}

function conversationRounds(value, fallbackGroups) {
  if (Array.isArray(value) && value.length > 0) {
    return value.map((round, index) => ({
      groupId: round.groupId || `round-${index + 1}`,
      round: round.round || index + 1,
      status: round.status || 'pending',
      stages: (Array.isArray(round.stages)
        ? round.stages
        : [round.request, round.decision, round.submission || round.result, ...(round.extras || [])])
        .map(normalizeConversationStage)
        .filter(Boolean)
    })).filter((round) => round.stages.length > 0);
  }
  const stages = ['request', 'decision', 'result']
    .flatMap((kind) => (fallbackGroups[kind] || []).map(normalizeConversationStage));
  return stages.length > 0 ? [{ groupId: 'round-1', round: 1, status: 'legacy', stages }] : [];
}

function normalizeConversationStage(stage) {
  if (!stage || typeof stage !== 'object') return null;
  const kind = stage.kind || stage.role || 'result';
  const result = stage.result && typeof stage.result === 'object' ? stage.result : null;
  return {
    kind,
    role: normalizeRole(stage.role, kind),
    label: stage.label || labelForKind(kind),
    text: stage.objective || stage.summary || stage.text || (result && result.text) || stage.justification || '',
    objective: stage.objective || null,
    phase: stage.phase || null,
    decisionKind: stage.decisionKind || null,
    justification: stage.justification || null,
    steps: Array.isArray(stage.steps) ? stage.steps : [],
    status: stage.status || (stage.isError ? 'error' : null),
    isError: stage.isError === true || (result && result.isError === true),
    seq: stage.seq == null ? null : stage.seq,
    ts: stage.ts || '',
    detailRef: stage.detailRef || null
  };
}

function roleForKind(kind) {
  if (kind === 'request' || kind === 'user') return 'requester';
  if (kind === 'decision' || kind === 'assistant' || kind === 'plan') return 'agent';
  if (kind === 'tool-call') return 'tool';
  return 'system';
}

function normalizeRole(role, kind) {
  const value = String(role || '').toLowerCase();
  if (value === 'request' || value === 'requester' || value === 'user' || value === 'caller') return 'requester';
  if (value === 'assistant' || value === 'agent') return 'agent';
  if (value === 'tool') return 'tool';
  if (value === 'system') return 'system';
  return roleForKind(kind);
}

function labelForKind(kind) {
  if (kind === 'request' || kind === 'user') return '调用方请求';
  if (kind === 'decision' || kind === 'assistant') return 'Uni-Agent 决策';
  if (kind === 'tool-call') return '能力调用';
  if (kind === 'tool-result' || kind === 'result') return '提交结果';
  if (kind === 'execution') return '执行结果';
  if (kind === 'verification') return '验证结果';
  return 'Uni-Agent';
}

function groupedPane(value, key, groupBy) {
  const pane = page(value);
  const groups = {};
  pane.items.map((item, index) => {
    const source = groupBy(item);
    const summary = item.summary || item.label || item.text || item.definition || item.type || item.kind || item.spanId || `${key} ${index + 1}`;
    if (!summary) return null;
    return {
      id: item.id || item.traceId || item.spanId || null,
      source,
      type: item.type || item.kind || null,
      label: item.label || null,
      summary: String(summary).split('\n')[0].slice(0, 240),
      text: item.text || null,
      ts: item.ts || null,
      seq: item.seq == null ? null : item.seq,
      authority: item.authority || null,
      correlationId: item.correlationId || null,
      parentSpanId: item.parentSpanId || null,
      captureSequence: item.captureSequence == null ? null : item.captureSequence,
      productSessionId: item.productSessionId || null,
      detailRef: item.detailRef || null,
      detailAvailable: Boolean(item.detailRef),
      raw: clone(item)
    };
  }).filter(Boolean).forEach((item) => {
    if (!groups[item.source]) groups[item.source] = [];
    groups[item.source].push(item);
  });
  return { status: pane.status, groups, errors: pane.errors, snapshotId: pane.snapshotId, revision: pane.revision, observedAt: pane.observedAt, itemKey: key };
}

function executionPane(session) {
  const items = Array.isArray(session && session.runStages) ? session.runStages.map((item) => ({
    kind: item.kind || 'execution',
    label: item.label || (item.kind === 'verification' ? '验证结果' : '执行结果'),
    status: item.status || 'unknown',
    text: item.text || '',
    source: item.source || null,
    evidenceRefs: Array.isArray(item.evidenceRefs) ? item.evidenceRefs : []
  })) : [];
  return { status: items.length > 0 ? 'ready' : 'empty', items };
}

function evidencePane(value) {
  const pane = page(value);
  return {
    status: pane.status,
    items: pane.items.map((item) => {
      const error = item.error || (item.errors && item.errors[0]);
      const code = error && error.code;
      const correlationStatus = item.correlationStatus || (item.productSessionId ? 'correlated' : null);
      return {
        id: item.id || item.evidenceId || null,
        title: item.title || item.name || item.label || item.detailRef && item.detailRef.refId || '未命名证据',
        source: item.source || null,
        authority: item.authority || null,
        correlationId: item.correlationId || null,
        correlationStatus,
        detailRef: item.detailRef || null,
        detailAction: item.detailRef && correlationStatus !== 'uncorrelated' && !['permission-denied', 'not-found', 'uncorrelated'].includes(code)
          ? { enabled: true, detailRef: clone(item.detailRef) }
          : { enabled: false, reason: correlationStatus === 'uncorrelated' ? 'uncorrelated' : (code || (item.detailRef ? 'unavailable' : 'not-found')) },
        error: error ? clone(error) : null,
        raw: clone(item)
      };
    }),
    errors: pane.errors
  };
}

function detailActions(state, productSessionId) {
  const detail = state.detail || {};
  return { status: detail.status || 'idle', enabled: Boolean(productSessionId), current: clone(detail.detail || null), errors: errorsOf(detail), resolve: detail.status === 'loading' ? 'loading' : 'available' };
}

function mergeMetadata(task, session) {
  const out = {};
  [task && task.metadata, session && session.metadata].forEach((metadata) => {
    if (metadata && typeof metadata === 'object' && !Array.isArray(metadata)) Object.assign(out, clone(metadata));
  });
  return out;
}

function collectNotices(state, selectedTask) {
  const notices = [];
  [state.projects, state.selection && state.selection.taskInstances, state.session, state.timeline, state.traces, state.evidence, state.detail]
    .forEach((part) => errorsOf(part).forEach((error) => notices.push({ level: severity(error.code), code: error.code || 'unknown', message: error.message || 'Workspace error', source: error.source || 'workspace', error: clone(error) })));
  if (selectedTask && selectedTask.correlationStatus === 'uncorrelated') notices.push({ level: 'warning', code: 'uncorrelated', message: '任务实例未关联 ProductSessionId', source: 'workspace' });
  [state.projects, state.selection && state.selection.taskInstances, state.traces, state.evidence].forEach((part) => {
    (part && Array.isArray(part.items) ? part.items : []).forEach((item) => {
      if (item && item.correlationStatus === 'uncorrelated') notices.push({ level: 'warning', code: 'uncorrelated', message: '记录未关联 ProductSessionId', source: item.source || 'workspace', error: { code: 'uncorrelated' } });
    });
  });
  return aggregateNotices(notices);
}

function aggregateNotices(notices) {
  const grouped = new Map();
  notices.forEach((notice) => {
    const code = notice.code || 'unknown';
    const message = notice.message || 'Workspace error';
    const key = `${code}\u0000${message}`;
    const current = grouped.get(key);
    if (current) {
      current.count += 1;
      if (notice.source && !current.sources.includes(notice.source)) current.sources.push(notice.source);
      current.occurrences.push({ source: notice.source || 'workspace', message });
      return;
    }
    grouped.set(key, {
      ...notice,
      code,
      message,
      count: 1,
      sources: notice.source ? [notice.source] : ['workspace'],
      occurrences: [{ source: notice.source || 'workspace', message }]
    });
  });
  return [...grouped.values()];
}

function overallStatus(state, notices) {
  const statuses = [state.projects, state.selection && state.selection.taskInstances, state.session, state.timeline, state.traces, state.evidence, state.detail].filter(Boolean).map((part) => part.status);
  if (statuses.includes('loading')) return 'loading';
  if (notices.length || statuses.includes('partial')) return 'partial';
  if (statuses.every((status) => status === 'idle')) return 'idle';
  return statuses.includes('error') ? 'error' : 'ready';
}

function page(value) { return { status: value && value.status || 'idle', items: Array.isArray(value && value.items) ? value.items : [], errors: errorsOf(value), snapshotId: value && value.snapshotId, revision: value && value.revision, observedAt: value && value.observedAt }; }
function errorsOf(value) { return Array.isArray(value && value.errors) ? value.errors : []; }
function combineStatus(a, b) { const statuses = [a && a.status, b && b.status]; return statuses.includes('error') ? 'error' : statuses.includes('loading') ? 'loading' : statuses.includes('partial') ? 'partial' : statuses[0] || 'idle'; }
function normalizeKind(kind) { const value = String(kind || '').toLowerCase(); return value.includes('request') ? 'request' : value.includes('decision') || value.includes('think') || value.includes('plan') ? 'decision' : 'result'; }
function severity(code) { return ['permission-denied', 'unavailable', 'timeout'].includes(code) ? 'error' : 'warning'; }
function clone(value) { return value === undefined ? undefined : JSON.parse(JSON.stringify(value)); }

module.exports = { createWorkspaceViewModel };

  },
  "src/ui/render-workspace-html.js": (require, module, exports) => {
'use strict';

function escapeHtml(value) {
  return String(value == null ? '' : value)
    .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
}

function text(value, fallback = '') { return escapeHtml(value == null || value === '' ? fallback : value); }
function attr(name, value) { return value == null || value === '' ? '' : ` ${name}="${escapeHtml(value)}"`; }
function status(value) { return `<span class="workspace-status workspace-status--${escapeHtml(value || 'unknown')}">${text(value, 'unknown')}</span>`; }
function errorText(errors) { return (errors || []).map((error) => `<li class="workspace-notice__item">${text(error.message || error.code || 'Workspace error')}</li>`).join(''); }

function renderWorkspaceHtml(viewModel = {}, options = {}) {
  const vm = viewModel || {};
  const nav = vm.navigation || {};
  const header = vm.taskHeader || {};
  const renderedTaskKeys = new Set();
  const projectsHtml = (nav.projects || []).map((project) => renderProject(project, renderedTaskKeys, nav.selectedProductSessionId)).join('');
  const taskHtml = (nav.taskInstances || [])
    .filter((task) => {
      const key = taskIdentity(task);
      return !key || !renderedTaskKeys.has(key);
    })
    .map((task) => {
      const key = taskIdentity(task);
      if (key) renderedTaskKeys.add(key);
      return renderTask(task, nav.selectedProductSessionId);
    }).join('');
  const root = `workspace workspace--${escapeHtml(vm.status || 'idle')}`;
  return `<div class="${root}" data-workspace-status="${escapeHtml(vm.status || 'idle')}">
  <nav class="workspace-navigation" aria-label="${text(options.navigationLabel, '任务导航')}">
    <h1 class="workspace-navigation__title">${text(options.title, 'UniClaw Workspace')}</h1>
    <div class="workspace-navigation__projects">${projectsHtml}</div>
    <div class="workspace-navigation__tasks" data-selected-product-session-id="${escapeHtml(nav.selectedProductSessionId || '')}">${taskHtml}</div>
  </nav>
  <main class="workspace-main">
    ${renderNotices(vm.notices)}
    <header class="workspace-task-header" data-selected-product-session-id="${escapeHtml(header.productSessionId || '')}">
      <div><p class="workspace-eyebrow">${text(header.source, 'Uni-Agent')}<span class="workspace-header-origin">${text(header.origin, '')}</span></p><h2>${text(header.title, '未选择任务')}</h2></div>
      <button type="button" class="workspace-refresh-action" data-workspace-action="refresh">刷新</button>
      <div class="workspace-task-header__meta">${status(header.status)}<span class="workspace-correlation">${text(header.correlationStatus, 'unselected')}</span></div>
    </header>
    <section class="workspace-observation-bar" aria-label="任务观察面板">
      <div class="workspace-pane-tabs" role="tablist" aria-label="任务信息标签页">
        ${renderTabButton('trace', 'Trace', vm.activePane)}
        ${renderTabButton('evidence', 'Evidence', vm.activePane)}
        ${renderTabButton('detail', '详情', vm.activePane)}
      </div>
      <div class="workspace-diagnostics-slot"><span><strong>诊断入口</strong><small>Skill 接入后可分析当前任务</small></span><button type="button" class="workspace-diagnostics-action" data-workspace-action="diagnose-task" disabled aria-disabled="true">诊断当前任务</button></div>
    </section>
    <section class="workspace-conversation" aria-labelledby="conversation-title"><h3 id="conversation-title">Uni-Agent 解决过程</h3><div class="workspace-conversation-shell">${renderTimeline(vm.conversationTimeline)}</div></section>
    <section class="workspace-inspector" aria-label="${text(options.panesLabel, '任务信息')}">
      <div class="workspace-inspector__main">
        <div class="workspace-tab-panels">
          ${renderTabPanel('trace', vm.activePane, renderTracePane(vm.tracePane))}
          ${renderTabPanel('evidence', vm.activePane, renderEvidencePane(vm.evidencePane))}
          ${renderTabPanel('detail', vm.activePane, renderDetail(vm.detailActions))}
        </div>
      </div>
      <aside class="workspace-side-rail" aria-label="任务概览">
        ${renderMetadataPane(vm.metadataPane)}
        ${renderExecutionPane(vm.executionPane)}
      </aside>
    </section>
  </main>
</div>`;
}

function renderProject(project, renderedTaskKeys, selectedProductSessionId) {
  const tasks = (project.tasks || []).filter((task) => {
    const key = taskIdentity(task);
    if (!key) return true;
    if (renderedTaskKeys.has(key)) return false;
    renderedTaskKeys.add(key);
    return true;
  });
  return `<section class="workspace-project" data-project-id="${escapeHtml(project.projectId || '')}"><button type="button" class="workspace-project__action" data-workspace-action="select-project" data-project-id="${escapeHtml(project.projectId || '')}">${text(project.name, '未命名项目')}</button>${tasks.map((task) => renderTask(task, selectedProductSessionId)).join('')}</section>`;
}
function taskIdentity(task = {}) { return task.productSessionId || task.id || null; }
function renderTask(task, selectedProductSessionId = null) {
  const orphan = task.correlationStatus === 'uncorrelated' || !task.productSessionId;
  const selected = !orphan && selectedProductSessionId && selectedProductSessionId === task.productSessionId;
  return `<article class="workspace-task-card${orphan ? ' is-uncorrelated' : ''}${selected ? ' is-selected' : ''}" data-task-id="${escapeHtml(task.id || '')}" data-correlation-status="${escapeHtml(task.correlationStatus || 'unknown')}"><button type="button" class="workspace-task-card__action" data-workspace-action="select-task" data-product-session-id="${escapeHtml(task.productSessionId || '')}"${orphan ? ' disabled aria-disabled="true"' : ''}${selected ? ' aria-current="true"' : ''}><h3>${text(task.title, '未命名任务')}</h3><div class="workspace-task-card__meta"><span>${text(task.productSessionId, orphan ? '未关联 ProductSession' : '未知 ProductSession')}</span>${status(task.status)}</div></button></article>`;
}
function renderTimeline(pane = {}) {
  const rounds = Array.isArray(pane.rounds) ? pane.rounds : [];
  const roundHtml = rounds.map(renderConversationRound).join('');
  const runStageHtml = (pane.runStages || []).map(renderConversationStage).join('');
  return `<div class="workspace-timeline workspace-timeline--conversation" data-status="${escapeHtml(pane.status || 'idle')}" data-conversation-mode="${escapeHtml(pane.mode || 'timeline')}">${roundHtml}${runStageHtml ? `<section class="workspace-conversation-results"><h4>执行与验证</h4>${runStageHtml}</section>` : ''}${roundHtml || runStageHtml ? '' : '<p class="workspace-empty">暂无 Uni-Agent 对话记录</p>'}</div>`;
}
function renderConversationRound(round = {}) { return `<section class="workspace-conversation-round" data-round="${escapeHtml(round.round || '')}" data-status="${escapeHtml(round.status || '')}"><header class="workspace-conversation-round__header"><span>Round ${escapeHtml(round.round || '')}</span>${status(round.status || 'pending')}</header><div class="workspace-dialogue">${(round.stages || []).map(renderConversationStage).join('')}</div></section>`; }
function renderConversationStage(stage = {}) {
  const body = stage.objective || stage.text || stage.justification || '';
  const steps = Array.isArray(stage.steps) && stage.steps.length ? `<ol class="workspace-message__steps">${stage.steps.map((step) => `<li>${text(typeof step === 'string' ? step : step.label || step.description || JSON.stringify(step))}</li>`).join('')}</ol>` : '';
  const meta = [stage.ts, stage.seq != null ? `#${stage.seq}` : ''].filter(Boolean).join(' · ');
  const avatar = stage.role === 'agent' ? 'U' : stage.role === 'requester' ? 'R' : stage.role === 'tool' ? 'T' : '•';
  return `<article class="workspace-message workspace-message--${escapeHtml(stage.role || 'system')}${stage.isError ? ' is-error' : ''}" data-kind="${escapeHtml(stage.kind || '')}"><div class="workspace-message__avatar">${avatar}</div><div class="workspace-message__body"><div class="workspace-message__head"><strong>${text(stage.label, 'Uni-Agent')}</strong>${stage.decisionKind ? `<span class="workspace-chip">${text(stage.decisionKind)}</span>` : ''}${meta ? `<small>${text(meta)}</small>` : ''}</div>${body ? `<p>${text(body)}</p>` : ''}${steps}${stage.detailRef ? `<button type="button" class="workspace-detail-action workspace-detail-action--small" data-workspace-action="resolve-detail" data-detail-ref="${escapeHtml(stage.detailRef.refId || stage.detailRef)}">查看明细</button>` : ''}</div></article>`;
}
function renderTabButton(id, label, active) { return `<button type="button" role="tab" class="workspace-pane-tab${active === id ? ' is-active' : ''}" aria-selected="${active === id ? 'true' : 'false'}" data-workspace-action="select-pane" data-pane-tab="${id}">${text(label)}</button>`; }
function renderTabPanel(id, active, content) { return `<div class="workspace-tab-panel${active === id ? ' is-active' : ''}" role="tabpanel" data-pane-panel="${id}"${active === id ? '' : ' hidden'}>${content}</div>`; }
function renderTracePane(pane = {}) {
  const groups = Object.keys(pane.groups || {});
  const total = pane.groups ? groups.reduce((n, key) => n + pane.groups[key].length, 0) : 0;
  return `<section class="workspace-pane workspace-pane--trace" data-pane="trace"><div class="workspace-pane__heading"><div><h3>Trace</h3><p class="workspace-pane__subheading">按来源折叠查看运行与诊断轨迹</p></div><span class="workspace-pane__count">${total}</span></div>${groups.length ? groups.map((source) => renderTraceGroup(source, pane.groups[source] || [])).join('') : '<p class="workspace-empty">暂无可展示的 Trace 事件</p>'}</section>`;
}
function renderTraceGroup(source, items) {
  const open = source !== 'uniflow';
  const label = source === 'uniclaw' ? 'UniClaw Runtime' : source === 'uniflow' ? 'UniFlow' : source;
  const byId = new Map(items.filter((item) => item.id).map((item) => [item.id, item]));
  return `<details class="workspace-trace-group" data-source="${escapeHtml(source)}"${open ? ' open' : ''}><summary><span>${text(label)}</span><span class="workspace-trace-group__count">${items.length}</span></summary><div class="workspace-trace-tree">${items.map((item) => renderTraceCard(item, byId)).join('')}</div></details>`;
}
function traceDepth(item, byId, seen = new Set()) {
  if (!item.parentSpanId || !byId.has(item.parentSpanId) || seen.has(item.id)) return 0;
  seen.add(item.id);
  return Math.min(4, 1 + traceDepth(byId.get(item.parentSpanId), byId, seen));
}
function renderTraceCard(item, byId) {
  const depth = traceDepth(item, byId);
  return `<article class="workspace-trace-card" style="--trace-depth:${depth}"><div><strong>${text(item.summary, '未命名事件')}</strong><small>${text([item.type, item.ts, item.seq != null ? `#${item.seq}` : ''].filter(Boolean).join(' · '))}</small></div>${item.detailAvailable && item.detailRef ? `<button type="button" class="workspace-detail-action" data-workspace-action="resolve-detail" data-detail-ref="${escapeHtml(item.detailRef.refId || item.detailRef)}">查看明细</button>` : ''}</article>`;
}
function renderEvidencePane(pane = {}) {
  return `<section class="workspace-pane workspace-pane--evidence" data-pane="evidence"><h3>Evidence</h3>${(pane.items || []).map((item) => { const action = item.detailAction || {}; return `<article class="workspace-evidence-card" data-correlation-status="${escapeHtml(item.correlationStatus || '')}"><h4>${text(item.title, '未命名证据')}</h4><p>${text(item.source, '未知来源')}</p>${action.enabled && action.detailRef ? `<button type="button" class="workspace-detail-action" data-workspace-action="resolve-detail" data-detail-ref="${escapeHtml(action.detailRef.refId || action.detailRef)}">查看明细</button>` : `<button type="button" class="workspace-detail-action" disabled aria-disabled="true">${text(action.reason, '详情不可用')}</button>`}${item.error ? `<p class="workspace-error">${text(item.error.message || item.error.code)}</p>` : ''}</article>`; }).join('')}</section>`;
}
function renderMetadataPane(pane = {}) { return `<section class="workspace-pane workspace-pane--metadata" data-pane="metadata"><div class="workspace-pane__heading"><h3>Metadata</h3><span class="workspace-status workspace-status--${escapeHtml(pane.status || 'unknown')}">${text(pane.status, 'unknown')}</span></div><dl>${Object.keys(pane.items || {}).map((key) => `<div><dt>${text(key)}</dt><dd>${text(pane.items[key])}</dd></div>`).join('')}</dl></section>`; }
function renderExecutionPane(pane = {}) { return `<section class="workspace-pane workspace-pane--execution" data-pane="execution"><div class="workspace-pane__heading"><div><h3>执行结果</h3><p class="workspace-pane__subheading">当前运行的结论与证据</p></div><span class="workspace-status workspace-status--${escapeHtml(pane.status || 'empty')}">${text(pane.status === 'ready' ? '已产生' : '待运行', pane.status || 'empty')}</span></div>${(pane.items || []).length ? (pane.items || []).map((item) => `<article class="workspace-execution-card"><div><strong>${text(item.label, '执行结果')}</strong><span class="workspace-chip">${text(item.status, 'unknown')}</span></div><p>${text(item.text, '暂无结果摘要')}</p>${item.evidenceRefs?.length ? `<div class="workspace-reference-list">${item.evidenceRefs.map((ref) => `<button type="button" class="workspace-reference-link" data-workspace-action="resolve-detail" data-detail-ref="${escapeHtml(String(ref).split('/').pop())}">${text(String(ref).split('/').pop())}</button>`).join('')}</div>` : ''}</article>`).join('') : '<p class="workspace-empty workspace-empty--compact">当前 session 尚无 UniClaw runtime 执行产物</p>'}</section>`; }
function renderDetail(detail = {}) { return `<aside class="workspace-detail-drawer" data-detail-status="${escapeHtml(detail.status || 'idle')}" aria-label="详情"><h3>详情</h3>${detail.current ? `<pre>${text(JSON.stringify(detail.current, null, 2))}</pre>` : `<p>${text(detail.status === 'loading' ? '正在加载详情' : '选择记录查看详情')}</p>`}</aside>`; }
function renderNotices(notices = []) {
  if (!notices.length) return '';
  const hasRetry = notices.some((notice) => ['timeout', 'unavailable', 'stale'].includes(notice.code));
  const rows = notices.map((notice) => {
    const count = Number(notice.count) > 1 ? `<span class="workspace-notice__count">${notice.count} 处</span>` : '';
    const source = Array.isArray(notice.sources) && notice.sources.length > 0 ? `<span class="workspace-notice__source">${text(notice.sources.join(' · '))}</span>` : '';
    const details = Number(notice.count) > 1
      ? `<details class="workspace-notice__details"><summary>查看受影响来源</summary><ul>${(notice.occurrences || []).map((item) => `<li>${text(item.source)}：${text(item.message)}</li>`).join('')}</ul></details>`
      : '';
    return `<div class="workspace-notice workspace-notice--${escapeHtml(notice.level || 'warning')}" data-code="${escapeHtml(notice.code || '')}"><div class="workspace-notice__headline"><span>${text(notice.message || notice.code || 'Workspace error')}</span>${count}${source}</div>${details}</div>`;
  }).join('');
  return `<aside class="workspace-notices" role="status" aria-label="Workspace 状态"><div class="workspace-notices__header"><strong>读取状态</strong>${hasRetry ? '<button type="button" class="workspace-notices__retry" data-workspace-action="refresh">重试当前任务</button>' : ''}</div>${rows}</aside>`;
}

module.exports = { renderWorkspaceHtml, escapeHtml };

  }
};const load=(id,parent="")=>{if(id==="react")return externalRequire(id);if(id==="@uniclaw/workspace/browser")id="src/browser/entry.js";if(id!=="src/browser/entry.js"&&!id.startsWith("."))return externalRequire(id);const base=parent.split("/").slice(0,-1).join("/");const key=id.split("/").reduce((p,x)=>{if(x==="..")p.pop();else if(x!==".")p.push(x);return p},base?base.split("/"):[]).join("/");const k0=key.endsWith(".js")?key:key+".js";const k=modules[k0]?k0:k0.replace(/\.js$/,"/index.js");if(cache[k])return cache[k].exports;if(!modules[k])throw Error("missing bundled module: "+k);const m={exports:{}};cache[k]=m;modules[k]((x)=>load(x,k),m,m.exports);return m.exports};const bridge=load("src/browser/entry.js");const workspaceStyleText=".workspace {\n  color-scheme: light;\n  font-family: Inter, ui-sans-serif, -apple-system, BlinkMacSystemFont, \"Segoe UI\", \"PingFang SC\", \"Microsoft YaHei\", sans-serif;\n  --workspace-bg: #f4f6fa; --workspace-surface: #fff; --workspace-surface-muted: #f8fafc;\n  --workspace-border: #e4e8f0; --workspace-border-strong: #d4dbe7; --workspace-text: #182230;\n  --workspace-muted: #718096; --workspace-accent: #5968d8; --workspace-accent-soft: #eef0ff;\n  --workspace-agent: #f0f2ff; --workspace-request: #eef8f5; --workspace-tool: #f7f4ff;\n  --workspace-danger: #b42318; --workspace-warning: #b54708; --workspace-radius: 16px; --workspace-gap: 18px;\n  min-height: 100vh; display: grid; align-items: start; grid-template-columns: minmax(270px, 300px) minmax(0, 1fr);\n  background: var(--workspace-bg); color: var(--workspace-text); font-size: 14px; line-height: 1.55;\n}\n.workspace, .workspace * , .workspace *::before, .workspace *::after { box-sizing: border-box; }\n.workspace button { font: inherit; }\n.workspace-navigation { position: sticky; top: 0; height: 100vh; overflow-y: auto; padding: 24px 18px; border-right: 1px solid var(--workspace-border); background: rgba(255,255,255,.94); }\n.workspace-navigation__title { margin: 0 4px 26px; font-size: 20px; letter-spacing: -.02em; }\n.workspace-navigation__projects { display: grid; grid-template-columns: minmax(0,1fr); gap: 20px; min-width: 0; max-width: 100%; }\n.workspace-project { display: grid; gap: 8px; min-width: 0; max-width: 100%; }\n.workspace-project__action { display: flex; width: 100%; padding: 0 4px; border: 0; background: transparent; color: var(--workspace-muted); font-size: 12px; font-weight: 700; letter-spacing: .05em; text-align: left; text-transform: uppercase; cursor: pointer; }\n.workspace-project__action:hover { color: var(--workspace-accent); }\n.workspace-navigation__tasks { display: grid; gap: 8px; margin-top: 20px; min-width: 0; max-width: 100%; }\n.workspace-task-card { min-width: 0; max-width: 100%; border: 1px solid var(--workspace-border); border-radius: 13px; background: var(--workspace-surface); box-shadow: 0 2px 8px rgba(27,39,71,.03); }\n.workspace-task-card__action { display: block; width: 100%; min-width: 0; max-width: 100%; padding: 13px 14px; border: 0; border-radius: inherit; background: transparent; color: inherit; text-align: left; cursor: pointer; }\n.workspace-task-card__action:hover { background: #f8f9ff; }\n.workspace-task-card.is-selected { border-color: #aeb7f3; box-shadow: 0 0 0 3px rgba(89,104,216,.12); }\n.workspace-task-card.is-selected .workspace-task-card__action { background: var(--workspace-accent-soft); }\n.workspace-task-card h3 { margin: 0 0 7px; font-size: 14px; line-height: 1.35; }\n.workspace-task-card__meta { display: flex; align-items: center; justify-content: space-between; gap: 8px; color: var(--workspace-muted); font-size: 12px; }\n.workspace-task-card__meta > span { min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }\n.workspace-task-card.is-uncorrelated { border-style: dashed; opacity: .78; }\n.workspace-task-card.is-uncorrelated .workspace-task-card__action { cursor: not-allowed; }\n.workspace-main { min-width: 0; max-width: 1440px; width: 100%; min-height: 100vh; padding: 28px 32px 48px; overflow: visible; }\n.workspace-notices { display: grid; gap: 8px; margin-bottom: var(--workspace-gap); padding: 10px; border: 1px solid #f6d7b9; border-radius: 14px; background: #fffaf5; }\n.workspace-notices__header { display: flex; align-items: center; justify-content: space-between; gap: 12px; padding: 2px 4px 4px; color: #7b4a22; font-size: 12px; }\n.workspace-notices__retry { border: 0; border-radius: 7px; padding: 5px 9px; background: #fff; color: var(--workspace-accent); font: inherit; font-weight: 700; cursor: pointer; }\n.workspace-notice { padding: 8px 10px; border: 1px solid #f6d7b9; border-radius: 9px; background: #fff8f1; color: var(--workspace-warning); font-size: 13px; }\n.workspace-notice--error { border-color: #f2c4c0; background: #fff5f4; color: var(--workspace-danger); }\n.workspace-notice__headline { display: flex; align-items: center; gap: 8px; flex-wrap: wrap; }\n.workspace-notice__count { padding: 2px 6px; border-radius: 999px; background: rgba(180, 56, 47, .1); font-size: 11px; font-weight: 700; }\n.workspace-notice__source { color: #8f6e67; font-size: 11px; }\n.workspace-notice__details { margin-top: 6px; color: #7c6660; font-size: 12px; }\n.workspace-notice__details summary { cursor: pointer; color: var(--workspace-accent); font-weight: 700; }\n.workspace-notice__details ul { display: grid; gap: 3px; margin: 5px 0 0 16px; padding: 0; }\n.workspace-task-header { display: flex; align-items: center; justify-content: space-between; gap: 18px; padding: 22px 24px; border: 1px solid var(--workspace-border); border-radius: var(--workspace-radius); background: var(--workspace-surface); box-shadow: 0 8px 24px rgba(27,39,71,.05); }\n.workspace-task-header h2 { margin: 2px 0 0; font-size: clamp(20px,2vw,28px); letter-spacing: -.025em; }\n.workspace-eyebrow { display: flex; align-items: center; gap: 8px; margin: 0; color: var(--workspace-accent); font-size: 12px; font-weight: 700; letter-spacing: .04em; text-transform: uppercase; }\n.workspace-header-origin { color: var(--workspace-muted); font-weight: 500; text-transform: none; }\n.workspace-task-header__meta { display: flex; align-items: center; gap: 10px; color: var(--workspace-muted); font-size: 12px; white-space: nowrap; }\n.workspace-refresh-action { order: 2; padding: 9px 13px; border: 1px solid var(--workspace-border-strong); border-radius: 9px; background: var(--workspace-surface-muted); color: var(--workspace-text); cursor: pointer; }\n.workspace-refresh-action:hover { border-color: var(--workspace-accent); color: var(--workspace-accent); }\n.workspace-correlation { color: var(--workspace-muted); }\n.workspace-status { display: inline-flex; align-items: center; padding: 3px 8px; border-radius: 999px; background: #eef2ff; color: var(--workspace-accent); font-size: 11px; font-weight: 700; line-height: 1.2; }\n.workspace-status--completed,.workspace-status--submitted,.workspace-status--ready { background: #eaf8f1; color: #137a4b; }\n.workspace-status--partial,.workspace-status--pending,.workspace-status--legacy { background: #fff5e8; color: var(--workspace-warning); }\n.workspace-status--error,.workspace-status--failed { background: #fff0ef; color: var(--workspace-danger); }\n.workspace-observation-bar { position: sticky; top: 12px; z-index: 8; display: flex; align-items: center; justify-content: space-between; gap: 14px; margin-top: 16px; padding: 7px; border: 1px solid rgba(212,219,231,.92); border-radius: 14px; background: rgba(248,250,252,.92); box-shadow: 0 8px 20px rgba(27,39,71,.08); backdrop-filter: blur(12px); }\n.workspace-observation-bar .workspace-pane-tabs { flex: 0 0 auto; border: 0; background: transparent; }\n.workspace-diagnostics-slot { display: flex; align-items: center; gap: 12px; min-width: 0; padding: 3px 5px 3px 12px; color: var(--workspace-muted); }\n.workspace-diagnostics-slot span { display: grid; gap: 1px; min-width: 0; }\n.workspace-diagnostics-slot strong { color: var(--workspace-text); font-size: 12px; }\n.workspace-diagnostics-slot small { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; font-size: 11px; }\n.workspace-diagnostics-action { flex: 0 0 auto; padding: 7px 10px; border: 1px solid var(--workspace-border-strong); border-radius: 8px; background: var(--workspace-surface); color: var(--workspace-muted); font-size: 11px; font-weight: 700; cursor: not-allowed; }\n.workspace-diagnostics-action:disabled { opacity: .82; }\n.workspace-conversation { margin-top: 18px; }\n.workspace-conversation > h3 { margin: 0 0 12px; font-size: 17px; letter-spacing: -.01em; }\n.workspace-conversation-shell { position: relative; min-width: 0; max-width: 100%; padding: 14px; border: 1px solid #dce3ef; border-radius: 20px; background: linear-gradient(180deg,#edf2f8 0%,#f5f7fb 100%); box-shadow: inset 0 1px 0 rgba(255,255,255,.8); }\n.workspace-timeline { min-width: 0; max-width: 100%; display: grid; gap: 12px; }\n.workspace-conversation-round { position: relative; min-width: 0; max-width: 100%; padding: 15px 16px 16px; border: 1px solid rgba(212,219,231,.9); border-radius: 15px; background: rgba(255,255,255,.82); box-shadow: 0 3px 10px rgba(27,39,71,.035); }\n.workspace-conversation-round + .workspace-conversation-round { margin-top: 2px; }\n.workspace-conversation-round__header { display: flex; align-items: center; justify-content: space-between; margin-bottom: 13px; padding-bottom: 9px; border-bottom: 1px solid rgba(228,232,240,.9); color: var(--workspace-muted); font-size: 12px; font-weight: 700; }\n.workspace-conversation-round__header::before { content: ''; width: 7px; height: 7px; margin-right: 6px; border-radius: 50%; background: var(--workspace-accent); box-shadow: 0 0 0 4px var(--workspace-accent-soft); }\n.workspace-conversation-round__header span { display: flex; align-items: center; margin-right: auto; }\n.workspace-dialogue { display: grid; min-width: 0; max-width: 100%; gap: 11px; }\n.workspace-message { display: flex; align-items: flex-start; min-width: 0; max-width: min(860px,92%); gap: 10px; }\n.workspace-message--agent { margin-right: auto; }\n.workspace-message--requester { margin-left: auto; flex-direction: row-reverse; }\n.workspace-message--tool,.workspace-message--system { margin-left: 34px; }\n.workspace-message__avatar { display: grid; flex: 0 0 28px; place-items: center; width: 28px; height: 28px; border-radius: 9px; background: #e8ebf5; color: #566173; font-size: 12px; font-weight: 800; }\n.workspace-message--requester .workspace-message__avatar { background: #d9f2e8; color: #15734e; }\n.workspace-message--agent .workspace-message__avatar { background: #dfe3ff; color: #4b58bb; }\n.workspace-message--tool .workspace-message__avatar { background: #eee5ff; color: #7251a8; }\n.workspace-message__body { min-width: 0; max-width: 100%; overflow: hidden; padding: 11px 14px; border: 1px solid var(--workspace-border); border-radius: 13px; background: var(--workspace-surface-muted); }\n.workspace-message--requester .workspace-message__body { border-color: #ccebdd; background: var(--workspace-request); }\n.workspace-message--agent .workspace-message__body { border-color: #d7dcff; background: var(--workspace-agent); }\n.workspace-message--tool .workspace-message__body { border-color: #e3d8f7; background: var(--workspace-tool); }\n.workspace-message.is-error .workspace-message__body { border-color: #f1c1bd; background: #fff3f2; }\n.workspace-message__head { display: flex; align-items: center; gap: 8px; margin-bottom: 5px; color: var(--workspace-muted); font-size: 12px; }\n.workspace-message__head strong { color: var(--workspace-text); font-size: 13px; }\n.workspace-message__head small { margin-left: auto; color: var(--workspace-muted); font-size: 11px; }\n.workspace-message__body p { margin: 0; white-space: pre-wrap; overflow-wrap: anywhere; }\n.workspace-message__steps { min-width: 0; max-width: 100%; margin: 8px 0 0 18px; padding: 0; color: #3c4860; overflow-wrap: anywhere; word-break: break-word; }\n.workspace-message__steps li { min-width: 0; max-width: 100%; overflow-wrap: anywhere; word-break: break-word; }\n.workspace-chip { padding: 2px 7px; border-radius: 999px; background: rgba(89,104,216,.12); color: #4b58bb; font-size: 10px; font-weight: 700; }\n.workspace-detail-action--small { margin-top: 9px; padding: 5px 9px; font-size: 11px; }\n.workspace-conversation-results { padding: 14px 16px; border: 1px dashed var(--workspace-border-strong); border-radius: 13px; background: rgba(255,255,255,.55); }\n.workspace-conversation-results h4 { margin: 0 0 10px; color: var(--workspace-muted); font-size: 12px; }\n.workspace-empty { margin: 0; padding: 28px; border: 1px dashed var(--workspace-border-strong); border-radius: var(--workspace-radius); color: var(--workspace-muted); text-align: center; }\n.workspace-inspector { display: grid; grid-template-columns: minmax(0,1fr) minmax(280px,.38fr); gap: var(--workspace-gap); margin-top: var(--workspace-gap); align-items: start; }\n.workspace-inspector__main { min-width: 0; }\n.workspace-pane-tabs { display: flex; gap: 6px; padding: 5px; border: 1px solid var(--workspace-border); border-radius: 12px; background: var(--workspace-surface-muted); }\n.workspace-pane-tab { flex: 0 0 auto; padding: 8px 15px; border: 0; border-radius: 8px; background: transparent; color: var(--workspace-muted); font-weight: 700; cursor: pointer; }\n.workspace-pane-tab:hover { color: var(--workspace-accent); }\n.workspace-pane-tab.is-active { background: var(--workspace-surface); color: var(--workspace-accent); box-shadow: 0 2px 7px rgba(27,39,71,.08); }\n.workspace-tab-panels { margin-top: 10px; }\n.workspace-tab-panel[hidden] { display: none; }\n.workspace-side-rail { display: grid; gap: var(--workspace-gap); min-width: 0; }\n.workspace-pane,.workspace-detail-drawer { min-width: 0; padding: 18px; border: 1px solid var(--workspace-border); border-radius: var(--workspace-radius); background: var(--workspace-surface); box-shadow: 0 4px 14px rgba(27,39,71,.03); }\n.workspace-pane h3,.workspace-detail-drawer h3 { margin: 0 0 12px; font-size: 16px; }\n.workspace-pane__heading { display: flex; align-items: center; justify-content: space-between; gap: 10px; }\n.workspace-pane__heading h3 { margin: 0; }\n.workspace-pane__subheading { margin: 2px 0 0; color: var(--workspace-muted); font-size: 11px; }\n.workspace-pane__count { min-width: 22px; padding: 2px 7px; border-radius: 999px; background: var(--workspace-accent-soft); color: var(--workspace-accent); font-size: 11px; font-weight: 700; text-align: center; }\n.workspace-trace-group { margin-top: 12px; border: 1px solid var(--workspace-border); border-radius: 12px; background: var(--workspace-surface-muted); overflow: hidden; }\n.workspace-trace-group summary { display: flex; align-items: center; justify-content: space-between; gap: 8px; padding: 10px 12px; color: var(--workspace-text); font-size: 12px; font-weight: 800; cursor: pointer; list-style: none; }\n.workspace-trace-group summary::-webkit-details-marker { display: none; }\n.workspace-trace-group summary::before { content: '›'; display: inline-block; width: 15px; color: var(--workspace-accent); font-size: 17px; line-height: 1; transform: rotate(0deg); transition: transform .15s ease; }\n.workspace-trace-group[open] summary::before { transform: rotate(90deg); }\n.workspace-trace-group__count { padding: 2px 7px; border-radius: 999px; background: var(--workspace-surface); color: var(--workspace-muted); font-size: 10px; }\n.workspace-trace-tree { padding: 0 8px 8px; border-top: 1px solid var(--workspace-border); }\n.workspace-trace-card,.workspace-evidence-card { display: flex; align-items: center; justify-content: space-between; gap: 10px; margin-top: 8px; padding: 10px 11px 10px calc(11px + (var(--trace-depth, 0) * 16px)); border: 1px solid var(--workspace-border); border-radius: 10px; background: var(--workspace-surface); }\n.workspace-trace-card { position: relative; min-width: 0; max-width: 100%; }\n.workspace-trace-card::before { content: ''; position: absolute; left: calc(5px + (var(--trace-depth, 0) * 16px)); top: 0; bottom: 0; width: 1px; background: #d8deeb; }\n.workspace-trace-card > div { min-width: 0; display: grid; gap: 3px; }\n.workspace-trace-card strong { display: block; min-width: 0; max-width: 100%; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; font-size: 13px; }\n.workspace-trace-card small { overflow: hidden; color: var(--workspace-muted); text-overflow: ellipsis; white-space: nowrap; font-size: 11px; }\n.workspace-evidence-card { display: block; }\n.workspace-evidence-card h4 { margin: 0 0 5px; font-size: 13px; }\n.workspace-evidence-card p { margin: 0 0 8px; color: var(--workspace-muted); font-size: 12px; }\n.workspace-execution-card { display: grid; gap: 7px; margin-top: 10px; padding: 12px; border: 1px solid #d7dcff; border-radius: 11px; background: var(--workspace-agent); }\n.workspace-execution-card > div { display: flex; align-items: center; justify-content: space-between; gap: 8px; }\n.workspace-execution-card p { margin: 0; color: #3c4860; font-size: 12px; }\n.workspace-execution-card small { color: var(--workspace-muted); font-size: 11px; overflow-wrap: anywhere; }\n.workspace-reference-list { display: flex; flex-wrap: wrap; gap: 6px; }\n.workspace-reference-link { padding: 0; border: 0; background: transparent; color: var(--workspace-accent); font: inherit; font-size: 11px; text-align: left; text-decoration: underline; cursor: pointer; }\n.workspace-empty--compact { padding: 16px 10px; font-size: 12px; }\n.workspace-detail-action { padding: 7px 11px; border: 0; border-radius: 8px; background: var(--workspace-accent); color: #fff; font-size: 12px; cursor: pointer; }\n.workspace-detail-action:hover { background: #4655c3; }\n.workspace-detail-action:disabled { background: #d8dde7; color: #7b8494; cursor: not-allowed; }\n.workspace-error { color: var(--workspace-danger) !important; }\n.workspace-pane dl { margin: 0; }\n.workspace-pane dl div { display: flex; justify-content: space-between; gap: 10px; padding: 7px 0; border-bottom: 1px solid var(--workspace-border); }\n.workspace-pane dt { color: var(--workspace-muted); }\n.workspace-pane dd { margin: 0; overflow-wrap: anywhere; text-align: right; }\n.workspace-detail-drawer { margin-top: var(--workspace-gap); }\n.workspace-detail-drawer pre { max-height: 360px; margin: 0; overflow: auto; white-space: pre-wrap; overflow-wrap: anywhere; color: #334155; font: 12px/1.6 ui-monospace,SFMono-Regular,Menlo,Consolas,monospace; }\n@media (max-width:1100px) { .workspace { grid-template-columns: minmax(240px,270px) minmax(0,1fr); } .workspace-main { padding: 22px; } .workspace-inspector { grid-template-columns: minmax(0,1fr) minmax(240px,.45fr); } }\n@media (max-width:760px) { .workspace { display: block; } .workspace-navigation { position: static; height: auto; max-height: none; border-right: 0; border-bottom: 1px solid var(--workspace-border); } .workspace-navigation__projects { grid-template-columns: repeat(2,minmax(0,1fr)); } .workspace-main { padding: 18px 14px 32px; } .workspace-task-header { align-items: flex-start; } .workspace-task-header__meta { flex-direction: column; align-items: flex-end; } .workspace-inspector { grid-template-columns: 1fr; } .workspace-message,.workspace-message--tool,.workspace-message--system { max-width: 100%; margin-left: 0; } }\n@media (max-width:520px) { .workspace-navigation__projects { grid-template-columns: 1fr; } .workspace-task-header { display: grid; grid-template-columns: 1fr auto; } .workspace-refresh-action { grid-column: 2; grid-row: 1; } .workspace-task-header__meta { grid-column: 2; grid-row: 2; } .workspace-message__head small { display: none; } .workspace-observation-bar { align-items: stretch; flex-direction: column; } .workspace-diagnostics-slot { justify-content: space-between; padding: 4px 5px; } }\n";const strict=(typeSymbol)=>({mode:"strict",typeSymbol,schema:{parse:(value)=>value},create:()=>({parse:(value)=>value})});const param=(name)=>({name,wire:name,source:"json",codec:strict("uniclaw-task-workbench/"+name)});const descriptor=(method,names)=>({id:"uniclawTaskPanel."+method,service:"uniclawTaskPanel",namespace:"uniclawTaskPanel",method,invocation:{kind:"direct"},parameters:names.map(param),result:strict("uniclaw-task-workbench/JsonValue")});const contribution={package:"@uniclaw/dsh-task-workbench",descriptors:[descriptor("workspace",[]),descriptor("session",["sessionId"]),descriptor("artifact",["sessionId","ref"])]};function apply(ctx){const slots=ctx.get("slots");if(!slots)return;slots.inject("sidebar.footer.action",()=>slots.register({name:"sidebar.footer.action",id:"uniclaw-task-panel-entry",order:12,label:"UniClaw 工作空间"},()=>externalRequire("react").createElement("button",{type:"button",onClick:()=>{const panel=ctx.get("remote.uniclawTaskPanel");const mountApi=panel&&typeof panel.$mount==="function"?panel:ctx.get("remote");if(!mountApi||typeof mountApi.$mount!=="function")return;mountApi.$mount(contribution).then(()=>{const mountedPanel=ctx.get("remote.uniclawTaskPanel");const api=mountedPanel&&typeof mountedPanel.workspace==="function"?mountedPanel:mountApi;if(!api||typeof api.workspace!=="function"||typeof api.session!=="function"||typeof api.artifact!=="function"||typeof document==="undefined"||!document.body)return;bridge.createDshWorkspaceBrowserBridge({remote:api,container:document.body,styleText:workspaceStyleText}).start()})}},"UniClaw 工作空间")));slots.inject("shell.overlay",()=>slots.register({name:"shell.overlay",id:"uniclaw-task-panel-overlay",order:12,label:"UniClaw 工作空间"},()=>null))}return{apply,inject:["slots","remote"]}}});
