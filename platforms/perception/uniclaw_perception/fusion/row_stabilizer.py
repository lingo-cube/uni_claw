"""Stateless row stabilizer (WI-CTX / CS-PERCEPTION-ROW-IDENTITY).

The perception layer's YOLO/OCR can emit different text for the same logical
row across frames (space differences, typos, a subtitle read instead of the
title).  This stabilizer recognizes the same row across frames and emits a
stable ``row_id`` for it.

Design (DESIGN-SPEC-row-identity-stabilization, D1-D5, leader-locked):

  * **D1/D3 — Python is stateless.**  The C# Runtime owns row identity memory
    (the ``known_rows`` context, alive for one Run).  Each call receives that
    context and returns a ``row_id`` per candidate; **nothing is retained**.
    Restarting the Python service never affects identity.
  * **D2 — ``row_id`` is the stable identity.**  Assigned by C# as
    ``row_NNN``; once assigned it is immutable for the Run.  ``text`` stays
    human-readable but does not participate in signature matching.
  * **D4 — context transport.**  ``known_rows`` arrives via the HTTP
    ``X-Known-Rows`` header (parsed by the server) as
    ``[{"id": "row_001", "text": "Network & internet"}, ...]``.
  * **D5 — matching (per candidate):**
      1. trigram-normalize (strip whitespace, lowercase);
      2. exact normalized match against a known row -> direct ``row_id``;
      3. trigram Jaccard ``>= 0.75`` + neighbor-context confirm -> ``row_id``;
      4. trigram Jaccard ``>= 0.90`` -> direct ``row_id`` (no context needed);
      5. otherwise -> ``row_id = None`` (new row).
      Ambiguity (two near-equal scores, both ``>= 0.75``, that context cannot
      disambiguate) -> ``row_id = None`` (let C# decide; never guess).

Single-frame safety: with no ``known_rows`` every row is new, so every
candidate is tagged ``row_id = None`` and its ``text`` is untouched
(equivalence-gate compatible).  The fusion engine invokes the stabilizer only
when explicitly opted in (``stabilize=True``); the default single-frame path
never touches the candidates.
"""

from __future__ import annotations

from typing import Any

#: Trigram Jaccard at/above which an observation becomes a candidate for an
#: existing known row (context confirmation required below the direct bar).
_CANDIDATE_THRESHOLD: float = 0.75

#: Trigram Jaccard at/above which an observation directly confirms a known row
#: without neighbor context.
_DIRECT_CONFIRM_THRESHOLD: float = 0.90

#: Neighbor-context trigram Jaccard at/above which a candidate's neighbor is
#: considered a recognized known row (context anchor).
_CONTEXT_CONFIRM_THRESHOLD: float = 0.60

#: Score margin below which two top candidates are "near-equal" (ambiguous).
_AMBIGUITY_MARGIN: float = 0.02


def _normalize(text: str) -> str:
    """Internal comparison form.

    Lowercases and removes ALL whitespace so that space-only differences
    (e.g. ``"Network & internet"`` vs ``"Network&internet"``) compare
    identical.  The emitted ``text`` keeps the original formatting; this form
    is never emitted.
    """
    return "".join(text.lower().split())


def _trigrams(normalized: str) -> frozenset[str]:
    if len(normalized) < 3:
        return frozenset({normalized}) if normalized else frozenset()
    return frozenset(normalized[i:i + 3] for i in range(len(normalized) - 2))


def _jaccard(a: frozenset[str], b: frozenset[str]) -> float:
    if not a or not b:
        return 0.0
    return len(a & b) / len(a | b)


def _center_y(cand: dict[str, Any]) -> float:
    """Vertical center (pixel) of a candidate, with deterministic fallbacks."""
    for key in ("centerPx", "center"):
        value = cand.get(key)
        if isinstance(value, list) and len(value) >= 2:
            try:
                return float(value[1])
            except (TypeError, ValueError):
                pass
    bounds = cand.get("bounds")
    if isinstance(bounds, dict) and "y1" in bounds:
        try:
            return float(bounds["y1"])
        except (TypeError, ValueError):
            pass
    bounds_px = cand.get("boundsPx")
    if isinstance(bounds_px, list) and len(bounds_px) >= 4:
        try:
            return float(bounds_px[1])
        except (TypeError, ValueError):
            pass
    return 0.0


def _context_confirms(
    neighbor_texts: tuple[str | None, str | None],
    known_by_norm: dict[str, list[str]],
    known_trigrams: dict[str, frozenset[str]],
) -> bool:
    """A candidate's above/below neighbor is itself a recognized known row.

    In the stateless model ``known_rows`` carry only ``id`` + ``text`` (no
    recorded neighbor), so context anchoring means: the garbled candidate is
    sandwiched between rows the caller already knows.  A neighbor confirms
    when its normalized text exactly hits a known row, or its trigram Jaccard
    with a known row reaches ``_CONTEXT_CONFIRM_THRESHOLD``.
    """
    above_text, below_text = neighbor_texts
    for neighbor in (above_text, below_text):
        if not neighbor or not neighbor.strip():
            continue
        norm = _normalize(neighbor)
        if norm in known_by_norm:
            return True
        n_tri = _trigrams(norm)
        if not n_tri:
            continue
        for k_tri in known_trigrams.values():
            if _jaccard(n_tri, k_tri) >= _CONTEXT_CONFIRM_THRESHOLD:
                return True
    return False


def stabilize_with_context(
    candidates: list[dict[str, Any]],
    known_rows: list[dict[str, Any]] | None = None,
) -> list[dict[str, Any]]:
    """Stateless row stabilizer: tag each candidate with a ``row_id``.

    Matches ``candidates`` against the caller-provided ``known_rows`` context
    (``[{"id": "row_001", "text": "Network & internet"}, ...]``) using trigram
    Jaccard similarity + neighbor-context anchoring.  Each candidate gains a
    ``row_id`` field: the matched known row's id, or ``None`` for a new row.
    Mutates candidates in place and returns them.  No state is retained.

    With ``known_rows`` falsy every candidate is new (``row_id = None``) and
    its ``text`` is untouched — byte-identical to the single-frame baseline
    (equivalence-gate compatible).
    """
    # No context -> every row is new.  Text is left untouched.
    if not known_rows:
        for cand in candidates:
            cand["row_id"] = None
        return candidates

    # Build deterministic lookup structures from the caller's context.
    # AUDITED (UNKNOWN_AFFORDANCE_BYPASS gate): a normalized text may map to
    # MULTIPLE known rows (same text at different physical positions — e.g.
    # 'Appearance' section header + 'Appearance' label on the Display page).
    # Unique text → direct match; ambiguous text (multiple ids) → return None
    # (let C# decide via position-band; never guess).
    known_by_norm: dict[str, list[str]] = {}
    known_trigrams: dict[str, frozenset[str]] = {}
    for kr in known_rows:
        text = kr.get("text") or ""
        norm = _normalize(text)
        if not norm:
            continue
        row_id = kr.get("id")
        if row_id:
            known_by_norm.setdefault(norm, []).append(row_id)
        known_trigrams.setdefault(norm, _trigrams(norm))

    # Order by vertical center so neighbor = centerY-nearest above/below.
    sorted_cands = sorted(candidates, key=_center_y)

    for i, cand in enumerate(sorted_cands):
        text = cand.get("text") or ""
        if not text.strip():
            cand["row_id"] = None
            continue

        norm = _normalize(text)

        # D5 step 2: exact normalized match. Unique → direct; ambiguous (same
        # text at multiple positions) → None (C# position-band disambiguates).
        if norm in known_by_norm:
            ids = known_by_norm[norm]
            cand["row_id"] = ids[0] if len(ids) == 1 else None
            continue

        # D5 steps 3-5: fuzzy match over all known rows (text → first id).
        text_tri = _trigrams(norm)
        best_id: str | None = None
        best_score = 0.0
        second_best_score = 0.0
        for known_norm, row_ids in known_by_norm.items():
            score = _jaccard(text_tri, known_trigrams[known_norm])
            if score > best_score:
                second_best_score = best_score
                best_score = score
                best_id = row_ids[0] if len(row_ids) == 1 else None  # ambiguous → None
            elif score > second_best_score:
                second_best_score = score

        # D5 step 4: direct confirm (>= 0.90) -> row_id, no context needed.
        if best_score >= _DIRECT_CONFIRM_THRESHOLD:
            cand["row_id"] = best_id
            continue

        # D5 step 3: candidate band (>= 0.75) -> needs neighbor-context confirm.
        if best_score >= _CANDIDATE_THRESHOLD:
            # Ambiguity: two near-equal candidates context cannot disambiguate
            # -> return None (let C# decide; never guess).
            if (
                best_score - second_best_score < _AMBIGUITY_MARGIN
                and second_best_score >= _CANDIDATE_THRESHOLD
            ):
                cand["row_id"] = None
                continue
            above = sorted_cands[i - 1] if i > 0 else None
            below = sorted_cands[i + 1] if i < len(sorted_cands) - 1 else None
            neighbor_texts = (
                (above.get("text") or "") if above else None,
                (below.get("text") or "") if below else None,
            )
            cand["row_id"] = (
                best_id
                if _context_confirms(neighbor_texts, known_by_norm, known_trigrams)
                else None
            )
            continue

        # D5 step 5: below candidate threshold -> brand-new row.
        cand["row_id"] = None

    return candidates
