"""Page fingerprint computation — pure functions, no side effects."""

from typing import Any


class PageSnapshotManager:
    """Computes stable page identity fingerprints.

    Pure functions — callers own the fingerprint lifecycle.
    """

    @staticmethod
    def fingerprint(page_analysis: Any) -> int:
        """Hash of page identity — stable across same-page vision calls.

        Uses sorted (type, name) tuples so element reordering doesn't
        produce a different fingerprint.
        """
        if page_analysis is None or not hasattr(page_analysis, "items"):
            return 0
        elements = tuple(
            sorted(
                (getattr(i, "type", "?"), getattr(i, "name", ""))
                for i in page_analysis.items
            )
        )
        return hash(elements)

    @staticmethod
    def has_changed(before: int, after: int) -> bool:
        """True when fingerprints differ."""
        return before != after
