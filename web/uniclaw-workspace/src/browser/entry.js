'use strict';
const { WorkspaceQueryCore } = require('../core');
const { createWorkspaceApp } = require('../app/create-workspace-app');
const { bindWorkspaceLayout } = require('../ui/workspace-layout');
const WORKSPACE_STYLE_ID = 'uniclaw-workspace-styles';
// A persisted DSH session may require a cold decompression/read on first access.
// Keep the browser seam bounded, while allowing the real product session to load.
const DSH_CAPABILITY_TIMEOUT_MS = 30000;
const MAX_REQUIREMENT_DOCUMENT_BYTES = 512 * 1024;

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
        ...(value.error?.partial !== undefined ? { partial: value.error.partial } : {}),
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
    DetailQuery: { resolveDetail: ({ productSessionId, detailRef }) => call('artifact', [sessionByProduct.get(productSessionId) || productSessionId, detailRef.refId || detailRef], 'DetailQuery') },
    TaskCommand: {
      launchTask: (request = {}) => {
        if (typeof remote.launch !== 'function') return Promise.resolve({ ok: false, error: { code: 'unavailable', message: 'DSH Host 未提供任务发起接口', retryable: false, source: 'TaskCommand' } });
        const values = ['schemaVersion', 'contractVersion', 'launchRequestId', 'projectRef', 'testSetRef', 'taskRef', 'idempotencyKey', 'correlationId', 'requestedAt', 'environmentIntent', 'metadata', 'taskId'].map((key) => request[key]);
        return call('launch', values, 'TaskCommand');
      }
    }
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
      } else if (action === 'launch-task' || action === 'launch-project') {
        controller.openLaunchComposer(action === 'launch-project' ? target.getAttribute('data-project-id') : app.getState().selection.projectId);
      } else if (action === 'submit-launch') {
        const state = app.getState();
        const composer = state.ui && state.ui.launchComposer;
        const requirement = typeof composer?.requirement === 'string' ? composer.requirement.trim() : '';
        if (!requirement) return;
        const refs = launchRefs(state, composer.projectId);
        if (!refs.projectRef || !refs.testSetRef || !refs.taskRef) return;
        const requestId = `launch-request-${Date.now()}`;
        const localConfig = state.projects?.localConfig || {};
        const deviceOverrideEnabled = composer.deviceOverrideEnabled === true;
        const selectedDevice = deviceOverrideEnabled ? composer.deviceOverride : localConfig.device;
        const metadata = [{ key: 'requirement', value: requirement, valueOrigin: 'configured', availability: 'present', source: 'workspace-launch-form', authority: 'user' }];
        if (selectedDevice) metadata.push({ key: 'device', value: selectedDevice, valueOrigin: 'configured', availability: 'present', source: deviceOverrideEnabled ? 'workspace-launch-form' : 'local-config', authority: deviceOverrideEnabled ? 'user' : 'host-local-config' });
        if (composer.requirementDocument) metadata.push({ key: 'requirementDocument', value: composer.requirementDocument, valueOrigin: 'configured', availability: 'present', source: 'workspace-launch-form', authority: 'user' });
        void controller.launchTask({
          schemaVersion: 'uniclaw.workspace.task-launch-request.v1', contractVersion: 'uniclaw.workspace.contract.v1',
          launchRequestId: requestId, projectRef: refs.projectRef, testSetRef: refs.testSetRef,
          taskRef: { ...refs.taskRef, label: requirement },
          idempotencyKey: `workspace-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`,
          correlationId: `workspace-${Date.now()}`, requestedAt: new Date().toISOString(),
          ...(selectedDevice ? { environmentIntent: { device: { id: selectedDevice, override: deviceOverrideEnabled, source: deviceOverrideEnabled ? 'workspace-launch-form' : 'local-config', valueOrigin: 'configured' } } } : {}),
          metadata
        }).then((result) => { if (result.status === 'ready') controller.closeLaunchComposer(); });
      } else if (action === 'close-launch-composer') {
        controller.closeLaunchComposer();
      } else if (action === 'select-pane') {
        const paneTab = target.getAttribute('data-pane-tab');
        controller.selectPane(paneTab);
        if (paneTab === 'tools' && app.getState().tools?.status === 'idle') void controller.loadTools();
      } else if (action === 'generate-report') {
        void controller.generateReport();
      } else if (action === 'diagnose-task') {
        // PNL-012：诊断入口（model-procedure）。结果只进 state.diagnosis。
        void controller.diagnoseRun();
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
    container.addEventListener('input', (event) => {
      const target = event.target;
      if (target && target.matches && target.matches('[data-workspace-launch-requirement]')) controller.setLaunchRequirement(target.value);
      if (target && target.matches && target.matches('[data-workspace-tools-run-dir]')) controller.setToolsRunDir(target.value);
      if (target && target.matches && target.matches('[data-workspace-launch-device]') && target.value) controller.setLaunchDeviceOverride(true, target.value);
    });
    container.addEventListener('change', (event) => {
      const target = event.target;
      if (!target || !target.matches) return;
      if (target.matches('[data-workspace-launch-device-override]')) {
        const composer = app.getState().ui?.launchComposer || {};
        controller.setLaunchDeviceOverride(target.checked, composer.deviceOverride || null);
      } else if (target.matches('[data-workspace-launch-document]')) {
        const file = target.files && target.files[0];
        if (!file) return controller.setLaunchDocument(null);
        if (file.size > MAX_REQUIREMENT_DOCUMENT_BYTES) return controller.setLaunchDocumentError('需求文档不能超过 512 KB');
        if (typeof file.text !== 'function') return controller.setLaunchDocumentError('当前浏览器无法读取该需求文档');
        void file.text().then((content) => controller.setLaunchDocument({ name: file.name, mimeType: file.type || 'text/plain', sizeBytes: file.size, text: content })).catch(() => controller.setLaunchDocumentError('需求文档读取失败'));
      }
    });
  } });
  return Object.freeze({ start: () => { ensureWorkspaceStyles(container, styleText); layout.apply(); return app.start(); }, stop: () => { layout.dispose(); app.stop(); }, getApp: () => app, getLayout: () => layout });
}

function launchRefs(state, projectId) {
  const selection = state.selection || {};
  const task = (state.projects?.items || []).flatMap((project) => project.instances || project.taskInstances || project.tasks || [])
    .find((item) => item.productSessionId === selection.productSessionId)
    || (selection.taskInstances?.items || [])[0] || {};
  const defaults = state.projects?.launchDefaults || {};
  return {
    projectRef: logicalLaunchRef(task.projectRef) || logicalLaunchRef(defaults.projectRef) || logicalLaunchRef(projectId),
    testSetRef: logicalTestSetRef(task.testSetRef) || logicalTestSetRef(defaults.testSetRef),
    taskRef: logicalLaunchRef(task.taskRef || task.taskId) || logicalLaunchRef(defaults.taskRef),
  };
}

function logicalTestSetRef(value) {
  const ref = typeof value === 'string' ? { id: value } : value;
  if (!ref || typeof ref.id !== 'string' || ref.id.length === 0 || ref.version !== 'default') return null;
  return { id: ref.id, version: 'default', ...(typeof ref.sourceRevision === 'string' && ref.sourceRevision.length > 0 ? { sourceRevision: ref.sourceRevision } : {}) };
}

function logicalLaunchRef(value) {
  const id = typeof value === 'string' ? value : value && value.id;
  if (typeof id !== 'string' || id.length === 0 || id.startsWith('/') || id.startsWith('./') || id.startsWith('../') || id.includes('\\') || id.split('/').includes('..')) return null;
  return { id, ...(typeof value === 'object' && typeof value.label === 'string' ? { label: value.label } : {}) };
}
module.exports = { createDshWorkspaceBrowserBridge, createCapabilities, ensureWorkspaceStyles, envelope };
