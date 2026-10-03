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
    <h1 class="workspace-navigation__title">${text(options.title, 'UniClaw Workspace')}</h1>
    <div class="workspace-navigation__projects">${projectsHtml}</div>
    <div class="workspace-navigation__tasks" data-selected-product-session-id="${escapeHtml(nav.selectedProductSessionId || '')}">${taskHtml}</div>
  </nav>
  <main class="workspace-main">
    ${renderNotices(vm.notices)}
    <header class="workspace-task-header" data-selected-product-session-id="${escapeHtml(header.productSessionId || '')}">
      <div><p class="workspace-eyebrow">${text(header.source, 'Uni-Agent')}<span class="workspace-header-origin">${text(header.origin, '')}</span></p><h2>${text(header.title, '未选择任务')}</h2></div>
      <button type="button" class="workspace-refresh-action" data-workspace-action="refresh">刷新</button>
      <div class="workspace-task-header__meta">${status(header.status)}<span class="workspace-correlation">${text(header.correlationStatus, 'unselected')}</span></div>
    </header>
    <section class="workspace-observation-bar" aria-label="任务观察面板">
      <div class="workspace-pane-tabs" role="tablist" aria-label="任务信息标签页">
        ${renderTabButton('trace', 'Trace', vm.activePane)}
        ${renderTabButton('evidence', 'Evidence', vm.activePane)}
        ${renderTabButton('detail', '详情', vm.activePane)}
      </div>
      <div class="workspace-diagnostics-slot"><span><strong>诊断入口</strong><small>Skill 接入后可分析当前任务</small></span><button type="button" class="workspace-diagnostics-action" data-workspace-action="diagnose-task" disabled aria-disabled="true">诊断当前任务</button></div>
    </section>
    <section class="workspace-conversation" aria-labelledby="conversation-title"><h3 id="conversation-title">Uni-Agent 解决过程</h3><div class="workspace-conversation-shell">${renderTimeline(vm.conversationTimeline)}</div></section>
    <section class="workspace-inspector" aria-label="${text(options.panesLabel, '任务信息')}">
      <div class="workspace-inspector__main">
        <div class="workspace-tab-panels">
          ${renderTabPanel('trace', vm.activePane, renderTracePane(vm.tracePane))}
          ${renderTabPanel('evidence', vm.activePane, renderEvidencePane(vm.evidencePane))}
          ${renderTabPanel('detail', vm.activePane, renderDetail(vm.detailActions))}
        </div>
      </div>
      <aside class="workspace-side-rail" aria-label="任务概览">
        ${renderMetadataPane(vm.metadataPane)}
        ${renderExecutionPane(vm.executionPane)}
      </aside>
    </section>
  </main>
</div>`;
}

function renderProject(project, renderedTaskKeys, selectedProductSessionId) {
  const tasks = (project.tasks || []).filter((task) => {
    const key = taskIdentity(task);
    if (!key) return true;
    if (renderedTaskKeys.has(key)) return false;
    renderedTaskKeys.add(key);
    return true;
  });
  return `<section class="workspace-project" data-project-id="${escapeHtml(project.projectId || '')}"><button type="button" class="workspace-project__action" data-workspace-action="select-project" data-project-id="${escapeHtml(project.projectId || '')}">${text(project.name, '未命名项目')}</button>${tasks.map((task) => renderTask(task, selectedProductSessionId)).join('')}</section>`;
}
function taskIdentity(task = {}) { return task.productSessionId || task.id || null; }
function renderTask(task, selectedProductSessionId = null) {
  const orphan = task.correlationStatus === 'uncorrelated' || !task.productSessionId;
  const selected = !orphan && selectedProductSessionId && selectedProductSessionId === task.productSessionId;
  return `<article class="workspace-task-card${orphan ? ' is-uncorrelated' : ''}${selected ? ' is-selected' : ''}" data-task-id="${escapeHtml(task.id || '')}" data-correlation-status="${escapeHtml(task.correlationStatus || 'unknown')}"><button type="button" class="workspace-task-card__action" data-workspace-action="select-task" data-product-session-id="${escapeHtml(task.productSessionId || '')}"${orphan ? ' disabled aria-disabled="true"' : ''}${selected ? ' aria-current="true"' : ''}><h3>${text(task.title, '未命名任务')}</h3><div class="workspace-task-card__meta"><span>${text(task.productSessionId, orphan ? '未关联 ProductSession' : '未知 ProductSession')}</span>${status(task.status)}</div></button></article>`;
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
  const groups = Object.keys(pane.groups || {});
  const total = pane.groups ? groups.reduce((n, key) => n + pane.groups[key].length, 0) : 0;
  return `<section class="workspace-pane workspace-pane--trace" data-pane="trace"><div class="workspace-pane__heading"><div><h3>Trace</h3><p class="workspace-pane__subheading">按来源折叠查看运行与诊断轨迹</p></div><span class="workspace-pane__count">${total}</span></div>${groups.length ? groups.map((source) => renderTraceGroup(source, pane.groups[source] || [])).join('') : '<p class="workspace-empty">暂无可展示的 Trace 事件</p>'}</section>`;
}
function renderTraceGroup(source, items) {
  const open = source !== 'uniflow';
  const label = source === 'uniclaw' ? 'UniClaw Runtime' : source === 'uniflow' ? 'UniFlow' : source;
  const byId = new Map(items.filter((item) => item.id).map((item) => [item.id, item]));
  return `<details class="workspace-trace-group" data-source="${escapeHtml(source)}"${open ? ' open' : ''}><summary><span>${text(label)}</span><span class="workspace-trace-group__count">${items.length}</span></summary><div class="workspace-trace-tree">${items.map((item) => renderTraceCard(item, byId)).join('')}</div></details>`;
}
function traceDepth(item, byId, seen = new Set()) {
  if (!item.parentSpanId || !byId.has(item.parentSpanId) || seen.has(item.id)) return 0;
  seen.add(item.id);
  return Math.min(4, 1 + traceDepth(byId.get(item.parentSpanId), byId, seen));
}
function renderTraceCard(item, byId) {
  const depth = traceDepth(item, byId);
  return `<article class="workspace-trace-card" style="--trace-depth:${depth}"><div><strong>${text(item.summary, '未命名事件')}</strong><small>${text([item.type, item.ts, item.seq != null ? `#${item.seq}` : ''].filter(Boolean).join(' · '))}</small></div>${item.detailAvailable && item.detailRef ? `<button type="button" class="workspace-detail-action" data-workspace-action="resolve-detail" data-detail-ref="${escapeHtml(item.detailRef.refId || item.detailRef)}">查看明细</button>` : ''}</article>`;
}
function renderEvidencePane(pane = {}) {
  return `<section class="workspace-pane workspace-pane--evidence" data-pane="evidence"><h3>Evidence</h3>${(pane.items || []).map((item) => { const action = item.detailAction || {}; return `<article class="workspace-evidence-card" data-correlation-status="${escapeHtml(item.correlationStatus || '')}"><h4>${text(item.title, '未命名证据')}</h4><p>${text(item.source, '未知来源')}</p>${action.enabled && action.detailRef ? `<button type="button" class="workspace-detail-action" data-workspace-action="resolve-detail" data-detail-ref="${escapeHtml(action.detailRef.refId || action.detailRef)}">查看明细</button>` : `<button type="button" class="workspace-detail-action" disabled aria-disabled="true">${text(action.reason, '详情不可用')}</button>`}${item.error ? `<p class="workspace-error">${text(item.error.message || item.error.code)}</p>` : ''}</article>`; }).join('')}</section>`;
}
function renderMetadataPane(pane = {}) { return `<section class="workspace-pane workspace-pane--metadata" data-pane="metadata"><div class="workspace-pane__heading"><h3>Metadata</h3><span class="workspace-status workspace-status--${escapeHtml(pane.status || 'unknown')}">${text(pane.status, 'unknown')}</span></div><dl>${Object.keys(pane.items || {}).map((key) => `<div><dt>${text(key)}</dt><dd>${text(pane.items[key])}</dd></div>`).join('')}</dl></section>`; }
function renderExecutionPane(pane = {}) { return `<section class="workspace-pane workspace-pane--execution" data-pane="execution"><div class="workspace-pane__heading"><div><h3>执行结果</h3><p class="workspace-pane__subheading">当前运行的结论与证据</p></div><span class="workspace-status workspace-status--${escapeHtml(pane.status || 'empty')}">${text(pane.status === 'ready' ? '已产生' : '待运行', pane.status || 'empty')}</span></div>${(pane.items || []).length ? (pane.items || []).map((item) => `<article class="workspace-execution-card"><div><strong>${text(item.label, '执行结果')}</strong><span class="workspace-chip">${text(item.status, 'unknown')}</span></div><p>${text(item.text, '暂无结果摘要')}</p>${item.evidenceRefs?.length ? `<div class="workspace-reference-list">${item.evidenceRefs.map((ref) => `<button type="button" class="workspace-reference-link" data-workspace-action="resolve-detail" data-detail-ref="${escapeHtml(String(ref).split('/').pop())}">${text(String(ref).split('/').pop())}</button>`).join('')}</div>` : ''}</article>`).join('') : '<p class="workspace-empty workspace-empty--compact">当前 session 尚无 UniClaw runtime 执行产物</p>'}</section>`; }
function renderDetail(detail = {}) { return `<aside class="workspace-detail-drawer" data-detail-status="${escapeHtml(detail.status || 'idle')}" aria-label="详情"><h3>详情</h3>${detail.current ? `<pre>${text(JSON.stringify(detail.current, null, 2))}</pre>` : `<p>${text(detail.status === 'loading' ? '正在加载详情' : '选择记录查看详情')}</p>`}</aside>`; }
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
