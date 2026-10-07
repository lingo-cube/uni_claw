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
  const hasSelectedTask = Boolean(header.productSessionId);
  const activePane = vm.activePane === 'detail' ? 'trace' : (vm.activePane || 'trace');
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
    <div class="workspace-navigation__header"><h1 class="workspace-navigation__title">${text(options.title, 'UniClaw Workspace')}</h1><button type="button" class="workspace-navigation__toggle" data-workspace-action="toggle-navigation" aria-expanded="true" aria-controls="workspace-navigation-content">隐藏任务栏</button></div>
    <div class="workspace-navigation__body" id="workspace-navigation-content"><div class="workspace-navigation__projects">${projectsHtml}</div><div class="workspace-navigation__tasks" data-selected-product-session-id="${escapeHtml(nav.selectedProductSessionId || '')}">${taskHtml}</div></div>
    <div class="workspace-navigation__resize" role="separator" aria-orientation="vertical" aria-label="调整任务栏宽度" data-workspace-resize="navigation" tabindex="0"></div>
  </nav>
  <main class="workspace-main">
    ${renderNotices(vm.notices)}
    <header class="workspace-task-header" data-selected-product-session-id="${escapeHtml(header.productSessionId || '')}">
      <div><p class="workspace-eyebrow">${text(header.source, 'Uni-Agent')}<span class="workspace-header-origin">${text(header.origin, '')}</span></p><h2>${text(header.title, '未选择任务')}</h2></div>
      <div class="workspace-header-actions">${hasSelectedTask ? `<button type="button" class="workspace-launch-action" data-workspace-action="launch-task"${header.launch?.enabled && header.launch.status !== 'loading' ? '' : ' disabled aria-disabled="true"'}>${header.launch?.status === 'loading' ? '发起中…' : '发起任务'}</button>` : ''}<button type="button" class="workspace-refresh-action" data-workspace-action="refresh">刷新</button></div>
      <div class="workspace-task-header__meta">${status(header.status)}<span class="workspace-correlation">${text(header.correlationStatus, 'unselected')}</span></div>
    </header>
    <div class="workspace-content"><div class="workspace-content__primary">
    <section class="workspace-observation-bar" aria-label="任务观察面板">
      <div class="workspace-pane-tabs" role="tablist" aria-label="任务信息标签页">
        ${renderTabButton('trace', 'Trace', activePane)}
        ${renderTabButton('evidence', 'Evidence', activePane)}
        ${renderTabButton('tools', '工具', activePane)}
      </div>
      <div class="workspace-diagnostics-slot"><span><strong>诊断入口</strong><small>Skill 接入后可分析当前任务</small></span><button type="button" class="workspace-diagnostics-action" data-workspace-action="diagnose-task"${vm.toolsPane.diagnosisAvailable ? '' : ' disabled aria-disabled="true"'} title="${text(vm.toolsPane.diagnosisAvailable ? '诊断当前任务（诊断输出：非权威观察，不构成 Runtime truth）' : vm.toolsPane.diagnosisUnavailableReason || '诊断当前任务不可用')}">诊断当前任务</button></div>
    </section>
    <section class="workspace-conversation" aria-labelledby="conversation-title"><h3 id="conversation-title">Uni-Agent 解决过程</h3><div class="workspace-conversation-shell">${renderTimeline(vm.conversationTimeline)}</div></section>
    <section class="workspace-inspector" aria-label="${text(options.panesLabel, '任务信息')}">
      <div class="workspace-inspector__main">
        <div class="workspace-tab-panels">
          ${renderTabPanel('trace', activePane, renderTracePane(vm.tracePane))}
          ${renderTabPanel('evidence', activePane, renderEvidencePane(vm.evidencePane))}
          ${renderTabPanel('tools', activePane, renderToolsPane(vm.toolsPane))}
        </div>
      </div>
    </section>
    </div><aside class="workspace-side-rail" aria-label="任务概览">
      ${renderExecutionPane(vm.executionPane)}
      ${renderMetadataPane(vm.metadataPane)}
    </aside></div>
  </main>
</div>${renderDetailModal(vm.detailActions)}${renderLaunchComposer(vm.launchComposer)}</div>`;
}

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
  return `<details class="${className}"${open}><summary><span>${text(title)}</span>${combined ? `<span class="workspace-trace-legend">${text(items.length)} 条</span>` : `<span class="workspace-trace-source-count">${text(items.length)} 条</span>`}</summary><ol class="workspace-trace-tree" role="tree">${roots.map((item) => renderTraceCard(item, children, 0)).join('')}</ol></details>`;
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
  return `<li class="workspace-trace-node" role="treeitem" aria-level="${depth + 1}">${nested.length ? `<details class="workspace-trace-branch"><summary class="workspace-trace-row">${content}<span class="workspace-trace-child-count">${nested.length}</span></summary>${detail}<ol role="group">${nested.map((child) => renderTraceCard(child, children, depth + 1)).join('')}</ol></details>` : `<div class="workspace-trace-row"><span class="workspace-trace-row__rail" aria-hidden="true"></span>${content}${detail}</div>`}</li>`;
}
function formatSpanDuration(item = {}) {
  if (!Number.isFinite(item.durationMs)) return '';
  return item.durationMs < 1000 ? `${item.durationMs}ms` : `${(item.durationMs / 1000).toFixed(2)}s`;
}

function renderEvidencePane(pane = {}) {
  return `<section class="workspace-pane workspace-pane--evidence" data-pane="evidence"><h3>Evidence</h3>${(pane.items || []).map((item) => { const action = item.detailAction || {}; return `<article class="workspace-evidence-card" data-correlation-status="${escapeHtml(item.correlationStatus || '')}"><h4>${text(item.title, '未命名证据')}</h4><p>${text(item.source, '未知来源')}</p>${action.enabled && action.detailRef ? `<button type="button" class="workspace-detail-action" data-workspace-action="resolve-detail" data-detail-ref="${escapeHtml(action.detailRef.refId || action.detailRef)}">查看明细</button>` : `<button type="button" class="workspace-detail-action" disabled aria-disabled="true">${text(action.reason, '详情不可用')}</button>`}${item.error ? `<p class="workspace-error">${text(item.error.message || item.error.code)}</p>` : ''}</article>`; }).join('')}</section>`;
}
function renderToolsPane(pane = {}) {
  const rows = (pane.items || []).map((tool) => `<tr><td><code>${text(tool.name)}</code></td><td>${text(tool.summary)}</td><td>${text(tool.invocation)}</td><td>${text(tool.posture)}</td><td>${status(tool.status)}</td></tr>`).join('');
  const report = pane.report || {};
  const result = report.data && report.data.result ? report.data.result : null;
  const resultBlock = report.status === 'idle' ? '' : `<div class="workspace-tools-result"><strong>生成结果</strong>${report.errors && report.errors.length ? `<ul class="workspace-notice__list">${errorText(report.errors)}</ul>` : ''}${result ? `<p>exitCode=${text(result.exitCode)}${result.reportMd ? ` · <code>${text(result.reportMd)}</code>` : ''}${result.reportJson ? ` · <code>${text(result.reportJson)}</code>` : ''}</p>${result.stderrTail ? `<pre class="workspace-tools-stderr">${text(result.stderrTail)}</pre>` : ''}` : (report.status === 'loading' ? '<p>生成中…</p>' : '')}</div>`;
  const diagnosisBlock = renderDiagnosisBlock(pane.diagnosis);
  return `<section class="workspace-pane workspace-pane--tools" data-pane="tools"><h3>工具</h3><p class="workspace-tools-note">Harness 工具暴露清单（tool-registry.yaml / ADR-0039）。</p><table class="workspace-tools-table"><thead><tr><th>名称</th><th>说明</th><th>调用形态</th><th>姿态</th><th>状态</th></tr></thead><tbody>${rows || '<tr><td colspan="5">未加载</td></tr>'}</tbody></table><div class="workspace-tools-invoke"><label for="workspace-tools-run-dir">Run 目录（runs 根下的目录名）</label><input id="workspace-tools-run-dir" type="text" data-workspace-tools-run-dir value="${text(pane.runDir)}" placeholder="run-20261003-075727-844"><button type="button" class="workspace-launch-action" data-workspace-action="generate-report"${pane.runDir && pane.runDir.trim() && report.status !== 'loading' ? '' : ' disabled aria-disabled="true"'}>${report.status === 'loading' ? '生成中…' : '生成全链路报告'}</button></div>${resultBlock}${diagnosisBlock}</section>`;
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
function renderDetailModal(detail = {}) {
  if (!detail.modalOpen) return '';
  const returnLabel = detail.returnPane === 'evidence' ? 'Evidence' : 'Trace';
  const content = detail.current ? renderDetailContent(detail.current) : `<p class="workspace-detail-modal__empty">${text(detail.status === 'loading' ? '正在加载详情' : '暂无可展示的详情')}</p>`;
  return `<div class="workspace-detail-modal" role="presentation"><div class="workspace-detail-modal__backdrop" data-workspace-action="close-detail"></div><section class="workspace-detail-modal__dialog" role="dialog" aria-modal="true" aria-labelledby="workspace-detail-title" data-detail-status="${escapeHtml(detail.status || 'idle')}"><header class="workspace-detail-modal__header"><div><p class="workspace-eyebrow">${text(returnLabel)}</p><h3 id="workspace-detail-title">记录详情</h3></div><button type="button" class="workspace-detail-modal__close" data-workspace-action="close-detail" aria-label="关闭详情">×</button></header><div class="workspace-detail-modal__body">${content}</div><footer class="workspace-detail-modal__footer"><span>${text(detail.status === 'loading' ? '读取中' : '可复制或展开查看原始记录')}</span><button type="button" class="workspace-detail-back" data-workspace-action="close-detail">返回 ${returnLabel}</button></footer></section></div>`;
}
function formatDetail(value) {
  if (!value || typeof value !== 'object' || typeof value.text !== 'string') return value;
  try { return { ...value, text: JSON.parse(value.text) }; } catch { return { ...value, __rawText: true }; }
}
function renderDetailContent(value) {
  if (value?.format === 'trace-record') return renderTraceRecord(value.record || {});
  const formatted = formatDetail(value);
  if (formatted && typeof formatted === 'object' && formatted.__rawText === true && typeof formatted.text === 'string') {
    const { text: rawText, __rawText, ...metadata } = formatted;
    return `<div class="workspace-detail-modal__raw"><pre>${text(JSON.stringify(metadata, null, 2))}</pre><pre>${text(rawText)}</pre></div>`;
  }
  return `<pre>${text(JSON.stringify(formatted, null, 2))}</pre>`;
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
