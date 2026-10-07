'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const { createWorkspaceViewModel } = require('../src/features/workspace-view-model');

function state(overrides = {}) {
  return {
    projects: { status: 'ready', items: [{ id: 'p1', name: '调研项目', tasks: [{ productSessionId: 'ps1', title: 'Uni-Agent 调研', status: 'running' }, { instanceId: 'observed:dsh2', observed: true, correlationStatus: 'uncorrelated', title: '孤立 DSH 观察' }] }], errors: [] },
    selection: { projectId: 'p1', productSessionId: 'ps1', taskInstances: { status: 'ready', items: [{ productSessionId: 'ps1', title: 'Uni-Agent 调研', projectId: 'p1', metadata: { owner: 'team-a' } }, { instanceId: 'observed:dsh2', observed: true, correlationStatus: 'uncorrelated', title: '孤立 DSH 观察' }], errors: [] } },
    session: { status: 'ready', session: { productSessionId: 'ps1', source: 'uniclaw', authority: 'runtime', metadata: { priority: 'high' } }, errors: [] },
    timeline: { status: 'ready', items: [{ id: 'r', kind: 'request', summary: '调研请求', detailRef: { source: 'u', refId: 'r' } }, { id: 'd', kind: 'decision', summary: '选择搜索策略' }, { id: 'o', kind: 'result', summary: '得到证据' }], errors: [] },
    traces: { status: 'ready', items: [{ id: 'a', source: 'uniclaw', authority: 'runtime', correlationId: 'c1' }, { id: 'b', source: 'dsh', authority: 'host', detailRef: { source: 'dsh', refId: 'b' } }, { id: 'c', source: 'uniflow', authority: 'harness' }], errors: [] },
    evidence: { status: 'partial', items: [{ id: 'e1', title: '报告', source: 'uniclaw', productSessionId: 'ps1', detailRef: { source: 'u', refId: 'e1' } }, { id: 'e2', title: '受限文件', detailRef: { source: 'u', refId: 'denied' }, error: { code: 'permission-denied', message: '拒绝' } }, { id: 'e3', title: '孤立证据', correlationStatus: 'uncorrelated' }], errors: [{ code: 'stale', message: '旧快照' }] },
    detail: { status: 'idle', detail: null, errors: [] },
    ...overrides
  };
}

test('projects complete controller state into renderer-neutral sections', () => {
  const input = state();
  const before = JSON.stringify(input);
  const view = createWorkspaceViewModel(input);
  assert.equal(view.navigation.selectedProductSessionId, 'ps1');
  assert.equal(view.taskHeader.productSessionId, 'ps1');
  assert.deepEqual(Object.keys(view.conversationTimeline.groups), ['request', 'decision', 'result']);
  assert.equal(view.conversationTimeline.groups.request[0].summary, '调研请求');
  assert.deepEqual(Object.keys(view.tracePane.groups).sort(), ['dsh', 'uniclaw', 'uniflow']);
  assert.equal(view.evidencePane.items[0].detailAction.enabled, true);
  assert.equal(view.evidencePane.items[1].detailAction.enabled, false);
  assert.equal(view.evidencePane.items[1].detailAction.reason, 'permission-denied');
  assert.equal(view.evidencePane.items[2].detailAction.enabled, false);
  assert.equal(view.evidencePane.items[2].detailAction.reason, 'uncorrelated');
  assert.equal(view.metadataPane.items.owner, 'team-a');
  assert.equal(view.metadataPane.items.priority, 'high');
  assert.equal(view.activePane, 'trace');
  assert.equal(view.executionPane.status, 'empty');
  assert.equal(view.taskHeader.launch.enabled, false);
  assert.equal(view.status, 'partial');
  assert.equal(JSON.stringify(input), before);
});

test('projects launch relations and metadata provenance without flattening availability', () => {
  const view = createWorkspaceViewModel(state({
    session: { status: 'ready', session: {
      productSessionId: 'ps1', runId: 'run-1', projectRef: 'project/p', testSetRef: 'testset/p', taskRef: 'task/p/t',
      storageNamespaceRef: 'local:task-instance/i', launchStages: [{ stage: 'run-bound', status: 'succeeded' }],
      metadata: { priority: 'high' },
      metadataClaims: [
        { key: 'agentPreset', value: 'uniagent-prod', valueOrigin: 'configured', availability: 'present', source: 'config', authority: 'host' },
        { key: 'device', value: 'emulator-5556', valueOrigin: 'observed', availability: 'present', source: 'artifact', authority: 'dsh-host' },
        { key: 'runId', value: 'run-1', valueOrigin: 'generated', availability: 'present', source: 'runtime', authority: 'uniclaw-runtime' },
        { key: 'outcome', value: 'Completion', valueOrigin: 'derived', availability: 'not-collected', source: 'runtime', authority: 'uniclaw-runtime' }
      ]
    }, errors: [] }
  }));
  assert.equal(view.taskHeader.runId, 'run-1');
  assert.deepEqual(view.taskHeader.logicalRefs, { projectRef: 'project/p', testSetRef: 'testset/p', taskRef: 'task/p/t', storageNamespaceRef: 'local:task-instance/i' });
  assert.equal(view.metadataPane.claims.find((claim) => claim.key === 'device').valueOrigin, 'observed');
  assert.equal(view.metadataPane.claims.find((claim) => claim.key === 'outcome').availability, 'not-collected');
  assert.equal(view.metadataPane.launchStages[0].stage, 'run-bound');
});

test('enables launch only for complete logical launch references', () => {
  const view = createWorkspaceViewModel(state({
    selection: { projectId: 'p1', productSessionId: 'ps1', taskInstances: { status: 'ready', items: [{ productSessionId: 'ps1', projectRef: { id: 'project/p' }, testSetRef: { id: 'testset/p', version: 'default' }, taskRef: { id: 'task/p/t' } }], errors: [] } }
  }));
  assert.equal(view.taskHeader.launch.enabled, true);
  assert.deepEqual(view.taskHeader.launch.projectRef, { id: 'project/p' });
  assert.deepEqual(view.taskHeader.launch.testSetRef, { id: 'testset/p', version: 'default' });
  assert.deepEqual(view.taskHeader.launch.taskRef, { id: 'task/p/t' });
  const unsafe = createWorkspaceViewModel(state({
    selection: { projectId: '/physical/path', productSessionId: 'ps1', taskInstances: { status: 'ready', items: [{ productSessionId: 'ps1', projectRef: { path: '/physical/path' }, testSetRef: { id: 'testset/p', version: 'default' }, taskRef: { id: 'task/p/t' } }], errors: [] } }
  }));
  assert.equal(unsafe.taskHeader.launch.enabled, false);
});

test('uses Host-provided local launch defaults when the selected task has no refs', () => {
  const defaults = {
    projectRef: { id: 'project/local-default' },
    testSetRef: { id: 'testset/local-default', version: 'default' },
    taskRef: { id: 'task/local-default/request', label: '本地默认任务' }
  };
  const view = createWorkspaceViewModel(state({
    projects: { status: 'ready', items: [{ projectId: 'p1', name: '项目', instances: [{ productSessionId: 'ps1', title: '已有任务' }] }], launchDefaults: defaults, errors: [] },
    selection: { projectId: 'p1', productSessionId: 'ps1', taskInstances: { status: 'ready', items: [{ productSessionId: 'ps1', title: '已有任务' }], errors: [] } }
  }));
  assert.equal(view.taskHeader.launch.enabled, true);
  assert.deepEqual(view.taskHeader.launch.projectRef, defaults.projectRef);
  assert.deepEqual(view.taskHeader.launch.testSetRef, defaults.testSetRef);
  assert.deepEqual(view.taskHeader.launch.taskRef, defaults.taskRef);
});

test('keeps uncorrelated instances visible without inventing product identity', () => {
  const view = createWorkspaceViewModel(state());
  const orphan = view.navigation.taskInstances.find((item) => item.observed);
  assert.equal(orphan.productSessionId, null);
  assert.equal(orphan.correlationStatus, 'uncorrelated');
  assert.equal(view.navigation.projects[0].tasks[1].productSessionId, null);
});

test('blocks detail action for uncorrelated evidence even when a reference exists', () => {
  const view = createWorkspaceViewModel(state({ evidence: { status: 'ready', items: [{ id: 'orphan-evidence', correlationStatus: 'uncorrelated', detailRef: { source: 'u', refId: 'orphan.md' } }], errors: [] } }));
  assert.equal(view.evidencePane.items[0].detailAction.enabled, false);
  assert.equal(view.evidencePane.items[0].detailAction.reason, 'uncorrelated');
  assert.ok(view.notices.some((notice) => notice.code === 'uncorrelated'));
  assert.equal(view.status, 'partial');
});

test('preserves loading, errors, stale and missing detail states', () => {
  const view = createWorkspaceViewModel(state({ timeline: { status: 'error', items: [], errors: [{ code: 'unavailable', message: 'offline' }] }, traces: { status: 'loading', items: [], errors: [] }, evidence: { status: 'ready', items: [{ id: 'e', title: 'missing' }], errors: [] } }));
  assert.equal(view.conversationTimeline.status, 'error');
  assert.equal(view.tracePane.status, 'loading');
  assert.equal(view.evidencePane.items[0].detailAction.enabled, false);
  assert.equal(view.evidencePane.items[0].detailAction.reason, 'not-found');
  assert.ok(view.notices.some((notice) => notice.code === 'unavailable'));
  assert.equal(view.status, 'loading');
});

test('aggregates repeated capability errors into one notice with occurrence details', () => {
  const timeout = { code: 'timeout', message: 'DSH session capability timed out', source: 'SessionQuery' };
  const view = createWorkspaceViewModel(state({
    session: { status: 'error', session: null, errors: [timeout] },
    timeline: { status: 'error', items: [], errors: [timeout] },
    traces: { status: 'error', items: [], errors: [timeout] },
    evidence: { status: 'error', items: [], errors: [timeout] }
  }));
  const grouped = view.notices.find((notice) => notice.code === 'timeout');
  assert.equal(grouped.count, 4);
  assert.equal(grouped.occurrences.length, 4);
  assert.deepEqual(grouped.sources, ['SessionQuery']);
});

test('projects Uni-Agent conversation groups and runtime stages into the main dialogue', () => {
  const view = createWorkspaceViewModel(state({
    session: {
      status: 'ready',
      session: {
        productSessionId: 'ps1',
        conversationGroups: [{
          groupId: 'round-1',
          round: 1,
          status: 'submitted',
          stages: [
            { kind: 'request', role: 'request', label: '调用方请求', objective: '完成 Settings 覆盖' },
            { kind: 'decision', role: 'assistant', label: 'Uni-Agent 决策', decisionKind: 'act', justification: '先处理弹窗' },
            { kind: 'result', role: 'tool', label: '提交结果', text: 'decision accepted' }
          ]
        }],
        runStages: [{ kind: 'verification', label: '验证结果', status: '已验证', text: '覆盖步骤 3/3' }]
      },
      errors: []
    }
  }));
  assert.equal(view.conversationTimeline.mode, 'agent-conversation');
  assert.equal(view.conversationTimeline.rounds[0].stages[0].text, '完成 Settings 覆盖');
  assert.equal(view.conversationTimeline.rounds[0].stages[0].role, 'requester');
  assert.equal(view.conversationTimeline.rounds[0].stages[1].role, 'agent');
  assert.equal(view.conversationTimeline.rounds[0].stages[1].decisionKind, 'act');
  assert.equal(view.conversationTimeline.runStages[0].kind, 'verification');
});

test('does not expose renderer or host dependencies', () => {
  const source = require('node:fs').readFileSync(require('node:path').join(__dirname, '../src/features/workspace-view-model.js'), 'utf8');
  assert.doesNotMatch(source, /react|document\.|window\.|fetch\(|node:fs|node:path|dsh|host/i);
});

// PNL-012：诊断入口门控投影。
test('diagnosis gate requires loaded tools, an implemented model-procedure tool, and a run dir', () => {
  const toolsReady = { status: 'ready', items: [{ name: 'run-diagnosis', status: 'implemented', invocation: 'model-procedure' }], errors: [] };
  const open = createWorkspaceViewModel({ tools: toolsReady, ui: { toolsRunDir: 'run-1' } });
  assert.equal(open.toolsPane.diagnosisAvailable, true);
  assert.equal(open.toolsPane.diagnosisUnavailableReason, '');
  const noRunDir = createWorkspaceViewModel({ tools: toolsReady, ui: { toolsRunDir: '  ' } });
  assert.equal(noRunDir.toolsPane.diagnosisAvailable, false);
  assert.match(noRunDir.toolsPane.diagnosisUnavailableReason, /Run 目录/);
  const notLoaded = createWorkspaceViewModel({ tools: { status: 'idle', items: [], errors: [] }, ui: { toolsRunDir: 'run-1' } });
  assert.equal(notLoaded.toolsPane.diagnosisAvailable, false);
  assert.match(notLoaded.toolsPane.diagnosisUnavailableReason, /未加载/);
  const noProcedureTool = createWorkspaceViewModel({ tools: { status: 'ready', items: [{ name: 'run-report', status: 'implemented', invocation: 'deterministic-script' }], errors: [] }, ui: { toolsRunDir: 'run-1' } });
  assert.equal(noProcedureTool.toolsPane.diagnosisAvailable, false);
  assert.match(noProcedureTool.toolsPane.diagnosisUnavailableReason, /model-procedure/);
  const planned = createWorkspaceViewModel({ tools: { status: 'ready', items: [{ name: 'run-diagnosis', status: 'planned', invocation: 'model-procedure' }], errors: [] }, ui: { toolsRunDir: 'run-1' } });
  assert.equal(planned.toolsPane.diagnosisAvailable, false);
});
