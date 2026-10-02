'use strict';
const { WorkspaceQueryCore } = require('../core');
const { createWorkspaceApp } = require('../app/create-workspace-app');
function envelope(value, capability) { if (value && typeof value === 'object' && typeof value.ok === 'boolean') return value; return { ok: false, error: { schemaVersion: 'uniclaw.workspace.query-error.v1', contractVersion: 'uniclaw.workspace.contract.v1', code: 'unavailable', message: 'Malformed DSH response', retryable: false, source: capability }, capability, source: 'dsh' }; }
function createCapabilities(remote) {
  if (!remote || typeof remote.workspace !== 'function' || typeof remote.session !== 'function' || typeof remote.artifact !== 'function') throw new TypeError('remote workspace/session/artifact methods are required');
  const call = (method, args, capability) => Promise.resolve().then(() => remote[method](...args)).then((value) => envelope(value, capability));
  return {
    TaskQuery: { listProjects: () => call('workspace', [], 'TaskQuery') },
    SessionQuery: { getSession: ({ productSessionId }) => call('session', [productSessionId], 'SessionQuery').then((r) => r.ok ? { ...r, data: r.data.session || r.data } : r), getTimeline: ({ productSessionId }) => call('session', [productSessionId], 'SessionQuery').then((r) => r.ok ? { ...r, data: r.data.conversation || [] } : r) },
    TraceQuery: { getTraces: ({ productSessionId }) => call('session', [productSessionId], 'TraceQuery').then((r) => r.ok ? { ...r, data: { traces: [...(r.data.dshTrace || []), ...(r.data.uniclawTrace || []), ...(r.data.uniflowTrace || [])] } } : r) },
    EvidenceQuery: { getEvidence: ({ productSessionId }) => call('session', [productSessionId], 'EvidenceQuery').then((r) => r.ok ? { ...r, data: { evidence: r.data.evidence || [] } } : r) },
    DetailQuery: { resolveDetail: ({ productSessionId, detailRef }) => call('artifact', [productSessionId, detailRef.refId || detailRef], 'DetailQuery') }
  };
}
function createDshWorkspaceBrowserBridge({ remote, container, render, viewOptions } = {}) {
  if (!container || typeof container.appendChild !== 'function') throw new TypeError('container is required');
  const app = createWorkspaceApp({ queryCore: new WorkspaceQueryCore(createCapabilities(remote)), render, viewOptions, mount: ({ html }) => { container.innerHTML = html; } });
  return Object.freeze({ start: () => app.start(), stop: () => app.stop(), getApp: () => app });
}
module.exports = { createDshWorkspaceBrowserBridge, createCapabilities };
