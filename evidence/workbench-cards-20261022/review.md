# Cards review
- Scope relative to entry snapshot: renderer, CSS, renderer tests, generated client and README only. Controller/ViewModel/Host/Runtime are untouched. cards-only.patch preserves the preceding layout changes.
- Authority: existing source fields only. Decision rationale never falls back to nested submission text. Tool status is read from status/isError; receipt acceptance never implies device action completion.
- Presentation: quiet user/agent hierarchy; numbered steps; compact tool header; long/structured output in native details; errors always outside disclosure. Source-record inspect remains available.
- Interaction: native summary keyboard control; stable IDs and disclosure keys reuse existing ephemeral reading state. Uninspectable rows without event IDs use rendering position for distinct keys.
- Catalog: all five original registry fields retained; existing report/diagnosis commands and gates unchanged. DSH does not provide ToolInvoke; catalog tested with standalone renderer fixture only, no new authority introduced.
- Escaping: external body/labels/attributes use escapeHtml; SVG paths are fixed local constants.
- Visual inspection: desktop.png, mobile.png and catalog-mobile.png were viewed. Long names wrap at 390px; messages, steps, receipts and error styling remain distinct. Intermediate captures are not deployment evidence.
- Boundary: isolated worktree delivery complete; shared integration remains blocked under WORKBENCH-LAYOUT-001. No running service changed.
