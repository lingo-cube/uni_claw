window.__ModuleLoader__.load({id:"@uniclaw/dsh-task-workbench",factory:(externalRequire)=>{const cache=Object.create(null);const modules={
  "src/browser/entry.js": (require, module, exports) => {
'use strict';
const { WorkspaceQueryCore } = require('../core');
const { createWorkspaceApp } = require('../app/create-workspace-app');
const { bindWorkspaceLayout } = require('../ui/workspace-layout');
const { bindWorkspaceReading } = require('../ui/workspace-reading');
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
  // A refresh reads several panes concurrently from the same Host detail.
  // Share only the in-flight read: later refreshes must re-check the source.
  const sessionReads = new Map();
  const readSession = (productSessionId, capability) => {
    const sessionId = sessionByProduct.get(productSessionId) || productSessionId;
    const key = JSON.stringify([productSessionId, sessionId]);
    if (!sessionReads.has(key)) {
      const pending = call('session', [sessionId], 'SessionQuery').finally(() => sessionReads.delete(key));
      sessionReads.set(key, pending);
    }
    return sessionReads.get(key).then((result) => ({
      ...result,
      capability,
      ...(!result.ok && result.error?.source === 'SessionQuery'
        ? { error: { ...result.error, source: capability } }
        : {})
    }));
  };
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
    SessionQuery: { getSession: ({ productSessionId }) => readSession(productSessionId, 'SessionQuery').then((r) => r.ok ? { ...r, data: r.data.session || r.data } : r), getTimeline: ({ productSessionId }) => readSession(productSessionId, 'SessionQuery').then((r) => r.ok ? { ...r, data: r.data.conversation || [] } : r) },
    TraceQuery: { getTraces: ({ productSessionId }) => readSession(productSessionId, 'TraceQuery').then((r) => {
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
    EvidenceQuery: { getEvidence: ({ productSessionId }) => readSession(productSessionId, 'EvidenceQuery').then((r) => {
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
  const reading = bindWorkspaceReading(container, { closeInspector: () => app.getController().closeInspector() });
  let eventsBound = false;
  const app = createWorkspaceApp({ queryCore: new WorkspaceQueryCore(createCapabilities(remote)), render, viewOptions, mount: ({ html }) => {
    reading.capture();
    container.innerHTML = html;
    layout.apply();
    reading.restore();
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
      } else if (action === 'select-view') {
        controller.selectView(target.getAttribute('data-view'));
      } else if (action === 'close-inspector') {
        controller.closeInspector();
      } else if (action === 'inspect-conversation') {
        controller.inspectConversation(Number(target.getAttribute('data-round-index')), Number(target.getAttribute('data-stage-index')));
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
  return Object.freeze({ start: () => { ensureWorkspaceStyles(container, styleText); layout.apply(); return app.start(); }, stop: () => { reading.dispose(); layout.dispose(); app.stop(); }, getApp: () => app, getLayout: () => layout });
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
    ui: { activeView: 'conversation', inspectorOpen: false, activePane: 'trace', traceMode: 'combined', traceSource: 'all', detailReturnPane: 'trace', detailModalOpen: false, toolsRunDir: '', launchComposer: { open: false, projectId: null, requirement: '', deviceOverrideEnabled: false, deviceOverride: null, requirementDocument: null, documentError: null } },
    session: idleData('session'),
    timeline: idlePage('timeline'),
    traces: idlePage('traces'),
    evidence: idlePage('evidence'),
    tools: idlePage('tools'),
    detail: idleData('detail'),
    report: { status: 'idle', data: null, errors: [] },
    diagnosis: { status: 'idle', data: null, errors: [] },
    launch: { status: 'idle', data: null, errors: [] }
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
  async function loadTools() {
    if (state.tools.status === 'loading') return publish();
    return action('tools', async (token) => {
      setLoading('tools');
      const result = await queryCore.listTools();
      const items = result.ok === false ? [] : (result.items || []);
      return applyPage('tools', { status: result.status || (result.errors?.length ? 'error' : 'ready'), items, errors: result.errors || [] }, token);
    });
  }
  function setToolsRunDir(value) {
    state.ui.toolsRunDir = typeof value === 'string' ? value : '';
    return publish();
  }
  async function generateReport() {
    const runDir = (state.ui.toolsRunDir || '').trim();
    if (!runDir) return publish();
    return action('report', async (token) => {
      state.report = { status: 'loading', data: null, errors: [] };
      publish();
      const result = await queryCore.invokeTool('run-report', { runDir });
      if (!current('report', token)) return publish();
      state.report = { status: result.status || (result.errors?.length ? 'error' : 'ready'), data: result.data || null, errors: result.errors || [] };
      return publish();
    });
  }
  // PNL-012：run 诊断（model-procedure 工具）。read-only posture：结果只进
  // state.diagnosis，不写任何 run 目录或 store；文本在展示层强制带非权威声明。
  async function diagnoseRun() {
    const runDir = (state.ui.toolsRunDir || '').trim();
    if (!runDir) return publish();
    return action('diagnosis', async (token) => {
      state.diagnosis = { status: 'loading', data: null, errors: [] };
      publish();
      const result = await queryCore.invokeTool('run-diagnosis', { runDir });
      if (!current('diagnosis', token)) return publish();
      state.diagnosis = { status: result.status || (result.errors?.length ? 'error' : 'ready'), data: result.data || null, errors: result.errors || [] };
      return publish();
    });
  }
  async function launchTask(request = {}) {
    if (typeof queryCore.launchTask !== 'function') {
      state.launch = { status: 'error', data: null, errors: [{ code: 'unavailable', message: '当前 Host 未提供任务发起能力', retryable: false, source: 'TaskCommand' }] };
      return publish();
    }
    state.launch = { status: 'loading', data: null, errors: [] };
    publish();
    state.launch = await queryCore.launchTask(request);
    publish();
    if (state.launch.status === 'ready') await refresh();
    return publish();
  }
  function openLaunchComposer(projectId = state.selection.projectId) {
    state.ui.launchComposer = { open: true, projectId: projectId ?? null, requirement: '', deviceOverrideEnabled: false, deviceOverride: null, requirementDocument: null, documentError: null };
    return publish();
  }
  function setLaunchRequirement(requirement = '') {
    state.ui.launchComposer = { ...state.ui.launchComposer, requirement: String(requirement) };
    return publish();
  }
  function setLaunchDeviceOverride(enabled, device = null) {
    state.ui.launchComposer = { ...state.ui.launchComposer, deviceOverrideEnabled: enabled === true, deviceOverride: enabled === true && device ? String(device) : null };
    return publish();
  }
  function setLaunchDocument(document = null) {
    state.ui.launchComposer = { ...state.ui.launchComposer, requirementDocument: document || null, documentError: null };
    return publish();
  }
  function setLaunchDocumentError(message = '') {
    state.ui.launchComposer = { ...state.ui.launchComposer, requirementDocument: null, documentError: message ? String(message) : null };
    return publish();
  }
  function closeLaunchComposer() {
    state.ui.launchComposer = { ...state.ui.launchComposer, open: false };
    return publish();
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
    state.ui.inspectorOpen = true;
    state.detail = { status: 'loading', detail: null, errors: [] };
    publish();
    const result = await queryCore.resolveDetail(detailRef, { productSessionId });
    if (selected() !== productSessionId) return publish();
    return applyData('detail', result, token);
  }
  function selectPane(pane) {
    const allowed = ['trace', 'evidence', 'tools', 'detail', 'metadata', 'execution'];
    state.ui.activePane = allowed.includes(pane) ? pane : 'trace';
    state.ui.detailModalOpen = pane === 'detail';
    state.ui.inspectorOpen = state.ui.activePane !== 'trace';
    if (state.ui.activePane === 'trace') state.ui.activeView = 'trace';
    return publish();
  }
  function selectView(view) {
    state.ui.activeView = view === 'trace' ? 'trace' : 'conversation';
    return publish();
  }
  function closeInspector() {
    state.ui.inspectorOpen = false;
    state.ui.detailModalOpen = false;
    return publish();
  }
  function inspectTrace(index) {
    if (!selected() || !Number.isInteger(index) || !state.traces.items?.[index]) return publish();
    tokens.set('detail', (tokens.get('detail') || 0) + 1);
    state.ui.detailReturnPane = 'trace';
    state.ui.detailModalOpen = true;
    state.ui.inspectorOpen = true;
    state.detail = { status: 'ready', detail: { format: 'trace-record', record: clone(state.traces.items[index]) }, errors: [] };
    return publish();
  }
  function inspectConversation(roundIndex, stageIndex) {
    if (!selected() || !Number.isInteger(roundIndex) || !Number.isInteger(stageIndex)) return publish();
    const round = state.session.session?.conversationGroups?.[roundIndex];
    const stages = round && (Array.isArray(round.stages) ? round.stages : [round.request, round.decision, round.submission || round.result, ...(round.extras || [])]);
    const stage = stages?.filter((item) => item && typeof item === 'object')[stageIndex];
    if (!stage) return publish();
    tokens.set('detail', (tokens.get('detail') || 0) + 1);
    state.ui.detailReturnPane = 'conversation';
    state.ui.detailModalOpen = true;
    state.ui.inspectorOpen = true;
    state.detail = { status: 'ready', detail: { format: 'conversation-record', record: clone(stage), round: round.round || roundIndex + 1 }, errors: [] };
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
    state.ui.inspectorOpen = ['evidence', 'tools', 'metadata', 'execution'].includes(state.ui.detailReturnPane);
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
    loadTraces, loadEvidence, launchTask, openLaunchComposer, setLaunchRequirement, setLaunchDeviceOverride, setLaunchDocument, setLaunchDocumentError, closeLaunchComposer, resolveDetail, inspectTrace, inspectConversation, selectPane, selectView, closeInspector, selectTraceMode, selectTraceSource, closeDetail, refresh, subscribe,
    loadTools, setToolsRunDir, generateReport, diagnoseRun,
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
    state.ui.inspectorOpen = false;
    state.ui.detailModalOpen = false;
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
  const toolsState = page(state.tools || { status: 'idle', items: [], errors: [] });

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
      taskStatus: selectedTask && selectedTask.status || null,
      source: options.agentLabel || 'Uni-Agent',
      origin: selectedTask && selectedTask.source || (session && session.source) || null,
      authority: selectedTask && selectedTask.authority || (session && session.authority) || null,
      metadata,
      ...launchProjection(selectedTask, session),
      launch: launchView(state.launch, selectedTask, state.selection && state.selection.projectId, projectsState.launchDefaults),
      errors: errorsOf(state.session)
    },
    conversationTimeline: timelinePane(state.timeline, session),
    tracePane: { ...groupedPane(state.traces, 'traces', (item) => item.source || 'unknown'), mode: state.ui && state.ui.traceMode === 'split' ? 'split' : 'combined', selectedSource: state.ui && state.ui.traceSource || 'all' },
    evidencePane: evidencePane(state.evidence),
    toolsPane: {
      status: toolsState.status,
      items: toolsState.items,
      errors: toolsState.errors,
      runDir: state.ui && state.ui.toolsRunDir || '',
      report: clone(state.report || { status: 'idle', data: null, errors: [] }),
      // PNL-012：诊断入口可用性投影（tools 已加载 + 存在 implemented 的
      // model-procedure 工具 + runDir 非空）；不可用时给 title 说明。
      diagnosis: clone(state.diagnosis || { status: 'idle', data: null, errors: [] }),
      ...diagnosisGate(toolsState, state.ui && state.ui.toolsRunDir)
    },
    executionPane: executionPane((state.session && state.session.session) || null),
    metadataPane: { status: state.session && state.session.status || 'idle', items: metadata, claims: metadataClaims(selectedTask, session), ...launchProjection(selectedTask, session), errors: errorsOf(state.session) },
    launchComposer: {
      ...(state.ui && state.ui.launchComposer ? clone(state.ui.launchComposer) : { open: false, projectId: null, requirement: '', deviceOverrideEnabled: false, deviceOverride: null, requirementDocument: null, documentError: null }),
      defaults: clone(projectsState.launchDefaults || null),
      localConfig: clone(projectsState.localConfig || null)
    },
    activePane: state.ui && state.ui.activePane || 'trace',
    layout: { activeView: state.ui?.activeView === 'trace' ? 'trace' : 'conversation', inspectorOpen: Boolean(state.ui?.inspectorOpen || state.ui?.detailModalOpen) },
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
    projectRef: clone(task.projectRef || null),
    testSetRef: clone(task.testSetRef || null),
    taskRef: clone(task.taskRef || task.taskId || null),
    observed: Boolean(task.observed),
    errors: clone(task.errors || [])
  };
}

function launchView(value, task, projectId, defaults = null) {
  const state = value || {};
  const source = task || {};
  const projectRef = logicalRef(source.projectRef, null) || logicalRef(defaults && defaults.projectRef, null) || logicalRef(projectId, null);
  const testSetRef = logicalTestSetRef(source.testSetRef) || logicalTestSetRef(defaults && defaults.testSetRef);
  const taskRef = logicalRef(source.taskRef || source.taskId, null) || logicalRef(defaults && defaults.taskRef, null);
  return {
    status: state.status || 'idle',
    errors: errorsOf(state),
    enabled: Boolean(projectRef && testSetRef && taskRef),
    projectRef,
    testSetRef,
    taskRef,
    result: clone(state.data || null)
  };
}

function logicalRef(value, fallbackId) {
  const id = typeof value === 'string' ? value : value && value.id;
  if (typeof id !== 'string' || id.length === 0 || id.startsWith('/') || id.startsWith('./') || id.startsWith('../') || id.includes('\\') || id.split('/').includes('..')) return fallbackId ? logicalRef(fallbackId, null) : null;
  return { id, ...(typeof value === 'object' && typeof value.label === 'string' ? { label: value.label } : {}) };
}

function logicalTestSetRef(value) {
  const ref = typeof value === 'string' ? { id: value } : value;
  if (!ref || typeof ref.id !== 'string' || ref.id.length === 0 || ref.version !== 'default') return null;
  return { id: ref.id, version: 'default', ...(typeof ref.sourceRevision === 'string' && ref.sourceRevision.length > 0 ? { sourceRevision: ref.sourceRevision } : {}) };
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
      inspectable: true,
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
  return stages.length > 0 ? [{ groupId: 'round-1', inspectable: false, round: 1, status: 'legacy', stages }] : [];
}

function normalizeConversationStage(stage) {
  if (!stage || typeof stage !== 'object') return null;
  const kind = stage.kind || stage.role || 'result';
  const result = stage.result && typeof stage.result === 'object' ? stage.result : null;
  return {
    kind,
    role: normalizeRole(stage.role, kind),
    label: stage.label || labelForKind(kind),
    // A decision explains the choice; its nested result only acknowledges
    // submission and is rendered in the separate result stage.
    text: stage.objective || stage.summary || stage.text || (kind === 'decision'
      ? stage.justification
      : (result && result.text) || stage.justification) || '',
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
  const items = pane.items.map((item, index) => {
    const source = groupBy(item);
    const summary = item.summary || item.label || item.text || item.definition || item.type || item.kind || item.spanId || `${key} ${index + 1}`;
    if (!summary) return null;
    return {
      recordIndex: index,
      id: item.spanId || item.id || null,
      source,
      type: item.type || item.kind || null,
      label: item.label || null,
      summary: String(summary).split('\n')[0].slice(0, 240),
      text: item.text || null,
      ts: item.ts || null,
      seq: item.seq == null ? null : item.seq,
      traceId: item.traceId || null,
      spanKind: item.spanKind || null,
      status: item.status || null,
      structuralOutcome: item.structuralOutcome || null,
      startTime: item.startTime || null,
      endTime: item.endTime || null,
      durationMs: Number.isFinite(item.durationMs) ? item.durationMs : null,
      attributes: item.attributes || null,
      resource: item.resource || null,
      links: Array.isArray(item.links) ? item.links : [],
      events: Array.isArray(item.events) ? item.events : [],
      authority: item.authority || null,
      correlationId: item.correlationId || null,
      parentSpanId: item.parentSpanId || null,
      captureSequence: item.captureSequence == null ? null : item.captureSequence,
      productSessionId: item.productSessionId || null,
      detailRef: item.detailRef || null,
      detailAvailable: Boolean(item.detailRef),
      raw: clone(item)
    };
  }).filter(Boolean);
  items.forEach((item) => {
    if (!groups[item.source]) groups[item.source] = [];
    groups[item.source].push(item);
  });
  const sourceCounts = Object.fromEntries(Object.entries(groups).map(([source, group]) => [source, group.length]));
  return { status: pane.status, items, groups, sourceCounts, context: pane.context || null, errors: pane.errors, snapshotId: pane.snapshotId, revision: pane.revision, observedAt: pane.observedAt, itemKey: key };
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

// PNL-012：诊断入口门控投影。available 仅在 tools 已加载、存在 implemented 的
// model-procedure 工具且 runDir 非空时为 true；否则 unavailableReason 说明原因。
function diagnosisGate(toolsState, runDir) {
  const items = Array.isArray(toolsState.items) ? toolsState.items : [];
  const hasProcedureTool = items.some((tool) => tool && tool.status === 'implemented' && tool.invocation === 'model-procedure');
  const hasRunDir = typeof runDir === 'string' && runDir.trim().length > 0;
  const available = toolsState.status === 'ready' && hasProcedureTool && hasRunDir;
  const unavailableReason = toolsState.status !== 'ready'
    ? '工具清单未加载，无法确认诊断能力'
    : !hasProcedureTool
      ? '清单中没有已实现（implemented）的 model-procedure 诊断工具'
      : !hasRunDir ? '请先在工具面板填写 Run 目录' : '';
  return { diagnosisAvailable: available, diagnosisUnavailableReason: unavailableReason };
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
  return { status: detail.status || 'idle', enabled: Boolean(productSessionId), current: clone(detail.detail || null), errors: errorsOf(detail), resolve: detail.status === 'loading' ? 'loading' : 'available', modalOpen: Boolean(state.ui && state.ui.detailModalOpen), returnPane: ['conversation', 'trace', 'evidence', 'tools', 'metadata', 'execution'].includes(state.ui && state.ui.detailReturnPane) ? state.ui.detailReturnPane : 'trace' };
}

function mergeMetadata(task, session) {
  const out = {};
  [task && task.metadata, session && session.metadata].forEach((metadata) => {
    if (metadata && typeof metadata === 'object' && !Array.isArray(metadata)) Object.assign(out, clone(metadata));
  });
  return out;
}

function metadataClaims(task, session) {
  const claims = [];
  [task && task.metadataClaims, session && session.metadataClaims].forEach((value) => {
    if (Array.isArray(value)) value.forEach((claim) => { if (claim && typeof claim === 'object' && claim.key) claims.push(clone(claim)); });
  });
  return claims;
}

function launchProjection(task, session) {
  const value = session || task || {};
  const logicalRefs = {};
  ['projectRef', 'testSetRef', 'taskRef', 'storageNamespaceRef'].forEach((key) => {
    if (value[key] !== undefined && value[key] !== null) logicalRefs[key] = clone(value[key]);
  });
  return {
    runId: value.runId || null,
    launchStages: Array.isArray(value.launchStages) ? clone(value.launchStages) : [],
    logicalRefs
  };
}

function collectNotices(state, selectedTask) {
  const notices = [];
  [state.projects, state.selection && state.selection.taskInstances, state.session, state.timeline, state.traces, state.evidence, state.detail, state.launch]
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

function page(value) { return { status: value && value.status || 'idle', items: Array.isArray(value && value.items) ? value.items : [], context: value && value.context || null, launchDefaults: value && value.launchDefaults || null, localConfig: value && value.localConfig || null, errors: errorsOf(value), snapshotId: value && value.snapshotId, revision: value && value.revision, observedAt: value && value.observedAt }; }
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
function statusLabel(value) { return ({ running: '进行中', completed: '已完成', Completed: '已完成', submitted: '已提交', ready: '就绪', pending: '等待中', loading: '读取中', idle: '未选择', error: '异常', failed: '失败', partial: '部分可用', active: '进行中', implemented: '已接入', planned: '规划中', rejected: '已拒绝' })[value] || value; }
function status(value) { return `<span class="workspace-status workspace-status--${escapeHtml(value || 'unknown')}">${text(statusLabel(value), '未知')}</span>`; }
function errorText(errors) { return (errors || []).map((error) => `<li class="workspace-notice__item">${text(error.message || error.code || 'Workspace error')}</li>`).join(''); }

function renderWorkspaceHtml(viewModel = {}, options = {}) {
  const vm = viewModel || {};
  const nav = vm.navigation || {};
  const header = vm.taskHeader || {};
  const hasSelectedTask = Boolean(header.productSessionId);
  const activeView = vm.layout?.activeView || 'conversation';
  const inspectorOpen = Boolean(vm.layout?.inspectorOpen);
  const activePane = vm.activePane || 'metadata';
  const renderedTaskKeys = new Set();
  const projectsHtml = (nav.projects || []).map((project) => renderProject(project, renderedTaskKeys, nav.selectedProductSessionId)).join('');
  const taskHtml = (nav.taskInstances || []).filter((task) => {
    const key = taskIdentity(task);
    if (key && renderedTaskKeys.has(key)) return false;
    if (key) renderedTaskKeys.add(key);
    return true;
  }).map((task) => renderTask(task, nav.selectedProductSessionId)).join('');
  const root = `workspace workspace--${escapeHtml(vm.status || 'idle')}${inspectorOpen ? ' workspace--inspector-open' : ''}`;
  const detail = vm.detailActions || {};
  const inspectorTitle = detail.modalOpen ? (detail.current?.format === 'conversation-record' ? '对话详情' : '记录详情')
    : { evidence: '证据', metadata: '任务概览', execution: '执行结果', tools: '工具' }[activePane] || '任务详情';
  return `<div class="${root}" data-workspace-status="${escapeHtml(vm.status || 'idle')}" data-main-view="${activeView}">
    <button type="button" class="workspace-navigation-backdrop" data-workspace-action="toggle-navigation" aria-label="关闭任务导航" tabindex="-1"></button>
    <nav class="workspace-navigation" aria-label="${text(options.navigationLabel, '任务导航')}">
      <div class="workspace-navigation__header"><div><p class="workspace-eyebrow">工作台</p><h1 class="workspace-navigation__title">${text(options.title, 'UniAgent')}</h1></div><button type="button" class="workspace-navigation__toggle" data-workspace-action="toggle-navigation" aria-expanded="true" aria-controls="workspace-navigation-content">隐藏任务栏</button></div>
      <div class="workspace-navigation__body" id="workspace-navigation-content"><div class="workspace-navigation__projects">${projectsHtml}</div><div class="workspace-navigation__tasks" data-selected-product-session-id="${escapeHtml(nav.selectedProductSessionId || '')}">${taskHtml}</div></div>
      <div class="workspace-navigation__resize" role="separator" aria-orientation="vertical" aria-label="调整任务栏宽度" data-workspace-resize="navigation" tabindex="0"></div>
    </nav>
    <main class="workspace-main">
      <header class="workspace-task-header" data-selected-product-session-id="${escapeHtml(header.productSessionId || '')}">
        <button type="button" class="workspace-mobile-nav-toggle" data-workspace-action="toggle-navigation" aria-label="打开任务导航" aria-controls="workspace-navigation-content">☰</button>
        <div class="workspace-task-header__identity"><p class="workspace-eyebrow">${text(header.source, 'Uni-Agent')}<span class="workspace-header-origin">${text(header.projectId, '')}</span></p><h2 title="${text(header.title)}">${text(header.title, '选择一个任务')}</h2></div>
        <div class="workspace-task-header__meta">${status(header.taskStatus || header.status)}</div>
        <div class="workspace-header-actions">${hasSelectedTask ? `<button type="button" class="workspace-launch-action" data-workspace-action="launch-task"${header.launch?.enabled && header.launch.status !== 'loading' ? '' : ' disabled aria-disabled="true"'}>${header.launch?.status === 'loading' ? '发起中…' : '发起任务'}</button>` : ''}<button type="button" class="workspace-refresh-action" data-workspace-action="refresh">刷新</button></div>
      </header>
      ${renderNotices(vm.notices)}
      <div class="workspace-stage">
        <section class="workspace-primary" aria-label="任务过程">
          <div class="workspace-observation-bar">
            <div class="workspace-pane-tabs" role="tablist" aria-label="主视图">${renderViewButton('conversation', '解决过程', activeView)}${renderViewButton('trace', 'Trace', activeView)}</div>
            <div class="workspace-inspector-actions" role="group" aria-label="任务信息">${renderInspectorButton('evidence', '证据', activePane, inspectorOpen)}${renderInspectorButton('metadata', '概览', activePane, inspectorOpen)}${renderInspectorButton('tools', '工具', activePane, inspectorOpen)}</div>
          </div>
          <section class="workspace-view workspace-conversation" id="workspace-view-conversation" role="tabpanel" aria-labelledby="workspace-tab-conversation" data-view-panel="conversation"${activeView === 'conversation' ? '' : ' hidden'}>
            <div class="workspace-view-heading"><div><h3 id="conversation-title">Uni-Agent 解决过程</h3><p>任务的每一次选择与结果，都在这里。</p></div><span class="workspace-view-count">${(vm.conversationTimeline?.rounds || []).length} 轮记录</span></div>
            <div class="workspace-conversation-shell" data-workspace-scroll="conversation" tabindex="0" aria-label="对话记录">${renderTimeline(vm.conversationTimeline)}</div>
            <button type="button" class="workspace-new-content" data-workspace-action="reading-latest" hidden>有新内容 · 回到最新 ↓</button>
          </section>
          <section class="workspace-view workspace-view--trace" id="workspace-view-trace" role="tabpanel" aria-labelledby="workspace-tab-trace" data-view-panel="trace" data-workspace-scroll="trace" tabindex="0"${activeView === 'trace' ? '' : ' hidden'}>${renderTracePane(vm.tracePane)}</section>
        </section>
        <button type="button" class="workspace-inspector-backdrop" data-workspace-action="close-inspector" aria-label="关闭详情面板" tabindex="-1"${inspectorOpen ? '' : ' hidden'}></button>
        <aside class="workspace-inspector workspace-detail-panel" id="workspace-inspector" aria-label="任务详情"${inspectorOpen ? '' : ' hidden'}>
          <header class="workspace-inspector-header"><div><p class="workspace-eyebrow">${detail.modalOpen ? '所选记录' : '任务信息'}</p><h3 tabindex="-1" data-inspector-focus>${text(inspectorTitle)}</h3></div><button type="button" class="workspace-detail-modal__close" data-workspace-action="close-inspector" aria-label="关闭详情面板">×</button></header>
          <div class="workspace-inspector__body" data-workspace-scroll="inspector">
            ${detail.modalOpen ? renderDetailPanel(detail) : `<div class="workspace-tab-panels">${renderTabPanel('evidence', activePane, renderEvidencePane(vm.evidencePane))}${renderTabPanel('metadata', activePane, renderExecutionPane(vm.executionPane) + renderMetadataPane(vm.metadataPane))}${renderTabPanel('execution', activePane, renderExecutionPane(vm.executionPane))}${renderTabPanel('tools', activePane, `<div class="workspace-diagnostics-slot"><button type="button" class="workspace-diagnostics-action" data-workspace-action="diagnose-task"${vm.toolsPane?.diagnosisAvailable ? '' : ' disabled aria-disabled="true"'} title="${text(vm.toolsPane?.diagnosisAvailable ? '诊断当前任务（非权威观察）' : vm.toolsPane?.diagnosisUnavailableReason || '诊断不可用')}">诊断当前任务</button></div>` + renderToolsPane(vm.toolsPane))}</div>`}
          </div>
        </aside>
      </div>
    </main>
  </div>${renderLaunchComposer(vm.launchComposer)}`;
}
function renderViewButton(id, label, active) { return `<button type="button" id="workspace-tab-${id}" role="tab" aria-selected="${active === id}" aria-controls="workspace-view-${id}" tabindex="${active === id ? '0' : '-1'}" class="workspace-pane-tab${active === id ? ' is-active' : ''}" data-workspace-action="select-view" data-view="${id}">${label}</button>`; }
function renderInspectorButton(id, label, active, open) { return `<button type="button" class="workspace-inspector-action${open && active === id ? ' is-active' : ''}" data-workspace-action="select-pane" data-pane-tab="${id}" aria-pressed="${open && active === id}" aria-controls="workspace-inspector">${label}</button>`; }

function renderProject(project, renderedTaskKeys, selectedProductSessionId) {
  const tasks = (project.tasks || []).filter((task) => {
    const key = taskIdentity(task);
    if (!key) return true;
    if (renderedTaskKeys.has(key)) return false;
    renderedTaskKeys.add(key);
    return true;
  });
  return `<section class="workspace-project" data-project-id="${escapeHtml(project.projectId || '')}"><div class="workspace-project__header"><button type="button" class="workspace-project__action" data-workspace-action="select-project" data-project-id="${escapeHtml(project.projectId || '')}">${text(project.name, '未命名项目')}</button><button type="button" class="workspace-project__launch" data-workspace-action="launch-project" data-project-id="${escapeHtml(project.projectId || '')}" aria-label="在项目 ${escapeHtml(project.name || '未命名项目')}中发起任务" title="发起任务">+</button></div>${tasks.map((task) => renderTask(task, selectedProductSessionId)).join('')}</section>`;
}
function taskIdentity(task = {}) { return task.productSessionId || task.id || null; }
function renderTask(task, selectedProductSessionId = null) {
  const orphan = task.correlationStatus === 'uncorrelated' || !task.productSessionId;
  const selected = !orphan && selectedProductSessionId && selectedProductSessionId === task.productSessionId;
  return `<article class="workspace-task-card${orphan ? ' is-uncorrelated' : ''}${selected ? ' is-selected' : ''}" data-task-id="${escapeHtml(task.id || '')}" data-correlation-status="${escapeHtml(task.correlationStatus || 'unknown')}"><button type="button" class="workspace-task-card__action" data-workspace-action="select-task" data-product-session-id="${escapeHtml(task.productSessionId || '')}"${orphan ? ' disabled aria-disabled="true"' : ''}${selected ? ' aria-current="true"' : ''}><h3>${text(task.title, '未命名任务')}</h3><div class="workspace-task-card__meta"><span>${text(task.productSessionId, orphan ? '未关联 ProductSession' : '未知 ProductSession')}</span>${status(task.status)}</div></button></article>`;
}

function renderLaunchComposer(composer = {}) {
  if (composer.open !== true) return '';
  const defaults = composer.defaults || {};
  const requirement = composer.requirement || '';
  const project = defaults.projectRef?.label || defaults.projectRef?.id || '本地默认项目';
  const testSet = defaults.testSetRef?.id || '本地默认测试集';
  const task = defaults.taskRef?.label || defaults.taskRef?.id || '本地默认任务';
  const localConfig = composer.localConfig || {};
  const defaultDevice = localConfig.device || '';
  const deviceOptions = [...new Set([defaultDevice, ...(Array.isArray(localConfig.deviceOptions) ? localConfig.deviceOptions : [])].filter(Boolean))];
  const deviceOverrideEnabled = composer.deviceOverrideEnabled === true;
  const selectedDevice = composer.deviceOverride || defaultDevice;
  const requirementDocument = composer.requirementDocument || null;
  const disabled = requirement.trim().length === 0 || !defaults.projectRef || !defaults.testSetRef || !defaults.taskRef;
  const configRows = [
    ['机器', localConfig.machine], ['宿主版本', localConfig[`${'h'}ostVersion`]], ['连接地址', localConfig[['h', 'ostEndpoint'].join('')]],
    ['认证方式', localConfig.authMode], ['认证状态', localConfig.authStatus], ['Node 版本', localConfig.nodeVersion],
    ['Workspace 版本', localConfig.workspaceVersion], ['Agent preset', localConfig.agentPreset], ['设备', localConfig.device]
  ].filter(([, value]) => value !== undefined && value !== null && value !== '').map(([label, value]) => `<div><dt>${text(label)}</dt><dd>${text(value)}</dd></div>`).join('');
  const deviceControl = deviceOptions.length ? `<div class="workspace-launch-composer__override"><label class="workspace-launch-composer__check"><input type="checkbox" data-workspace-launch-device-override${deviceOverrideEnabled ? ' checked' : ''}>本次任务修改设备</label><select data-workspace-launch-device${deviceOverrideEnabled ? '' : ' disabled aria-disabled="true"'} aria-label="本次任务设备">${deviceOptions.map((option) => `<option value="${escapeHtml(option)}"${option === selectedDevice ? ' selected' : ''}>${text(option)}</option>`).join('')}</select><small>默认使用 ${text(defaultDevice || '本地配置')}</small></div>` : '';
  const documentControl = `<div class="workspace-launch-composer__document"><label for="workspace-launch-document">需求文档（可选）</label><input id="workspace-launch-document" type="file" data-workspace-launch-document accept=".md,.txt,.json,.yaml,.yml,text/plain,text/markdown,application/json"><small>支持 Markdown、TXT、JSON、YAML，单个文件不超过 512 KB。</small>${requirementDocument ? `<span class="workspace-launch-composer__file">已选择：${text(requirementDocument['name'])}（${text(requirementDocument['sizeBytes'])} B）</span>` : ''}${composer.documentError ? `<span class="workspace-error">${text(composer.documentError)}</span>` : ''}</div>`;
  return `<div class="workspace-launch-composer" role="dialog" aria-modal="true" aria-labelledby="workspace-launch-title"><div class="workspace-launch-composer__backdrop" data-workspace-action="close-launch-composer"></div><form class="workspace-launch-composer__dialog" data-workspace-launch-form><header><div><p class="workspace-eyebrow">UNI-AGENT</p><h3 id="workspace-launch-title">发起任务</h3><p>只填写这次任务需求，其他信息跟随本地默认配置。</p></div><button type="button" class="workspace-detail-modal__close" data-workspace-action="close-launch-composer" aria-label="关闭">×</button></header><div class="workspace-launch-composer__body"><label for="workspace-launch-requirement">任务需求</label><textarea id="workspace-launch-requirement" data-workspace-launch-requirement rows="5" placeholder="例如：确认 Android 设置中的 Wi‑Fi 状态并保留当前状态">${text(requirement)}</textarea>${deviceControl}${documentControl}<div class="workspace-launch-composer__defaults"><strong>本地默认配置</strong><span>${text(project)}</span><span>${text(testSet)}</span><span>${text(task)}</span></div>${configRows ? `<div class="workspace-launch-composer__machine"><strong>本地运行配置</strong><dl>${configRows}</dl></div>` : ''}</div><footer><button type="button" class="workspace-refresh-action" data-workspace-action="close-launch-composer">取消</button><button type="button" class="workspace-launch-action" data-workspace-action="submit-launch"${disabled ? ' disabled aria-disabled="true"' : ''}>发起</button></footer></form></div>`;
}
function renderTimeline(pane = {}) {
  const rounds = Array.isArray(pane.rounds) ? pane.rounds : [];
  const roundHtml = rounds.map(renderConversationRound).join('');
  const runStageHtml = (pane.runStages || []).map((stage, index) => renderConversationStage(stage, null, `run:${index}`)).join('');
  return `<div class="workspace-timeline workspace-timeline--conversation" data-status="${escapeHtml(pane.status || 'idle')}" data-conversation-mode="${escapeHtml(pane.mode || 'timeline')}">${roundHtml}${runStageHtml ? `<section class="workspace-conversation-results"><h4>执行与验证</h4>${runStageHtml}</section>` : ''}${roundHtml || runStageHtml ? '' : '<p class="workspace-empty">暂无 Uni-Agent 对话记录</p>'}</div>`;
}
function renderConversationRound(round = {}, roundIndex = 0) { return `<section class="workspace-conversation-round" data-round="${escapeHtml(round.round || '')}" data-status="${escapeHtml(round.status || '')}"><header class="workspace-conversation-round__header"><span>第 ${escapeHtml(round.round || '')} 轮</span>${status(round.status || 'pending')}</header><div class="workspace-dialogue">${(round.stages || []).map((stage, index) => renderConversationStage(stage, round.inspectable === false ? null : { roundIndex, stageIndex: index, groupId: round.groupId }, `round:${roundIndex}:${index}`)).join('')}</div></section>`; }
function workspaceIcon(kind) {
  const paths = {
    agent: '<path d="m12 3 2.6 6.4L21 12l-6.4 2.6L12 21l-2.6-6.4L3 12l6.4-2.6Z"/>',
    requester: '<rect x="4" y="5" width="16" height="12" rx="3"/><path d="m8 17-2 4m3-12h6m-6 4h4"/>',
    tool: '<path d="m8 7-5 5 5 5m8-10 5 5-5 5m-3-13-2 20"/>',
    receipt: '<path d="M7 3h10v18l-3-2-2 2-2-2-3 2V3Zm3 5h4m-4 4h4"/>',
    system: '<circle cx="12" cy="12" r="9"/><path d="M12 8v5m0 3h.01"/>'
  };
  return `<svg class="workspace-icon" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">${paths[kind] || paths.system}</svg>`;
}
function conversationInspect(stage, context) {
  return `${context ? `<button type="button" class="workspace-message-inspect" data-workspace-action="inspect-conversation" data-round-index="${context.roundIndex}" data-stage-index="${context.stageIndex}">${stage.kind === 'decision' ? '查看依据' : '查看记录'} ↗</button>` : ''}${stage.detailRef ? `<button type="button" class="workspace-detail-action workspace-detail-action--small" data-workspace-action="resolve-detail" data-detail-ref="${escapeHtml(stage.detailRef.refId || stage.detailRef)}">查看明细</button>` : ''}`;
}
// Read-only presentation of legacy action proposals; no execution status is inferred.
function renderConversationStep(step, key) {
  if (!step || typeof step !== 'object') return `<li><div class="workspace-step-content">${text(step, '未提供步骤内容')}</div></li>`;
  const supplied = [step.label, step.description].find((value) => typeof value === 'string' && value.trim());
  const action = typeof step.effectClass === 'string' ? (step.effectClass === 'tap' ? '点击' : step.effectClass) : '';
  const target = typeof step.targetDescriptor === 'string' && step.targetDescriptor.trim()
    ? step.targetDescriptor : typeof step.targetRole === 'string' ? `目标角色：${step.targetRole}` : '';
  const summary = supplied || [action, target].filter(Boolean).join(' · ') || '结构化步骤';
  const desired = typeof step.desiredState === 'string' && step.desiredState ? `<span class="workspace-step-state">目标状态：${text(step.desiredState)}</span>` : '';
  const disclosureKey = `step:${key}`;
  return `<li><div class="workspace-step-content"><span class="workspace-step-summary">${text(summary)}</span>${desired}<details class="workspace-step-detail" data-reading-disclosure="${escapeHtml(disclosureKey)}"><summary id="workspace-${escapeHtml(disclosureKey)}">完整参数</summary><pre>${text(JSON.stringify(step, null, 2))}</pre></details></div></li>`;
}
function renderConversationStage(stage = {}, context = null, readingKey = null) {
  const body = stage.objective || stage.text || stage.justification || '';
  const key = context ? `${context.groupId || context.roundIndex}:${stage.seq ?? context.stageIndex}` : readingKey;
  const error = stage.isError || ['error', 'failed', 'rejected'].includes(stage.status);
  const attributes = `data-kind="${escapeHtml(stage.kind || '')}"${key ? ` data-reading-anchor="${escapeHtml(key)}"` : ''}`;
  const meta = [stage.ts, stage.seq != null ? `#${stage.seq}` : ''].filter(Boolean).join(' · ');
  if (stage.role === 'tool') {
    const structured = /^[{[]/.test(String(body).trim());
    const detailed = structured || String(body).length > 180;
    const summary = body && !error ? (detailed ? (structured ? '结构化内容 · 展开查看' : '完整输出 · 展开查看') : body) : '';
    const disclosureKey = `tool:${key || `${stage.kind}:${stage.seq ?? stage.ts}`}`;
    return `<article class="workspace-message workspace-message--tool workspace-tool-card${error ? ' is-error' : ''}" ${attributes}><details data-reading-disclosure="${escapeHtml(disclosureKey)}"><summary id="workspace-${escapeHtml(disclosureKey)}"><span class="workspace-tool-card__head">${workspaceIcon(stage.kind === 'result' || stage.kind === 'submission' ? 'receipt' : 'tool')}<strong>${text(stage.label, '工具记录')}</strong>${stage.status || error ? status(stage.status || 'error') : ''}<span class="workspace-tool-card__expand">详情 <span aria-hidden="true">⌄</span></span></span>${summary ? `<span class="workspace-tool-card__summary">${text(summary)}</span>` : ''}</summary><div class="workspace-tool-card__detail">${detailed && !error ? `<pre>${text(body)}</pre>` : ''}${meta ? `<p class="workspace-tool-card__meta">${text(meta)}</p>` : ''}${conversationInspect(stage, context)}</div></details>${error && body ? `<p class="workspace-tool-card__error" role="note">${text(body)}</p>` : ''}</article>`;
  }
  const steps = Array.isArray(stage.steps) && stage.steps.length ? `<div class="workspace-message__plan"><span>步骤</span><ol class="workspace-message__steps">${stage.steps.map((step, index) => renderConversationStep(step, `${key || 'stage'}:${index}`)).join('')}</ol></div>` : '';
  return `<article class="workspace-message workspace-message--${escapeHtml(stage.role || 'system')}${error ? ' is-error' : ''}" ${attributes}><div class="workspace-message__avatar">${workspaceIcon(stage.role)}</div><div class="workspace-message__body"><div class="workspace-message__head"><strong>${text(stage.label, 'Uni-Agent')}</strong>${stage.decisionKind ? `<span class="workspace-chip">${text(stage.decisionKind)}</span>` : ''}${stage.status ? status(stage.status) : ''}${meta ? `<small>${text(meta)}</small>` : ''}</div>${body ? `<p>${text(body)}</p>` : ''}${steps}${conversationInspect(stage, context)}</div></article>`;
}
function renderTabButton(id, label, active) { return `<button type="button" role="tab" class="workspace-pane-tab${active === id ? ' is-active' : ''}" aria-selected="${active === id ? 'true' : 'false'}" data-workspace-action="select-pane" data-pane-tab="${id}">${text(label)}</button>`; }
function renderTabPanel(id, active, content) { return `<div class="workspace-tab-panel${active === id ? ' is-active' : ''}" role="region" aria-label="${text(id)}" data-pane-panel="${id}"${active === id ? '' : ' hidden'}>${content}</div>`; }
function renderTracePane(pane = {}) {
  const items = Array.isArray(pane.items) ? pane.items : Object.values(pane.groups || {}).flat();
  const mode = pane.mode === 'split' ? 'split' : 'combined';
  const selectedSource = pane.selectedSource || 'all';
  const grouped = Object.entries(pane.groups || {}).filter(([, group]) => Array.isArray(group) && group.length);
  const counts = items.reduce((result, item) => { const source = item.source || 'unknown'; result[source] = (result[source] || 0) + 1; return result; }, {});
  const legend = Object.entries(counts).map(([source, count]) => `<span class="workspace-trace-source workspace-trace-source--${escapeHtml(source)}">${text(traceSourceLabel(source))}<b>${count}</b></span>`).join('');
  const sourceButtons = [`<button type="button" class="workspace-trace-source-switch__button${selectedSource === 'all' ? ' is-active' : ''}" data-workspace-action="select-trace-source" data-trace-source="all" aria-pressed="${selectedSource === 'all' ? 'true' : 'false'}">全部 <b>${items.length}</b></button>`]
    .concat(grouped.map(([source, sourceItems]) => `<button type="button" class="workspace-trace-source-switch__button${selectedSource === source ? ' is-active' : ''}" data-workspace-action="select-trace-source" data-trace-source="${escapeHtml(source)}" aria-pressed="${selectedSource === source ? 'true' : 'false'}">${text(traceSourceLabel(source))} <b>${sourceItems.length}</b></button>`)).join('');
  let body = '';
  if (selectedSource !== 'all') {
    const sourceItems = (pane.groups && pane.groups[selectedSource]) || [];
    body = sourceItems.length ? renderTraceGroup(selectedSource, sourceItems, false, true) : '<p class="workspace-empty">该来源暂无 Trace 事件</p>';
  } else if (mode === 'split') {
    body = grouped.map(([source, sourceItems]) => renderTraceGroup(source, sourceItems, false, false)).join('');
  } else {
    body = renderTraceGroup('all', items, true, false);
  }
  return `<section class="workspace-pane workspace-pane--trace" data-pane="trace"><div class="workspace-trace-toolbar"><div class="workspace-pane__heading"><div><h3>Trace</h3><p class="workspace-pane__subheading">查看运行层级，选择节点查看属性、事件与关联信息</p></div><span class="workspace-pane__count">${items.length}</span></div><div class="workspace-trace-mode" role="group" aria-label="Trace 显示方式">${renderTraceModeButton('combined', '合并', mode)}${renderTraceModeButton('split', '按来源', mode)}</div></div>${renderTraceContext(pane.context, pane.context?.source ? (pane.groups?.[pane.context.source] || []).length : items.length)}${items.length ? `<div class="workspace-trace-source-switch" role="tablist" aria-label="Trace 来源">${sourceButtons}</div><div class="workspace-trace-legend workspace-trace-legend--standalone">${legend}</div><div class="workspace-trace-viewer">${body}</div>` : '<p class="workspace-empty">暂无可展示的 Trace 事件</p>'}</section>`;
}
function renderTraceContext(context = {}, visibleCount = null) {
  context = context || {};
  const spanCount = context.spanCount != null && visibleCount != null ? `${visibleCount}/${context.spanCount}` : context.spanCount;
  const fields = [['来源', context.source && traceSourceLabel(context.source)], ['traceId', context.traceId], ['rootSpanId', context.rootSpanId], ['runId', context.runId], ['已展示/总 spans', spanCount]]
    .filter(([, value]) => value !== undefined && value !== null && value !== '')
    .map(([key, value]) => `<span class="workspace-trace-context__field"><b>${text(key)}</b>${text(value)}</span>`).join('');
  return fields ? `<div class="workspace-trace-context" aria-label="Trace context"><span class="workspace-trace-context__title">Trace context</span>${fields}</div>` : '';
}
function traceSourceLabel(source) { return source === 'uniclaw' ? 'UniClaw' : source === 'uniflow' ? 'UniFlow' : String(source).toUpperCase(); }
function renderTraceModeButton(mode, label, active) { return `<button type="button" class="workspace-trace-mode__button${active === mode ? ' is-active' : ''}" data-workspace-action="select-trace-mode" data-trace-mode="${mode}" aria-pressed="${active === mode ? 'true' : 'false'}">${text(label)}</button>`; }
function renderTraceGroup(source, items, combined, selected) {
  const ordered = items.map((item, index) => ({ item, index }));
  const children = new Map();
  const key = (item, id) => `${item.source || 'unknown'}:${id}`;
  const byId = new Map(ordered.filter(({ item }) => item.id).map(({ item }) => [key(item, item.id), item]));
  const roots = [];
  for (const { item } of ordered) {
    const parent = item.parentSpanId && byId.get(key(item, item.parentSpanId));
    const visited = new Set([item]);
    let ancestor = parent;
    while (ancestor && !visited.has(ancestor)) { visited.add(ancestor); ancestor = ancestor.parentSpanId && byId.get(key(ancestor, ancestor.parentSpanId)); }
    if (!parent || ancestor) { roots.push(item); continue; }
    if (!children.has(parent)) children.set(parent, []);
    children.get(parent).push(item);
  }
  const className = combined ? 'workspace-trace-root' : 'workspace-trace-source-group';
  const title = combined ? '运行轨迹 · 全部来源' : traceSourceLabel(source);
  const open = selected || (combined && items.length < 20) ? ' open' : '';
  return `<details class="${className}" data-reading-disclosure="${escapeHtml(`trace-group:${source}`)}"${open}><summary><span>${text(title)}</span>${combined ? `<span class="workspace-trace-legend">${text(items.length)} 条</span>` : `<span class="workspace-trace-source-count">${text(items.length)} 条</span>`}</summary><ol class="workspace-trace-tree" role="tree">${roots.map((item) => renderTraceCard(item, children, 0)).join('')}</ol></details>`;
}
function renderTraceCard(item, children, depth) {
  const source = item.source || 'unknown';
  const nested = children.get(item) || [];
  const label = item.label || String(item.summary || '未命名事件').split(' · ')[0];
  const fileDetail = item.detailAvailable && item.detailRef ? `<button type="button" class="workspace-trace-file" data-workspace-action="resolve-detail" data-detail-ref="${escapeHtml(item.detailRef.refId || item.detailRef)}">原始文件</button>` : '';
  const detail = `<div class="workspace-trace-actions"><button type="button" class="workspace-detail-action" data-workspace-action="inspect-trace" data-trace-index="${item.recordIndex}">查看节点</button>${fileDetail}</div>`;
  const status = item.structuralOutcome || item.status || null;
  const statusClass = status && /fault|error|cancel|fail/i.test(String(status)) ? ' workspace-trace-status--error' : '';
  const timing = [item.type, item.ts, formatSpanDuration(item)].filter(Boolean).join(' · ');
  const facts = [['span', item.id], ['parent', item.parentSpanId], ['kind', item.spanKind], ['authority', item.authority], ['correlation', item.correlationId], ['events', Array.isArray(item.events) && item.events.length ? item.events.length : null], ['links', Array.isArray(item.links) && item.links.length ? item.links.length : null], ['attrs', item.attributes && typeof item.attributes === 'object' ? Object.keys(item.attributes).length : null], ['capture', item.captureSequence != null ? `#${item.captureSequence}` : item.seq != null ? `#${item.seq}` : null]].filter(([, value]) => value != null && value !== '').map(([key, value]) => `<span><b>${text(key)}</b>${text(value)}</span>`).join('');
  const content = `<div class="workspace-trace-row__content"><div class="workspace-trace-row__head"><span class="workspace-trace-source workspace-trace-source--${escapeHtml(source)}">${text(traceSourceLabel(source))}</span><strong>${text(label)}</strong>${status ? `<span class="workspace-trace-status${statusClass}">${text(status)}</span>` : ''}<small>${text(timing)}</small></div>${item.text && item.text !== label ? `<p>${text(String(item.text).slice(0, 280))}</p>` : ''}${facts ? `<div class="workspace-trace-row__facts">${facts}</div>` : ''}</div>`;
  return `<li class="workspace-trace-node" role="treeitem" aria-level="${depth + 1}">${nested.length ? `<details class="workspace-trace-branch" data-reading-disclosure="${escapeHtml(`trace:${source}:${item.id || item.recordIndex}`)}"><summary class="workspace-trace-row">${content}<span class="workspace-trace-child-count">${nested.length}</span></summary>${detail}<ol role="group">${nested.map((child) => renderTraceCard(child, children, depth + 1)).join('')}</ol></details>` : `<div class="workspace-trace-row"><span class="workspace-trace-row__rail" aria-hidden="true"></span>${content}${detail}</div>`}</li>`;
}
function formatSpanDuration(item = {}) {
  if (!Number.isFinite(item.durationMs)) return '';
  return item.durationMs < 1000 ? `${item.durationMs}ms` : `${(item.durationMs / 1000).toFixed(2)}s`;
}

function renderEvidencePane(pane = {}) {
  return `<section class="workspace-pane workspace-pane--evidence" data-pane="evidence"><h3>Evidence</h3>${(pane.items || []).map((item) => { const action = item.detailAction || {}; return `<article class="workspace-evidence-card" data-correlation-status="${escapeHtml(item.correlationStatus || '')}"><h4>${text(item.title, '未命名证据')}</h4><p>${text(item.source, '未知来源')}</p>${action.enabled && action.detailRef ? `<button type="button" class="workspace-detail-action" data-workspace-action="resolve-detail" data-detail-ref="${escapeHtml(action.detailRef.refId || action.detailRef)}">查看明细</button>` : `<button type="button" class="workspace-detail-action" disabled aria-disabled="true">${text(action.reason, '详情不可用')}</button>`}${item.error ? `<p class="workspace-error">${text(item.error.message || item.error.code)}</p>` : ''}</article>`; }).join('')}</section>`;
}
function renderToolsPane(pane = {}) {
  const rows = (pane.items || []).map((tool, index) => `<details class="workspace-tool-catalog__card" data-reading-disclosure="${escapeHtml(`catalog:${index}:${tool.name}`)}"><summary id="workspace-catalog-${index}"><span class="workspace-tool-catalog__head">${workspaceIcon('tool')}<code>${text(tool.name)}</code>${status(tool.status)}<span class="workspace-tool-catalog__chevron" aria-hidden="true">⌄</span></span>${tool.summary ? `<span class="workspace-tool-catalog__summary">${text(tool.summary)}</span>` : ''}</summary><dl><div><dt>调用形态</dt><dd>${text(tool.invocation, '未提供')}</dd></div><div><dt>姿态</dt><dd>${text(tool.posture, '未提供')}</dd></div></dl></details>`).join('');
  const report = pane.report || {};
  const result = report.data && report.data.result ? report.data.result : null;
  const resultBlock = report.status === 'idle' ? '' : `<div class="workspace-tools-result"><strong>生成结果</strong>${report.errors && report.errors.length ? `<ul class="workspace-notice__list">${errorText(report.errors)}</ul>` : ''}${result ? `<p>exitCode=${text(result.exitCode)}${result.reportMd ? ` · <code>${text(result.reportMd)}</code>` : ''}${result.reportJson ? ` · <code>${text(result.reportJson)}</code>` : ''}</p>${result.stderrTail ? `<pre class="workspace-tools-stderr">${text(result.stderrTail)}</pre>` : ''}` : (report.status === 'loading' ? '<p>生成中…</p>' : '')}</div>`;
  const diagnosisBlock = renderDiagnosisBlock(pane.diagnosis);
  return `<section class="workspace-pane workspace-pane--tools" data-pane="tools"><div class="workspace-pane__heading"><h3>工具目录</h3><span class="workspace-pane__count">${(pane.items || []).length}</span></div><p class="workspace-tools-note">查看已登记工具及其接入状态。</p><div class="workspace-tool-catalog">${rows || '<p class="workspace-empty workspace-empty--compact">未加载工具</p>'}</div><div class="workspace-tools-invoke"><label for="workspace-tools-run-dir">Run 目录（runs 根下的目录名）</label><input id="workspace-tools-run-dir" type="text" data-workspace-tools-run-dir value="${text(pane.runDir)}" placeholder="run-20261003-075727-844"><button type="button" class="workspace-launch-action" data-workspace-action="generate-report"${pane.runDir && pane.runDir.trim() && report.status !== 'loading' ? '' : ' disabled aria-disabled="true"'}>${report.status === 'loading' ? '生成中…' : '生成全链路报告'}</button></div>${resultBlock}${diagnosisBlock}</section>`;
}

// PNL-012：诊断结果区块（read-only：只展示；文本前强制显示非权威声明）。
function renderDiagnosisBlock(diagnosis = {}) {
  if (diagnosis.status === 'idle') return '';
  const result = diagnosis.data && diagnosis.data.result ? diagnosis.data.result : null;
  const errors = diagnosis.errors && diagnosis.errors.length ? `<ul class="workspace-notice__list">${errorText(diagnosis.errors)}</ul>` : '';
  const body = result
    ? `<p class="workspace-diagnosis-notice"><strong>诊断输出：非权威观察，不构成 Runtime truth</strong></p><p>model=<code>${text(result.model)}</code>${result.reportRef ? ` · report=<code>${text(result.reportRef)}</code>` : ''}</p><pre class="workspace-tools-stderr">${text(result.text)}</pre>`
    : (diagnosis.status === 'loading' ? '<p>诊断中…</p>' : errors);
  return `<div class="workspace-tools-result workspace-diagnosis-result"><strong>诊断结果</strong>${body}</div>`;
}
function renderMetadataPane(pane = {}) {
  const items = pane.items || {};
  const deviceKeys = ['device', 'androidApi', 'wmSize', 'real'];
  const deviceItems = deviceKeys.filter((key) => items[key] !== undefined && items[key] !== null);
  const deviceBlock = deviceItems.length ? `<div class="workspace-metadata-device"><div class="workspace-metadata-device__heading"><strong>设备与运行环境</strong><span class="workspace-chip">${text(items.real === true ? 'real device' : 'runtime')}</span></div><dl>${deviceItems.map((key) => renderMetadataRow(key, items[key])).join('')}</dl></div>` : '';
  const rows = Object.keys(items).filter((key) => !deviceKeys.includes(key)).map((key) => renderMetadataRow(key, items[key])).join('');
  const claims = Array.isArray(pane.claims) && pane.claims.length ? `<div class="workspace-metadata-claims"><strong>字段来源</strong>${pane.claims.map((claim) => `<div class="workspace-metadata-claim"><span>${text(claim.key)}</span><span>${text(claim.valueOrigin, 'unknown')}</span><span>${text(claim.availability, 'unknown')}</span></div>`).join('')}</div>` : '';
  const refs = Object.keys(pane.logicalRefs || {}).length ? `<div class="workspace-metadata-refs"><strong>任务关联</strong><dl>${Object.entries(pane.logicalRefs).map(([key, value]) => renderMetadataRow(key, value)).join('')}</dl>${pane.runId ? `<p class="workspace-metadata-run">runId · ${text(pane.runId)}</p>` : ''}</div>` : '';
  return `<section class="workspace-pane workspace-pane--metadata" data-pane="metadata"><div class="workspace-pane__heading"><h3>Metadata</h3><span class="workspace-status workspace-status--${escapeHtml(pane.status || 'unknown')}">${text(pane.status, 'unknown')}</span></div>${deviceBlock}${refs}${claims}<dl>${rows}</dl></section>`;
}
function renderMetadataRow(key, value) { const endpointKey = ['d', 'sh', 'Endpoint'].join(''); const formattedKey = { androidApi: 'Android API', wmSize: '屏幕尺寸', taskSet: '测试集', productModel: '模型', sessionId: 'Session ID' }[key] || (key === endpointKey ? '连接端点' : key); return `<div><dt>${text(formattedKey)}</dt><dd>${typeof value === 'object' && value !== null ? `<details class="workspace-metadata-value"><summary>查看结构化数据</summary><pre>${text(JSON.stringify(value, null, 2))}</pre></details>` : text(value)}</dd></div>`; }
function renderExecutionPane(pane = {}) { return `<section class="workspace-pane workspace-pane--execution" data-pane="execution"><div class="workspace-pane__heading"><div><h3>执行结果</h3><p class="workspace-pane__subheading">当前运行的结论与证据</p></div><span class="workspace-status workspace-status--${escapeHtml(pane.status || 'empty')}">${text(pane.status === 'ready' ? '已产生' : '待运行', pane.status || 'empty')}</span></div>${(pane.items || []).length ? (pane.items || []).map((item) => `<article class="workspace-execution-card"><div><strong>${text(item.label, '执行结果')}</strong><span class="workspace-chip">${text(item.status, 'unknown')}</span></div><p>${text(item.text, '暂无结果摘要')}</p>${item.evidenceRefs?.length ? `<div class="workspace-reference-list">${item.evidenceRefs.map((ref) => `<button type="button" class="workspace-reference-link" data-workspace-action="resolve-detail" data-detail-ref="${escapeHtml(String(ref).split('/').pop())}">${text(String(ref).split('/').pop())}</button>`).join('')}</div>` : ''}</article>`).join('') : '<p class="workspace-empty workspace-empty--compact">当前 session 尚无 UniClaw runtime 执行产物</p>'}</section>`; }
function renderDetailPanel(detail = {}) {
  const returnLabel = { conversation: '解决过程', trace: 'Trace', evidence: 'Evidence', metadata: '概览', execution: '执行结果', tools: '工具' }[detail.returnPane] || 'Trace';
  const content = detail.current ? renderDetailContent(detail.current) : `<p class="workspace-empty">${text(detail.status === 'loading' ? '正在加载详情…' : '暂无可展示的详情')}</p>`;
  return `<section class="workspace-record-detail" data-detail-status="${escapeHtml(detail.status || 'idle')}">${detail.errors?.length ? `<ul class="workspace-notice__list">${errorText(detail.errors)}</ul>` : ''}${content}<footer class="workspace-record-detail__footer"><button type="button" class="workspace-detail-back" data-workspace-action="close-detail">返回 ${returnLabel}</button></footer></section>`;
}

function formatDetail(value) {
  if (!value || typeof value !== 'object' || typeof value.text !== 'string') return value;
  try { return { ...value, text: JSON.parse(value.text) }; } catch { return { ...value, __rawText: true }; }
}
function renderDetailContent(value) {
  if (value?.format === 'conversation-record') return renderConversationRecord(value);
  if (value?.format === 'trace-record') return renderTraceRecord(value.record || {});
  const formatted = formatDetail(value);
  if (formatted && typeof formatted === 'object' && formatted.__rawText === true && typeof formatted.text === 'string') {
    const { text: rawText, __rawText, ...metadata } = formatted;
    return `<div class="workspace-detail-modal__raw"><pre>${text(JSON.stringify(metadata, null, 2))}</pre><pre>${text(rawText)}</pre></div>`;
  }
  return `<pre>${text(JSON.stringify(formatted, null, 2))}</pre>`;
}
function renderConversationRecord(value) {
  const record = value.record || {};
  const fields = [['轮次', value.round], ['阶段', record.label || record.kind], ['Decision ID', record.decisionId], ['Call ID', record.callId], ['Run ID', record.runId], ['时间', record.ts], ['事件序号', record.seq]];
  const rows = fields.filter(([, v]) => v != null && v !== '').map(([k, v]) => `<div><dt>${text(k)}</dt><dd>${text(v)}</dd></div>`).join('');
  return `<div class="workspace-trace-record"><section><h4>${record.kind === 'decision' ? '选择与理由' : '记录内容'}</h4><p class="workspace-record-text">${text(record.objective || record.justification || record.text || record.summary, '未记录正文')}</p></section><dl>${rows}</dl>${record.result?.text ? `<section class="workspace-trace-record__section"><h4>提交回执</h4><p class="workspace-record-text">${text(record.result.text)}</p></section>` : ''}<details class="workspace-trace-record__section"><summary>完整记录与关联信息</summary><pre>${text(JSON.stringify(record, null, 2))}</pre></details></div>`;
}
function renderTraceRecord(record) {
  const fields = [['名称', record.label || record.definition || record.type], ['来源', record.source], ['Trace ID', record.traceId], ['Span ID', record.spanId || record.id], ['Parent Span', record.parentSpanId], ['Kind', record.spanKind], ['Status', record.status], ['结构结果', record.structuralOutcome], ['开始', record.startTime || record.ts], ['结束', record.endTime], ['耗时', formatSpanDuration(record)], ['采集序号', record.captureSequence ?? record.seq]];
  const overview = fields.filter(([, v]) => v != null && v !== '').map(([k, v]) => `<div><dt>${text(k)}</dt><dd>${text(typeof v === 'object' ? JSON.stringify(v) : v)}</dd></div>`).join('');
  const sections = [['属性', record.attributes], ['事件', record.events], ['关联引用', record.references], ['关联 Span', record.links], ['资源', record.resource]].filter(([, v]) => v && Object.keys(v).length).map(([label, value]) => `<section class="workspace-trace-record__section"><h4>${text(label)}</h4><pre>${text(JSON.stringify(value, null, 2))}</pre></section>`).join('');
  return `<div class="workspace-trace-record"><dl>${overview}</dl>${record.text ? `<section class="workspace-trace-record__section"><h4>内容</h4><pre>${text(record.text)}</pre></section>` : ''}${sections}<details class="workspace-trace-record__section"><summary>完整节点记录</summary><pre>${text(JSON.stringify(record, null, 2))}</pre></details></div>`;
}
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

  },
  "src/ui/workspace-layout.js": (require, module, exports) => {
'use strict';

const DEFAULT_WIDTH = 300;
const MIN_WIDTH = 238;
const MAX_WIDTH = 440;
const STORAGE_KEY = 'uniclaw.workspace.layout.v1';

function clampWidth(value) {
  const width = Number(value);
  return Number.isFinite(width) ? Math.max(MIN_WIDTH, Math.min(MAX_WIDTH, Math.round(width))) : DEFAULT_WIDTH;
}

function readPreferences(storage) {
  try {
    const value = storage?.getItem(STORAGE_KEY);
    if (!value) return { width: DEFAULT_WIDTH, collapsed: false };
    const parsed = JSON.parse(value);
    return { width: clampWidth(parsed.width), collapsed: parsed.collapsed === true };
  } catch (_) {
    return { width: DEFAULT_WIDTH, collapsed: false };
  }
}

function writePreferences(storage, preferences) {
  try { storage?.setItem(STORAGE_KEY, JSON.stringify(preferences)); } catch (_) { /* storage is optional */ }
}

function bindWorkspaceLayout(container, { storage } = {}) {
  if (!container || typeof container.addEventListener !== 'function') throw new TypeError('container is required');
  const ownerDocument = container.ownerDocument || (typeof document !== 'undefined' ? document : null);
  const view = ownerDocument?.defaultView || (typeof window !== 'undefined' ? window : null);
  const preferences = readPreferences(storage || view?.localStorage);
  const mobile = view?.matchMedia?.('(max-width:760px)');
  let drawerOpen = false;
  let resizing = null;

  function apply() {
    const root = container.querySelector('.workspace');
    if (!root) return;
    root.style.setProperty('--workspace-nav-width', `${preferences.width}px`);
    root.classList.toggle('workspace--navigation-collapsed', !mobile?.matches && preferences.collapsed);
    root.classList.toggle('workspace--navigation-open', Boolean(mobile?.matches && drawerOpen));
    const nav = root.querySelector('.workspace-navigation');
    if (nav) nav.inert = Boolean(mobile?.matches && !drawerOpen);
    const main = root.querySelector('.workspace-main');
    if (main) main.inert = Boolean(mobile?.matches && drawerOpen);
    root.querySelector('.workspace-mobile-nav-toggle')?.setAttribute('aria-expanded', String(Boolean(drawerOpen && mobile?.matches)));
    const toggle = root.querySelector('.workspace-navigation__toggle');
    if (toggle) {
      toggle.textContent = mobile?.matches ? '收起任务栏' : preferences.collapsed ? '展开任务栏' : '隐藏任务栏';
      toggle.setAttribute('aria-expanded', String(mobile?.matches ? drawerOpen : !preferences.collapsed));
    }
  }

  function persist() { writePreferences(storage || view?.localStorage, preferences); }
  function toggle() {
    if (mobile?.matches) {
      drawerOpen = !drawerOpen;
      apply();
      container.querySelector(drawerOpen ? '.workspace-navigation__toggle' : '.workspace-mobile-nav-toggle')?.focus({ preventScroll: true });
    } else { preferences.collapsed = !preferences.collapsed; persist(); apply(); }
  }
  function setWidth(width) { preferences.width = clampWidth(width); persist(); apply(); }
  function stopResize() {
    if (!resizing) return;
    view?.removeEventListener('pointermove', resizing.move);
    view?.removeEventListener('pointerup', resizing.end);
    view?.removeEventListener('pointercancel', resizing.end);
    container.querySelector('.workspace')?.classList.remove('workspace--navigation-resizing');
    resizing = null;
  }
  function startResize(event) {
    if (event.button !== 0 || preferences.collapsed) return;
    const root = container.querySelector('.workspace');
    if (!root) return;
    const startX = event.clientX;
    const startWidth = preferences.width;
    const move = (nextEvent) => setWidth(startWidth + nextEvent.clientX - startX);
    const end = () => stopResize();
    resizing = { move, end };
    root.classList.add('workspace--navigation-resizing');
    view?.addEventListener('pointermove', move);
    view?.addEventListener('pointerup', end, { once: true });
    view?.addEventListener('pointercancel', end, { once: true });
    event.preventDefault();
  }
  function keyboardResize(event) {
    if (preferences.collapsed) return;
    if (event.key === 'ArrowLeft') { setWidth(preferences.width - 16); event.preventDefault(); }
    if (event.key === 'ArrowRight') { setWidth(preferences.width + 16); event.preventDefault(); }
  }
  container.addEventListener('click', (event) => {
    const target = event.target?.closest?.('[data-workspace-action="toggle-navigation"]');
    if (!target || !container.contains(target)) return;
    event.preventDefault();
    toggle();
  });
  const onSelect = (event) => {
    if (mobile?.matches && event.target?.closest?.('[data-workspace-action="select-task"]')) { drawerOpen = false; apply(); }
  };
  const onEscape = (event) => {
    if (event.key === 'Escape' && mobile?.matches && drawerOpen) { event.preventDefault(); toggle(); }
    if (event.key === 'Tab' && mobile?.matches && drawerOpen) {
      const buttons = [...container.querySelectorAll('.workspace-navigation button:not(:disabled)')].filter((node) => node.getClientRects().length);
      const first = buttons[0], last = buttons.at(-1);
      if (event.shiftKey && ownerDocument.activeElement === first) { event.preventDefault(); last?.focus(); }
      else if (!event.shiftKey && ownerDocument.activeElement === last) { event.preventDefault(); first?.focus(); }
    }
  };
  container.addEventListener('click', onSelect);
  container.addEventListener('keydown', onEscape);
  mobile?.addEventListener('change', apply);
  container.addEventListener('pointerdown', (event) => {
    const target = event.target?.closest?.('[data-workspace-resize="navigation"]');
    if (target && container.contains(target)) startResize(event);
  });
  container.addEventListener('keydown', (event) => {
    const target = event.target?.closest?.('[data-workspace-resize="navigation"]');
    if (target && container.contains(target)) keyboardResize(event);
  });
  apply();
  return Object.freeze({ apply, toggle, setWidth, getPreferences: () => ({ ...preferences }), dispose: () => {
    stopResize();
    container.removeEventListener('click', onSelect);
    container.removeEventListener('keydown', onEscape);
    mobile?.removeEventListener('change', apply);
  } });
}

module.exports = { bindWorkspaceLayout, clampWidth };

  },
  "src/ui/workspace-reading.js": (require, module, exports) => {
'use strict';

// Short-lived DOM reading state. No source data or product state is retained.
function bindWorkspaceReading(container, { closeInspector } = {}) {
  const document = container.ownerDocument;
  const view = document?.defaultView;
  const drawer = view?.matchMedia?.('(max-width:1200px)');
  const positions = new Map();
  const disclosures = new Map();
  let previous = {};
  let returnFocus = null;

  const root = () => container.querySelector('.workspace');
  const session = () => container.querySelector('.workspace-task-header')?.getAttribute('data-selected-product-session-id') || '';
  const open = () => root()?.classList.contains('workspace--inspector-open');
  const signature = (region) => {
    const last = region.querySelector('.workspace-timeline')?.lastElementChild;
    return last?.textContent || '';
  };
  const key = (region) => `${session()}:${region.getAttribute('data-workspace-scroll')}`;
  function focusRef(element) {
    if (!element || !container.contains(element)) return null;
    const attrs = [...(element.attributes || [])].filter((a) => a.name === 'id' || a.name.startsWith('data-')).map((a) => [a.name, a.value]);
    return attrs.length ? { tag: element.tagName, attrs, start: element.selectionStart, end: element.selectionEnd } : null;
  }
  function focusElement(ref) {
    if (!ref) return;
    const element = [...container.querySelectorAll('[id], [data-workspace-action], [data-inspector-focus], input, textarea, select')]
      .find((node) => node.tagName === ref.tag && ref.attrs.every(([name, value]) => node.getAttribute(name) === value));
    if (!element || element.closest('[hidden], [inert]')) return;
    element.focus?.({ preventScroll: true });
    if (typeof ref.start === 'number' && typeof element.setSelectionRange === 'function') element.setSelectionRange(ref.start, ref.end);
  }
  function capture() {
    previous = { focus: focusRef(document?.activeElement), open: open(), session: session() };
    for (const node of container.querySelectorAll('[data-reading-disclosure]')) {
      disclosures.set(`${session()}:${node.getAttribute('data-reading-disclosure')}`, node.open);
    }
    for (const region of container.querySelectorAll('[data-workspace-scroll]')) {
      if (!region.clientHeight) continue;
      if (region.getAttribute('data-workspace-scroll') === 'conversation' && !region.querySelector('.workspace-message')) continue;
      const top = region.getBoundingClientRect().top;
      const anchor = [...region.querySelectorAll('[data-reading-anchor]')].find((node) => node.getBoundingClientRect().bottom > top);
      positions.set(key(region), {
        top: region.scrollTop,
        follow: region.scrollHeight - region.scrollTop - region.clientHeight < 48,
        signature: signature(region),
        anchor: anchor?.getAttribute('data-reading-anchor'), offset: anchor ? anchor.getBoundingClientRect().top - top : 0,
        hasNew: positions.get(key(region))?.hasNew || false
      });
    }
  }
  function accessibility() {
    const panel = container.querySelector('#workspace-inspector');
    const modal = Boolean(open() && drawer?.matches);
    if (panel) {
      panel.setAttribute('role', modal ? 'dialog' : 'complementary');
      if (modal) panel.setAttribute('aria-modal', 'true'); else panel.removeAttribute('aria-modal');
    }
    for (const node of container.querySelectorAll('.workspace-primary, .workspace-task-header, .workspace-navigation')) {
      // The navigation layout owns its closed-mobile state.
      if (node.classList.contains('workspace-navigation') && !modal) continue;
      node.inert = modal;
    }
  }
  function restore() {
    accessibility();
    for (const node of container.querySelectorAll('[data-reading-disclosure]')) {
      const id = `${session()}:${node.getAttribute('data-reading-disclosure')}`;
      if (disclosures.has(id)) node.open = disclosures.get(id);
    }
    for (const region of container.querySelectorAll('[data-workspace-scroll]')) {
      if (!region.clientHeight) continue;
      const saved = positions.get(key(region));
      const conversation = region.getAttribute('data-workspace-scroll') === 'conversation';
      if (saved) {
        region.scrollTop = saved.follow && conversation ? region.scrollHeight : saved.top;
        if (!saved.follow && saved.anchor) {
          const anchor = [...region.querySelectorAll('[data-reading-anchor]')].find((node) => node.getAttribute('data-reading-anchor') === saved.anchor);
          if (anchor) region.scrollTop += anchor.getBoundingClientRect().top - region.getBoundingClientRect().top - saved.offset;
        }
        if (conversation && !saved.follow && saved.signature !== signature(region)) saved.hasNew = true;
      } else if (conversation) region.scrollTop = region.scrollHeight;
      if (conversation) {
        const button = container.querySelector('[data-workspace-action="reading-latest"]');
        if (button) button.hidden = !saved?.hasNew;
      }
    }
    if (open() && !previous.open) {
      returnFocus = previous.focus;
      container.querySelector('[data-inspector-focus]')?.focus({ preventScroll: true });
    } else if (!open() && previous.open && previous.session === session()) focusElement(returnFocus);
    else focusElement(previous.focus);
  }
  function onScroll(event) {
    const region = event.target;
    if (region?.getAttribute?.('data-workspace-scroll') !== 'conversation') return;
    if (region.scrollHeight - region.scrollTop - region.clientHeight >= 48) return;
    const saved = positions.get(key(region));
    if (saved) saved.hasNew = false;
    const button = container.querySelector('[data-workspace-action="reading-latest"]');
    if (button) button.hidden = true;
  }
  function onClick(event) {
    if (!event.target.closest?.('[data-workspace-action="reading-latest"]')) return;
    const region = container.querySelector('[data-workspace-scroll="conversation"]');
    if (region) { region.scrollTop = region.scrollHeight; onScroll({ target: region }); }
  }
  function onKey(event) {
    const tabs = [...container.querySelectorAll('[data-workspace-action="select-view"]')];
    if (tabs.includes(event.target) && ['ArrowLeft', 'ArrowRight'].includes(event.key)) {
      event.preventDefault();
      const next = tabs[(tabs.indexOf(event.target) + 1) % tabs.length];
      const id = next?.id;
      next?.click();
      if (id) document.getElementById(id)?.focus();
      return;
    }
    if (!open()) return;
    if (event.key === 'Escape') { event.preventDefault(); closeInspector?.(); return; }
    if (event.key !== 'Tab' || !drawer?.matches) return;
    const panel = container.querySelector('#workspace-inspector');
    const elements = [...panel.querySelectorAll('button:not(:disabled), a[href], input, textarea, select, summary, [tabindex="0"]')].filter((node) => node.getClientRects().length);
    const first = elements[0], last = elements.at(-1);
    if (!first) return;
    if (event.shiftKey && (document.activeElement === first || document.activeElement?.matches('[data-inspector-focus]'))) { event.preventDefault(); last.focus(); }
    else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first.focus(); }
  }
  container.addEventListener('scroll', onScroll, true);
  container.addEventListener('click', onClick);
  container.addEventListener('keydown', onKey);
  drawer?.addEventListener('change', accessibility);
  return { capture, restore, dispose() {
    container.removeEventListener('scroll', onScroll, true);
    container.removeEventListener('click', onClick);
    container.removeEventListener('keydown', onKey);
    drawer?.removeEventListener('change', accessibility);
    positions.clear();
    disclosures.clear();
  } };
}
module.exports = { bindWorkspaceReading };

  }
};const load=(id,parent="")=>{if(id==="react")return externalRequire(id);if(id==="@uniclaw/workspace/browser")id="src/browser/entry.js";if(id!=="src/browser/entry.js"&&!id.startsWith("."))return externalRequire(id);const base=parent.split("/").slice(0,-1).join("/");const key=id.split("/").reduce((p,x)=>{if(x==="..")p.pop();else if(x!==".")p.push(x);return p},base?base.split("/"):[]).join("/");const k0=key.endsWith(".js")?key:key+".js";const k=modules[k0]?k0:k0.replace(/\.js$/,"/index.js");if(cache[k])return cache[k].exports;if(!modules[k])throw Error("missing bundled module: "+k);const m={exports:{}};cache[k]=m;modules[k]((x)=>load(x,k),m,m.exports);return m.exports};const bridge=load("src/browser/entry.js");const workspaceStyleText=".workspace {\n  color-scheme: light;\n  font-family: Inter, ui-sans-serif, -apple-system, BlinkMacSystemFont, \"Segoe UI\", \"PingFang SC\", \"Microsoft YaHei\", sans-serif;\n  --workspace-bg: #f4f6fa; --workspace-surface: #fff; --workspace-surface-muted: #f8fafc;\n  --workspace-border: #e4e8f0; --workspace-border-strong: #d4dbe7; --workspace-text: #182230;\n  --workspace-muted: #718096; --workspace-accent: #5968d8; --workspace-accent-soft: #eef0ff;\n  --workspace-agent: #f0f2ff; --workspace-request: #eef8f5; --workspace-tool: #f7f4ff;\n  --workspace-danger: #b42318; --workspace-warning: #b54708; --workspace-radius: 16px; --workspace-gap: 18px;\n  height: 100dvh; min-height: 0; overflow: hidden; display: grid; align-items: stretch; grid-template-columns: var(--workspace-nav-width, clamp(270px, 18vw, 340px)) minmax(0, 1fr);\n  background: var(--workspace-bg); color: var(--workspace-text); font-size: 14px; line-height: 1.55;\n}\n.workspace, .workspace * , .workspace *::before, .workspace *::after { box-sizing: border-box; }\n.workspace button { font: inherit; }\n.workspace-navigation { position: relative; min-height: 0; height: 100%; overflow-y: auto; overflow-x: hidden; padding: 24px 18px; border-right: 1px solid var(--workspace-border); background: rgba(255,255,255,.94); }\n.workspace-navigation__header { display: flex; align-items: center; justify-content: space-between; gap: 10px; min-width: 0; margin: 0 4px 26px; }\n.workspace-navigation__title { min-width: 0; margin: 0; font-size: 20px; letter-spacing: -.02em; }\n.workspace-navigation__toggle { flex: 0 0 auto; padding: 5px 7px; border: 1px solid var(--workspace-border); border-radius: 7px; background: var(--workspace-surface); color: var(--workspace-muted); font-size: 11px; cursor: pointer; }\n.workspace-navigation__toggle:hover { color: var(--workspace-accent); border-color: var(--workspace-accent); }\n.workspace-navigation__body { min-width: 0; }\n.workspace-navigation__resize { position: absolute; top: 0; right: 0; z-index: 9; width: 5px; height: 100%; cursor: col-resize; }\n.workspace-navigation__resize:focus-visible { outline: 2px solid var(--workspace-accent); outline-offset: -2px; }\n.workspace-navigation__projects { display: grid; grid-template-columns: minmax(0,1fr); gap: 20px; min-width: 0; max-width: 100%; }\n.workspace-project { display: grid; gap: 8px; min-width: 0; max-width: 100%; }\n.workspace-project__action { display: flex; width: 100%; padding: 0 4px; border: 0; background: transparent; color: var(--workspace-muted); font-size: 12px; font-weight: 700; letter-spacing: .05em; text-align: left; text-transform: uppercase; cursor: pointer; }\n.workspace-project__action:hover { color: var(--workspace-accent); }\n.workspace-navigation__tasks { display: grid; gap: 8px; margin-top: 20px; min-width: 0; max-width: 100%; }\n.workspace-task-card { min-width: 0; max-width: 100%; border: 1px solid var(--workspace-border); border-radius: 13px; background: var(--workspace-surface); box-shadow: 0 2px 8px rgba(27,39,71,.03); }\n.workspace-task-card__action { display: block; width: 100%; min-width: 0; max-width: 100%; padding: 13px 14px; border: 0; border-radius: inherit; background: transparent; color: inherit; text-align: left; cursor: pointer; }\n.workspace-task-card__action:hover { background: #f8f9ff; }\n.workspace-task-card.is-selected { border-color: #aeb7f3; box-shadow: 0 0 0 3px rgba(89,104,216,.12); }\n.workspace-task-card.is-selected .workspace-task-card__action { background: var(--workspace-accent-soft); }\n.workspace-task-card h3 { margin: 0 0 7px; font-size: 14px; line-height: 1.35; }\n.workspace-task-card__meta { display: flex; align-items: center; justify-content: space-between; gap: 8px; color: var(--workspace-muted); font-size: 12px; }\n.workspace-task-card__meta .workspace-status { flex: 0 0 auto; white-space: nowrap; }\n.workspace-task-card__meta > span { min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }\n.workspace-task-card.is-uncorrelated { border-style: dashed; opacity: .78; }\n.workspace-task-card.is-uncorrelated .workspace-task-card__action { cursor: not-allowed; }\n.workspace-main { min-width: 0; min-height: 0; height: 100%; display: flex; flex-direction: column; overflow: hidden; }\n.workspace-notices { display: grid; gap: 8px; margin-bottom: var(--workspace-gap); padding: 10px; border: 1px solid #f6d7b9; border-radius: 14px; background: #fffaf5; }\n.workspace-notices__header { display: flex; align-items: center; justify-content: space-between; gap: 12px; padding: 2px 4px 4px; color: #7b4a22; font-size: 12px; }\n.workspace-notices__retry { border: 0; border-radius: 7px; padding: 5px 9px; background: #fff; color: var(--workspace-accent); font: inherit; font-weight: 700; cursor: pointer; }\n.workspace-notice { padding: 8px 10px; border: 1px solid #f6d7b9; border-radius: 9px; background: #fff8f1; color: var(--workspace-warning); font-size: 13px; }\n.workspace-notice--error { border-color: #f2c4c0; background: #fff5f4; color: var(--workspace-danger); }\n.workspace-notice__headline { display: flex; align-items: center; gap: 8px; flex-wrap: wrap; }\n.workspace-notice__count { padding: 2px 6px; border-radius: 999px; background: rgba(180, 56, 47, .1); font-size: 11px; font-weight: 700; }\n.workspace-notice__source { color: #8f6e67; font-size: 11px; }\n.workspace-notice__details { margin-top: 6px; color: #7c6660; font-size: 12px; }\n.workspace-notice__details summary { cursor: pointer; color: var(--workspace-accent); font-weight: 700; }\n.workspace-notice__details ul { display: grid; gap: 3px; margin: 5px 0 0 16px; padding: 0; }\n.workspace-task-header { display: flex; align-items: center; justify-content: space-between; gap: 18px; padding: 22px 24px; border: 1px solid var(--workspace-border); border-radius: var(--workspace-radius); background: var(--workspace-surface); box-shadow: 0 8px 24px rgba(27,39,71,.05); }\n.workspace-task-header h2 { margin: 2px 0 0; font-size: clamp(20px,2vw,28px); letter-spacing: -.025em; }\n.workspace-eyebrow { display: flex; align-items: center; gap: 8px; margin: 0; color: var(--workspace-accent); font-size: 12px; font-weight: 700; letter-spacing: .04em; text-transform: uppercase; }\n.workspace-header-origin { color: var(--workspace-muted); font-weight: 500; text-transform: none; }\n.workspace-task-header__meta { display: flex; align-items: center; gap: 8px; color: var(--workspace-muted); font-size: 12px; white-space: nowrap; }\n.workspace-task-header__meta > .workspace-status,\n.workspace-task-header__meta > .workspace-correlation { display: inline-flex; align-items: center; min-height: 32px; padding: 0 12px; line-height: 30px; }\n.workspace-refresh-action { order: 2; padding: 9px 13px; border: 1px solid var(--workspace-border-strong); border-radius: 9px; background: var(--workspace-surface-muted); color: var(--workspace-text); cursor: pointer; }\n.workspace-header-actions { display: flex; align-items: center; gap: 8px; }\n.workspace-launch-action { padding: 9px 13px; border: 1px solid var(--workspace-accent); border-radius: 9px; background: var(--workspace-accent); color: #fff; cursor: pointer; font-weight: 700; }\n.workspace-launch-action:disabled { border-color: var(--workspace-border); background: var(--workspace-surface-muted); color: var(--workspace-muted); cursor: not-allowed; }\n.workspace-refresh-action:hover { border-color: var(--workspace-accent); color: var(--workspace-accent); }\n.workspace-correlation { display: inline-flex; align-items: center; padding: 5px 9px; border: 1px solid var(--workspace-border); border-radius: 999px; background: var(--workspace-surface-muted); color: var(--workspace-muted); font-size: 11px; white-space: nowrap; }\n.workspace-status { display: inline-flex; align-items: center; padding: 3px 8px; border-radius: 999px; background: #eef2ff; color: var(--workspace-accent); font-size: 11px; font-weight: 700; line-height: 1.2; }\n.workspace-status--completed,.workspace-status--submitted,.workspace-status--ready { background: #eaf8f1; color: #137a4b; }\n.workspace-status--partial,.workspace-status--pending,.workspace-status--legacy { background: #fff5e8; color: var(--workspace-warning); }\n.workspace-status--error,.workspace-status--failed { background: #fff0ef; color: var(--workspace-danger); }\n.workspace-observation-bar { position: sticky; top: 12px; z-index: 8; display: flex; align-items: center; justify-content: space-between; gap: 14px; margin-top: 16px; padding: 7px; border: 1px solid rgba(212,219,231,.92); border-radius: 14px; background: rgba(248,250,252,.92); box-shadow: 0 8px 20px rgba(27,39,71,.08); backdrop-filter: blur(12px); }\n.workspace-observation-bar .workspace-pane-tabs { flex: 0 0 auto; border: 0; background: transparent; }\n.workspace-diagnostics-slot { display: flex; align-items: center; gap: 12px; min-width: 0; padding: 3px 5px 3px 12px; color: var(--workspace-muted); }\n.workspace-diagnostics-slot span { display: grid; gap: 1px; min-width: 0; }\n.workspace-diagnostics-slot strong { color: var(--workspace-text); font-size: 12px; }\n.workspace-diagnostics-slot small { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; font-size: 11px; }\n.workspace-diagnostics-action { flex: 0 0 auto; padding: 7px 10px; border: 1px solid var(--workspace-border-strong); border-radius: 8px; background: var(--workspace-surface); color: var(--workspace-muted); font-size: 11px; font-weight: 700; cursor: not-allowed; }\n.workspace-diagnostics-action:disabled { opacity: .82; }\n.workspace-conversation { margin-top: 18px; }\n.workspace-conversation > h3 { margin: 0 0 12px; font-size: 17px; letter-spacing: -.01em; }\n.workspace-conversation-shell { position: relative; min-width: 0; max-width: 100%; max-height: min(720px, calc(100vh - 280px)); overflow: auto; overscroll-behavior: contain; padding: 14px; border: 1px solid #dce3ef; border-radius: 20px; background: linear-gradient(180deg,#edf2f8 0%,#f5f7fb 100%); box-shadow: inset 0 1px 0 rgba(255,255,255,.8); }\n.workspace-timeline { min-width: 0; max-width: 100%; display: grid; gap: 12px; }\n.workspace-conversation-round { position: relative; min-width: 0; max-width: 100%; padding: 15px 16px 16px; border: 1px solid rgba(212,219,231,.9); border-radius: 15px; background: rgba(255,255,255,.82); box-shadow: 0 3px 10px rgba(27,39,71,.035); }\n.workspace-conversation-round + .workspace-conversation-round { margin-top: 2px; }\n.workspace-conversation-round__header { display: flex; align-items: center; justify-content: space-between; margin-bottom: 13px; padding-bottom: 9px; border-bottom: 1px solid rgba(228,232,240,.9); color: var(--workspace-muted); font-size: 12px; font-weight: 700; }\n.workspace-conversation-round__header::before { content: ''; width: 7px; height: 7px; margin-right: 6px; border-radius: 50%; background: var(--workspace-accent); box-shadow: 0 0 0 4px var(--workspace-accent-soft); }\n.workspace-conversation-round__header span { display: flex; align-items: center; margin-right: auto; }\n.workspace-dialogue { display: grid; min-width: 0; max-width: 100%; gap: 11px; }\n.workspace-message { display: flex; align-items: flex-start; min-width: 0; max-width: min(860px,92%); gap: 10px; }\n.workspace-message--agent { margin-right: auto; }\n.workspace-message--requester { margin-left: auto; flex-direction: row-reverse; }\n.workspace-message--tool,.workspace-message--system { margin-left: 34px; }\n.workspace-message__avatar { display: grid; flex: 0 0 28px; place-items: center; width: 28px; height: 28px; border-radius: 9px; background: #e8ebf5; color: #566173; font-size: 12px; font-weight: 800; }\n.workspace-message--requester .workspace-message__avatar { background: #d9f2e8; color: #15734e; }\n.workspace-message--agent .workspace-message__avatar { background: #dfe3ff; color: #4b58bb; }\n.workspace-message--tool .workspace-message__avatar { background: #eee5ff; color: #7251a8; }\n.workspace-message__body { min-width: 0; max-width: 100%; overflow: hidden; padding: 11px 14px; border: 1px solid var(--workspace-border); border-radius: 13px; background: var(--workspace-surface-muted); }\n.workspace-message--requester .workspace-message__body { border-color: #ccebdd; background: var(--workspace-request); }\n.workspace-message--agent .workspace-message__body { border-color: #d7dcff; background: var(--workspace-agent); }\n.workspace-message--tool .workspace-message__body { border-color: #e3d8f7; background: var(--workspace-tool); }\n.workspace-message.is-error .workspace-message__body { border-color: #f1c1bd; background: #fff3f2; }\n.workspace-message__head { display: flex; align-items: center; gap: 8px; margin-bottom: 5px; color: var(--workspace-muted); font-size: 12px; }\n.workspace-message__head strong { color: var(--workspace-text); font-size: 13px; }\n.workspace-message__head small { margin-left: auto; color: var(--workspace-muted); font-size: 11px; }\n.workspace-message__body p { margin: 0; white-space: pre-wrap; overflow-wrap: anywhere; }\n.workspace-message__steps { min-width: 0; max-width: 100%; margin: 8px 0 0 18px; padding: 0; color: #3c4860; overflow-wrap: anywhere; word-break: break-word; }\n.workspace-message__steps li { min-width: 0; max-width: 100%; overflow-wrap: anywhere; word-break: break-word; }\n.workspace-chip { padding: 2px 7px; border-radius: 999px; background: rgba(89,104,216,.12); color: #4b58bb; font-size: 10px; font-weight: 700; }\n.workspace-detail-action--small { margin-top: 9px; padding: 5px 9px; font-size: 11px; }\n.workspace-conversation-results { padding: 14px 16px; border: 1px dashed var(--workspace-border-strong); border-radius: 13px; background: rgba(255,255,255,.55); }\n.workspace-conversation-results h4 { margin: 0 0 10px; color: var(--workspace-muted); font-size: 12px; }\n.workspace-empty { margin: 0; padding: 28px; border: 1px dashed var(--workspace-border-strong); border-radius: var(--workspace-radius); color: var(--workspace-muted); text-align: center; }\n.workspace-inspector { display: grid; grid-template-columns: minmax(0,1fr); gap: var(--workspace-gap); margin-top: var(--workspace-gap); align-items: start; }\n.workspace-inspector__main { min-width: 0; }\n.workspace-pane-tabs { display: flex; gap: 6px; padding: 5px; border: 1px solid var(--workspace-border); border-radius: 12px; background: var(--workspace-surface-muted); }\n.workspace-pane-tab { flex: 0 0 auto; padding: 8px 15px; border: 0; border-radius: 8px; background: transparent; color: var(--workspace-muted); font-weight: 700; cursor: pointer; }\n.workspace-pane-tab:hover { color: var(--workspace-accent); }\n.workspace-pane-tab.is-active { background: var(--workspace-surface); color: var(--workspace-accent); box-shadow: 0 2px 7px rgba(27,39,71,.08); }\n.workspace-tab-panels { margin-top: 10px; }\n.workspace-tab-panel[hidden] { display: none; }\n.workspace-content .workspace-observation-bar { margin-top:0; }\n.workspace-pane,.workspace-detail-drawer { min-width: 0; padding: 18px; border: 1px solid var(--workspace-border); border-radius: var(--workspace-radius); background: var(--workspace-surface); box-shadow: 0 4px 14px rgba(27,39,71,.03); }\n.workspace-pane h3,.workspace-detail-drawer h3 { margin: 0 0 12px; font-size: 16px; }\n.workspace-pane__heading { display: flex; align-items: center; justify-content: space-between; gap: 10px; }\n.workspace-pane__heading h3 { margin: 0; }\n.workspace-pane__subheading { margin: 2px 0 0; color: var(--workspace-muted); font-size: 11px; }\n.workspace-pane__count { min-width: 22px; padding: 2px 7px; border-radius: 999px; background: var(--workspace-accent-soft); color: var(--workspace-accent); font-size: 11px; font-weight: 700; text-align: center; }\n.workspace-trace-root { margin-top: 12px; border: 1px solid var(--workspace-border); border-radius: 12px; background: var(--workspace-surface-muted); overflow: hidden; }\n.workspace-trace-root > summary { display: flex; align-items: center; gap: 12px; padding: 11px 13px; color: var(--workspace-text); font-size: 12px; font-weight: 800; cursor: pointer; list-style: none; }\n.workspace-trace-root > summary > span:first-child { flex: 0 0 auto; white-space: nowrap; }\n.workspace-trace-root > summary::-webkit-details-marker { display: none; }\n.workspace-trace-root > summary::before { content: '›'; display: inline-block; width: 15px; color: var(--workspace-accent); font-size: 17px; line-height: 1; transform: rotate(0deg); transition: transform .15s ease; }\n.workspace-trace-root[open] > summary::before { transform: rotate(90deg); }\n.workspace-trace-legend { display: flex; flex-wrap: wrap; gap: 6px; margin-left: auto; }\n.workspace-trace-toolbar { display: flex; align-items: flex-start; justify-content: space-between; gap: 12px; }\n.workspace-trace-context { display: flex; flex-wrap: wrap; align-items: center; gap: 6px 8px; margin-top: 10px; padding: 8px 10px; border: 1px solid #dce3ef; border-radius: 10px; background: #f8fafc; color: var(--workspace-muted); font: 10px/1.4 ui-monospace,SFMono-Regular,Menlo,monospace; }\n.workspace-trace-context__title { color: var(--workspace-text); font-family: inherit; font-size: 11px; font-weight: 800; }\n.workspace-trace-context__field { min-width: 0; max-width: 100%; padding: 3px 6px; overflow: hidden; border: 1px solid var(--workspace-border); border-radius: 6px; background: #fff; text-overflow: ellipsis; white-space: nowrap; }\n.workspace-trace-context__field b { margin-right: 3px; color: #55627a; font-weight: 800; }\n.workspace-trace-mode { display: flex; flex: 0 0 auto; gap: 3px; padding: 3px; border: 1px solid var(--workspace-border); border-radius: 9px; background: var(--workspace-surface-muted); }\n.workspace-trace-mode__button { padding: 5px 9px; border: 0; border-radius: 6px; background: transparent; color: var(--workspace-muted); font-size: 11px; font-weight: 700; cursor: pointer; }\n.workspace-trace-mode__button.is-active { background: var(--workspace-surface); color: var(--workspace-accent); box-shadow: 0 1px 4px rgba(27,39,71,.08); }\n.workspace-trace-source-switch { display: flex; flex-wrap: wrap; gap: 5px; margin-top: 10px; padding: 4px; border: 1px solid var(--workspace-border); border-radius: 10px; background: var(--workspace-surface-muted); }\n.workspace-trace-source-switch__button { padding: 6px 10px; border: 0; border-radius: 7px; background: transparent; color: var(--workspace-muted); font-size: 11px; font-weight: 700; cursor: pointer; }\n.workspace-trace-source-switch__button b { margin-left: 3px; font-size: 10px; }\n.workspace-trace-source-switch__button:hover { color: var(--workspace-accent); }\n.workspace-trace-source-switch__button.is-active { background: var(--workspace-surface); color: var(--workspace-accent); box-shadow: 0 1px 4px rgba(27,39,71,.08); }\n.workspace-trace-legend--standalone { margin-top: 10px; }\n.workspace-trace-viewer { max-height: min(56vh, 620px); overflow: auto; overscroll-behavior: contain; padding-right: 3px; }\n.workspace-trace-source-group { margin-top: 10px; border: 1px solid var(--workspace-border); border-radius: 12px; background: var(--workspace-surface-muted); overflow: hidden; }\n.workspace-trace-source-group > summary { display: flex; align-items: center; justify-content: space-between; padding: 10px 13px; color: var(--workspace-text); font-size: 12px; font-weight: 800; cursor: pointer; list-style: none; }\n.workspace-trace-source-group > summary::-webkit-details-marker { display: none; }\n.workspace-trace-source-count { color: var(--workspace-muted); font-size: 11px; font-weight: 600; }\n.workspace-trace-source { display: inline-flex; align-items: center; gap: 5px; width: fit-content; padding: 2px 7px; border-radius: 999px; background: #edf0f7; color: #5b6577; font-size: 10px; font-weight: 700; white-space: nowrap; }\n.workspace-trace-source b { color: inherit; font-size: 10px; }\n.workspace-trace-source--uniclaw { background: #e9f7f2; color: #1b765c; }\n.workspace-trace-source--uniflow { background: #f3ecff; color: #7653a7; }\n.workspace-trace-tree { display: grid; gap: 0; margin: 0; padding: 7px 12px 12px 18px; border-top: 1px solid var(--workspace-border); list-style: none; }\n.workspace-trace-row { position: relative; display: flex; align-items: flex-start; gap: 10px; min-width: 0; padding: 9px 0 9px calc(var(--trace-depth, 0) * 18px); }\n.workspace-trace-row__rail { position: relative; flex: 0 0 16px; width: 16px; min-height: 22px; border-left: 1px solid #d6ddea; }\n.workspace-trace-row__rail::after { content: ''; position: absolute; top: 10px; left: -1px; width: 10px; border-top: 1px solid #d6ddea; }\n.workspace-trace-row__content { min-width: 0; flex: 1 1 auto; display: grid; gap: 3px; }\n.workspace-trace-row__head { display: flex; align-items: center; gap: 7px; min-width: 0; }\n.workspace-trace-row__head strong { min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; font-size: 12px; }\n.workspace-trace-row__head .workspace-trace-status { flex: 0 0 auto; padding: 2px 6px; border-radius: 999px; background: #eaf8f1; color: #137a4b; font-size: 10px; font-weight: 800; }\n.workspace-trace-row__head .workspace-trace-status--error { background: #fff0ef; color: var(--workspace-danger); }\n.workspace-trace-row__head small { margin-left: auto; overflow: hidden; color: var(--workspace-muted); text-overflow: ellipsis; white-space: nowrap; font-size: 10px; }\n.workspace-trace-row__content p { margin: 0; overflow: hidden; color: var(--workspace-muted); text-overflow: ellipsis; white-space: nowrap; font-size: 11px; }\n.workspace-trace-row__facts { display: flex; flex-wrap: wrap; gap: 4px 8px; color: var(--workspace-muted); font: 10px/1.4 ui-monospace,SFMono-Regular,Menlo,monospace; }\n.workspace-trace-row__facts span { min-width: 0; max-width: 100%; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }\n.workspace-trace-row__facts b { margin-right: 3px; color: #55627a; font-weight: 700; }\n.workspace-trace-row .workspace-detail-action { flex: 0 0 auto; padding: 5px 8px; font-size: 11px; }\n.workspace-trace-branch > .workspace-trace-actions { margin: -2px 12px 4px 42px; }\n.workspace-trace-actions { display: flex; flex: 0 0 auto; align-items: center; gap: 5px; }\n.workspace-trace-file { padding: 5px 7px; border: 1px solid var(--workspace-border-strong); border-radius: 7px; background: var(--workspace-surface); color: var(--workspace-accent); font-size: 10px; cursor: pointer; }\n.workspace-trace-file:hover { border-color: var(--workspace-accent); }\n.workspace-evidence-card { display: flex; align-items: center; justify-content: space-between; gap: 10px; margin-top: 8px; padding: 10px 11px; border: 1px solid var(--workspace-border); border-radius: 10px; background: var(--workspace-surface); }\n.workspace-evidence-card { display: block; }\n.workspace-evidence-card h4 { margin: 0 0 5px; font-size: 13px; }\n.workspace-evidence-card p { margin: 0 0 8px; color: var(--workspace-muted); font-size: 12px; }\n.workspace-execution-card { display: grid; gap: 7px; margin-top: 10px; padding: 12px; border: 1px solid #d7dcff; border-radius: 11px; background: var(--workspace-agent); }\n.workspace-execution-card > div { display: flex; align-items: center; justify-content: space-between; gap: 8px; }\n.workspace-execution-card p { margin: 0; color: #3c4860; font-size: 12px; }\n.workspace-execution-card small { color: var(--workspace-muted); font-size: 11px; overflow-wrap: anywhere; }\n.workspace-reference-list { display: flex; flex-wrap: wrap; gap: 6px; }\n.workspace-reference-link { padding: 0; border: 0; background: transparent; color: var(--workspace-accent); font: inherit; font-size: 11px; text-align: left; text-decoration: underline; cursor: pointer; }\n.workspace-empty--compact { padding: 16px 10px; font-size: 12px; }\n.workspace-detail-action { padding: 7px 11px; border: 0; border-radius: 8px; background: var(--workspace-accent); color: #fff; font-size: 12px; cursor: pointer; }\n.workspace-detail-action:hover { background: #4655c3; }\n.workspace-detail-action:disabled { background: #d8dde7; color: #7b8494; cursor: not-allowed; }\n.workspace-error { color: var(--workspace-danger) !important; }\n.workspace-pane dl { margin: 0; }\n.workspace-pane dl div { display: flex; justify-content: space-between; gap: 10px; padding: 7px 0; border-bottom: 1px solid var(--workspace-border); }\n.workspace-pane dt { color: var(--workspace-muted); }\n.workspace-pane dd { margin: 0; overflow-wrap: anywhere; text-align: right; }\n.workspace-metadata-device { margin: 10px 0 12px; padding: 10px 12px; border: 1px solid #d7dcff; border-radius: 11px; background: #f5f6ff; }\n.workspace-metadata-device__heading { display: flex; align-items: center; justify-content: space-between; gap: 8px; margin-bottom: 3px; }\n.workspace-metadata-device__heading strong { font-size: 12px; }\n.workspace-metadata-device dl div:last-child { border-bottom: 0; }\n.workspace-detail-drawer { margin-top: var(--workspace-gap); }\n.workspace-detail-drawer__header { display: flex; align-items: center; justify-content: space-between; gap: 10px; margin-bottom: 12px; }\n.workspace-detail-drawer__header h3 { margin: 0; }\n.workspace-detail-back { padding: 6px 9px; border: 1px solid var(--workspace-border-strong); border-radius: 8px; background: var(--workspace-surface-muted); color: var(--workspace-accent); font-size: 11px; font-weight: 700; cursor: pointer; }\n.workspace-detail-back:hover { border-color: var(--workspace-accent); }\n.workspace-detail-drawer pre { max-height: 360px; margin: 0; overflow: auto; white-space: pre-wrap; overflow-wrap: anywhere; color: #334155; font: 12px/1.6 ui-monospace,SFMono-Regular,Menlo,Consolas,monospace; }\n.workspace--navigation-collapsed { grid-template-columns: 58px minmax(0,1fr); }\n.workspace--navigation-collapsed .workspace-navigation { padding: 16px 8px; overflow: hidden; }\n.workspace--navigation-collapsed .workspace-navigation__header { justify-content: center; margin: 0; }\n.workspace--navigation-collapsed .workspace-navigation__title,.workspace--navigation-collapsed .workspace-navigation__body,.workspace--navigation-collapsed .workspace-navigation__resize { display: none; }\n.workspace--navigation-collapsed .workspace-navigation__toggle { width: 40px; height: 40px; padding: 0; border-radius: 11px; font-size: 0; }\n.workspace--navigation-collapsed .workspace-navigation__toggle::before { content: '›'; font-size: 22px; line-height: 1; }\n.workspace--navigation-resizing,.workspace--navigation-resizing * { cursor: col-resize !important; user-select: none !important; }\n\n.workspace-trace-tree { max-height: none; overflow: visible; }\n.workspace-trace-tree ol { list-style:none; margin:0 0 0 17px; padding:0 0 0 12px; border-left:1px solid var(--workspace-border-strong); }\n.workspace-trace-node { list-style:none; padding:0; }\n.workspace-trace-branch > summary { cursor:pointer; list-style:none; }\n.workspace-trace-branch > summary::before { content:'›'; color:var(--workspace-muted); font-size:18px; }\n.workspace-trace-branch[open] > summary::before { transform:rotate(90deg); }\n.workspace-trace-branch > summary::-webkit-details-marker { display:none; }\n.workspace-trace-child-count { color:var(--workspace-muted); font-size:11px; background:var(--workspace-surface-muted); padding:1px 5px; border-radius:4px; }\n.workspace-metadata-value { text-align:left; }\n.workspace-metadata-value summary { cursor:pointer; color:var(--workspace-accent); }\n.workspace-metadata-value pre { max-width:100%; max-height:240px; overflow:auto; white-space:pre-wrap; overflow-wrap:anywhere; font:11px/1.6 ui-monospace,monospace; }\n.workspace-navigation__resize:hover { background:var(--workspace-accent-soft); }\n\n.workspace-trace-branch__detail { margin: -5px 0 4px 42px; }\n.workspace-detail-modal { position: fixed; inset: 0; z-index: 40; display: grid; place-items: center; padding: 24px; }\n.workspace-detail-modal__backdrop { position: absolute; inset: 0; background: rgba(15,23,42,.42); backdrop-filter: blur(3px); cursor: pointer; }\n.workspace-detail-modal__dialog { position: relative; display: grid; grid-template-rows: auto minmax(0,1fr) auto; width: min(900px, 100%); max-height: min(760px, calc(100vh - 48px)); border: 1px solid var(--workspace-border); border-radius: 18px; background: #fff; opacity: 1; isolation: isolate; box-shadow: 0 24px 80px rgba(15,23,42,.28); overflow: hidden; }\n.workspace-detail-modal__header,.workspace-detail-modal__footer { display: flex; align-items: center; justify-content: space-between; gap: 12px; padding: 16px 18px; border-bottom: 1px solid var(--workspace-border); }\n.workspace-detail-modal__header h3 { margin: 2px 0 0; font-size: 18px; }\n.workspace-detail-modal__footer { border-top: 1px solid var(--workspace-border); border-bottom: 0; color: var(--workspace-muted); font-size: 11px; }\n.workspace-detail-modal__close { width: 30px; height: 30px; border: 1px solid var(--workspace-border); border-radius: 8px; background: var(--workspace-surface-muted); color: var(--workspace-muted); font-size: 20px; line-height: 1; cursor: pointer; }\n.workspace-detail-modal__close:hover { color: var(--workspace-accent); border-color: var(--workspace-accent); }\n.workspace-detail-modal__body { min-height: 0; padding: 18px; overflow: auto; }\n.workspace-detail-modal__body pre { margin: 0; white-space: pre-wrap; overflow-wrap: anywhere; color: #334155; font: 12px/1.6 ui-monospace,SFMono-Regular,Menlo,Consolas,monospace; }\n.workspace-trace-record { display: grid; gap: 14px; }\n.workspace-trace-record dl { margin: 0; }\n.workspace-trace-record dl div { display: flex; justify-content: space-between; gap: 16px; padding: 7px 0; border-bottom: 1px solid var(--workspace-border); }\n.workspace-trace-record dt { color: var(--workspace-muted); }\n.workspace-trace-record dd { max-width: 72%; margin: 0; overflow-wrap: anywhere; text-align: right; }\n.workspace-trace-record__section { padding-top: 10px; border-top: 1px solid var(--workspace-border); }\n.workspace-trace-record__section h4 { margin: 0 0 7px; color: var(--workspace-text); font-size: 12px; }\n.workspace-trace-record__section details summary { cursor: pointer; color: var(--workspace-accent); font-size: 12px; font-weight: 700; }\n.workspace-detail-modal__empty { margin: 0; padding: 44px 20px; color: var(--workspace-muted); text-align: center; }\n.workspace-project__header { display: flex; align-items: center; gap: 6px; min-width: 0; }\n.workspace-project__header .workspace-project__action { flex: 1 1 auto; min-width: 0; }\n.workspace-project__launch { flex: 0 0 auto; width: 26px; height: 26px; border: 1px solid var(--workspace-border-strong); border-radius: 8px; background: var(--workspace-surface); color: var(--workspace-accent); font-size: 18px; line-height: 1; cursor: pointer; }\n.workspace-project__launch:hover { border-color: var(--workspace-accent); background: var(--workspace-accent-soft); }\n.workspace-launch-composer { position: fixed; inset: 0; z-index: 45; display: grid; place-items: center; padding: 24px; }\n.workspace-launch-composer__backdrop { position: absolute; inset: 0; background: rgba(15,23,42,.42); backdrop-filter: blur(3px); }\n.workspace-launch-composer__dialog { position: relative; display: grid; grid-template-rows: auto minmax(0,1fr) auto; width: min(560px, 100%); max-height: min(680px, calc(100vh - 48px)); overflow: hidden; border: 1px solid var(--workspace-border); border-radius: 18px; background: #fff; box-shadow: 0 24px 80px rgba(15,23,42,.28); }\n.workspace-launch-composer__dialog > header,.workspace-launch-composer__dialog > footer { display: flex; align-items: flex-start; justify-content: space-between; gap: 14px; padding: 18px 20px; border-bottom: 1px solid var(--workspace-border); }\n.workspace-launch-composer__dialog > header p { margin: 4px 0 0; color: var(--workspace-muted); font-size: 12px; }\n.workspace-launch-composer__body { display: grid; gap: 9px; min-height: 0; padding: 20px; overflow: auto; }\n.workspace-launch-composer__body label { color: var(--workspace-text); font-weight: 700; }\n.workspace-launch-composer__body textarea { width: 100%; resize: vertical; min-height: 120px; padding: 11px 12px; border: 1px solid var(--workspace-border-strong); border-radius: 10px; background: var(--workspace-surface-muted); color: var(--workspace-text); font: inherit; }\n.workspace-launch-composer__body textarea:focus { outline: 2px solid rgba(89,104,216,.24); border-color: var(--workspace-accent); }\n.workspace-launch-composer__defaults { display: grid; gap: 4px; margin-top: 6px; padding: 11px 12px; border: 1px solid var(--workspace-border); border-radius: 10px; background: #fafbff; color: var(--workspace-muted); font-size: 12px; }\n.workspace-launch-composer__defaults strong { color: var(--workspace-text); }\n.workspace-launch-composer__machine { display: grid; gap: 6px; margin-top: 2px; padding: 11px 12px; border: 1px solid var(--workspace-border); border-radius: 10px; background: var(--workspace-surface-muted); color: var(--workspace-muted); font-size: 12px; }\n.workspace-launch-composer__machine strong { color: var(--workspace-text); }\n.workspace-launch-composer__machine dl { display: grid; gap: 3px; margin: 0; }\n.workspace-launch-composer__machine dl div { display: flex; justify-content: space-between; gap: 12px; }\n.workspace-launch-composer__machine dt { color: var(--workspace-muted); }\n.workspace-launch-composer__machine dd { margin: 0; color: var(--workspace-text); text-align: right; overflow-wrap: anywhere; }\n.workspace-launch-composer__override,.workspace-launch-composer__document { display: grid; gap: 6px; padding: 11px 12px; border: 1px solid var(--workspace-border); border-radius: 10px; background: #fff; }\n.workspace-launch-composer__check { display: flex; align-items: center; gap: 7px; color: var(--workspace-text); font-size: 12px; font-weight: 700; }\n.workspace-launch-composer__check input { accent-color: var(--workspace-accent); }\n.workspace-launch-composer__override select { width: 100%; padding: 8px 10px; border: 1px solid var(--workspace-border-strong); border-radius: 8px; background: var(--workspace-surface-muted); color: var(--workspace-text); font: inherit; }\n.workspace-launch-composer__override select:disabled { opacity: .7; cursor: not-allowed; }\n.workspace-launch-composer__override small,.workspace-launch-composer__document small { color: var(--workspace-muted); font-size: 11px; }\n.workspace-launch-composer__document input[type=file] { width: 100%; color: var(--workspace-text); font-size: 12px; }\n.workspace-launch-composer__file { color: var(--workspace-accent); font-size: 11px; overflow-wrap: anywhere; }\n.workspace-launch-composer__dialog > footer { align-items: center; justify-content: flex-end; border-top: 1px solid var(--workspace-border); border-bottom: 0; }\n\n/* Workspace shell: one primary reading surface, with an on-demand inspector. */\n.workspace [hidden] { display: none !important; }\n.workspace button:focus-visible, .workspace [tabindex]:focus-visible { outline: 2px solid var(--workspace-accent); outline-offset: 3px; }\n.workspace-task-header { flex: 0 0 auto; gap: 16px; padding: 20px 28px; border: 0; border-bottom: 1px solid var(--workspace-border); border-radius: 0; box-shadow: none; min-height: 88px; }\n.workspace-task-header__identity { min-width: 0; flex: 1; }\n.workspace-task-header h2 { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; font-size: 21px; line-height: 1.4; }\n.workspace-task-header__meta { flex: 0 0 auto; }\n.workspace-task-header__meta > .workspace-status { min-height: 24px; padding: 4px 9px; line-height: 1.2; }\n.workspace-header-actions { flex: 0 0 auto; }\n.workspace-header-actions button { font-size: 12px; white-space: nowrap; }\n.workspace-stage { flex: 1 1 auto; min-height: 0; min-width: 0; display: flex; position: relative; overflow: hidden; }\n.workspace-primary { min-width: 0; min-height: 0; flex: 1 1 0; display: flex; flex-direction: column; background: #fbfcfe; }\n.workspace-observation-bar { flex: 0 0 auto; position: static; z-index: auto; margin: 0; padding: 10px 24px; min-height: 57px; border: 0; border-bottom: 1px solid var(--workspace-border); border-radius: 0; box-shadow: none; background: #fff; backdrop-filter: none; }\n.workspace-inspector-actions { display: flex; gap: 4px; }\n.workspace-inspector-action { border: 1px solid transparent; border-radius: 7px; padding: 6px 10px; background: transparent; color: var(--workspace-muted); font-size: 12px !important; cursor: pointer; }\n.workspace-inspector-action:hover, .workspace-inspector-action.is-active { background: var(--workspace-accent-soft); color: var(--workspace-accent); }\n.workspace-view { flex: 1 1 auto; min-height: 0; min-width: 0; position: relative; }\n.workspace-conversation { display: flex; flex-direction: column; margin: 0; }\n.workspace-view-heading { display: flex; align-items: center; justify-content: space-between; gap: 16px; padding: 24px 32px 12px; width: min(100%, 960px); margin: 0 auto; }\n.workspace-view-heading h3 { margin: 0; font-size: 14px; font-weight: 650; }\n.workspace-view-heading p { margin: 4px 0 0; font-size: 12px; color: var(--workspace-muted); }\n.workspace-view-count { font-size: 11px; white-space: nowrap; color: var(--workspace-muted); }\n.workspace-conversation-shell { flex: 1 1 auto; min-height: 0; max-height: none; width: 100%; padding: 16px max(28px, calc((100% - 900px) / 2)) 56px; border: 0; border-radius: 0; background: transparent; box-shadow: none; overflow-y: auto; overflow-x: hidden; scrollbar-gutter: stable; overflow-anchor: none; }\n.workspace-conversation-round { padding: 10px 0 22px; border: 0; border-radius: 0; background: transparent; box-shadow: none; }\n.workspace-conversation-round__header { padding: 0 0 12px; border: 0; margin: 0 0 14px; font-size: 11px; }\n.workspace-conversation-round__header::before { width: 5px; height: 5px; box-shadow: none; background: #a7b0c8; }\n.workspace-conversation-round__header .workspace-status { font-weight: 500; }\n.workspace-message { max-width: min(780px, 96%); }\n.workspace-message__body { padding: 13px 16px; border-radius: 12px; }\n.workspace-message--agent .workspace-message__body { background: #fff; border-color: #e0e5f0; box-shadow: 0 2px 5px rgba(24,34,48,.025); }\n.workspace-message--tool .workspace-message__body { background: #f1f3f8; border-color: transparent; font-size: 12px; color: #596579; }\n.workspace-message--tool .workspace-message__avatar { background: #e9edf4; color: #718096; }\n.workspace-message-inspect { margin-top: 10px; padding: 0; border: 0; background: transparent; color: var(--workspace-accent); font-size: 11px !important; cursor: pointer; }\n.workspace-new-content { position: absolute; bottom: 20px; left: 50%; transform: translateX(-50%); padding: 9px 15px; background: #fff; color: var(--workspace-accent); border: 1px solid #d9def4; border-radius: 999px; box-shadow: 0 4px 18px rgba(28,42,80,.12); font-size: 12px !important; cursor: pointer; white-space: nowrap; }\n.workspace-view--trace { padding: 24px; overflow: auto; }\n.workspace-view--trace .workspace-pane--trace { padding: 0; background: transparent; border: 0; box-shadow: none; }\n.workspace-view--trace .workspace-trace-viewer { max-height: none; overflow: visible; }\n.workspace-detail-panel { align-items: stretch; gap: 0; display: flex; flex: 0 0 clamp(320px, 29%, 400px); flex-direction: column; grid-template-columns: none; min-width: 0; min-height: 0; margin: 0; padding: 0; border: 0; border-left: 1px solid var(--workspace-border); border-radius: 0; background: #fff; overflow: hidden; }\n.workspace-inspector-header { display: flex; align-items: center; justify-content: space-between; gap: 12px; padding: 18px 20px; border-bottom: 1px solid var(--workspace-border); flex: 0 0 auto; min-height: 76px; }\n.workspace-inspector-header h3 { font-size: 15px; margin: 3px 0 0; }\n.workspace-inspector__body { flex: 1; min-height: 0; padding: 20px; overflow: auto; overflow-wrap: anywhere; }\n.workspace-inspector__body .workspace-pane { margin: 0 0 20px; padding: 0; border: 0; border-radius: 0; box-shadow: none; }\n.workspace-inspector__body .workspace-pane h3 { font-size: 13px; }\n.workspace-inspector__body pre { max-width: 100%; overflow: auto; white-space: pre-wrap; overflow-wrap: anywhere; font: 11px/1.7 ui-monospace, monospace; background: #f7f8fb; padding: 12px; border-radius: 8px; }\n.workspace-record-text { margin: 8px 0; white-space: pre-wrap; font-size: 13px; line-height: 1.8; }\n.workspace-record-detail__footer { margin-top: 24px; border-top: 1px solid var(--workspace-border); padding-top: 16px; }\n.workspace-inspector__body .workspace-tools-table { font-size: 10px; table-layout: fixed; width: 100%; overflow-wrap: anywhere; }\n.workspace-inspector__body .workspace-tools-invoke { display: grid; }\n.workspace-inspector__body .workspace-diagnostics-slot { padding: 0 0 20px; }\n.workspace-notices { flex: 0 0 auto; max-height: 22vh; overflow: auto; margin: 10px 20px 0; border-radius: 9px; }\n.workspace-mobile-nav-toggle, .workspace-navigation-backdrop, .workspace-inspector-backdrop { display: none; }\n@media (max-width:1200px) {\n  .workspace-detail-panel { position: absolute; top: 0; right: 0; bottom: 0; z-index: 31; width: min(400px, 88%); box-shadow: -16px 0 40px rgba(24,34,48,.12); }\n  .workspace-inspector-backdrop:not([hidden]) { display: block; position: absolute; inset: 0; z-index: 30; border: 0; background: rgba(24,34,48,.25); cursor: pointer; }\n}\n@media (max-width:760px) {\n  .workspace, .workspace.workspace--navigation-collapsed { grid-template-columns: minmax(0,1fr); }\n  .workspace-navigation { position: fixed; inset: 0 auto 0 0; z-index: 51; width: min(300px, 86vw); transform: translateX(-105%); transition: transform .18s ease; box-shadow: 12px 0 35px rgba(24,34,48,.12); }\n  .workspace--navigation-open .workspace-navigation { transform: none; }\n  .workspace--navigation-open .workspace-navigation-backdrop { display: block; position: fixed; inset: 0; z-index: 50; border: 0; background: rgba(24,34,48,.3); }\n  .workspace-navigation__resize { display: none; }\n  .workspace-mobile-nav-toggle { display: block; width: 32px; height: 32px; padding: 0; border: 1px solid var(--workspace-border); border-radius: 8px; background: #fff; color: var(--workspace-text); }\n  .workspace-task-header { display: flex; padding: 14px 14px; gap: 10px; min-height: 76px; }\n  .workspace-task-header h2 { font-size: 15px; }\n  .workspace-task-header__meta { display: none; }\n  .workspace-header-origin { display: none; }\n  .workspace-header-actions { gap: 6px; }\n  .workspace-header-actions button { padding: 7px 8px; font-size: 11px; }\n  .workspace-observation-bar { flex-direction: row; align-items: center; padding: 8px 10px; gap: 4px; }\n  .workspace-inspector-action { padding: 5px 7px; font-size: 11px !important; }\n  .workspace-pane-tab { padding: 7px 9px; font-size: 12px; }\n  .workspace-view-heading { padding: 18px 16px 8px; }\n  .workspace-view-heading p { font-size: 11px; }\n  .workspace-conversation-shell { padding: 12px 14px 56px; }\n  .workspace-message, .workspace-message--tool, .workspace-message--system { max-width: 100%; margin-left: 0; }\n  .workspace-message__head { flex-wrap: wrap; }\n  .workspace-message__head small { display: none; }\n  .workspace-view--trace { padding: 16px 12px; }\n  .workspace-trace-toolbar { align-items: stretch; flex-direction: column; }\n  .workspace-detail-panel { width: min(390px, 94vw); }\n}\n@media (prefers-reduced-motion:reduce) { .workspace-navigation { transition: none; } }\n\n.workspace--navigation-collapsed .workspace-navigation__header > div { display: none; }\n\n/* Message and tool hierarchy: quiet conversation, compact inspectable records. */\n.workspace-icon { display: block; flex: 0 0 auto; }\n.workspace summary:focus-visible { outline: 2px solid var(--workspace-accent); outline-offset: 3px; border-radius: 7px; }\n.workspace-dialogue { gap: 20px; }\n.workspace-conversation-round { padding-bottom: 28px; }\n.workspace-conversation-round__header { gap: 8px; padding-bottom: 0; margin-bottom: 22px; font-weight: 500; }\n.workspace-conversation-round__header::before { display: none; }\n.workspace-conversation-round__header::after { content: ''; height: 1px; flex: 1; background: var(--workspace-border); }\n.workspace-conversation-round__header > span { flex: 0 0 auto; margin: 0; }\n.workspace-conversation-round__header .workspace-status { padding: 0; color: var(--workspace-muted); background: transparent; font-size: 10px; }\n.workspace-message { max-width: min(780px, 100%); gap: 12px; line-height: 1.8; }\n.workspace-message__avatar { margin-top: 1px; border-radius: 50%; background: transparent; color: var(--workspace-muted); }\n.workspace-message--agent .workspace-message__avatar { background: #eeedf9; color: #7065a5; }\n.workspace-message--requester { max-width: min(680px, 90%); }\n.workspace-message--requester .workspace-message__avatar { display: none; }\n.workspace-message--requester .workspace-message__body { border-color: transparent; background: #f0f1f6; border-radius: 14px 14px 4px 14px; padding: 13px 18px; }\n.workspace-message--agent .workspace-message__body { padding: 0; border: 0; border-radius: 0; background: transparent; box-shadow: none; }\n.workspace-message__head { gap: 9px; margin-bottom: 8px; line-height: 1.6; }\n.workspace-message__head strong { font-size: 12px; font-weight: 600; }\n.workspace-message__head small { font-size: 10px; opacity: .75; }\n.workspace-message--requester .workspace-message__head { margin-bottom: 4px; }\n.workspace-message--requester .workspace-message__head strong { color: #7a8193; font-size: 11px; font-weight: 500; }\n.workspace-message__body p { font-size: 14px; line-height: 1.9; }\n.workspace-message__plan { margin-top: 16px; padding-left: 14px; border-left: 2px solid #e2e0ef; }\n.workspace-message__plan > span { color: var(--workspace-muted); font-size: 10px; }\n.workspace-message__steps { counter-reset: workspace-step; list-style: none; margin: 5px 0 0; display: grid; gap: 6px; font-size: 13px; }\n.workspace-message__steps li { display: flex; align-items: baseline; gap: 8px; }\n.workspace-message__steps li::before { counter-increment: workspace-step; content: counter(workspace-step, decimal-leading-zero); flex: 0 0 auto; color: #9693ab; font: 10px/1.8 ui-monospace, monospace; }\n.workspace-message-inspect { margin-top: 12px; color: #77758c; font-size: 11px !important; }\n.workspace-message-inspect:hover { color: var(--workspace-accent); }\n.workspace-chip { border-radius: 5px; padding: 1px 6px; font-weight: 500; background: #eeedf8; color: #787092; }\n.workspace-message.workspace-tool-card { display: block; width: calc(100% - 40px); max-width: 740px; margin-left: 40px; padding: 0; border: 1px solid #e4e7ed; border-radius: 10px; background: #fff; overflow: hidden; }\n.workspace-tool-card summary, .workspace-tool-catalog summary { list-style: none; cursor: pointer; }\n.workspace-tool-card summary::-webkit-details-marker, .workspace-tool-catalog summary::-webkit-details-marker { display: none; }\n.workspace-tool-card summary { padding: 10px 13px; }\n.workspace-tool-card__head { display: flex; align-items: center; gap: 9px; color: #8c91a1; line-height: 1.6; }\n.workspace-tool-card__head strong { flex: 1; min-width: 0; color: #5e6476; font-size: 12px; font-weight: 500; overflow-wrap: anywhere; }\n.workspace-tool-card__head .workspace-status { flex: 0 0 auto; padding: 2px 6px; font-size: 10px; font-weight: 500; }\n.workspace-tool-card__expand { flex: 0 0 auto; color: #9296a4; font-size: 10px; }\n.workspace-tool-card__expand > span, .workspace-tool-catalog__chevron { display: inline-block; transition: transform .15s; }\n.workspace-tool-card details[open] .workspace-tool-card__expand > span, .workspace-tool-catalog details[open] .workspace-tool-catalog__chevron { transform: rotate(180deg); }\n.workspace-tool-card__summary { display: block; margin: 4px 0 0 27px; white-space: pre-wrap; overflow-wrap: anywhere; color: #8b90a0; font-size: 12px; line-height: 1.7; }\n.workspace-tool-card summary:hover { background: #fafbfe; }\n.workspace-tool-card__detail { margin: 0 13px; padding: 9px 0 12px; border-top: 1px solid #eceef3; }\n.workspace-tool-card__detail pre { max-height: 320px; overflow: auto; white-space: pre-wrap; overflow-wrap: anywhere; margin: 0; padding: 10px 12px; border-radius: 6px; background: #f7f8fa; color: #687082; font: 11px/1.8 ui-monospace, monospace; }\n.workspace-tool-card__meta { margin: 5px 0 0; font-size: 10px; color: var(--workspace-muted); }\n.workspace-tool-card__detail .workspace-message-inspect { margin-top: 5px; }\n.workspace-message.workspace-tool-card.is-error { border-color: #e9c6c3; background: #fffafa; }\n.workspace-tool-card.is-error .workspace-tool-card__head strong { color: #a65350; }\n.workspace-tool-card__error { margin: 0; padding: 0 13px 12px; white-space: pre-wrap; overflow-wrap: anywhere; font-size: 12px; line-height: 1.8; color: var(--workspace-danger); }\n.workspace-tool-catalog { display: grid; gap: 9px; margin: 14px 0 24px; }\n.workspace-tool-catalog__card { min-width: 0; border: 1px solid #e6e8ef; border-radius: 10px; overflow: hidden; background: #fff; }\n.workspace-tool-catalog summary { padding: 12px; }\n.workspace-tool-catalog summary:hover { background: #fafbfe; }\n.workspace-tool-catalog__head { display: flex; align-items: center; gap: 8px; min-width: 0; color: #9296a6; }\n.workspace-tool-catalog__head code { flex: 1; min-width: 0; color: #555d71; font: 11px/1.6 ui-monospace, monospace; overflow-wrap: anywhere; }\n.workspace-tool-catalog__head .workspace-status { max-width: 100px; overflow-wrap: anywhere; font-size: 9px; font-weight: 500; border-radius: 5px; }\n.workspace-tool-catalog__summary { display: block; margin: 6px 0 0 26px; color: var(--workspace-muted); font-size: 12px; line-height: 1.8; overflow-wrap: anywhere; }\n.workspace-tool-catalog__chevron { flex: 0 0 auto; font-size: 12px; }\n.workspace-tool-catalog__card dl { margin: 0 12px 8px; padding-top: 4px; border-top: 1px solid var(--workspace-border); font-size: 11px; }\n.workspace-tool-catalog__card dl div:last-child { border-bottom: 0; }\n.workspace-tools-note { margin: 9px 0 0; color: var(--workspace-muted); font-size: 12px; line-height: 1.8; }\n.workspace-tools-invoke { gap: 9px; padding-top: 18px; border-top: 1px solid var(--workspace-border); }\n.workspace-tools-invoke label { color: var(--workspace-muted); font-size: 11px; line-height: 1.6; }\n.workspace-tools-invoke input { min-width: 0; width: 100%; box-sizing: border-box; padding: 9px 11px; border: 1px solid var(--workspace-border-strong); border-radius: 8px; color: var(--workspace-text); background: #fafbfe; font: 11px/1.6 ui-monospace, monospace; }\n.workspace-tools-invoke input:focus-visible { outline: 2px solid var(--workspace-accent); outline-offset: 2px; }\n@media (max-width:760px) {\n  .workspace-message { gap: 9px; }\n  .workspace-message--requester { max-width: 94%; }\n  .workspace-message__body p { font-size: 13px; }\n  .workspace-message.workspace-tool-card { margin-left: 0; width: 100%; }\n  .workspace-tool-card summary { padding: 10px 12px; }\n  .workspace-dialogue { gap: 17px; }\n}\n@media (prefers-reduced-motion:reduce) { .workspace-tool-card__expand > span, .workspace-tool-catalog__chevron { transition: none; } }\n\n.workspace-step-content { min-width: 0; flex: 1; }\n.workspace-step-summary { overflow-wrap: anywhere; }\n.workspace-step-state { display: block; margin-top: 2px; color: var(--workspace-muted); font-size: 11px; }\n.workspace-step-detail { margin-top: 3px; }\n.workspace-step-detail > summary { width: fit-content; color: #77758c; font-size: 10px; cursor: pointer; }\n.workspace-step-detail pre { max-height: 280px; max-width: 100%; overflow: auto; white-space: pre-wrap; overflow-wrap: anywhere; margin: 7px 0 0; padding: 10px 12px; border: 1px solid var(--workspace-border); border-radius: 8px; background: #f5f6fa; color: #687082; font: 11px/1.8 ui-monospace, monospace; }\n";const strict=(typeSymbol)=>({mode:"strict",typeSymbol,schema:{parse:(value)=>value},create:()=>({parse:(value)=>value})});const param=(name)=>({name,wire:name,source:"json",codec:strict("uniclaw-task-workbench/"+name)});const descriptor=(method,names)=>({id:"uniclawTaskPanel."+method,service:"uniclawTaskPanel",namespace:"uniclawTaskPanel",method,invocation:{kind:"direct"},parameters:names.map(param),result:strict("uniclaw-task-workbench/JsonValue")});const contribution={package:"@uniclaw/dsh-task-workbench",descriptors:[descriptor("workspace",[]),descriptor("session",["sessionId"]),descriptor("artifact",["sessionId","ref"]),descriptor("launch",["schemaVersion","contractVersion","launchRequestId","projectRef","testSetRef","taskRef","idempotencyKey","correlationId","requestedAt","environmentIntent","metadata","taskId"])]};function apply(ctx){const slots=ctx.get("slots");if(!slots)return;slots.inject("sidebar.footer.action",()=>slots.register({name:"sidebar.footer.action",id:"uniclaw-task-panel-entry",order:12,label:"UniClaw 工作空间"},()=>externalRequire("react").createElement("button",{type:"button",onClick:()=>{const panel=ctx.get("remote.uniclawTaskPanel");const mountApi=panel&&typeof panel.$mount==="function"?panel:ctx.get("remote");if(!mountApi||typeof mountApi.$mount!=="function")return;mountApi.$mount(contribution).then(()=>{const mountedPanel=ctx.get("remote.uniclawTaskPanel");const api=mountedPanel&&typeof mountedPanel.workspace==="function"?mountedPanel:mountApi;if(!api||typeof api.workspace!=="function"||typeof api.session!=="function"||typeof api.artifact!=="function"||typeof document==="undefined"||!document.body)return;bridge.createDshWorkspaceBrowserBridge({remote:api,container:document.body,styleText:workspaceStyleText}).start()})}},"UniClaw 工作空间")));slots.inject("shell.overlay",()=>slots.register({name:"shell.overlay",id:"uniclaw-task-panel-overlay",order:12,label:"UniClaw 工作空间"},()=>null))}return{apply,inject:["slots","remote"]}}});
