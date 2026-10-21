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
  const mobile = view?.matchMedia?.('(max-width:760px)');
  let drawerOpen = false;
  let resizing = null;

  function apply() {
    const root = container.querySelector('.workspace');
    if (!root) return;
    root.style.setProperty('--workspace-nav-width', `${preferences.width}px`);
    root.classList.toggle('workspace--navigation-collapsed', !mobile?.matches && preferences.collapsed);
    root.classList.toggle('workspace--navigation-open', Boolean(mobile?.matches && drawerOpen));
    const nav = root.querySelector('.workspace-navigation');
    if (nav) nav.inert = Boolean(mobile?.matches && !drawerOpen);
    const main = root.querySelector('.workspace-main');
    if (main) main.inert = Boolean(mobile?.matches && drawerOpen);
    root.querySelector('.workspace-mobile-nav-toggle')?.setAttribute('aria-expanded', String(Boolean(drawerOpen && mobile?.matches)));
    const toggle = root.querySelector('.workspace-navigation__toggle');
    if (toggle) {
      toggle.textContent = mobile?.matches ? '收起任务栏' : preferences.collapsed ? '展开任务栏' : '隐藏任务栏';
      toggle.setAttribute('aria-expanded', String(mobile?.matches ? drawerOpen : !preferences.collapsed));
    }
  }

  function persist() { writePreferences(storage || view?.localStorage, preferences); }
  function toggle() {
    if (mobile?.matches) {
      drawerOpen = !drawerOpen;
      apply();
      container.querySelector(drawerOpen ? '.workspace-navigation__toggle' : '.workspace-mobile-nav-toggle')?.focus({ preventScroll: true });
    } else { preferences.collapsed = !preferences.collapsed; persist(); apply(); }
  }
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
  const onSelect = (event) => {
    if (mobile?.matches && event.target?.closest?.('[data-workspace-action="select-task"]')) { drawerOpen = false; apply(); }
  };
  const onEscape = (event) => {
    if (event.key === 'Escape' && mobile?.matches && drawerOpen) { event.preventDefault(); toggle(); }
    if (event.key === 'Tab' && mobile?.matches && drawerOpen) {
      const buttons = [...container.querySelectorAll('.workspace-navigation button:not(:disabled)')].filter((node) => node.getClientRects().length);
      const first = buttons[0], last = buttons.at(-1);
      if (event.shiftKey && ownerDocument.activeElement === first) { event.preventDefault(); last?.focus(); }
      else if (!event.shiftKey && ownerDocument.activeElement === last) { event.preventDefault(); first?.focus(); }
    }
  };
  container.addEventListener('click', onSelect);
  container.addEventListener('keydown', onEscape);
  mobile?.addEventListener('change', apply);
  container.addEventListener('pointerdown', (event) => {
    const target = event.target?.closest?.('[data-workspace-resize="navigation"]');
    if (target && container.contains(target)) startResize(event);
  });
  container.addEventListener('keydown', (event) => {
    const target = event.target?.closest?.('[data-workspace-resize="navigation"]');
    if (target && container.contains(target)) keyboardResize(event);
  });
  apply();
  return Object.freeze({ apply, toggle, setWidth, getPreferences: () => ({ ...preferences }), dispose: () => {
    stopResize();
    container.removeEventListener('click', onSelect);
    container.removeEventListener('keydown', onEscape);
    mobile?.removeEventListener('change', apply);
  } });
}

module.exports = { bindWorkspaceLayout, clampWidth };
