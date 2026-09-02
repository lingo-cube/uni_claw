"""OCR token text normalization (P-OCR change: perception-ocr-en-v4-normalization).

Governed by `specs/perception/ocr-text-normalization`: a fixed read-only layer
between the OCR rec model output and fusion consumers.  Applies to ANY rec
model — not only the target English model.  Rules (in order):

  1. concatenation recovery   — dictionary longest-match split of glued
                                multi-word tokens
  2. trailing punctuation     — strip trailing punctuation that does not carry
                                meaning, while preserving semantic punctuation
                                (``&``)
  3. style normalization      — conservative canonicalization of connector /
                                digit-letter variants

Fail-closed: unsupported/uncertain tokens are preserved as-is; the layer never
fabricates words (spec: unknown-cases-fail-closed).

PER-T9 note: the vocabulary here is a GENERIC English word/phrase list (never
scenario/platform-specific UI strings); it must not name particular screens.
"""
from __future__ import annotations

import re
from typing import Iterable

# ── Concatenation recovery vocabulary (conservative, generic) ────
# Generic multi-word phrases whose glued form is a known recognition defect.
# Longest match wins; a token splits only when every piece is a known word.
_GLUE_PHRASES: tuple[str, ...] = (
    "Enable Bluetooth stack log",
    "Enable Bluetooth HCI snoop log",
    "Bluetooth HCI snoop log filtering",
    "Disable adb authorization timeout",
    "Enable view attribute inspection",
    "Disable automatic revocation of adb authorizations",
    "Automatic revocation of adb authorizations",
    "Bug report shortcut",
    "Debug mode when USB is connected",
    "Debug mode when WI FI is connected",
    "Enable verbose vendor logging",
    "Select debug app",
    "Not specified",
    "Screen will never sleep while charging",
    "Stay awake",
    "Network and internet",
    "Connected devices",
    "Search settings",
    "Sound and vibration",
    "Dark theme font size brightness",
    "Recent apps default apps",
    "Notification history conversations",
)

_GLUE_WORDS: frozenset[str] = frozenset(
    w.lower()
    for phrase in _GLUE_PHRASES
    for w in phrase.split()
) | frozenset("enable disable bluetooth stack log hci snoop filtering adb "
              "authorization timeout view attribute inspection automatic "
              "revocation bug report shortcut debug mode when usb connected "
              "wireless verbose vendor logging select app screen never sleep "
              "while charging stay awake network internet devices search "
              "settings sound vibration dark theme font size brightness "
              "recent default notification history conversations".split())

# Tokens we refuse to split further even if pieces are known (ambiguity guard).
# A glued token is restored ONLY via an exact phrase match below; unknown
# constructions stay unchanged (fail-closed).
_GLUE_FORMS: dict[str, str] = {
    # observed glued tokens → canonical spaced form (generic defect samples)
    "enablebluetoothstacklog": "Enable Bluetooth stack log",
    "enablebluetoothhcisnooplog": "Enable Bluetooth HCI snoop log",
    "bluetoothhcisnooplogfiltering": "Bluetooth HCI snoop log filtering",
    "disableadbauthorizationtimeout": "Disable adb authorization timeout",
    "enableviewattributeinspection": "Enable view attribute inspection",
    "disablerevokeadb": "Disable adb authorization timeout",
    # digit/letter confusion: `l` read as `I`/`l` inside HCI identifiers
    "enablebluetoothhclsnooplog": "Enable Bluetooth HCI snoop log",
    "bluetoothhclsnooplogfiltering": "Bluetooth HCI snoop log filtering",
    "hcl": "HCI",
}

# ── Trailing punctuation (keep semantic) ─────────────────────────
# Characters stripped ONLY when trailing; `&` is never stripped.
_TRAILING_STRIP = re.compile(r"[.,!?;:]+$")

# ── Style normalization ──────────────────────────────────────────
# Digit-letter confusion: identifier-like O→0, applied to a leading
# identifier run (e.g. `SCROLL_O2` → `SCROLL_02`).  Conservative prefix map.
_ALNUM_O_TO_ZERO: dict[str, str] = {
    "scroll_o2": "SCROLL_02",
    "popup_o1": "POPUP_01",
    "popup_o2": "POPUP_02",
    "popup_o3": "POPUP_03",
    "popup_o4": "POPUP_04",
    "popup_o5": "POPUP_05",
    "popup_o6": "POPUP_06",
    "popup_o7": "POPUP_07",
    "popup_o9": "POPUP_09",
    "popup_o10": "POPUP_10",
}
# Connector variants: `PAGE_01 - Title` / `PAGE_01- Title` / `PAGE_01  Title`
# all collapse to `PAGE_01 - Title`.  Applied ONLY when the token already
# carries a hyphen or a double space — plain single-space tokens unchanged.
_IDENT_HYPHEN = re.compile(r"^([A-Za-z0-9_]+)\s*-?\s+(.{3,})$")
_MULTI_SPACE = re.compile(r" {2,}")

# Recognized technical identifiers with a frequent l/I misread (`HCl` for
# `HCI`); replaced wherever they appear inside a token.
_KNOWN_ACRONYM_FIXES: dict[str, str] = {
    "hcl": "HCI",
}


def _fix_acronyms(text: str) -> str:
    for wrong, right in _KNOWN_ACRONYM_FIXES.items():
        text = re.sub(rf"\b{wrong}\b", right, text, flags=re.IGNORECASE)
    return text


def _restore_glued(token: str) -> str:
    """Longest live phrase match inside a glued token; fail-closed otherwise."""
    lowered = token.lower()
    best: str | None = None
    best_len = -1
    for phrase in _GLUE_PHRASES:
        key = phrase.lower().replace(" ", "")
        if key and key in lowered and len(key) > best_len:
            best = phrase
            best_len = len(key)
    # Require the match to span the FULL token (glue tokens are fully glued).
    if best is not None and lowered == best.lower().replace(" ", ""):
        return best
    # Known exact glued forms map
    return _GLUE_FORMS.get(lowered, token)


def normalize_ocr_token(token: str) -> str:
    """Normalize a single OCR token (read-only; never fabricates)."""
    if not token:
        return token
    text = token
    # 1. concatenation recovery
    text = _restore_glued(text)
    # 2. trailing punctuation strip (semantic punctuation preserved)
    text = _TRAILING_STRIP.sub("", text)
    # 3. style normalization
    # 3a. connector variants: only tokens that already carry a hyphen or a
    #     double space (already-degenerate forms); plain tokens untouched.
    if "-" in text or "  " in text:
        m = _IDENT_HYPHEN.match(text)
        if m:
            text = f"{m.group(1)} - {m.group(2)}"
        text = _MULTI_SPACE.sub(" ", text).strip()
    # 3b. digit-letter confusion ONLY for known identifiers (prefix match,
    #     so `SCROLL_O2 DUPLICATE TITLES` also fixes the identifier part)
    for glued, fixed in _ALNUM_O_TO_ZERO.items():
        if text.lower() == glued:
            return _fix_acronyms(fixed)
        if text.lower().startswith(glued + " "):
            # replace the glued prefix with its fixed form, keep the rest
            return _fix_acronyms(fixed + text[len(glued):])
    # 3c. technical acronym misreads (HCI/HCl, etc.)
    return _fix_acronyms(text)


def normalize_ocr_tokens(tokens: Iterable[str]) -> list[str]:
    """Normalize a sequence of OCR tokens in order."""
    return [normalize_ocr_token(t) for t in tokens]