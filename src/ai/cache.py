"""AI response caching for performance optimization."""

import time
import hashlib
import json
from typing import Any, Dict, Optional, Tuple
from collections import OrderedDict
from datetime import datetime, timedelta


class AIResponseCache:
    """LRU cache for AI responses with TTL support.
    
    Caches AI responses to avoid redundant calls for identical inputs.
    """
    
    def __init__(self, maxsize: int = 100, ttl_seconds: int = 300):
        """Initialize the cache.
        
        Args:
            maxsize: Maximum number of cached responses
            ttl_seconds: Time-to-live for cached entries in seconds
        """
        self.maxsize = maxsize
        self.ttl_seconds = ttl_seconds
        self._cache: OrderedDict[str, Tuple[Any, float]] = OrderedDict()
        
    def _make_key(self, prompt: str, **kwargs) -> str:
        """Create a cache key from prompt and kwargs.
        
        Args:
            prompt: The prompt text
            **kwargs: Additional parameters
            
        Returns:
            Hash string for caching
        """
        key_data = {"prompt": prompt, **kwargs}
        key_str = json.dumps(key_data, sort_keys=True)
        return hashlib.md5(key_str.encode()).hexdigest()
    
    def get(self, prompt: str, **kwargs) -> Optional[Any]:
        """Get cached response if available and not expired.
        
        Args:
            prompt: The prompt text
            **kwargs: Additional parameters
            
        Returns:
            Cached response or None if not found/expired
        """
        key = self._make_key(prompt, **kwargs)
        
        if key not in self._cache:
            return None
            
        response, timestamp = self._cache[key]
        
        # Check if expired
        if time.time() - timestamp > self.ttl_seconds:
            del self._cache[key]
            return None
            
        # Move to end (LRU)
        self._cache.move_to_end(key)
        return response
    
    def set(self, prompt: str, response: Any, **kwargs) -> None:
        """Cache a response.
        
        Args:
            prompt: The prompt text
            response: The response to cache
            **kwargs: Additional parameters
        """
        key = self._make_key(prompt, **kwargs)
        
        # Evict oldest if at capacity
        if len(self._cache) >= self.maxsize and key not in self._cache:
            self._cache.popitem(last=False)
            
        self._cache[key] = (response, time.time())
        
    def clear(self) -> None:
        """Clear all cached entries."""
        self._cache.clear()
        
    def size(self) -> int:
        """Return current cache size."""
        return len(self._cache)


class DebounceTracker:
    """Track recent actions to prevent repetitive AI calls.
    
    Prevents redundant AI calls for similar actions within a short time window.
    """
    
    def __init__(self, window_seconds: float = 30.0):
        """Initialize the tracker.
        
        Args:
            window_seconds: Time window for deduplication
        """
        self.window_seconds = window_seconds
        self._recent_calls: Dict[str, float] = {}
        
    def _make_key(self, action: str, target: Optional[str] = None) -> str:
        """Create a tracking key.
        
        Args:
            action: The action type
            target: Optional target identifier
            
        Returns:
            Key string
        """
        return f"{action}:{target or ''}"
        
    def should_call(self, action: str, target: Optional[str] = None) -> bool:
        """Check if an AI call should be made (not within debounce window).
        
        Args:
            action: The action type
            target: Optional target identifier
            
        Returns:
            True if call should proceed, False if debounced
        """
        key = self._make_key(action, target)
        now = time.time()
        
        if key in self._recent_calls:
            last_call = self._recent_calls[key]
            if now - last_call < self.window_seconds:
                return False
                
        self._recent_calls[key] = now
        return True
        
    def clear(self) -> None:
        """Clear all tracking data."""
        self._recent_calls.clear()
        
    def cleanup_old(self) -> None:
        """Remove entries older than the window."""
        now = time.time()
        cutoff = now - self.window_seconds
        self._recent_calls = {
            k: v for k, v in self._recent_calls.items()
            if v > cutoff
        }


__all__ = ["AIResponseCache", "DebounceTracker"]
