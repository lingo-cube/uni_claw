"""Safety filter for AI advisor outputs.

This module provides the SafetyFilter class, which validates AI-generated
operations to prevent dangerous actions.
"""

import logging
from dataclasses import dataclass, field
from datetime import datetime
from typing import Optional, Set
from enum import Enum


logger = logging.getLogger(__name__)


class SafetyReason(str, Enum):
    """Reason for safety rejection."""

    ACTION_NOT_ALLOWED = "action_not_allowed"
    TEXT_BLOCKED = "text_blocked"
    CONFIDENCE_LOW = "confidence_low"


@dataclass(frozen=True)
class SafetyResult:
    """Result of safety validation."""

    is_safe: bool
    reason: Optional[str] = None
    fallback_node: Optional[dict] = None

    def __post_init__(self):
        """Ensure reason is provided when not safe."""
        if not self.is_safe and not self.reason:
            object.__setattr__(self, "reason", "No reason provided")


@dataclass(frozen=True)
class AuditLogEntry:
    """Entry in safety audit log."""

    timestamp: datetime
    original_operation: dict
    rejection_reason: str
    current_path: list[str]
    action_taken: str


class SafetyFilter:
    """Validates AI advisor outputs to prevent dangerous operations.

    This filter implements a whitelist + blacklist approach:
    - Whitelist: Only allowed operation types
    - Blacklist: Blocked text patterns (e.g., "恢复出厂设置", "清除数据")

    All rejected operations are logged for audit purposes.
    """

    # Allowed operation types (whitelist)
    ALLOWED_ACTIONS: Set[str] = {
        "click",
        "swipe",
        "back",
        "input_text",
        "no_action",
    }

    # Blocked text patterns (blacklist)
    BLOCKED_TEXTS: Set[str] = {
        "恢复出厂设置",
        "清除数据",
        "删除所有",
        "格式化",
        "重置系统",
        "factory reset",
        "clear data",
        "delete all",
        "format",
        "reset system",
    }

    def __init__(self, enable_audit_log: bool = True):
        """Initialize safety filter.

        Args:
            enable_audit_log: Whether to record rejected operations
        """
        self.enable_audit_log = enable_audit_log
        self._audit_log: list[AuditLogEntry] = []

    def validate(
        self,
        node: dict,
        context: Optional[dict] = None,
    ) -> SafetyResult:
        """Validate a traversal node from AI advisor.

        Args:
            node: Node data from AI advisor with 'action' and optional 'text' fields
            context: Optional context dict with current_path

        Returns:
            SafetyResult with validation outcome and fallback if rejected
        """
        current_path = context.get("current_path", []) if context else []

        # Check 1: Operation type whitelist
        action = node.get("action", "")
        if action not in self.ALLOWED_ACTIONS:
            reason = f"Action '{action}' not in whitelist"
            fallback = self._create_fallback_node("Action not allowed")
            self._log_if_enabled(node, reason, current_path, "Used fallback")
            return SafetyResult(is_safe=False, reason=reason, fallback_node=fallback)

        # Check 2: Target text blacklist
        text = node.get("text", "")
        if text and self._is_text_blocked(text):
            reason = f"Text '{text}' matches blacklist"
            fallback = self._create_fallback_node("Text blocked")
            self._log_if_enabled(node, reason, current_path, "Used fallback")
            return SafetyResult(is_safe=False, reason=reason, fallback_node=fallback)

        # All checks passed
        return SafetyResult(is_safe=True, reason=None, fallback_node=None)

    def _is_text_blocked(self, text: str) -> bool:
        """Check if text matches any blocked pattern.

        Args:
            text: Text to check

        Returns:
            True if text is blocked, False otherwise
        """
        text_lower = text.lower()
        for blocked in self.BLOCKED_TEXTS:
            if blocked.lower() in text_lower:
                return True
        return False

    def _create_fallback_node(self, reason: str) -> dict:
        """Create a fallback node that skips the current operation.

        Args:
            reason: Why the original node was rejected

        Returns:
            Fallback node dict with no_action
        """
        return {
            "action": "no_action",
            "reason": f"Safety filter: {reason}",
            "skipped": True,
        }

    def _log_if_enabled(
        self,
        original_operation: dict,
        rejection_reason: str,
        current_path: list[str],
        action_taken: str,
    ) -> None:
        """Log rejected operation if audit logging is enabled.

        Args:
            original_operation: The rejected node data
            rejection_reason: Why it was rejected
            current_path: Current traversal path
            action_taken: What action was taken (e.g., "Used fallback")
        """
        if not self.enable_audit_log:
            return

        entry = AuditLogEntry(
            timestamp=datetime.now(),
            original_operation=original_operation,
            rejection_reason=rejection_reason,
            current_path=current_path,
            action_taken=action_taken,
        )
        self._audit_log.append(entry)
        logger.info(
            f"Safety filter rejected: {rejection_reason} at {current_path} -> {action_taken}"
        )

    def get_audit_log(self) -> list[AuditLogEntry]:
        """Get the audit log of rejected operations.

        Returns:
            List of audit log entries
        """
        return self._audit_log.copy()

    def clear_audit_log(self) -> None:
        """Clear the audit log."""
        self._audit_log.clear()


__all__ = ["SafetyFilter", "SafetyResult", "SafetyReason", "AuditLogEntry"]
