"""Unit tests for SafetyFilter."""

import pytest

from src.safety.filter import SafetyFilter, SafetyResult, SafetyReason, AuditLogEntry


class TestSafetyResult:
    """Tests for SafetyResult."""

    def test_safe_result(self):
        """Test SafetyResult when operation is safe."""
        result = SafetyResult(is_safe=True, reason=None, fallback_node=None)
        assert result.is_safe is True
        assert result.reason is None
        assert result.fallback_node is None

    def test_unsafe_result(self):
        """Test SafetyResult when operation is unsafe."""
        fallback = {"action": "no_action"}
        result = SafetyResult(
            is_safe=False, reason="Action not allowed", fallback_node=fallback
        )
        assert result.is_safe is False
        assert result.reason == "Action not allowed"
        assert result.fallback_node == fallback

    def test_unsafe_requires_reason(self):
        """Test SafetyResult enforces reason when unsafe."""
        result = SafetyResult(is_safe=False)
        assert result.reason is not None


class TestSafetyFilter:
    """Tests for SafetyFilter."""

    def test_allowed_actions(self):
        """Test ALLOWED_ACTIONS contains expected actions."""
        assert "click" in SafetyFilter.ALLOWED_ACTIONS
        assert "swipe" in SafetyFilter.ALLOWED_ACTIONS
        assert "back" in SafetyFilter.ALLOWED_ACTIONS
        assert "input_text" in SafetyFilter.ALLOWED_ACTIONS
        assert "no_action" in SafetyFilter.ALLOWED_ACTIONS

    def test_blocked_texts(self):
        """Test BLOCKED_TEXTS contains dangerous patterns."""
        assert "恢复出厂设置" in SafetyFilter.BLOCKED_TEXTS
        assert "清除数据" in SafetyFilter.BLOCKED_TEXTS
        assert "删除所有" in SafetyFilter.BLOCKED_TEXTS
        assert "format" in SafetyFilter.BLOCKED_TEXTS

    def test_validate_allowed_action(self):
        """Test validation passes for allowed action."""
        filter = SafetyFilter()
        node = {"action": "click", "text": "Settings"}
        result = filter.validate(node)
        assert result.is_safe is True

    def test_validate_blocked_action(self):
        """Test validation fails for blocked action type."""
        filter = SafetyFilter()
        node = {"action": "delete", "text": "Item"}
        result = filter.validate(node)
        assert result.is_safe is False
        assert "not in whitelist" in result.reason
        assert result.fallback_node is not None
        assert result.fallback_node["action"] == "no_action"

    def test_validate_blocked_text(self):
        """Test validation fails for blocked text."""
        filter = SafetyFilter()
        node = {"action": "click", "text": "恢复出厂设置"}
        result = filter.validate(node)
        assert result.is_safe is False
        assert "blacklist" in result.reason.lower() or "blocked" in result.reason.lower()
        assert result.fallback_node is not None

    def test_validate_blocked_text_case_insensitive(self):
        """Test text blocking is case-insensitive."""
        filter = SafetyFilter()
        # Test lowercase
        result1 = filter.validate({"action": "click", "text": "factory reset"})
        assert result1.is_safe is False
        # Test mixed case
        result2 = filter.validate({"action": "click", "text": "Factory Reset"})
        assert result2.is_safe is False

    def test_validate_blocked_text_partial_match(self):
        """Test text blocking works for partial matches."""
        filter = SafetyFilter()
        # "清除数据" should be blocked even if surrounded by other text
        result = filter.validate({"action": "click", "text": "确认清除数据吗？"})
        assert result.is_safe is False

    def test_validate_safe_text(self):
        """Test safe text passes validation."""
        filter = SafetyFilter()
        node = {"action": "click", "text": "Settings"}
        result = filter.validate(node)
        assert result.is_safe is True

    def test_validate_no_text_field(self):
        """Test validation when node has no text field."""
        filter = SafetyFilter()
        node = {"action": "swipe"}
        result = filter.validate(node)
        assert result.is_safe is True

    def test_validate_with_context(self):
        """Test validation with context."""
        filter = SafetyFilter()
        node = {"action": "click", "text": "Settings"}
        context = {"current_path": ["Home", "Settings"]}
        result = filter.validate(node, context)
        assert result.is_safe is True

    def test_audit_log_enabled(self):
        """Test audit log records rejected operations."""
        filter = SafetyFilter(enable_audit_log=True)
        node = {"action": "delete", "text": "Item"}
        filter.validate(node)

        log = filter.get_audit_log()
        assert len(log) == 1
        assert log[0].rejection_reason == "Action 'delete' not in whitelist"
        assert log[0].original_operation == node

    def test_audit_log_disabled(self):
        """Test audit log is not recorded when disabled."""
        filter = SafetyFilter(enable_audit_log=False)
        node = {"action": "delete", "text": "Item"}
        filter.validate(node)

        log = filter.get_audit_log()
        assert len(log) == 0

    def test_audit_log_safe_operations(self):
        """Test safe operations are not logged."""
        filter = SafetyFilter(enable_audit_log=True)
        node = {"action": "click", "text": "Settings"}
        filter.validate(node)

        log = filter.get_audit_log()
        assert len(log) == 0

    def test_clear_audit_log(self):
        """Test clearing audit log."""
        filter = SafetyFilter(enable_audit_log=True)
        filter.validate({"action": "delete", "text": "Item"})
        assert len(filter.get_audit_log()) == 1

        filter.clear_audit_log()
        assert len(filter.get_audit_log()) == 0

    def test_fallback_node_structure(self):
        """Test fallback node has correct structure."""
        filter = SafetyFilter()
        node = {"action": "invalid", "text": "Test"}
        result = filter.validate(node)

        assert result.fallback_node is not None
        assert result.fallback_node["action"] == "no_action"
        assert "skipped" in result.fallback_node
        assert result.fallback_node["skipped"] is True

    def test_multiple_blocked_texts(self):
        """Test multiple blocked text patterns."""
        filter = SafetyFilter()
        blocked_texts = [
            "恢复出厂设置",
            "清除数据",
            "删除所有",
            "格式化",
            "重置系统",
        ]

        for text in blocked_texts:
            result = filter.validate({"action": "click", "text": text})
            assert result.is_safe is False, f"Failed to block: {text}"

    def test_all_allowed_actions(self):
        """Test all whitelisted actions pass validation."""
        filter = SafetyFilter()
        for action in SafetyFilter.ALLOWED_ACTIONS:
            result = filter.validate({"action": action})
            assert result.is_safe is True, f"Failed to allow: {action}"
