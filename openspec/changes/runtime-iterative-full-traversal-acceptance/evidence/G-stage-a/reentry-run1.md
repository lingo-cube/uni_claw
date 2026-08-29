# Phase 2.6 Re-entry — Run 1 (2026-08-27, after S1+S2+S4)

Conditions met for re-entry (Gate #2): S1/S2/S4 all PASS; corpus-level
one-candidate-per-provable-row green; no Runtime/CURRENT-ACTIVE change (working tree
verified). Campaign ran against the WORKING-TREE perception candidate via a
validation-scoped receipt (`perception-operator-rule-framework/evidence/candidate-receipt-s1s2s4.json`
— shadow-receipt pattern; canonical receipt untouched; identity facts observed live).

## Result: IR-G0 unblock CONFIRMED on the real emulator

- Admission ✓; autonomy ✓ (exactly one accepted start, zero post-admission driver
  calls); four invariants ✓; gates ✓ (real terminal).
- **16 observed frames, 7 viewport-exploration cycles, 7 dispatched actions**: the run
  resolved the root inventory (normalization PASSED — the original IR-G0 blocker is
  gone), scrolled the real Settings root exhaustively (frames seq 2→20 traverse the
  list to its bottom: 'About emulated device' visible), then **dispatched into a child
  page** (recursive descent began: seq 22+ show the 'Wallpaper & style' page).
- Terminal: honest fail-closed at a DEEPER boundary: `post-action transition did not
  settle within 3 fresh observations` (Agent.OpenWorld.cs:2051 — settle requires
  candidate+confirmation frames where the transition predicate resolves the destination
  identity).

## New FDP (E4, evidence in /tmp/p26-frames.json seq 22/23)

The child page 'Wallpaper & style' carries `Navigate up` (structured ImageButton) but
**no `collapsing_toolbar`** — its title is a plain left-margin text ('Choose wallpaper',
x=0.064, topmost, NOT clickable; content rows Gallery/Live Wallpapers/Wallpaper & style
are indented x≈0.19 AND clickable). The binding's graduated resolver
(search_action_bar → collapsing_toolbar → null) returns null on this shape → the
settle predicate never satisfies → correct fail-closed.

Structural repair signal (no literals): TITLE = unique leftmost-column text above the
topmost clickable row → `SettingsSubpage(Choose wallpaper)`. Harness-side capability
extension (F-group area, within this change's authorization) — dispatched as a bounded
(binding + unit tests; ambiguity → null fail-closed; existing paths/regressions frozen).

## Assessment

The composition chain is now TRAVERSING the real tree (root exhaustion + first descent
both executed). The remaining boundary is page-identity vocabulary breadth in the
harness binding — an iterative-acceptance-shaped gap (exactly what Stage A exists to
surface), not a Runtime or contract gap. AuthorityDelta: NONE (binding-only repair).

---

# Re-entry Runs 2–3 + Defect Diagnosis (appended same day)

## Run 2 (after the binding fix)

Failed at STARTUP: "Open-world specification entry does not match the verified Startup
boundary." — the Settings task stack survived run 1 (top = Wallpaper child page); the
launch intent resumed the EXISTING task instead of resetting to root. Environment
preparation issue, not code: campaigns need a pre-run `am force-stop com.android.settings`
(clean-stack semantics; also relevant for Stage C's clean-emulator requirement).

## Run 3 (clean stack)

**Deepest yet**: 9 viewport decisions + 8 dispatched actions; root exhaustion + descent +
child-page settling PASSED (the R1 title-fallback works — the child page resolved
`SettingsSubpage(Choose wallpaper)` and the settle candidate+confirmation succeeded);
child-page exploration ran; terminal fail-closed at child completeness:
"Unknown interaction affordances remain."

## E4 chain to the true FDP (leader-run diagnostics)

1. Child-page fused candidates (standalone probe): ALL `icon`/`text_block`, **zero
   menu_item** (vs root pages which compose rows) → the capability admits evidence only
   to corroborated/clickable rows → uncorroborated title/caption texts stay Unknown →
   completeness correctly fail-closes.
2. Offline reproduction through `fuse_evidence` on the exact child shapes: same zero
   composition.
3. `row_relation_head.run` DIRECTLY on the same shapes (correct JSON input): composes
   **4 menu_items correctly** — the operator is innocent.
4. Router anchor count on the child state: 0 confirmed → relation-head SHOULD activate.
5. **Root cause**: `engine.py` builds the raw_sources bundle with key `"yolo"`;
   `relation_head_router.py` reads `"detections"` → the real engine path hands
   relation-head an EMPTY detection list (silent no-op). The replay/test path builds its
   own bundle with the router's keys — which is why every corpus/equivalence test stayed
   green while the real engine never composed. Defect class: replay≠engine bundle
   construction divergence.

Disposition: defect repair INSIDE S2's acceptance envelope (the sanctioned behavior never
actually fired in the engine path): unified `build_raw_sources`
helper (single source for engine + replay), unified keys, defensive dual-key read, and an
END-TO-END `fuse_evidence` regression on the frozen child-page shapes (the exact failure
shape locked). No behavior-semantics change; equivalence gate must stay green.

---

# Re-entry Run 4 (after S2fix + S2fix2 + R1 + R2 fix stack)

Root page now FULLY composed (all list rows as menu_item: Wallpaper, Accessibility,
Security & privacy, Location, Safety & emergency, Passwords, System, About emulated
device), 7 viewport cycles + 6 dispatches, root scroll exhaustion executed. Honest
fail-closed at ROOT completeness: "Unknown interaction affordances remain."

## FDP (frames run4 seq 2→17)

The blocker is a NEW duplicate-box instance of the original IR-G0 family, on a NON-NAV
line: 2–4 same-text `input` candidates 'Q Search settings' per frame (OCR multi-box,
horizontally offset — under the IoU≥0.6 dedup threshold). On ≥4-anchor frames the
relation-head merge (which suppresses same-text non-nav line dups) never runs (delegated
path = byte-untouched by design), so fusion's own dedup is the only guard — and it misses
horizontally-offset same-line boxes. Downstream, the duplicates become evidence-less
Unknown elements blocking completeness (frozen C# stack).

## Fix dispatched (sanctioned defect repair)

Deterministic same-line same-text NON-NAV dedup at the engine's final candidate assembly
(highest-confidence survivor; tie → larger area; tie → smallest id; suppressed details
recorded). Same text on DIFFERENT lines survives (repeated-labels semantics preserved);
menu_item candidates untouched; equivalence gate must stay byte-green.

## Fix stack so far (re-entry chain)

R1 (binding structural title fallback) → S2fix (shared raw_sources builder + defensive
keys) → S2fix2 (verifier title-column exemption) → R2 (binding title exclusion from
inventory/authorization) → S2fix3 (same-line non-nav dedup, in flight). Each link
leader-verified with locked regressions; candidate receipt refreshed per perception
identity change (shadow-receipt pattern; canonical untouched).

---

# Re-entry Run 5 + the caption-inversion diagnosis

Run 5 (after S2fix3): root fully composed AND clean at top/bottom viewports; honest
fail-closed at root scroll-exhaustion normalization. Frames analysis:

- Frames 9/10 (mid-list viewport) show the v1n-class inversion ON REAL DATA: captions
  promoted to menu heads ('38%used-9.97GBfree', 'Volume, vibration, Do Not Disturb',
  'Dark theme…', 'Home, lock screen', 'On / 1 app…') while their TITLES became
  NonInteractive satellites; the polluted signatures + missing titles destabilized the
  viewport overlap chain (empty overlaps at 4 adjacent pairs) → normalization Unresolved.
- Geometry (frame 9, extracted): every row = title(above)/caption(below), same column
  x1≈0.17; captions are WIDER boxes (longer strings). relation-head's "widest = head"
  rule elects the caption. The corpus never had caption-wider-than-title shapes, so all
  gates passed while the real mid-list viewport inverts.
- Fix (sanctioned defect repair): head election primary = TOPMOST text
  box at the band's title column (title-above-caption is the real layout); width only
  as same-line tiebreak; fail-closed unchanged. Frozen real-geometry regression locks
  the 8-title expectation.
