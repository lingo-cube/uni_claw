'use strict';

// Short-lived DOM reading state. No source data or product state is retained.
function bindWorkspaceReading(container, { closeInspector } = {}) {
  const document = container.ownerDocument;
  const view = document?.defaultView;
  const drawer = view?.matchMedia?.('(max-width:1200px)');
  const positions = new Map();
  const disclosures = new Map();
  let previous = {};
  let returnFocus = null;

  const root = () => container.querySelector('.workspace');
  const session = () => container.querySelector('.workspace-task-header')?.getAttribute('data-selected-product-session-id') || '';
  const open = () => root()?.classList.contains('workspace--inspector-open');
  const signature = (region) => {
    const last = region.querySelector('.workspace-timeline')?.lastElementChild;
    return last?.textContent || '';
  };
  const key = (region) => `${session()}:${region.getAttribute('data-workspace-scroll')}`;
  function focusRef(element) {
    if (!element || !container.contains(element)) return null;
    const attrs = [...(element.attributes || [])].filter((a) => a.name === 'id' || a.name.startsWith('data-')).map((a) => [a.name, a.value]);
    return attrs.length ? { tag: element.tagName, attrs, start: element.selectionStart, end: element.selectionEnd } : null;
  }
  function focusElement(ref) {
    if (!ref) return;
    const element = [...container.querySelectorAll('[id], [data-workspace-action], [data-inspector-focus], input, textarea, select')]
      .find((node) => node.tagName === ref.tag && ref.attrs.every(([name, value]) => node.getAttribute(name) === value));
    if (!element || element.closest('[hidden], [inert]')) return;
    element.focus?.({ preventScroll: true });
    if (typeof ref.start === 'number' && typeof element.setSelectionRange === 'function') element.setSelectionRange(ref.start, ref.end);
  }
  function capture() {
    previous = { focus: focusRef(document?.activeElement), open: open(), session: session() };
    for (const node of container.querySelectorAll('[data-reading-disclosure]')) {
      disclosures.set(`${session()}:${node.getAttribute('data-reading-disclosure')}`, node.open);
    }
    for (const region of container.querySelectorAll('[data-workspace-scroll]')) {
      if (!region.clientHeight) continue;
      if (region.getAttribute('data-workspace-scroll') === 'conversation' && !region.querySelector('.workspace-message')) continue;
      const top = region.getBoundingClientRect().top;
      const anchor = [...region.querySelectorAll('[data-reading-anchor]')].find((node) => node.getBoundingClientRect().bottom > top);
      positions.set(key(region), {
        top: region.scrollTop,
        follow: region.scrollHeight - region.scrollTop - region.clientHeight < 48,
        signature: signature(region),
        anchor: anchor?.getAttribute('data-reading-anchor'), offset: anchor ? anchor.getBoundingClientRect().top - top : 0,
        hasNew: positions.get(key(region))?.hasNew || false
      });
    }
  }
  function accessibility() {
    const panel = container.querySelector('#workspace-inspector');
    const modal = Boolean(open() && drawer?.matches);
    if (panel) {
      panel.setAttribute('role', modal ? 'dialog' : 'complementary');
      if (modal) panel.setAttribute('aria-modal', 'true'); else panel.removeAttribute('aria-modal');
    }
    for (const node of container.querySelectorAll('.workspace-primary, .workspace-task-header, .workspace-navigation')) {
      // The navigation layout owns its closed-mobile state.
      if (node.classList.contains('workspace-navigation') && !modal) continue;
      node.inert = modal;
    }
  }
  function restore() {
    accessibility();
    for (const node of container.querySelectorAll('[data-reading-disclosure]')) {
      const id = `${session()}:${node.getAttribute('data-reading-disclosure')}`;
      if (disclosures.has(id)) node.open = disclosures.get(id);
    }
    for (const region of container.querySelectorAll('[data-workspace-scroll]')) {
      if (!region.clientHeight) continue;
      const saved = positions.get(key(region));
      const conversation = region.getAttribute('data-workspace-scroll') === 'conversation';
      if (saved) {
        region.scrollTop = saved.follow && conversation ? region.scrollHeight : saved.top;
        if (!saved.follow && saved.anchor) {
          const anchor = [...region.querySelectorAll('[data-reading-anchor]')].find((node) => node.getAttribute('data-reading-anchor') === saved.anchor);
          if (anchor) region.scrollTop += anchor.getBoundingClientRect().top - region.getBoundingClientRect().top - saved.offset;
        }
        if (conversation && !saved.follow && saved.signature !== signature(region)) saved.hasNew = true;
      } else if (conversation) region.scrollTop = region.scrollHeight;
      if (conversation) {
        const button = container.querySelector('[data-workspace-action="reading-latest"]');
        if (button) button.hidden = !saved?.hasNew;
      }
    }
    if (open() && !previous.open) {
      returnFocus = previous.focus;
      container.querySelector('[data-inspector-focus]')?.focus({ preventScroll: true });
    } else if (!open() && previous.open && previous.session === session()) focusElement(returnFocus);
    else focusElement(previous.focus);
  }
  function onScroll(event) {
    const region = event.target;
    if (region?.getAttribute?.('data-workspace-scroll') !== 'conversation') return;
    if (region.scrollHeight - region.scrollTop - region.clientHeight >= 48) return;
    const saved = positions.get(key(region));
    if (saved) saved.hasNew = false;
    const button = container.querySelector('[data-workspace-action="reading-latest"]');
    if (button) button.hidden = true;
  }
  function onClick(event) {
    if (!event.target.closest?.('[data-workspace-action="reading-latest"]')) return;
    const region = container.querySelector('[data-workspace-scroll="conversation"]');
    if (region) { region.scrollTop = region.scrollHeight; onScroll({ target: region }); }
  }
  function onKey(event) {
    const tabs = [...container.querySelectorAll('[data-workspace-action="select-view"]')];
    if (tabs.includes(event.target) && ['ArrowLeft', 'ArrowRight'].includes(event.key)) {
      event.preventDefault();
      const next = tabs[(tabs.indexOf(event.target) + 1) % tabs.length];
      const id = next?.id;
      next?.click();
      if (id) document.getElementById(id)?.focus();
      return;
    }
    if (!open()) return;
    if (event.key === 'Escape') { event.preventDefault(); closeInspector?.(); return; }
    if (event.key !== 'Tab' || !drawer?.matches) return;
    const panel = container.querySelector('#workspace-inspector');
    const elements = [...panel.querySelectorAll('button:not(:disabled), a[href], input, textarea, select, summary, [tabindex="0"]')].filter((node) => node.getClientRects().length);
    const first = elements[0], last = elements.at(-1);
    if (!first) return;
    if (event.shiftKey && (document.activeElement === first || document.activeElement?.matches('[data-inspector-focus]'))) { event.preventDefault(); last.focus(); }
    else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first.focus(); }
  }
  container.addEventListener('scroll', onScroll, true);
  container.addEventListener('click', onClick);
  container.addEventListener('keydown', onKey);
  drawer?.addEventListener('change', accessibility);
  return { capture, restore, dispose() {
    container.removeEventListener('scroll', onScroll, true);
    container.removeEventListener('click', onClick);
    container.removeEventListener('keydown', onKey);
    drawer?.removeEventListener('change', accessibility);
    positions.clear();
    disclosures.clear();
  } };
}
module.exports = { bindWorkspaceReading };
