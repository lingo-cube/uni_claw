'use strict';

function createWorkspaceController({ queryCore } = {}) {
  if (!queryCore || typeof queryCore !== 'object') throw new TypeError('queryCore is required');
  const state = {
    projects: idlePage('projects'),
    selection: { projectId: null, productSessionId: null },
    ui: { activePane: 'trace', traceMode: 'combined', traceSource: 'all', detailReturnPane: 'trace', detailModalOpen: false },
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
    state.ui.detailReturnPane = state.ui.activePane === 'detail'
      ? (state.ui.detailReturnPane || 'trace')
      : state.ui.activePane;
    state.ui.detailModalOpen = true;
    state.detail = { status: 'loading', detail: null, errors: [] };
    publish();
    const result = await queryCore.resolveDetail(detailRef, { productSessionId });
    if (selected() !== productSessionId) return publish();
    return applyData('detail', result, token);
  }
  function selectPane(pane) {
    const allowed = ['trace', 'evidence', 'detail'];
    state.ui.activePane = allowed.includes(pane) ? pane : 'trace';
    if (pane === 'detail') state.ui.detailModalOpen = true;
    return publish();
  }
  function selectTraceMode(mode) {
    state.ui.traceMode = mode === 'split' ? 'split' : 'combined';
    return publish();
  }
  function selectTraceSource(source) {
    state.ui.traceSource = typeof source === 'string' && source ? source : 'all';
    return publish();
  }
  function closeDetail() {
    state.ui.detailModalOpen = false;
    if (state.ui.activePane === 'detail') state.ui.activePane = state.ui.detailReturnPane || 'trace';
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
    loadTraces, loadEvidence, resolveDetail, selectPane, selectTraceMode, selectTraceSource, closeDetail, refresh, subscribe,
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
