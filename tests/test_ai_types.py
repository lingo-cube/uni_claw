"""Unit tests for AI types."""

import pytest

from src.ai.types import DecisionResult, ContainerInference


class TestDecisionResult:
    """Tests for DecisionResult enum."""

    def test_enum_values(self):
        """Test DecisionResult has correct enum values."""
        assert DecisionResult.SUCCESS.value == "success"
        assert DecisionResult.UNSURE.value == "unsure"
        assert DecisionResult.GIVE_UP.value == "give_up"

    def test_enum_comparison(self):
        """Test DecisionResult enum comparison."""
        assert DecisionResult.SUCCESS == DecisionResult.SUCCESS
        assert DecisionResult.SUCCESS != DecisionResult.UNSURE


class TestContainerInference:
    """Tests for ContainerInference dataclass."""

    def test_basic_creation(self):
        """Test basic ContainerInference creation."""
        inference = ContainerInference(
            container_type="GRID_MENU",
            confidence=0.85,
            matched_template="grid_template_v1",
        )
        assert inference.container_type == "GRID_MENU"
        assert inference.confidence == 0.85
        assert inference.matched_template == "grid_template_v1"

    def test_optional_matched_template(self):
        """Test ContainerInference with optional matched_template."""
        inference = ContainerInference(
            container_type="UNKNOWN",
            confidence=0.0,
        )
        assert inference.matched_template is None

    def test_confidence_validation_valid(self):
        """Test confidence validation for valid values."""
        # Edge cases
        ContainerInference("TEST", 0.0)
        ContainerInference("TEST", 0.5)
        ContainerInference("TEST", 1.0)

    def test_confidence_validation_invalid(self):
        """Test confidence validation rejects invalid values."""
        with pytest.raises(ValueError, match="Confidence must be between 0 and 1"):
            ContainerInference("TEST", -0.1)

        with pytest.raises(ValueError, match="Confidence must be between 0 and 1"):
            ContainerInference("TEST", 1.1)

    def test_frozen_immutability(self):
        """Test ContainerInference is frozen (immutable)."""
        inference = ContainerInference("TEST", 0.5)
        with pytest.raises(Exception):  # FrozenInstanceError
            inference.confidence = 0.8
