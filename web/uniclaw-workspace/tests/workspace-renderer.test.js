'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const { renderWorkspaceHtml } = require('../src/ui/render-workspace-html');
const { createWorkspaceViewModel } = require('../src/features/workspace-view-model');

function view(overrides = {}) { return createWorkspaceViewModel({ projects:{status:'ready',items:[{projectId:'p1',name:'项目',instances:[{productSessionId:'ps1',title:'任务',status:'running'}]}],errors:[]}, selection:{projectId:'p1',productSessionId:'ps1',taskInstances:{status:'ready',items:[{productSessionId:'ps1',title:'任务',status:'running'}],errors:[]}}, session:{status:'ready',session:{productSessionId:'ps1',metadata:{owner:'a'}}}, timeline:{status:'ready',items:[{kind:'request',summary:'请求'},{kind:'decision',summary:'决策'},{kind:'result',summary:'结果'}],errors:[]}, traces:{status:'ready',items:[{id:'t1',source:'uniclaw',detailRef:{refId:'trace-1'}}],errors:[]}, evidence:{status:'ready',items:[{id:'e1',title:'证据',detailRef:{refId:'evidence-1'}},{id:'e2',title:'受限',detailRef:{refId:'denied'},error:{code:'permission-denied',message:'拒绝'}}],errors:[]}, detail:{status:'idle'},...overrides }); }
test('renders semantic workspace sections, tabs, side rail and trace timeline', () => { const html = renderWorkspaceHtml(view()); for (const selector of ['workspace-navigation','workspace-navigation__resize','workspace-navigation__toggle','workspace-task-header','workspace-observation-bar','workspace-diagnostics-slot','workspace-conversation-shell','workspace-conversation','workspace-pane-tabs','workspace-pane--trace','workspace-pane--evidence','workspace-pane--metadata','workspace-pane--execution','workspace-side-rail','workspace-trace-source-switch']) assert.match(html, new RegExp(selector)); assert.match(html,/data-workspace-action="launch-task"/); assert.match(html,/请求/); assert.match(html,/决策/); assert.match(html,/结果/); assert.match(html,/<details class="workspace-trace-root"/); assert.match(html,/data-detail-ref="evidence-1"/); });
test('renders a project-local plus action and requirement-only launch composer', () => {
  const html = renderWorkspaceHtml(view({
    projects: { status: 'ready', items: [{ projectId: 'p1', name: '项目', instances: [] }], launchDefaults: { projectRef: { id: 'project/local' }, testSetRef: { id: 'testset/local', version: 'default' }, taskRef: { id: 'task/local/request', label: '默认任务' } }, errors: [] },
    selection: { projectId: 'p1', productSessionId: null, taskInstances: { status: 'ready', items: [], errors: [] } },
    ui: { launchComposer: { open: true, projectId: 'p1', requirement: '' } }
  }));
  assert.match(html, /data-workspace-action="launch-project"/);
  assert.match(html, /aria-label="在项目 项目中发起任务"/);
  assert.match(html, /workspace-launch-composer/);
  assert.match(html, /data-workspace-launch-requirement/);
  assert.match(html, /data-workspace-action="submit-launch"[^>]*disabled/);
  assert.doesNotMatch(html, /data-workspace-action="launch-task"/);
});
test('renders local machine and version configuration in the launch composer', () => {
  const html = renderWorkspaceHtml(view({
    projects: { status: 'ready', items: [{ projectId: 'p1', name: '项目', instances: [] }], launchDefaults: { projectRef: { id: 'project/local' }, testSetRef: { id: 'testset/local', version: 'default' }, taskRef: { id: 'task/local/request', label: '默认任务' } }, localConfig: { machine: 'darwin · arm64', nodeVersion: 'v24.0.0', hostVersion: 'dsh-local', workspaceVersion: '0.1.0', agentPreset: 'uniagent-prod', device: 'emulator-5556', deviceOptions: ['emulator-5556', 'emulator-5558'] }, errors: [] },
    selection: { projectId: 'p1', productSessionId: null, taskInstances: { status: 'ready', items: [], errors: [] } },
    ui: { launchComposer: { open: true, projectId: 'p1', requirement: '', deviceOverrideEnabled: true, deviceOverride: 'emulator-5558', requirementDocument: { name: '需求.md', sizeBytes: 12 }, documentError: null } }
  }));
  assert.match(html, /本地运行配置/);
  assert.match(html, /darwin · arm64/);
  assert.match(html, /v24\.0\.0/);
  assert.match(html, /uniagent-prod/);
  assert.match(html, /emulator-5556/);
  assert.match(html, /本次任务修改设备/);
  assert.match(html, /emulator-5558/);
  assert.match(html, /需求文档/);
  assert.match(html, /需求\.md/);
});
test('offers combined and split trace views and a return action from details', () => {
  const split = renderWorkspaceHtml(view({ ui: { activePane: 'trace', traceMode: 'split' } }));
  assert.match(split, /data-trace-mode="combined"/);
  assert.match(split, /data-trace-mode="split"[^>]*aria-pressed="true"/);
  assert.match(split, /workspace-trace-source-group/);
  const detail = renderWorkspaceHtml(view({ ui: { activePane: 'evidence', detailReturnPane: 'evidence', detailModalOpen: true }, detail: { status: 'ready', detail: { ok: true } } }));
  assert.match(detail, /workspace-detail-modal/);
  assert.match(detail, /返回 Evidence/);
  assert.match(detail, /data-workspace-action="close-detail"/);
});
test('formats structured detail text and highlights device metadata', () => {
  const html = renderWorkspaceHtml(view({
    session: { status: 'ready', session: { productSessionId: 'ps1', metadata: { device: 'emulator-5556', androidApi: 35, wmSize: '1080x1920', real: true } } },
    ui: { activePane: 'trace', detailModalOpen: true },
    detail: { status: 'ready', detail: { name: 'facts.json', text: '{"outcome":"Completion","delivered":2}' } }
  }));
  assert.match(html, /设备与运行环境/);
  assert.match(html, /emulator-5556/);
  assert.match(html, /Android API/);
  assert.match(html, /&quot;outcome&quot;: &quot;Completion&quot;/);
  assert.doesNotMatch(html, /\\&quot;outcome\\&quot;:/);
});
test('keeps truncated JSON detail readable without escaped string wrapping', () => {
  const html = renderWorkspaceHtml(view({
    ui: { activePane: 'trace', detailModalOpen: true },
    detail: { status: 'ready', detail: { name: 'trace.json', text: '{\n  "spans": [\n    {"spanId":"sp-0001"}' } }
  }));
  assert.match(html, /workspace-detail-modal__raw/);
  assert.match(html, /&quot;spanId&quot;:&quot;sp-0001&quot;/);
  assert.doesNotMatch(html, /\\n/);
});
test('renders an OTel-shaped trace context and span summary without widening the row', () => {
  const html = renderWorkspaceHtml(view({
    traces: { status: 'ready', items: [{ id: 'sp-1', source: 'uniclaw', label: 'world.reconcile', status: 'Completed', spanKind: 'internal', durationMs: 125, events: [{}], links: [], detailRef: { refId: 'trace.json' } }], context: { traceId: 'trc-1', rootSpanId: 'sp-1', runId: 'run-1', spanCount: 1 }, errors: [] },
    taskHeader: { source: 'Uni-Agent', title: '任务', status: 'ready', correlationStatus: 'correlated' }
  }));
  assert.match(html, /Trace context/);
  assert.match(html, /trc-1/);
  assert.match(html, /world\.reconcile/);
  assert.match(html, /Completed/);
  assert.match(html, /125ms/);
  assert.match(html, /events/);
});
test('renders Uni-Agent dialogue rounds with readable roles and runtime stages', () => {
  const html = renderWorkspaceHtml(view({ session: { status: 'ready', session: { productSessionId: 'ps1', conversationGroups: [{ round: 1, status: 'submitted', stages: [{ kind: 'request', role: 'requester', label: '调用方请求', text: '完成调研' }, { kind: 'decision', role: 'agent', label: 'Uni-Agent 决策', text: '选择搜索策略', decisionKind: 'act' }, { kind: 'result', role: 'tool', label: '提交结果', text: 'accepted' }] }], runStages: [{ kind: 'verification', label: '验证结果', status: '已验证', text: '证据齐全' }] }, errors: [] } }));
  assert.match(html, /workspace-conversation-round/);
  assert.match(html, /workspace-message--requester/);
  assert.match(html, /workspace-message--agent/);
  assert.match(html, /选择搜索策略/);
  assert.match(html, /执行与验证/);
  assert.match(html, /证据齐全/);
});
test('escapes text, attributes and detail refs', () => { const html = renderWorkspaceHtml(view({ selection:{projectId:'<p>',productSessionId:'ps&',taskInstances:{status:'ready',items:[{productSessionId:'ps&',title:'<script>alert(1)</script>'}],errors:[]}}, timeline:{status:'ready',items:[{kind:'request',summary:'<img src=x>'}],errors:[]} })); assert.doesNotMatch(html, /<script|<img/); assert.ok(html.includes('&lt;script&gt;alert(1)&lt;/script&gt;')); assert.match(html,/ps&amp;/); });
test('keeps uncorrelated and unavailable detail visible but inert', () => { const html = renderWorkspaceHtml(view({ evidence:{status:'partial',items:[{title:'孤立',correlationStatus:'uncorrelated',detailRef:{refId:'orphan'}},{title:'拒绝',detailRef:{refId:'denied'},error:{code:'permission-denied',message:'permission-denied'}}],errors:[{code:'stale',message:'旧快照'}]}, notices:[{level:'warning',code:'uncorrelated',message:'未关联'}] })); assert.match(html,/未关联/); assert.match(html,/permission-denied/); assert.match(html,/disabled aria-disabled/); assert.doesNotMatch(html,/data-detail-ref="orphan"/); });
test('renders grouped notices as a compact expandable status band with retry', () => {
  const timeout = { code: 'timeout', message: 'DSH session capability timed out', source: 'SessionQuery' };
  const html = renderWorkspaceHtml(view({
    session: { status: 'error', session: null, errors: [timeout] },
    timeline: { status: 'error', items: [], errors: [timeout] },
    traces: { status: 'error', items: [], errors: [timeout] },
    evidence: { status: 'error', items: [], errors: [timeout] }
  }));
  assert.match(html, /读取状态/);
  assert.match(html, /4 处/);
  assert.match(html, /查看受影响来源/);
  assert.match(html, /重试当前任务/);
  assert.equal((html.match(/class="workspace-notice workspace-notice--error"/g) || []).length, 1);
  assert.equal((html.match(/DSH session capability timed out/g) || []).length, 5); // headline plus four expandable occurrences
});
test('deduplicates flat task cards already nested in project navigation', () => {
  const html = renderWorkspaceHtml(view());
  assert.equal((html.match(/data-task-id="ps1"/g) || []).length, 1);
  assert.equal((html.match(/data-product-session-id="ps1"/g) || []).length, 1); // task header only; navigation uses selected-product-session-id
});
test('renders flat task instances when projects have no nested tasks and preserves uncorrelated cards', () => {
  const html = renderWorkspaceHtml(view({
    projects: { status: 'ready', items: [{ projectId: 'p1', name: '项目', instances: [] }], errors: [] },
    selection: { projectId: 'p1', productSessionId: 'ps2', taskInstances: { status: 'ready', items: [
      { productSessionId: 'ps2', title: '当前任务', status: 'running' },
      { id: 'orphan-1', title: '孤立任务', status: 'blocked', correlationStatus: 'uncorrelated' }
    ], errors: [] }
  }}));
  assert.match(html, /data-task-id="ps2"/);
  assert.match(html, /data-task-id="orphan-1"/);
  assert.match(html, /data-correlation-status="uncorrelated"/);
});
test('renderer and stylesheet have no host dependencies', () => { const fs = require('node:fs'); const source = fs.readFileSync(require('node:path').join(__dirname,'../src/ui/render-workspace-html.js'),'utf8'); assert.doesNotMatch(source,/react|document\.|window\.|fetch\(|node:fs|node:path|dsh|host/i); const css = fs.readFileSync(require('node:path').join(__dirname,'../src/styles/workspace.css'),'utf8'); for (const selector of ['.workspace-navigation','.workspace-timeline','.workspace-pane','.workspace-detail-action','@media']) assert.match(css,new RegExp(selector.replace(/[.*+?^${}()|[\]\\]/g,'\\$&'))); });
