"""
Test configuration module.

Provides centralized configuration constants for test execution control.
Includes timeout, retry, concurrency, and test ID generation utilities.
"""

from tests.config.constants import Timeout, Retry, Concurrency, ScrollThreshold
from tests.config.test_ids import TestIdGenerator

__all__ = [
    "Timeout",
    "Retry",
    "Concurrency",
    "ScrollThreshold",
    "TestIdGenerator",
]
