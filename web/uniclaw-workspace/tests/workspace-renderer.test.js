'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const { renderWorkspaceHtml } = require('../src/ui/render-workspace-html');
const { createWorkspaceViewModel } = require('../src/features/workspace-view-model');

function view(overrides = {}) { return createWorkspaceViewModel({ projects:{status:'ready',items:[{projectId:'p1',name:'项目',instances:[{productSessionId:'ps1',title:'任务',status:'running'}]}],errors:[]}, selection:{projectId:'p1',productSessionId:'ps1',taskInstances:{status:'ready',items:[{productSessionId:'ps1',title:'任务',status:'running'}],errors:[]}}, session:{status:'ready',session:{productSessionId:'ps1',metadata:{owner:'a'}}}, timeline:{status:'ready',items:[{kind:'request',summary:'请求'},{kind:'decision',summary:'决策'},{kind:'result',summary:'结果'}],errors:[]}, traces:{status:'ready',items:[{id:'t1',source:'uniclaw',detailRef:{refId:'trace-1'}}],errors:[]}, evidence:{status:'ready',items:[{id:'e1',title:'证据',detailRef:{refId:'evidence-1'}},{id:'e2',title:'受限',detailRef:{refId:'denied'},error:{code:'permission-denied',message:'拒绝'}}],errors:[]}, detail:{status:'idle'},...overrides }); }
test('makes conversation primary with an independent Trace view and a closed inspector', () => {
  const html = renderWorkspaceHtml(view());
  assert.match(html, /data-main-view="conversation"/);
  assert.match(html, /data-workspace-action="select-view"[^>]*data-view="trace"/);
  assert.match(html, /id="workspace-inspector"[^>]*hidden/);
  assert.match(html, /data-view-panel="trace"[^>]*hidden/);
  const open = renderWorkspaceHtml(view({ ui: { activeView: 'trace', activePane: 'metadata', inspectorOpen: true } }));
  assert.match(open, /workspace--inspector-open/);
  assert.match(open, /data-main-view="trace"/);
  assert.doesNotMatch(open, /id="workspace-inspector"[^>]*hidden/);
  assert.match(open, /data-workspace-action="close-inspector"/);
});
test('renders semantic workspace sections, primary views, on-demand inspector and trace timeline', () => { const html = renderWorkspaceHtml(view()); for (const selector of ['workspace-navigation','workspace-navigation__resize','workspace-navigation__toggle','workspace-task-header','workspace-observation-bar','workspace-diagnostics-slot','workspace-conversation-shell','workspace-conversation','workspace-pane-tabs','workspace-pane--trace','workspace-pane--evidence','workspace-pane--metadata','workspace-pane--execution','workspace-detail-panel','workspace-primary','workspace-view--trace','workspace-trace-source-switch']) assert.match(html, new RegExp(selector)); assert.match(html,/data-workspace-action="launch-task"/); assert.match(html,/请求/); assert.match(html,/决策/); assert.match(html,/结果/); assert.match(html,/<details class="workspace-trace-root"/); assert.match(html,/data-detail-ref="evidence-1"/); });
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
  assert.match(detail, /workspace-record-detail/);
  assert.match(detail, /返回 Evidence/);
  assert.match(detail, /data-workspace-action="close-detail"/);
});
test('shows device metadata in the overview and formats selected artifact detail separately', () => {
  const session = { status: 'ready', session: { productSessionId: 'ps1', metadata: { device: 'emulator-5556', androidApi: 35, wmSize: '1080x1920', real: true } } };
  const overview = renderWorkspaceHtml(view({ session, ui: { activePane: 'metadata', inspectorOpen: true } }));
  assert.match(overview, /设备与运行环境/);
  assert.match(overview, /emulator-5556/);
  assert.match(overview, /Android API/);
  const html = renderWorkspaceHtml(view({ session,
    ui: { activePane: 'trace', detailModalOpen: true },
    detail: { status: 'ready', detail: { name: 'facts.json', text: '{"outcome":"Completion","delivered":2}' } }
  }));
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
test('renders the rationale on the decision card and the receipt only on the submission card', () => {
  const html = renderWorkspaceHtml(view({ session: { status: 'ready', session: {
    conversationGroups: [{ stages: [
      { kind: 'decision', role: 'assistant', justification: '先检查 <Wi-Fi>', result: { text: 'submit_decision accepted' } },
      { kind: 'result', role: 'tool', text: 'submit_decision accepted' }
    ] }]
  } } }));
  const decision = html.match(/<article[^>]*data-kind="decision"[^>]*>(.*?)<\/article>/s)[1];
  assert.match(decision, /先检查 &lt;Wi-Fi&gt;/);
  assert.doesNotMatch(decision, /submit_decision accepted/);
  assert.equal((html.match(/submit_decision accepted/g) || []).length, 1);
  assert.doesNotMatch(html, /<Wi-Fi>/);
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

// PNL-012：诊断入口按钮可用性与诊断结果区块。
test('diagnose-task button stays disabled with a reason until the diagnosis gate opens', () => {
  const closed = renderWorkspaceHtml(view());
  assert.match(closed, /data-workspace-action="diagnose-task"[^>]*disabled/);
  assert.match(closed, /title="[^"]*(?:Run 目录|未加载)[^"]*"/);
  const open = renderWorkspaceHtml(view({
    tools: { status: 'ready', items: [{ name: 'run-diagnosis', status: 'implemented', invocation: 'model-procedure' }], errors: [] },
    ui: { activePane: 'tools', toolsRunDir: 'run-1' }
  }));
  assert.doesNotMatch(open, /data-workspace-action="diagnose-task"[^>]*disabled/);
});
test('diagnosis result block renders the non-authoritative notice before the text', () => {
  const html = renderWorkspaceHtml(view({
    tools: { status: 'ready', items: [{ name: 'run-diagnosis', status: 'implemented', invocation: 'model-procedure' }], errors: [] },
    ui: { activePane: 'tools', toolsRunDir: 'run-1' },
    diagnosis: { status: 'ready', data: { result: { model: 'prov/diag-1', text: '根因：A', reportRef: 'run-1/report/report.json' } }, errors: [] }
  }));
  assert.match(html, /workspace-diagnosis-result/);
  assert.match(html, /诊断输出：非权威观察，不构成 Runtime truth/);
  assert.match(html, /model=<code>prov\/diag-1<\/code>/);
  assert.match(html, /根因：A/);
  const failed = renderWorkspaceHtml(view({ ui: { activePane: 'tools' }, diagnosis: { status: 'error', data: null, errors: [{ code: 'diagnosis-report-missing', message: 'diagnosis-report-missing: invoke run-report first' }] } }));
  assert.match(failed, /diagnosis-report-missing/);
});


test('tool cards fold structured receipts, expose errors, and retain source-only status and record actions', () => {
  const html = renderWorkspaceHtml(view({ session: { status: 'ready', session: { conversationGroups: [{ groupId: 'g1', stages: [
    { kind: 'result', role: 'tool', label: '决策提交结果', text: '{"accepted":true,"value":"<script>"}', seq: 3 },
    { kind: 'result', role: 'tool', label: '能力结果', text: 'permission <denied>', isError: true, seq: 4 },
    { kind: 'tool-call', role: 'tool', label: '能力调用', text: '读取页面', status: 'running', seq: 5 }
  ] }] } } }));
  const cards = [...html.matchAll(/<article[^>]*data-kind="(?:result|tool-call)"[^>]*>(.*?)<\/article>/gs)].map(m => m[1]);
  assert.match(cards[0], /<details[^>]*data-reading-disclosure="tool:g1:3"[^>]*>/);
  assert.match(cards[0], /结构化内容/);
  assert.match(cards[0], /&lt;script&gt;/);
  assert.doesNotMatch(cards[0], /已完成|已成功|workspace-status--completed/);
  assert.match(cards[0], /data-workspace-action="inspect-conversation"/);
  assert.match(cards[1], /class="workspace-tool-card__error"[^>]*>permission &lt;denied&gt;/);
  assert.match(cards[1], /workspace-status--error/);
  assert.match(cards[2], /进行中/);
  assert.doesNotMatch(html, /<script>/);
});

test('tool catalog cards keep registry fields accessible without adding execution authority', () => {
  const html = renderWorkspaceHtml(view({ tools: { status: 'ready', items: [
    { name: 'run-<report>', summary: '生成报告', invocation: 'command', posture: 'readonly', status: 'implemented' },
    { name: 'future-tool', summary: '待接入', invocation: 'model-procedure', posture: 'analysis', status: 'planned' }
  ], errors: [] }, ui: { activePane: 'tools', inspectorOpen: true } }));
  assert.match(html, /workspace-tool-catalog/);
  assert.doesNotMatch(html, /<table class="workspace-tools-table"/);
  assert.match(html, /run-&lt;report&gt;/);
  assert.match(html, /data-reading-disclosure="catalog:0:run-&lt;report&gt;"/);
  for (const value of ['command', 'readonly', 'model-procedure', 'analysis', 'implemented', 'planned']) assert.match(html, new RegExp(value));
  assert.match(html, /data-workspace-action="generate-report"[^>]*disabled/);
  assert.doesNotMatch(html, /data-workspace-action="execute-tool"/);
});


test('uninspectable tool rows without event IDs receive distinct disclosure keys and no dead inspect actions', () => {
  const html = renderWorkspaceHtml({ conversationTimeline: { rounds: [{ inspectable: false, stages: [
    { kind: 'result', role: 'tool', text: '读取第一项' },
    { kind: 'result', role: 'tool', text: '读取第二项' }
  ] }] } });
  const keys = [...html.matchAll(/data-reading-disclosure="(tool:[^"]+)"/g)].map(m => m[1]);
  assert.equal(keys.length, 2);
  assert.equal(new Set(keys).size, 2);
  assert.doesNotMatch(html, /data-workspace-action="inspect-conversation"/);
});


test('renders legacy action steps as readable action and target with complete expandable parameters', () => {
  const html = renderWorkspaceHtml(view({ session: { status: 'ready', session: { conversationGroups: [{ groupId: 'g1', stages: [
    { kind: 'decision', role: 'agent', steps: [{ effectClass: 'tap', targetDescriptor: 'Network & internet', targetRole: 'ui.element', desiredState: 'checked' }, '保持当前设置'] }
  ] }] } } }));
  assert.match(html, /workspace-step-summary">点击 · Network &amp; internet/);
  assert.match(html, /目标状态：checked/);
  assert.match(html, /<details[^>]*data-reading-disclosure="step:g1:0:0"/);
  assert.match(html, /&quot;targetRole&quot;: &quot;ui.element&quot;/);
  assert.match(html, /保持当前设置/);
  assert.doesNotMatch(html, /执行成功/);
});


test('keeps supplied labels and unknown actions without inventing results or losing raw fields', () => {
  const html = renderWorkspaceHtml({ conversationTimeline: { rounds: [{ groupId: 'g2', stages: [{ kind: 'decision', role: 'agent', steps: [
    { label: '保留 <当前状态>', description: '其他说明', effectClass: 'tap', targetDescriptor: 'Wi-Fi', extra: '参数仍保留' },
    { effectClass: 'custom.action', targetRole: 'ui.<switch>', desiredState: '<enabled>' },
    { unrecognized: '<img src=x>' }, null
  ] }] }] } });
  assert.match(html, /workspace-step-summary">保留 &lt;当前状态&gt;/);
  assert.match(html, /workspace-step-summary">custom.action · 目标角色：ui.&lt;switch&gt;/);
  assert.match(html, /目标状态：&lt;enabled&gt;/);
  assert.match(html, /workspace-step-summary">结构化步骤/);
  assert.match(html, /参数仍保留/);
  assert.match(html, /&lt;img src=x&gt;/);
  assert.match(html, /未提供步骤内容/);
  assert.doesNotMatch(html, /<img|执行成功|已执行/);
});
