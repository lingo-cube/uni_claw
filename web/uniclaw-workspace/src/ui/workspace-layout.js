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
  let resizing = null;

  function apply() {
    const root = container.querySelector('.workspace');
    if (!root) return;
    root.style.setProperty('--workspace-nav-width', `${preferences.width}px`);
    root.classList.toggle('workspace--navigation-collapsed', preferences.collapsed);
    const toggle = root.querySelector('.workspace-navigation__toggle');
    if (toggle) {
      toggle.textContent = preferences.collapsed ? '展开任务栏' : '隐藏任务栏';
      toggle.setAttribute('aria-expanded', String(!preferences.collapsed));
    }
  }

  function persist() { writePreferences(storage || view?.localStorage, preferences); }
  function toggle() { preferences.collapsed = !preferences.collapsed; persist(); apply(); }
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
  container.addEventListener('pointerdown', (event) => {
    const target = event.target?.closest?.('[data-workspace-resize="navigation"]');
    if (target && container.contains(target)) startResize(event);
  });
  container.addEventListener('keydown', (event) => {
    const target = event.target?.closest?.('[data-workspace-resize="navigation"]');
    if (target && container.contains(target)) keyboardResize(event);
  });
  apply();
  return Object.freeze({ apply, toggle, setWidth, getPreferences: () => ({ ...preferences }), dispose: stopResize });
}

module.exports = { bindWorkspaceLayout, clampWidth };
