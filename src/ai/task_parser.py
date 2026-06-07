"""
Task parser for V6.9 natural language to intent slots conversion.

This module provides heuristic-based extraction of IntentSlots from
natural language task descriptions. AI integration is planned for V6.10.
"""

from typing import Optional

from src.graph.node import IntentSlots


# Chinese app keywords
CHINESE_APP_KEYWORDS = {
    "设置": "settings",
    "显示": "display",
    "屏幕": "screen",
    "声音": "sound",
    "音频": "audio",
    "网络": "network",
    "wifi": "wifi",
    "蓝牙": "bluetooth",
    "存储": "storage",
    "应用": "apps",
    "应用程序": "apps",
    "微信": "wechat",
    "相册": "gallery",
    "照片": "photos",
}

# English app keywords
ENGLISH_APP_KEYWORDS = {
    "settings": "settings",
    "display": "display",
    "screen": "screen",
    "sound": "sound",
    "audio": "audio",
    "network": "network",
    "wifi": "wifi",
    "bluetooth": "bluetooth",
    "storage": "storage",
    "apps": "apps",
    "applications": "apps",
    "wechat": "wechat",
    "gallery": "gallery",
    "photos": "photos",
}

# Search keywords for scope extraction
SEARCH_KEYWORDS = ["找到", "查找", "搜索", "查看", "find", "search", "look for"]

# Partial scope keywords
PARTIAL_KEYWORDS = ["部分", "一些", "partial", "some"]


def parse_task_to_slots(task: str, provider=None) -> IntentSlots:
    """
    Parse natural language task to IntentSlots using heuristic rules.

    V6.9: Uses heuristic extraction. AI integration planned for V6.10.

    Args:
        task: Natural language task description
        provider: Unused in V6.9, reserved for future AI integration

    Returns:
        IntentSlots with extracted information
    """
    if not task:
        return IntentSlots()

    task_lower = task.lower()

    # Extract target_app
    target_app = _extract_target_app(task, task_lower)

    # Extract scope
    scope = _extract_scope(task, task_lower)

    # Extract target
    target = _extract_target(task)

    return IntentSlots(
        target_app=target_app,
        scope=scope,
        target=target,
    )


def _extract_target_app(task: str, task_lower: str) -> Optional[str]:
    """Extract target app from task using keyword matching."""
    # Try Chinese keywords first
    for keyword, app_id in CHINESE_APP_KEYWORDS.items():
        if keyword in task_lower:
            return keyword

    # Try English keywords
    for keyword, app_id in ENGLISH_APP_KEYWORDS.items():
        if keyword in task_lower:
            return keyword

    return None


def _extract_scope(task: str, task_lower: str) -> str:
    """Extract traversal scope from task."""
    # Check for search keywords (target_only scope)
    for keyword in SEARCH_KEYWORDS:
        if keyword in task_lower:
            return "target_only"

    # Check for partial keywords
    for keyword in PARTIAL_KEYWORDS:
        if keyword in task_lower:
            return "partial"

    # Default to full traversal
    return "full"


def _extract_target(task: str) -> Optional[str]:
    """
    Extract target text from task.

    Looks for text after search keywords like "找到", "查找", etc.
    """
    for keyword in SEARCH_KEYWORDS:
        if keyword in task:
            idx = task.find(keyword)
            # Get text after keyword
            target = task[idx + len(keyword):].strip()

            # Strip trailing punctuation
            for punct in ["。", ".", "！", "!"]:
                if target.endswith(punct):
                    target = target[:-len(punct)].strip()

            if target:
                return target

    return None
