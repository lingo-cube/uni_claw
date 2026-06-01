"""AI call caching and timeout mechanisms.

This module provides decorators and utilities for controlling AI calls,
including timeout handling, response caching, and debounce mechanisms.
"""

import functools
import hashlib
import logging
import time
from collections import OrderedDict
from dataclasses import dataclass, field
from typing import Any, Callable, Dict, Optional, Tuple
from datetime import datetime, timedelta


logger = logging.getLogger(__name__)


@dataclass
class AIResponseCache:
    """TTL cache for AI responses.

    This cache stores AI responses with a time-to-live (TTL) to avoid
    redundant AI calls for identical inputs.

    Args:
        maxsize: Maximum number of entries to store
        ttl_seconds: Time-to-live in seconds (default 300 = 5 minutes)
    """

    maxsize: int = 100
    ttl_seconds: int = 300

    _cache: Dict[str, Tuple[Any, datetime]] = field(default_factory=dict)
    _access_order: OrderedDict = field(default_factory=OrderedDict)

    def get(self, key: str) -> Optional[Any]:
        """Get cached value if exists and not expired.

        Args:
            key: Cache key

        Returns:
            Cached value or None if not found/expired
        """
        if key not in self._cache:
            return None

        value, timestamp = self._cache[key]
        now = datetime.now()

        # Check if expired
        if (now - timestamp).total_seconds() > self.ttl_seconds:
            del self._cache[key]
            self._access_order.pop(key, None)
            return None

        # Update access order (LRU)
        self._access_order.move_to_end(key)
        return value

    def put(self, key: str, value: Any) -> None:
        """Store value in cache.

        Args:
            key: Cache key
            value: Value to cache
        """
        now = datetime.now()

        # Evict oldest if at capacity
        if len(self._cache) >= self.maxsize and key not in self._cache:
            oldest_key = next(iter(self._access_order))
            del self._cache[oldest_key]
            self._access_order.pop(oldest_key)

        self._cache[key] = (value, now)
        self._access_order[key] = True
        self._access_order.move_to_end(key)

    def clear(self) -> None:
        """Clear all cache entries."""
        self._cache.clear()
        self._access_order.clear()

    def size(self) -> int:
        """Get current cache size."""
        return len(self._cache)


@dataclass
class DebounceTracker:
    """Track AI call counts for debounce mechanism.

    This tracks how many times AI has been called for the same
    node and exception to prevent infinite loops.

    Limit: Same node, same exception, max 2 calls.
    """

    _counts: Dict[Tuple[str, str], int] = field(default_factory=dict)

    def should_allow(self, node_id: str, exception_type: str, max_calls: int = 2) -> bool:
        """Check if AI call should be allowed based on debounce limit.

        Args:
            node_id: Identifier for the current node
            exception_type: Type of exception being handled
            max_calls: Maximum allowed calls (default 2)

        Returns:
            True if call should be allowed, False if limit exceeded
        """
        key = (node_id, exception_type)
        current_count = self._counts.get(key, 0)

        if current_count >= max_calls:
            return False

        self._counts[key] = current_count + 1
        return True

    def reset(self, node_id: Optional[str] = None) -> None:
        """Reset debounce counters.

        Args:
            node_id: If provided, only reset counters for this node.
                     If None, reset all counters.
        """
        if node_id is None:
            self._counts.clear()
        else:
            keys_to_delete = [k for k in self._counts if k[0] == node_id]
            for key in keys_to_delete:
                del self._counts[key]


def make_cache_key(ui_hash: str, path_hash: str, method_name: str) -> str:
    """Generate cache key for AI response.

    Args:
        ui_hash: Hash of UI state (e.g., screenshot hash)
        path_hash: Hash of current path
        method_name: Name of AI method being called

    Returns:
        Cache key string
    """
    combined = f"{method_name}:{ui_hash}:{path_hash}"
    return hashlib.sha256(combined.encode()).hexdigest()


def ai_call_decorator(
    timeout: float = 30.0,
    cache: Optional[AIResponseCache] = None,
    min_confidence: float = 0.0,
):
    """Decorator for AI advisor methods with timeout and caching.

    Args:
        timeout: Maximum allowed time for AI call in seconds
        cache: Optional AIResponseCache for caching responses
        min_confidence: Minimum confidence threshold (0.0-1.0)

    Returns:
        Decorated function
    """

    def decorator(func: Callable) -> Callable:
        @functools.wraps(func)
        def wrapper(*args, **kwargs):
            # Extract ui_hash and path_hash from kwargs for cache key
            ui_hash = kwargs.pop("_ui_hash", "default")
            path_hash = kwargs.pop("_path_hash", "default")

            # Check cache
            if cache is not None:
                cache_key = make_cache_key(ui_hash, path_hash, func.__name__)
                cached_result = cache.get(cache_key)
                if cached_result is not None:
                    logger.debug(f"Cache hit for {func.__name__}")
                    return cached_result

            # Execute with timeout
            start_time = time.time()
            try:
                result = func(*args, **kwargs)
                elapsed = time.time() - start_time

                if elapsed > timeout:
                    logger.warning(
                        f"AI call {func.__name__} exceeded timeout: {elapsed:.2f}s > {timeout}s"
                    )
                    # Return UNSURE on timeout
                    return _timeout_result(func.__name__)

                # Check confidence threshold for ContainerInference results
                if min_confidence > 0:
                    result = _apply_confidence_check(result, min_confidence)

                # Cache result
                if cache is not None:
                    cache.put(cache_key, result)

                return result

            except Exception as e:
                logger.error(f"AI call {func.__name__} failed: {e}")
                return _timeout_result(func.__name__)

        return wrapper

    return decorator


def _timeout_result(method_name: str) -> Any:
    """Generate default result when AI call times out or fails.

    Args:
        method_name: Name of the AI method

    Returns:
        Appropriate default result based on method
    """
    from .types import DecisionResult, ContainerInference

    if method_name == "infer_container_type":
        return ContainerInference("UNKNOWN", 0.0)
    else:
        # For decide_next_action and handle_exception
        return (DecisionResult.UNSURE, None)


def _apply_confidence_check(result: Any, min_confidence: float) -> Any:
    """Apply confidence threshold check to AI result.

    Args:
        result: Result from AI call
        min_confidence: Minimum confidence threshold

    Returns:
        Original result or default if confidence too low
    """
    from .types import DecisionResult, ContainerInference

    # Check ContainerInference results
    if isinstance(result, ContainerInference):
        if result.confidence < min_confidence:
            logger.info(
                f"Confidence {result.confidence} below threshold {min_confidence}"
            )
            return ContainerInference("UNKNOWN", 0.0)

    # Check tuple results (DecisionResult, node)
    if isinstance(result, tuple) and len(result) == 2:
        decision_result, node = result
        if hasattr(decision_result, 'confidence') and decision_result.confidence < min_confidence:
            return (DecisionResult.UNSURE, None)

    return result


__all__ = [
    "AIResponseCache",
    "DebounceTracker",
    "make_cache_key",
    "ai_call_decorator",
]
