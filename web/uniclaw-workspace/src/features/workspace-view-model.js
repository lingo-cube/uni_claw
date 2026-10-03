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
      source: options.agentLabel || 'Uni-Agent',
      origin: selectedTask && selectedTask.source || (session && session.source) || null,
      authority: selectedTask && selectedTask.authority || (session && session.authority) || null,
      metadata,
      errors: errorsOf(state.session)
    },
    conversationTimeline: timelinePane(state.timeline, session),
    tracePane: { ...groupedPane(state.traces, 'traces', (item) => item.source || 'unknown'), mode: state.ui && state.ui.traceMode === 'split' ? 'split' : 'combined' },
    evidencePane: evidencePane(state.evidence),
    executionPane: executionPane((state.session && state.session.session) || null),
    metadataPane: { status: state.session && state.session.status || 'idle', items: metadata, errors: errorsOf(state.session) },
    activePane: state.ui && state.ui.activePane || 'trace',
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
    observed: Boolean(task.observed),
    errors: clone(task.errors || [])
  };
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
  return stages.length > 0 ? [{ groupId: 'round-1', round: 1, status: 'legacy', stages }] : [];
}

function normalizeConversationStage(stage) {
  if (!stage || typeof stage !== 'object') return null;
  const kind = stage.kind || stage.role || 'result';
  const result = stage.result && typeof stage.result === 'object' ? stage.result : null;
  return {
    kind,
    role: normalizeRole(stage.role, kind),
    label: stage.label || labelForKind(kind),
    text: stage.objective || stage.summary || stage.text || (result && result.text) || stage.justification || '',
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
      id: item.spanId || item.id || null,
      source,
      type: item.type || item.kind || null,
      label: item.label || null,
      summary: String(summary).split('\n')[0].slice(0, 240),
      text: item.text || null,
      ts: item.ts || null,
      seq: item.seq == null ? null : item.seq,
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
  return { status: pane.status, items, groups, errors: pane.errors, snapshotId: pane.snapshotId, revision: pane.revision, observedAt: pane.observedAt, itemKey: key };
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
  return { status: detail.status || 'idle', enabled: Boolean(productSessionId), current: clone(detail.detail || null), errors: errorsOf(detail), resolve: detail.status === 'loading' ? 'loading' : 'available', returnPane: ['trace', 'evidence'].includes(state.ui && state.ui.detailReturnPane) ? state.ui.detailReturnPane : 'trace' };
}

function mergeMetadata(task, session) {
  const out = {};
  [task && task.metadata, session && session.metadata].forEach((metadata) => {
    if (metadata && typeof metadata === 'object' && !Array.isArray(metadata)) Object.assign(out, clone(metadata));
  });
  return out;
}

function collectNotices(state, selectedTask) {
  const notices = [];
  [state.projects, state.selection && state.selection.taskInstances, state.session, state.timeline, state.traces, state.evidence, state.detail]
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

function page(value) { return { status: value && value.status || 'idle', items: Array.isArray(value && value.items) ? value.items : [], errors: errorsOf(value), snapshotId: value && value.snapshotId, revision: value && value.revision, observedAt: value && value.observedAt }; }
function errorsOf(value) { return Array.isArray(value && value.errors) ? value.errors : []; }
function combineStatus(a, b) { const statuses = [a && a.status, b && b.status]; return statuses.includes('error') ? 'error' : statuses.includes('loading') ? 'loading' : statuses.includes('partial') ? 'partial' : statuses[0] || 'idle'; }
function normalizeKind(kind) { const value = String(kind || '').toLowerCase(); return value.includes('request') ? 'request' : value.includes('decision') || value.includes('think') || value.includes('plan') ? 'decision' : 'result'; }
function severity(code) { return ['permission-denied', 'unavailable', 'timeout'].includes(code) ? 'error' : 'warning'; }
function clone(value) { return value === undefined ? undefined : JSON.parse(JSON.stringify(value)); }

module.exports = { createWorkspaceViewModel };
