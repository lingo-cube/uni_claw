/**
 * dsh-plugin-uniclaw-ui — browser-half visualization for dsh-plugin-uniclaw.
 *
 * Hand-written client bundle (closure-factory format served as
 * /plugins/dsh-plugin-uniclaw-ui/client.js). Registers keyed
 * `conversation.chat.commandview` slot entries so the uniclaw-* commands
 * render as structured cards instead of the generic text fallback.
 *
 * Read-only surface: the component only re-renders `node.outcome.text`
 * (the durably logged CommandResult text) into a classified card. It never
 * re-runs a command, never calls the DriverHost, never writes anything.
 *
 * The factory is deliberately dependency-light: React + the ui-primitives
 * DisclosureRow, all resolved from the client module table at materialization.
 */
window.__ModuleLoader__.load({
  id: '/Users/fran/Documents/Code/spacex/uni_claw/dsh-plugin-uniclaw/client',
  factory: (require) => {
    const React = require('react');
    const { createElement: h } = React;
    const ReactDOMClient = require('react-dom/client');
    const { DisclosureRow } = require('@deepseek-ai/dsh-client-ui-primitives');

    /* ------------------------------------------------------------------ *
     * Parsing helpers — CommandResult text is the durable source of truth.
     * ------------------------------------------------------------------ */

    /** Split `label: value (classification — source)` lines into rows. */
    function parseLabeledLines(text) {
      const rows = [];
      for (const line of String(text ?? '').split('\n')) {
        const m = /^([^:]+):\s*(.*)$/.exec(line);
        if (!m) continue;
        const rest = m[2];
        // Annotation is the LAST balanced top-level parenthesized group
        // (`(classification — truth source)`), which itself may contain
        // parentheses (e.g. "Agent.State (public read model)"). Take the
        // final `(...)` group so nested parens stay inside the annotation.
        const paren = /^(.*?)\s*\(([\s\S]*)\)\s*$/.exec(rest);
        rows.push({
          label: m[1].trim(),
          value: paren ? paren[1].trim() : rest.trim(),
          annotation: paren ? paren[2].trim() : null,
        });
      }
      return rows;
    }

    /** Color chip for a classification token (kernel-fact vs inference vs projection). */
    function classificationColor(annotation) {
      if (!annotation) return { bg: '#f1f5f9', fg: '#334155' };
      if (annotation.includes('kernel-fact')) return { bg: '#dcfce7', fg: '#166534' };
      if (annotation.includes('shadow-inference')) return { bg: '#fed7aa', fg: '#9a3412' };
      if (annotation.includes('derived-read-model')) return { bg: '#dbeafe', fg: '#1e40af' };
      if (annotation.includes('directPublicProjection')) return { bg: '#f1f5f9', fg: '#334155' };
      return { bg: '#f1f5f9', fg: '#334155' };
    }

    /** One key/value row with an optional classification chip. */
    function FieldRow({ label, value, annotation }) {
      const chip = classificationColor(annotation);
      return h('div', {
        style: {
          display: 'flex', alignItems: 'baseline', gap: '8px',
          padding: '4px 0', borderBottom: '1px solid #e2e8f0', fontSize: '13px',
        },
      },
        h('span', { style: { color: '#64748b', width: '180px', flexShrink: 0, fontWeight: 600 } }, label),
        h('span', { style: { color: '#0f172a', fontFamily: 'ui-monospace, monospace', wordBreak: 'break-all', flex: 1 } }, value),
        annotation
          ? h('span', {
              style: {
                background: chip.bg, color: chip.fg, fontSize: '11px',
                padding: '1px 8px', borderRadius: '999px', whiteSpace: 'nowrap', flexShrink: 0,
              },
            }, annotation)
          : null,
      );
    }

    /** Card shell: title, optional running/error state, body. */
    function Card({ name, state, summary, children }) {
      const stateColor = state === 'error' ? '#ef4444' : state === 'running' ? '#f59e0b' : '#10b981';
      return h('div', {
        style: {
          border: '1px solid #e2e8f0', borderRadius: '10px', overflow: 'hidden',
          background: '#ffffff', margin: '4px 0',
        },
      },
        h('div', {
          style: {
            display: 'flex', alignItems: 'center', gap: '8px', padding: '8px 12px',
            background: '#f8fafc', borderBottom: '1px solid #e2e8f0',
          },
        },
          h('span', { style: { width: '8px', height: '8px', borderRadius: '50%', background: stateColor, flexShrink: 0 } }),
          h('span', { style: { fontWeight: 700, color: '#0f172a', fontSize: '13px', fontFamily: 'ui-monospace, monospace' } }, name),
          h('span', { style: { color: '#94a3b8', fontSize: '12px', marginLeft: 'auto' } }, summary ?? ''),
        ),
        h('div', { style: { padding: '10px 12px' } }, children),
      );
    }

    /** Shared props pick from the slot: node (CommandNode) carries the outcome. */
    function baseProps(props) {
      return {
        node: props.node,
        outcome: props.node?.outcome ?? null,
        text: props.node?.outcome?.text ?? null,
      };
    }

    /* ------------------------------------------------------------------ *
     * Per-command card components.
     * ------------------------------------------------------------------ */

    /** /uniclaw-runs-list — one line per run id. */
    function RunListCard(props) {
      const { node, outcome, text } = baseProps(props);
      if (outcome === null) return h(Card, { name: '/uniclaw-runs-list', state: 'running', summary: 'running…' }, '…');
      const runs = String(text ?? '').split('\n').map((s) => s.trim()).filter(Boolean);
      const isError = outcome.kind === 'error';
      return h(Card, { name: '/uniclaw-runs-list', state: isError ? 'error' : 'ok', summary: `${runs.length} run(s)` },
        isError
          ? h('div', { style: { color: '#b91c1c' } }, text)
          : runs.length === 0
            ? h('div', { style: { color: '#94a3b8' } }, '(no runs registered)')
            : h('ul', { style: { margin: 0, paddingLeft: '18px' } },
                runs.map((runId, i) => h('li', {
                  key: i,
                  style: {
                    fontFamily: 'ui-monospace, monospace', fontSize: '13px', color: '#0f172a',
                    padding: '2px 0',
                  },
                }, runId))),
      );
    }

    /** /uniclaw-inspect-run — classified snapshot field table. */
    function InspectRunCard(props) {
      const { node, outcome, text } = baseProps(props);
      if (outcome === null) return h(Card, { name: '/uniclaw-inspect-run', state: 'running', summary: 'running…' }, '…');
      if (outcome.kind === 'error') return h(Card, { name: '/uniclaw-inspect-run', state: 'error', summary: 'error' }, text);
      const rows = parseLabeledLines(text);
      return h(Card, { name: '/uniclaw-inspect-run', state: 'ok', summary: node.name ?? '' },
        rows.length === 0
          ? h('div', { style: { color: '#94a3b8' } }, text)
          : h('div', null, rows.map((r, i) => h(FieldRow, { key: i, ...r }))),
      );
    }

    /** /uniclaw-inspect-trap — expected vs observed mismatch. */
    function InspectTrapCard(props) {
      const { node, outcome, text } = baseProps(props);
      if (outcome === null) return h(Card, { name: '/uniclaw-inspect-trap', state: 'running', summary: 'running…' }, '…');
      if (outcome.kind === 'error') return h(Card, { name: '/uniclaw-inspect-trap', state: 'error', summary: 'error' }, text);
      const rows = parseLabeledLines(text);
      const byLabel = Object.fromEntries(rows.map((r) => [r.label, r.value]));
      const found = byLabel['found'] !== 'false';
      return h(Card, {
        name: '/uniclaw-inspect-trap', state: 'ok',
        summary: found ? `trap: ${byLabel['kind'] ?? 'n/a'}` : 'no active trap',
      },
        !found
          ? h('div', { style: { color: '#10b981', fontWeight: 600 } }, 'No active trap — run is clean.')
          : h('div', null,
              h(FieldRow, { label: 'kind', value: byLabel['kind'] ?? 'n/a', annotation: 'directPublicProjection' }),
              h(FieldRow, { label: 'scope', value: byLabel['scope'] ?? 'n/a' }),
              h('div', {
                style: {
                  display: 'flex', gap: '10px', margin: '8px 0',
                },
              },
                h('div', {
                  style: {
                    flex: 1, borderRadius: '8px', padding: '8px 10px',
                    background: '#dcfce7', border: '1px solid #bbf7d0',
                  },
                },
                  h('div', { style: { fontSize: '11px', color: '#166534', fontWeight: 700 } }, 'expected'),
                  h('div', { style: { fontSize: '13px', fontFamily: 'ui-monospace, monospace', color: '#14532d' } }, byLabel['expected'] ?? 'n/a'),
                ),
                h('div', {
                  style: {
                    flex: 1, borderRadius: '8px', padding: '8px 10px',
                    background: '#fee2e2', border: '1px solid #fecaca',
                  },
                },
                  h('div', { style: { fontSize: '11px', color: '#991b1b', fontWeight: 700 } }, 'observed'),
                  h('div', { style: { fontSize: '13px', fontFamily: 'ui-monospace, monospace', color: '#7f1d1d' } }, byLabel['observed'] ?? 'n/a'),
                ),
              ),
              h(FieldRow, { label: 'source', value: byLabel['source'] ?? 'n/a' }),
              h(FieldRow, { label: 'evidence', value: byLabel['evidence'] ?? 'n/a' }),
              byLabel['lastAction'] ? h(FieldRow, { label: 'lastAction', value: byLabel['lastAction'] }) : null,
            ),
      );
    }

    /** /uniclaw-evidence-open — logical evidence ref metadata. */
    function EvidenceCard(props) {
      const { node, outcome, text } = baseProps(props);
      if (outcome === null) return h(Card, { name: '/uniclaw-evidence-open', state: 'running', summary: 'running…' }, '…');
      if (outcome.kind === 'error') return h(Card, { name: '/uniclaw-evidence-open', state: 'error', summary: 'error' }, text);
      const rows = parseLabeledLines(text);
      const found = rows.find((r) => r.label === 'found')?.value !== 'false';
      return h(Card, { name: '/uniclaw-evidence-open', state: 'ok', summary: found ? 'resolved' : 'unresolved' },
        rows.length === 0
          ? h('div', { style: { color: '#94a3b8' } }, text)
          : h('div', null, rows.map((r, i) => h(FieldRow, { key: i, ...r }))),
      );
    }

    /** /uniclaw-shadow-analyze — classified ShadowAnalysis sections. */
    function ShadowCard(props) {
      const { node, outcome, text } = baseProps(props);
      if (outcome === null) return h(Card, { name: '/uniclaw-shadow-analyze', state: 'running', summary: 'running…' }, '…');
      if (outcome.kind === 'error') return h(Card, { name: '/uniclaw-shadow-analyze', state: 'error', summary: 'error' }, text);
      const lines = String(text ?? '').split('\n');
      const headers = [];
      const body = [];
      let inSection = false;
      for (const line of lines) {
        if (/^(observedFacts|hypotheses|uncertainties|recommendations):\s*$/.test(line)) {
          headers.push({ title: line.replace(':', ''), items: [] });
          inSection = true;
          continue;
        }
        if (!inSection) {
          body.push(line);
          continue;
        }
        const trimmed = line.trim();
        if (trimmed === '') continue;
        headers[headers.length - 1].items.push(trimmed);
      }
      const meta = body.filter((l) => l.trim() !== '');
      const state = String(outcome.kind);

      return h(Card, { name: '/uniclaw-shadow-analyze', state, summary: 'COGNITIVE_INFERENCE' },
        h('div', { style: { marginBottom: '8px' } },
          meta.map((line, i) => {
            const m = /^([^:]+):\s*(.*)$/.exec(line);
            if (!m) return h('div', { key: i, style: { fontSize: '12px', color: '#475569' } }, line);
            return h(FieldRow, { key: i, label: m[1].trim(), value: m[2].trim() });
          }),
        ),
        headers.map((section, si) => h('div', { key: si, style: { marginTop: '10px' } },
          h('div', { style: { fontWeight: 700, fontSize: '12px', color: '#334155', marginBottom: '4px' } }, section.title),
          h('ul', { style: { margin: 0, paddingLeft: '18px' } },
            section.items.map((item, ii) => {
              const chip = /^\[([^\]]+)\]/.exec(item);
              const label = chip ? chip[1] : null;
              const color = classificationColor(label ?? '');
              return h('li', { key: ii, style: { fontSize: '12.5px', color: '#0f172a', padding: '2px 0' } },
                label
                  ? h('span', {
                      style: {
                        display: 'inline-block', background: color.bg, color: color.fg,
                        fontSize: '11px', padding: '0 6px', borderRadius: '4px',
                        marginRight: '6px', fontWeight: 600,
                      },
                    }, label)
                  : null,
                item.replace(/^\[[^\]]+\]\s*/, ''),
              );
            }),
          ),
        )),
      );
    }

    /* ------------------------------------------------------------------ *
     * Control plane — full-screen three-column task console.
     * Data flows through the verified command channel:
     *   ctx.remote.commands.execute(sessionId, '/uniclaw-*') → CommandResult
     * (the same wire the chat commands already use; nothing new on the
     * DriverHost side).
     * ------------------------------------------------------------------ */

    const STATE_COLORS = {
      completed: '#10b981',
      running: '#f59e0b',
      failed: '#ef4444',
      trapped: '#f97316',
      queued: '#94a3b8',
    };
    const STATE_LABELS = {
      completed: '完成', running: '运行中', failed: '失败', trapped: '陷阱', queued: '排队',
    };

    /** One line → `{label, value, annotation}` (same parser as the cards). */
    function parseFieldLine(line) {
      const m = /^([^:]+):\s*(.*)$/.exec(line);
      if (!m) return null;
      const paren = /^(.*?)\s*\(([\s\S]*)\)\s*$/.exec(m[2]);
      return {
        label: m[1].trim(),
        value: paren ? paren[1].trim() : m[2].trim(),
        annotation: paren ? paren[2].trim() : null,
      };
    }

    /** Parse the formatted CommandResult text of one command into rows. */
    function parseRows(text) {
      return String(text ?? '').split('\n')
        .map(parseFieldLine)
        .filter(Boolean);
    }

    /**
     * Run the uniclaw command through the session's command remote.
     * `commands` accepts either the injected `remote.commands` sub-service
     * (has `.execute`) or the whole `remote` face (has `.commands.execute`).
     */
    async function runCommand(commands, sessionId, line) {
      const execute = commands?.commands?.execute ?? commands?.execute;
      if (typeof execute !== 'function') throw new Error('command channel unavailable (remote.commands not injected)');
      const result = await execute(sessionId, line);
      if (!result.ok) throw new Error(`command failed: ${result.error.code}: ${result.error.message}`);
      return result.value?.result ?? null;
    }

    /** Fetch the task list via /uniclaw-runs-list. */
    async function fetchTaskList(remote, sessionId) {
      const res = await runCommand(remote, sessionId, '/uniclaw-runs-list');
      const text = String(res?.text ?? '');
      if (res?.kind !== 'success' || /no runs registered|error/i.test(text)) return [];
      return text.split('\n').map((s) => s.trim()).filter(Boolean);
    }

    /** Fetch one task's snapshot / trap / evidence rows via the read commands. */
    async function fetchTaskDetail(remote, sessionId, runId) {
      const snapshotRes = await runCommand(remote, sessionId, `/uniclaw-inspect-run ${runId}`);
      const trapRes = await runCommand(remote, sessionId, `/uniclaw-inspect-trap ${runId}`);
      const snapshotRows = parseRows(snapshotRes?.text ?? '');
      const trapRows = parseRows(trapRes?.text ?? '');
      const byLabel = (rows) => Object.fromEntries(rows.map((r) => [r.label, r.value]));
      const snap = byLabel(snapshotRows);
      const trap = byLabel(trapRows);
      const state = String(snap.runState ?? 'unknown').replaceAll('"', '');
      const page = String(snap.currentSemanticPage ?? '').replaceAll('"', '');
      // Historical metadata rides the snapshot `diagnostics` line:
      //   diagnostics: executedAt: 2026-08-14 11:19:09; durationMs: 93000; outcome: …
      let history = null;
      const diag = String(snap.diagnostics ?? '');
      if (diag && diag !== 'undefined') {
        const fields = Object.fromEntries(
          diag.split(';').map((s) => s.trim()).filter(Boolean).map((kv) => {
            const i = kv.indexOf(':');
            return i === -1 ? [kv, ''] : [kv.slice(0, i).trim(), kv.slice(i + 1).trim()];
          }),
        );
        if (fields.executedAt || fields.durationMs || fields.outcome) history = fields;
      }
      return {
        runId,
        state,
        page,
        goal: String(snap.currentGoal ?? '').replaceAll('"', ''),
        device: 'emulator-5554',
        scenario: page,
        snapshotRows,
        trapFound: trap.found !== 'false' && trap.kind !== undefined,
        trap: trap.kind ? trapRows : null,
        evidenceLocator: String(snap.latestGoalEvidence ?? '').replaceAll('"', '') || null,
        history,
      };
    }

    /** Parse the formatted `uniclaw-events-after` output into event rows. */
    function parseEventLines(text) {
      const events = [];
      for (const line of String(text ?? '').split('\n')) {
        const m = /^event: (\S+) \[([^\]]+)\] seq=(\d+)(?: obs=(\d+|null))?(.*)$/.exec(line);
        if (!m) continue;
        const kind = m[2];
        const seq = Number(m[3]);
        const tail = m[5] ?? '';
        // Rebuild a readable line: kind + payload highlights.
        const payload = /payload=(\{.*?\})(?: |$)/.exec(tail);
        const refs = /refs=([^\s]+)/.exec(tail);
        const detail = payload ? ` ${payload[1]}` : '';
        const refText = refs ? ` refs=${refs[1]}` : '';
        events.push({
          eventId: m[1],
          kind,
          sequence: seq,
          text: `[${kind}] ${kind}${detail}${refText}`.trim(),
        });
      }
      return events;
    }

    /** Fetch the real RuntimeEvent stream via /uniclaw-events-after (frozen wire). */
    async function fetchEvents(remote, sessionId, runId) {
      try {
        const res = await runCommand(remote, sessionId, `/uniclaw-events-after ${runId}`);
        const text = String(res?.text ?? '');
        if (res?.kind !== 'success') return [];
        return parseEventLines(text);
      } catch {
        return [];
      }
    }

    /* --- Control-plane components (React, createElement only) --- */

    function StatusDot({ state }) {
      return h('span', {
        style: {
          width: '8px', height: '8px', borderRadius: '50%', flexShrink: 0,
          background: STATE_COLORS[state] ?? '#94a3b8',
        },
      });
    }

    function TaskRow({ task, selected, onSelect }) {
      const row = h('button', {
        onClick: () => onSelect(task.runId),
        style: {
          display: 'flex', alignItems: 'center', gap: '8px', width: '100%',
          padding: '8px 10px', border: 'none', borderRadius: '8px', cursor: 'pointer',
          background: selected ? '#e0f2fe' : 'transparent', textAlign: 'left',
          fontFamily: 'inherit', fontSize: '13px',
        },
      },
        h(StatusDot, { state: task.state }),
        h('div', { style: { flex: 1, minWidth: 0 } },
          h('div', { style: { fontWeight: 600, color: '#0f172a', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', fontFamily: 'ui-monospace, monospace' } }, task.runId),
          h('div', { style: { color: '#64748b', fontSize: '11px', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' } }, task.scenario || task.goal || ''),
        ),
        h('span', { style: { color: STATE_COLORS[task.state] ?? '#94a3b8', fontSize: '11px', flexShrink: 0 } }, STATE_LABELS[task.state] ?? task.state),
      );
      return row;
    }

    /** Left column: task list with state filter + live/history grouping. */
    function TaskListPane({ tasks, selected, onSelect, filter, onFilter }) {
      const counts = tasks.reduce((acc, t) => { acc[t.state] = (acc[t.state] ?? 0) + 1; return acc; }, {});
      const filters = ['全部', ...Object.keys(STATE_COLORS)];
      const live = tasks.filter((t) => !t.history);
      const history = tasks.filter((t) => t.history);
      const visible = (list) => list.filter((t) => filter === null || t.state === filter);
      const renderGroup = (title, list) => {
        const items = visible(list);
        if (items.length === 0) return null;
        return h('div', { key: title, style: { marginBottom: '8px' } },
          h('div', {
            style: {
              fontSize: '11px', fontWeight: 700, color: '#94a3b8',
              padding: '6px 10px 2px', textTransform: 'uppercase', letterSpacing: '0.05em',
            },
          }, `${title} (${items.length})`),
          items.map((t) => h(TaskRow, { key: t.runId, task: t, selected: selected === t.runId, onSelect })),
        );
      };
      return h('div', { style: { display: 'flex', flexDirection: 'column', height: '100%', minWidth: 0 } },
        h('div', { style: { padding: '12px 12px 8px', borderBottom: '1px solid #e2e8f0' } },
          h('div', { style: { fontWeight: 700, color: '#0f172a', fontSize: '13px', marginBottom: '8px' } }, '测试任务'),
          h('div', { style: { display: 'flex', flexWrap: 'wrap', gap: '4px' } },
            filters.map((f) => h('button', {
              key: f,
              onClick: () => onFilter(f === '全部' ? null : f),
              style: {
                fontSize: '11px', padding: '2px 8px', borderRadius: '999px', border: '1px solid #e2e8f0', cursor: 'pointer',
                background: filter === (f === '全部' ? null : f) ? '#0284c7' : '#f8fafc',
                color: filter === (f === '全部' ? null : f) ? '#fff' : '#334155',
              },
            }, f === '全部' ? `全部 (${tasks.length})` : `${STATE_LABELS[f] ?? f} (${counts[f] ?? 0})`))),
        ),
        h('div', { style: { flex: 1, overflowY: 'auto', padding: '6px' } },
          renderGroup('实时', live),
          renderGroup('历史', history),
          tasks.length === 0 && h('div', { style: { color: '#94a3b8', padding: '16px', fontSize: '12px' } }, '(no tasks)'),
        ),
      );
    }

    /** One event row in the workbench stream. */
    function EventRow({ event }) {
      const isKernelFact = /kernel-fact/.test(event.text);
      const isFailure = /RunFailed|TrapRaised/.test(event.text);
      return h('div', {
        style: {
          display: 'flex', gap: '8px', padding: '6px 0', borderBottom: '1px solid #f1f5f9',
          alignItems: 'baseline', fontSize: '12.5px',
        },
      },
        h('span', {
          style: {
            width: '8px', height: '8px', borderRadius: '50%', flexShrink: 0, alignSelf: 'center',
            background: isFailure ? '#ef4444' : isKernelFact ? '#10b981' : '#64748b',
          },
        }),
        h('span', { style: { color: isFailure ? '#b91c1c' : '#334155', fontFamily: 'ui-monospace, monospace' } }, event.text),
      );
    }

    /** Center column: selected task workbench. */
    function WorkbenchPane({ task, events, onRefresh, onStartTask, busy }) {
      if (!task) {
        return h('div', { style: { display: 'flex', alignItems: 'center', justifyContent: 'center', height: '100%', color: '#94a3b8', fontSize: '13px' } },
          '← 从左侧选择一个测试任务');
      }
      return h('div', { style: { display: 'flex', flexDirection: 'column', height: '100%', minWidth: 0 } },
        h('div', { style: { padding: '14px 16px', borderBottom: '1px solid #e2e8f0' } },
          h('div', { style: { display: 'flex', alignItems: 'center', gap: '10px' } },
            h(StatusDot, { state: task.state }),
            h('span', { style: { fontWeight: 700, color: '#0f172a', fontFamily: 'ui-monospace, monospace', fontSize: '15px' } }, task.runId),
            h('span', { style: { marginLeft: 'auto', display: 'flex', gap: '6px' } },
              h('button', { onClick: onRefresh, disabled: busy, style: btnStyle }, '刷新'),
              h('button', { onClick: () => onStartTask(task.runId), disabled: busy, style: { ...btnStyle, background: '#0284c7', color: '#fff', borderColor: '#0284c7' } }, '启动场景'),
            ),
          ),
          h('div', { style: { display: 'flex', gap: '12px', marginTop: '8px', fontSize: '12px', color: '#64748b', flexWrap: 'wrap' } },
            h('span', {}, `目标: ${task.goal || '—'}`),
            h('span', {}, `页面: ${task.page || '—'}`),
            h('span', {}, `设备: ${task.device}`),
            h('span', { style: { color: STATE_COLORS[task.state], fontWeight: 600 } }, STATE_LABELS[task.state] ?? task.state),
          ),
        ),
        h('div', { style: { flex: 1, overflowY: 'auto', padding: '12px 16px' } },
          h('div', { style: { fontWeight: 700, color: '#334155', fontSize: '12px', marginBottom: '6px' } }, '实时事件流'),
          events.length === 0
            ? h('div', { style: { color: '#94a3b8', fontSize: '12px' } }, '(暂无事件 — 该任务尚未产生 RuntimeEvent)')
            : events.map((e, i) => h(EventRow, { key: i, event: e })),
        ),
      );
    }

    /** Right column: detail panel for the selected task. */
    function DetailPane({ task }) {
      if (!task) {
        return h('div', { style: { display: 'flex', alignItems: 'center', justifyContent: 'center', height: '100%', color: '#94a3b8', fontSize: '13px' } },
          '选择任务查看详情');
      }
      const sections = [];
      if (task.history) {
        sections.push(h('div', { key: 'history' },
          h('div', { style: sectionTitleStyle }, '历史信息'),
          h('div', {
            style: {
              background: '#f8fafc', border: '1px solid #e2e8f0', borderRadius: '8px',
              padding: '8px 10px', fontSize: '12.5px', color: '#334155',
            },
          },
            Object.entries(task.history).map(([k, v]) =>
              h('div', { key: k, style: { display: 'flex', gap: '8px', padding: '2px 0' } },
                h('span', { style: { color: '#64748b', width: '90px', flexShrink: 0 } }, k),
                h('span', { style: { color: '#0f172a', fontFamily: 'ui-monospace, monospace', wordBreak: 'break-all' } }, String(v)),
              ),
            ),
          ),
        ));
      }
      if (task.snapshotRows.length > 0) {
        sections.push(h('div', { key: 'snapshot' },
          h('div', { style: sectionTitleStyle }, '快照'),
          h('div', null, task.snapshotRows.map((r, i) => h(FieldRow, { key: i, ...r }))),
        ));
      }
      if (task.trap) {
        sections.push(h('div', { key: 'trap', style: { marginTop: '14px' } },
          h('div', { style: sectionTitleStyle }, '活动陷阱'),
          h(FieldRow, { label: 'kind', value: task.trap.find((r) => r.label === 'kind')?.value ?? 'n/a', annotation: 'directPublicProjection' }),
          h('div', { style: { display: 'flex', gap: '10px', margin: '8px 0' } },
            h('div', { style: { flex: 1, borderRadius: '8px', padding: '8px 10px', background: '#dcfce7', border: '1px solid #bbf7d0' } },
              h('div', { style: { fontSize: '11px', color: '#166534', fontWeight: 700 } }, 'expected'),
              h('div', { style: { fontSize: '13px', fontFamily: 'ui-monospace, monospace', color: '#14532d' } }, task.trap.find((r) => r.label === 'expected')?.value ?? 'n/a'),
            ),
            h('div', { style: { flex: 1, borderRadius: '8px', padding: '8px 10px', background: '#fee2e2', border: '1px solid #fecaca' } },
              h('div', { style: { fontSize: '11px', color: '#991b1b', fontWeight: 700 } }, 'observed'),
              h('div', { style: { fontSize: '13px', fontFamily: 'ui-monospace, monospace', color: '#7f1d1d' } }, task.trap.find((r) => r.label === 'observed')?.value ?? 'n/a'),
            ),
          ),
          task.trap.filter((r) => ['source', 'evidence', 'lastAction'].includes(r.label)).map((r, i) => h(FieldRow, { key: i, ...r })),
        ));
      }
      if (task.evidenceLocator) {
        sections.push(h('div', { key: 'evidence', style: { marginTop: '14px' } },
          h('div', { style: sectionTitleStyle }, '证据'),
          h('div', { style: { fontSize: '12.5px', color: '#0f172a', fontFamily: 'ui-monospace, monospace', padding: '4px 0' } }, task.evidenceLocator),
        ));
      }
      return h('div', { style: { padding: '14px 16px', overflowY: 'auto', height: '100%' } }, sections);
    }

    const sectionTitleStyle = { fontWeight: 700, color: '#334155', fontSize: '12px', marginBottom: '6px' };
    const btnStyle = {
      fontSize: '12px', padding: '4px 10px', borderRadius: '6px', cursor: 'pointer',
      border: '1px solid #e2e8f0', background: '#f8fafc', color: '#334155', fontFamily: 'inherit',
    };

    /** Full-screen control plane overlay (three columns). */
    function ControlPlane({ open, onClose, remote, sessionId }) {
      const [tasks, setTasks] = React.useState([]);
      const [selected, setSelected] = React.useState(null);
      const [filter, setFilter] = React.useState(null);
      const [details, setDetails] = React.useState(null);
      const [events, setEvents] = React.useState([]);
      const [busy, setBusy] = React.useState(false);
      const [notice, setNotice] = React.useState(null);
      // Incremental event cursor: last seen sequence per run (bounded polling).
      const lastSeqRef = React.useRef({});

      // Load events for a run; incremental=true fetches only events after the
      // last seen sequence and appends (bounded polling), false replaces the
      // whole stream (task switch / first load).
      const loadEvents = React.useCallback(async (runId, incremental) => {
        if (!remote || !sessionId || !runId) return;
        try {
          const last = incremental ? lastSeqRef.current[runId] : undefined;
          const suffix = last !== undefined ? ` --cursor ${last}` : '';
          const res = await runCommand(remote, sessionId, `/uniclaw-events-after ${runId}${suffix}`);
          const newEvents = parseEventLines(res?.text ?? '');
          if (newEvents.length === 0) return;
          lastSeqRef.current[runId] = Math.max(
            lastSeqRef.current[runId] ?? 0,
            ...newEvents.map((e) => e.sequence),
          );
          setEvents((prev) => {
            if (!incremental) return newEvents;
            const seen = new Set(prev.map((e) => e.eventId));
            return [...prev, ...newEvents.filter((e) => !seen.has(e.eventId))];
          });
        } catch {
          // poll failures are silent; the next tick retries
        }
      }, [remote, sessionId]);

      // Diagnostics: surface exactly why the console cannot fetch, so an
      // empty panel is never silent.
      const diag = [
        `remote: ${typeof remote === 'object' && remote !== null ? 'ok' : String(remote)}`,
        `sessionId: ${String(sessionId ?? '(null)')}`,
        `has commands: ${String(!!(remote && remote.commands && typeof remote.commands.execute === 'function'))}`,
        ...(consoleState.setupError ? [`setup: ${consoleState.setupError}`] : []),
      ].join(' · ');

      const refresh = React.useCallback(async (selectRunId) => {
        if (!remote || !sessionId) return;
        setBusy(true);
        try {
          const ids = await fetchTaskList(remote, sessionId);
          // Enrich every list row with its live state/scenario/goal up front
          // (parallel snapshot reads over the command channel), so the task
          // list renders with correct status dots instead of a default queue.
          const enriched = await Promise.all(ids.map(async (runId) => {
            try {
              const detail = await fetchTaskDetail(remote, sessionId, runId);
              return { runId, state: detail.state, scenario: detail.scenario, goal: detail.goal, history: detail.history };
            } catch {
              return { runId, state: 'queued', scenario: '', goal: '', history: null };
            }
          }));
          setTasks(enriched);
          const target = selectRunId ?? enriched[0]?.runId ?? null;
          if (target) {
            const detail = await fetchTaskDetail(remote, sessionId, target);
            setDetails(detail);
            setSelected(target);
            lastSeqRef.current[target] = undefined;
            await loadEvents(target, false);
          } else {
            setSelected(null);
            setDetails(null);
            setEvents([]);
          }
        } catch (err) {
          setNotice(String(err?.message ?? err));
        } finally {
          setBusy(false);
        }
      }, [remote, sessionId]);

      React.useEffect(() => {
        if (open) void refresh(null);
      }, [open, refresh]);

      // Bounded event polling: while the console is open and a task is
      // selected, incrementally pull new events every 2s (cursor-based).
      React.useEffect(() => {
        if (!open || !selected) return;
        const timer = setInterval(() => {
          void loadEvents(selected, true);
        }, 2000);
        return () => clearInterval(timer);
      }, [open, selected, loadEvents]);

      const selectTask = React.useCallback(async (runId) => {
        setSelected(runId);
        setBusy(true);
        try {
          const detail = await fetchTaskDetail(remote, sessionId, runId);
          setDetails(detail);
          // merge state into the list row
          setTasks((prev) => prev.map((t) => t.runId === runId ? { ...t, state: detail.state, scenario: detail.scenario, goal: detail.goal } : t));
          lastSeqRef.current[runId] = undefined;
          await loadEvents(runId, false);
        } catch (err) {
          setNotice(String(err?.message ?? err));
        } finally {
          setBusy(false);
        }
      }, [remote, sessionId]);

      const startTask = React.useCallback((runId) => {
        setNotice(`[占位] 启动场景 ${runId} —— Kernel 控制入口待接入`);
      }, []);

      if (!open) return null;
      return h('div', {
        style: {
          position: 'fixed', inset: 0, zIndex: 1000, background: '#ffffff',
          display: 'flex', flexDirection: 'column', fontFamily: 'system-ui, -apple-system, sans-serif',
        },
      },
        h('div', {
          style: {
            display: 'flex', alignItems: 'center', gap: '10px', padding: '10px 16px',
            background: '#0f172a', color: '#fff', flexShrink: 0,
          },
        },
          h('span', { style: { fontWeight: 700, fontSize: '14px' } }, 'UniClaw 控制平面'),
          h('span', { style: { color: '#94a3b8', fontSize: '12px' } }, '· UI 自动化测试任务'),
          h('span', { style: { marginLeft: 'auto', display: 'flex', gap: '8px', alignItems: 'center' } },
            h('span', { style: { color: '#7dd3fc', fontSize: '11px', fontFamily: 'ui-monospace, monospace' } }, diag),
            notice && h('span', { style: { color: '#fbbf24', fontSize: '12px', alignSelf: 'center' } }, notice),
            h('button', { onClick: () => void refresh(null), disabled: busy, style: { ...btnStyle, background: '#1e293b', color: '#fff', borderColor: '#334155' } }, '刷新全部'),
            h('button', { onClick: onClose, style: { ...btnStyle, background: '#1e293b', color: '#fff', borderColor: '#334155' } }, '关闭'),
          ),
        ),
        h('div', { style: { flex: 1, display: 'grid', gridTemplateColumns: '280px minmax(0,1fr) 340px', minHeight: 0 } },
          h(TaskListPane, { tasks, selected, onSelect: selectTask, filter, onFilter: setFilter }),
          h('div', { style: { borderLeft: '1px solid #e2e8f0', borderRight: '1px solid #e2e8f0', minWidth: 0 } },
            h(WorkbenchPane, { task: details, events, onRefresh: () => void selectTask(selected), onStartTask: startTask, busy })),
          h(DetailPane, { task: details }),
        ),
      );
    }

    /* ------------------------------------------------------------------ *
     * Plugin body: register the keyed commandview entries + control-plane
     * sidebar entry (full-screen overlay toggled from the sidebar foot).
     * ------------------------------------------------------------------ */

    const NAME = 'dsh-plugin-uniclaw-ui';

    /**
     * Cordis service injection. `remote` makes `ctx.remote` available;
     * `remote.commands` makes `ctx.remote.commands` (the command channel)
     * available — both are required (ui-goal declares `remote` + its sub).
     */
    const inject = ['slots', 'remote', 'remote.commands', 'sessions'];

    const REGISTRATIONS = [
      ['uniclaw-runs-list', RunListCard],
      ['uniclaw-inspect-run', InspectRunCard],
      ['uniclaw-inspect-trap', InspectTrapCard],
      ['uniclaw-evidence-open', EvidenceCard],
      ['uniclaw-shadow-analyze', ShadowCard],
    ];

    /** Module-level control-plane state. `open` lives here (not in React state
     * alone) so the sidebar action survives remounts: clicking the button can
     * trigger sidebar re-renders that unmount/remount this entry, and a pure
     * `useState(false)` would silently reset and close the panel. */
    const consoleState = { open: false, remote: null, sessionId: null, setupError: null };

    /**
     * Native-DOM control-plane launcher. Mounts a fixed-position button on
     * document.body (independent of the sidebar slot tree, so sidebar clicks
     * / re-renders can never unmount or reset it) and renders the full-screen
     * panel through its OWN React root (also independent of the app tree).
     * State lives in this closure — physically cannot be lost to remounts.
     */
    function mountNativeConsole(ctx) {
      // Module-level facts captured from the injected services.
      consoleState.remote = ctx.remote ?? null;
      const sessions = ctx.sessions;
      const readCurrent = () => sessions?.list?.getSnapshot?.()?.current ?? null;
      consoleState.sessionId = readCurrent();
      if (sessions?.list?.subscribe) {
        ctx.effect(() => sessions.list.subscribe(() => {
          consoleState.sessionId = readCurrent();
        }), 'console: session tracking');
      }

      const button = document.createElement('button');
      button.textContent = '▦ 控制平面';
      button.title = 'UniClaw 控制平面';
      button.setAttribute('data-uniclaw-console', 'open');
      Object.assign(button.style, {
        position: 'fixed',
        right: '16px',
        bottom: '16px',
        zIndex: '5000',
        display: 'inline-flex',
        alignItems: 'center',
        gap: '6px',
        padding: '8px 14px',
        borderRadius: '8px',
        border: '1px solid #334155',
        background: '#0f172a',
        color: '#ffffff',
        fontSize: '13px',
        fontFamily: 'system-ui, sans-serif',
        cursor: 'pointer',
        boxShadow: '0 4px 12px rgba(0,0,0,0.25)',
      });
      document.body.appendChild(button);

      const mount = document.createElement('div');
      mount.setAttribute('data-uniclaw-console', 'mount');
      document.body.appendChild(mount);
      const root = ReactDOMClient.createRoot(mount);

      let open = false;
      const render = () => {
        if (!open) {
          root.render(null);
          button.textContent = '▦ 控制平面';
          return;
        }
        button.textContent = '✕ 关闭控制台';
        root.render(
          h(ConsoleErrorBoundary, null,
            h(ControlPlane, {
              open,
              onClose: () => { open = false; render(); },
              remote: consoleState.remote,
              sessionId: consoleState.sessionId,
            })),
        );
      };
      button.addEventListener('click', (event) => {
        event.stopPropagation();
        open = !open;
        render();
      });

      ctx.effect(() => () => {
        try { root.unmount(); } catch { /* best-effort */ }
        try { button.remove(); } catch { /* best-effort */ }
        try { mount.remove(); } catch { /* best-effort */ }
      }, 'console: native teardown');
    }

    /** Minimal error boundary: render errors surface red text instead of a dead panel. */
    class ConsoleErrorBoundary extends React.Component {
      constructor(props) {
        super(props);
        this.state = { error: null };
      }
      static getDerivedStateFromError(error) {
        return { error: String(error?.message ?? error) };
      }
      render() {
        if (this.state.error) {
          return h('div', {
            style: {
              position: 'fixed', inset: 0, zIndex: 2000, background: '#fff',
              display: 'flex', alignItems: 'center', justifyContent: 'center',
              fontFamily: 'ui-monospace, monospace', fontSize: '13px', color: '#b91c1c',
              padding: '24px', whiteSpace: 'pre-wrap',
            },
          }, `控制面板渲染错误:\n${this.state.error}\n\n(请把这段贴给开发者)`);
        }
        return this.props.children;
      }
    }

    function apply(ctx) {
      // Command result cards (existing).
      for (const [commandName, Component] of REGISTRATIONS) {
        ctx.slots.inject(
          'conversation.chat.commandview',
          () => ctx.slots.register({
            name: 'conversation.chat.commandview',
            key: commandName,
          }, Component),
        );
      }

      // Control-plane entry: native-DOM launcher (independent of the slot
      // tree — sidebar re-renders can never unmount it). Guarded so a setup
      // failure never rolls back the command-card registrations.
      try {
        mountNativeConsole(ctx);
      } catch (err) {
        consoleState.setupError = String(err?.message ?? err);
      }
    }

    const exports = { NAME, inject, apply };
    exports.default = exports;
    return exports;
  },
});
