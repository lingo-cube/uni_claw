"""Page cache operations — caches page analysis results per path."""

import time
from dataclasses import dataclass, field
from typing import Any, Dict, List, Optional


@dataclass
class PageCacheInfo:
    """Cached page information."""
    items: List[Dict[str, Any]] = field(default_factory=list)
    timestamp: float = field(default_factory=time.time)
    screen_hash: Optional[str] = None


class PageCacheManager:
    """Reads/writes page analysis cache in the traversal context."""

    def __init__(self, context):
        self._context = context

    def update(self, path: str, page_info: Dict[str, Any]) -> None:
        self._context.page_cache[path] = PageCacheInfo(
            items=page_info.get("items", []),
            timestamp=time.time(),
            screen_hash=page_info.get("hash"),
        )

    def restore(self, path: str) -> Optional[Dict[str, Any]]:
        cached = self._context.page_cache.get(path)
        if cached:
            return {"items": cached.items, "from_cache": True}
        return None
