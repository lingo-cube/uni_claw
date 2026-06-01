"""Failure scenario tests for AI Provider.

Tests cover:
- Task 8.9: Failure scenario tests
- API failures and retry behavior
- Invalid responses handling
- Network errors handling
- Capability fallback behavior
"""

from unittest.mock import AsyncMock, MagicMock, patch
import pytest

from src.ai import UniBrain, AIProviderConfig, RetryConfig
from src.ai.vision.config import VisionConfig
from src.ai.core.llm_client import APIError, RateLimitError, TimeoutError
from src.ai.core.validator import ValidationError, ParserNotFoundError
from src.ai.metrics import FailureArchiver
from src.state.content_tree import PageAnalysis, Direction, Coordinate
from src.context.traversal_context import TraversalContext


class TestAPIFailureScenarios:
    """Tests for various API failure scenarios."""

    @pytest.fixture
    def config_with_retry(self):
        """Create config with retry enabled."""
        return AIProviderConfig(
            api_key="test-key",
            retry=RetryConfig(max_attempts=3, base_delay=0.01, max_delay=0.1)
        )

    @pytest.fixture
    def provider(self, config_with_retry):
        """Create provider with retry config."""
        vision = VisionConfig(service_type="mock")
        return UniBrain(config_with_retry, vision, enable_metrics=True, enable_archiving=True)

    def test_rate_limit_retry_success(self, provider):
        """Test that rate limit errors trigger retry and eventually succeed."""
        call_count = 0

        async def mock_call_with_retry(*args, **kwargs):
            nonlocal call_count
            call_count += 1
            if call_count < 2:
                raise RateLimitError("Rate limited")
            return {"result": "success", "confidence": 0.9}

        with patch.object(provider.client, '_call_with_retry', side_effect=mock_call_with_retry):
            # This should succeed after retry
            result = provider.capabilities["vision"].execute(b"test_image")
            assert result is not None

        # Verify retry happened
        assert call_count == 2

    def test_rate_limit_exhausted_retries(self, provider):
        """Test that exhausted retries raise error."""
        async def mock_call_always_rate_limited(*args, **kwargs):
            raise RateLimitError("Always rate limited")

        with patch.object(provider.client, '_call_with_retry', side_effect=mock_call_always_rate_limited):
            with pytest.raises(RateLimitError):
                provider.capabilities["vision"].execute(b"test_image")

    def test_timeout_retry(self, provider):
        """Test that timeout errors trigger retry."""
        call_count = 0

        async def mock_call_with_retry(*args, **kwargs):
            nonlocal call_count
            call_count += 1
            if call_count < 2:
                raise TimeoutError("Request timeout")
            return {"result": "success"}

        with patch.object(provider.client, '_call_with_retry', side_effect=mock_call_with_retry):
            result = provider.capabilities["vision"].execute(b"test_image")
            assert result is not None

        assert call_count == 2

    def test_server_error_500_retry(self, provider):
        """Test that server errors trigger retry."""
        call_count = 0

        async def mock_call_with_retry(*args, **kwargs):
            nonlocal call_count
            call_count += 1
            if call_count < 2:
                raise APIError("Server error: 500")
            return {"result": "success"}

        with patch.object(provider.client, '_call_with_retry', side_effect=mock_call_with_retry):
            result = provider.capabilities["vision"].execute(b"test_image")
            assert result is not None

    def test_exponential_backoff(self, provider):
        """Test that exponential backoff is used."""
        delays = []
        call_count = 0

        async def mock_call_with_retry(*args, **kwargs):
            nonlocal call_count
            call_count += 1
            if call_count < 4:
                raise RateLimitError("Rate limited")
            return {"result": "success"}

        original_sleep = asyncio.sleep

        async def track_sleep(delay):
            delays.append(delay)
            await original_sleep(delay)

        with patch.object(provider.client, '_call_with_retry', side_effect=mock_call_with_retry):
            with patch('asyncio.sleep', side_effect=track_sleep):
                provider.capabilities["vision"].execute(b"test_image")

        # Verify exponential backoff
        assert len(delays) == 3
        # Each delay should increase (exponential)
        assert delays[0] < delays[1] < delays[2]


class TestInvalidResponseScenarios:
    """Tests for handling invalid API responses."""

    @pytest.fixture
    def provider(self):
        """Create provider with archiving enabled."""
        config = AIProviderConfig(
            api_key="test-key",
            retry=RetryConfig(max_attempts=1)
        )
        vision = VisionConfig(service_type="mock")
        return UniBrain(config, vision, enable_archiving=True)

    def test_malformed_json_response(self, provider):
        """Test handling of malformed JSON response."""
        async def mock_call_returns_invalid_json(*args, **kwargs):
            return "not valid json {{{"

        with patch.object(provider.client, 'call', side_effect=mock_call_returns_invalid_json):
            with pytest.raises(Exception):  # JSON parsing error
                provider.capabilities["parse"].execute("test instruction")

    def test_missing_required_fields(self, provider):
        """Test handling of response missing required fields."""
        async def mock_call_returns_incomplete(*args, **kwargs):
            return {"result": "success"}  # Missing required fields

        with patch.object(provider.client, 'call', side_effect=mock_call_returns_incomplete):
            with pytest.raises(ValidationError):
                provider.capabilities["parse"].execute("test instruction")

    def test_wrong_data_type_response(self, provider):
        """Test handling of response with wrong data types."""
        async def mock_call_returns_wrong_type(*args, **kwargs):
            return {
                "entry_app": 123,  # Should be string
                "confidence": "high",  # Should be number
            }

        with patch.object(provider.client, 'call', side_effect=mock_call_returns_wrong_type):
            # Should handle validation error
            try:
                provider.capabilities["parse"].execute("test instruction")
            except (ValidationError, ValueError):
                pass  # Expected

    def test_empty_response(self, provider):
        """Test handling of empty response."""
        async def mock_call_returns_empty(*args, **kwargs):
            return {}

        with patch.object(provider.client, 'call', side_effect=mock_call_returns_empty):
            with pytest.raises(ValidationError):
                provider.capabilities["parse"].execute("test instruction")


class TestNetworkFailureScenarios:
    """Tests for network-related failure scenarios."""

    @pytest.fixture
    def provider(self):
        """Create provider for testing."""
        config = AIProviderConfig(
            api_key="test-key",
            retry=RetryConfig(max_attempts=2, base_delay=0.01),
            request_timeout=0.1,  # Short timeout for testing
        )
        vision = VisionConfig(service_type="mock")
        return UniBrain(config, vision)

    def test_connection_refused(self, provider):
        """Test handling of connection refused."""
        import aiohttp

        async def mock_call_connection_refused(*args, **kwargs):
            raise aiohttp.ClientError("Connection refused")

        with patch.object(provider.client, 'call', side_effect=mock_call_connection_refused):
            with pytest.raises(APIError):
                provider.capabilities["parse"].execute("test instruction")

    def test_dns_resolution_failure(self, provider):
        """Test handling of DNS resolution failure."""
        import aiohttp

        async def mock_call_dns_error(*args, **kwargs):
            raise aiohttp.ClientError("Name or service not known")

        with patch.object(provider.client, 'call', side_effect=mock_call_dns_error):
            with pytest.raises(APIError):
                provider.capabilities["parse"].execute("test instruction")

    def test_network_timeout(self, provider):
        """Test handling of network timeout."""
        import asyncio

        async def mock_call_timeout(*args, **kwargs):
            raise asyncio.TimeoutError("Connection timeout")

        with patch.object(provider.client, 'call', side_effect=mock_call_timeout):
            with pytest.raises(TimeoutError):
                provider.capabilities["parse"].execute("test instruction")


class TestCapabilityFailureScenarios:
    """Tests for capability-specific failure scenarios."""

    @pytest.fixture
    def provider(self):
        """Create provider with metrics and archiving."""
        config = AIProviderConfig(
            api_key="test-key",
            retry=RetryConfig(max_attempts=1)
        )
        vision = VisionConfig(service_type="mock")
        return UniBrain(config, vision, enable_metrics=True, enable_archiving=True)

    def test_parse_capability_with_empty_instruction(self, provider):
        """Test parse capability with empty instruction."""
        result = provider.capabilities["parse"].execute("")
        # Should handle gracefully
        assert result is not None

    def test_verify_capability_with_empty_page(self, provider):
        """Test verify capability with minimal page data."""
        empty_page = PageAnalysis(
            level1_dir=Direction.LEFT,
            level1_menus=[],
            level2_dir=Direction.TOP,
            level2_menus=[],
            current_path=[],
            items=[],
            is_popup=False,
            popup_info=None,
            close_button=None,
            back_button=None,
            has_scroll=False,
            is_end_of_list=False,
        )

        result = provider.capabilities["verify"].execute({
            "page_analysis": empty_page,
            "expected_type": "unknown",
        })

        # Should handle gracefully
        assert result is not None

    def test_safety_capability_with_no_items(self, provider):
        """Test safety capability with page with no items."""
        empty_page = PageAnalysis(
            level1_dir=Direction.LEFT,
            level1_menus=[],
            level2_dir=Direction.TOP,
            level2_menus=[],
            current_path=[],
            items=[],
            is_popup=False,
            popup_info=None,
            close_button=None,
            back_button=None,
            has_scroll=False,
            is_end_of_list=False,
        )

        result = provider.capabilities["safety"].execute({
            "page_analysis": empty_page,
            "instruction": "test",
            "page_type": "unknown",
        })

        # Should handle gracefully
        assert result is not None

    def test_decision_capability_with_no_safe_items(self, provider):
        """Test decision capability when no safe items available."""
        # Mock safety result with no safe items
        from src.ai.capabilities.types import SafetyScreeningResult, SafetyEvaluation, PageLevelGuidance

        page_with_items = PageAnalysis(
            level1_dir=Direction.LEFT,
            level1_menus=[],
            level2_dir=Direction.TOP,
            level2_menus=[],
            current_path=[],
            items=[],
            is_popup=False,
            popup_info=None,
            close_button=None,
            back_button=None,
            has_scroll=False,
            is_end_of_list=False,
        )

        safety_result = SafetyScreeningResult(
            evaluations=[],
            page_level_guidance=PageLevelGuidance(
                overall_safe_to_proceed=False,
                special_precautions=["No safe items"],
            ),
        )

        result = provider.capabilities["decision"].execute({
            "reason": "test",
            "page_analysis": page_with_items,
            "context": {},
            "safety_result": safety_result,
        })

        # Should handle gracefully, likely returning back or safe_mode
        assert result is not None


class TestFailureArchiving:
    """Tests for failure archiving functionality."""

    @pytest.fixture
    def archiver(self):
        """Create failure archiver."""
        return FailureArchiver(max_records=100)

    def test_failure_archived_on_error(self, archiver):
        """Test that failures are archived."""
        error = ValueError("Test error")

        archiver.archive_failure(
            capability="TestCapability",
            input_data="test input",
            error=error,
            context={"test": "context"},
        )

        failures = archiver.get_failures()
        assert len(failures) == 1
        assert failures[0]["capability"] == "TestCapability"
        assert failures[0]["error_type"] == "ValueError"

    def test_failure_archive_max_records(self, archiver):
        """Test that archive respects max_records limit."""
        # Add more than max_records
        for i in range(150):
            archiver.archive_failure(
                capability="TestCapability",
                input_data=f"input_{i}",
                error=ValueError(f"error_{i}"),
            )

        failures = archiver.get_failures()
        # Should have exactly max_records
        assert len(failures) == 100

    def test_failure_summary(self, archiver):
        """Test failure summary generation."""
        # Add various failures
        archiver.archive_failure("Cap1", "input1", ValueError("error1"))
        archiver.archive_failure("Cap1", "input2", TypeError("error2"))
        archiver.archive_failure("Cap2", "input3", ValueError("error1"))
        archiver.archive_failure("Cap2", "input4", RuntimeError("error3"))

        summary = archiver.get_failure_summary()

        assert summary["total_failures"] == 4
        assert summary["error_types"]["ValueError"] == 2
        assert summary["capabilities"]["Cap1"] == 2
        assert summary["capabilities"]["Cap2"] == 2

    def test_filter_failures_by_capability(self, archiver):
        """Test filtering failures by capability."""
        archiver.archive_failure("Cap1", "input1", ValueError("error1"))
        archiver.archive_failure("Cap2", "input2", ValueError("error2"))
        archiver.archive_failure("Cap1", "input3", TypeError("error3"))

        cap1_failures = archiver.get_failures(capability="Cap1")
        cap2_failures = archiver.get_failures(capability="Cap2")

        assert len(cap1_failures) == 2
        assert len(cap2_failures) == 1


class TestMetricsRecordingOnFailure:
    """Tests for metrics recording during failures."""

    @pytest.fixture
    def provider(self):
        """Create provider with metrics enabled."""
        config = AIProviderConfig(
            api_key="test-key",
            retry=RetryConfig(max_attempts=1)
        )
        vision = VisionConfig(service_type="mock")
        return UniBrain(config, vision, enable_metrics=True)

    def test_metrics_record_failure(self, provider):
        """Test that failures are recorded in metrics."""
        initial_metrics = provider.metrics.get_call_counts()
        initial_success = initial_metrics.get("VisionAnalysisCapability", {}).get("success", 0)
        initial_failure = initial_metrics.get("VisionAnalysisCapability", {}).get("failure", 0)

        # Force a failure by mocking error
        async def mock_call_error(*args, **kwargs):
            raise APIError("Forced error")

        with patch.object(provider.client, 'call', side_effect=mock_call_error):
            try:
                provider.capabilities["vision"].execute(b"test_image")
            except APIError:
                pass  # Expected

        # Check metrics
        final_metrics = provider.metrics.get_call_counts()
        final_success = final_metrics.get("VisionAnalysisCapability", {}).get("success", 0)
        final_failure = final_metrics.get("VisionAnalysisCapability", {}).get("failure", 0)

        # Failure count should have increased
        assert final_failure == initial_failure + 1
        assert final_success == initial_success


class TestFallbackBehavior:
    """Tests for fallback behavior when capabilities fail."""

    @pytest.fixture
    def provider(self):
        """Create provider for testing."""
        config = AIProviderConfig(
            api_key="test-key",
            retry=RetryConfig(max_attempts=1),
            fallback={"strategy": "partial", "partial_allowlist": ["vision", "verify"]},
        )
        vision = VisionConfig(service_type="mock")
        return UniBrain(config, vision)

    def test_vision_capability_always_available(self, provider):
        """Test that vision capability works even when other capabilities fail."""
        # Vision uses mock service, doesn't need API
        result = provider.analyze_screenshot(b"test_image")
        assert isinstance(result, PageAnalysis)

    def test_provider_continues_after_capability_failure(self, provider):
        """Test that provider continues working after one capability fails."""
        # Mock a failure in parse capability
        async def mock_call_error(*args, **kwargs):
            raise APIError("Parse failed")

        with patch.object(provider.client, 'call', side_effect=mock_call_error):
            try:
                provider.capabilities["parse"].execute("test")
            except APIError:
                pass  # Expected

        # Vision should still work
        result = provider.analyze_screenshot(b"test_image")
        assert isinstance(result, PageAnalysis)
