"""YOLO label aliases — raw Deki-Yolo labels → canonical perception labels.

Extracted from backends.py YOLO_LABEL_ALIASES. Preserved exactly.
"""
from __future__ import annotations

YOLO_LABEL_ALIASES = {
    "backgroundimage": "image",
    "bottom_navigation": "tab",
    "card": "list_item",
    "checkbox": "checkbox",
    "checkedtextview": "checkbox",
    "drawer": "toolbar",
    "edittext": "input",
    "icon": "icon",
    "image": "image",
    "imageview": "icon",       # deki-yolo: ImageView → icon
    "line": "icon",            # deki-yolo: Line → icon (decorative, not interactive)
    "map": "image",
    "modal": "popup",
    "multi_tab": "tab",
    "pageindicator": "icon",
    "remember": "checkbox",
    "spinner": "input",
    "switch": "switch",
    "text": "text_block",
    "textbutton": "button",
    "view": "list_item",       # deki-yolo: View container → list_item
    "toolbar": "toolbar",
    "uppertaskbar": "toolbar",
}


def normalize_yolo_label(label: str) -> str:
    """Normalize a raw YOLO class label to a canonical perception label."""
    key = label.strip().replace("-", "_").replace(" ", "_").lower()
    return YOLO_LABEL_ALIASES.get(key, key)
