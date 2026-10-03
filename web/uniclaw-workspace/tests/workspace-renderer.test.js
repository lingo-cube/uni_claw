'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const { renderWorkspaceHtml } = require('../src/ui/render-workspace-html');
const { createWorkspaceViewModel } = require('../src/features/workspace-view-model');

function view(overrides = {}) { return createWorkspaceViewModel({ projects:{status:'ready',items:[{projectId:'p1',name:'项目',instances:[{productSessionId:'ps1',title:'任务',status:'running'}]}],errors:[]}, selection:{projectId:'p1',productSessionId:'ps1',taskInstances:{status:'ready',items:[{productSessionId:'ps1',title:'任务',status:'running'}],errors:[]}}, session:{status:'ready',session:{productSessionId:'ps1',metadata:{owner:'a'}}}, timeline:{status:'ready',items:[{kind:'request',summary:'请求'},{kind:'decision',summary:'决策'},{kind:'result',summary:'结果'}],errors:[]}, traces:{status:'ready',items:[{id:'t1',source:'uniclaw',detailRef:{refId:'trace-1'}}],errors:[]}, evidence:{status:'ready',items:[{id:'e1',title:'证据',detailRef:{refId:'evidence-1'}},{id:'e2',title:'受限',detailRef:{refId:'denied'},error:{code:'permission-denied',message:'拒绝'}}],errors:[]}, detail:{status:'idle'},...overrides }); }
test('renders semantic workspace sections, tabs, side rail and trace timeline', () => { const html = renderWorkspaceHtml(view()); for (const selector of ['workspace-navigation','workspace-navigation__resize','workspace-navigation__toggle','workspace-task-header','workspace-observation-bar','workspace-diagnostics-slot','workspace-conversation-shell','workspace-conversation','workspace-pane-tabs','workspace-pane--trace','workspace-pane--evidence','workspace-pane--metadata','workspace-pane--execution','workspace-side-rail','workspace-detail-drawer']) assert.match(html, new RegExp(selector)); assert.match(html,/请求/); assert.match(html,/决策/); assert.match(html,/结果/); assert.match(html,/<details class="workspace-trace-root"/); assert.match(html,/data-detail-ref="evidence-1"/); });
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
