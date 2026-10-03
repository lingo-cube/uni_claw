'use strict';
const { WorkspaceQueryCore } = require('../core');
const { createWorkspaceApp } = require('../app/create-workspace-app');
const { bindWorkspaceLayout } = require('../ui/workspace-layout');
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
  const call = async (method, args, capability) => {
    let timer;
    try { return await Promise.race([
    Promise.resolve().then(() => remote[method](...args)).then((value) => envelope(value, capability)),
    new Promise((resolve) => { timer = setTimeout(() => resolve({
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
    }), DSH_CAPABILITY_TIMEOUT_MS); }),
    ]); } finally { clearTimeout(timer); }
  };
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
      return { ...r, data: { traces, context: r.data.uniclawTraceContext ? { ...r.data.uniclawTraceContext, source: 'uniclaw' } : null } };
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
  const layout = bindWorkspaceLayout(container);
  let eventsBound = false;
  const app = createWorkspaceApp({ queryCore: new WorkspaceQueryCore(createCapabilities(remote)), render, viewOptions, mount: ({ html }) => {
    container.innerHTML = html;
    layout.apply();
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
      } else if (action === 'select-trace-mode') {
        controller.selectTraceMode(target.getAttribute('data-trace-mode'));
      } else if (action === 'select-trace-source') {
        controller.selectTraceSource(target.getAttribute('data-trace-source'));
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
      } else if (action === 'inspect-trace') {
        controller.inspectTrace(Number(target.getAttribute('data-trace-index')));
      } else if (action === 'close-detail') {
        controller.closeDetail();
      }
    });
  } });
  return Object.freeze({ start: () => { ensureWorkspaceStyles(container, styleText); layout.apply(); return app.start(); }, stop: () => { layout.dispose(); app.stop(); }, getApp: () => app, getLayout: () => layout });
}
module.exports = { createDshWorkspaceBrowserBridge, createCapabilities, ensureWorkspaceStyles, envelope };
