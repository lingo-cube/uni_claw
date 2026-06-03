"""Pytest configuration for uni-claw tests."""

from pathlib import Path
import sys
import pytest

# Add project root to path for all tests
project_root = Path(__file__).parent.parent
sys.path.insert(0, str(project_root))


# ============================================================================
# AI Module Fixtures
# ============================================================================

@pytest.fixture
def mock_provider():
    """Mock AI Provider fixture for testing."""
    from tests.ai.fixtures import MockProvider

    provider = MockProvider(provider_id="test_mock", use_recorded_data=True)
    provider.reset_call_history()
    return provider


@pytest.fixture
def mock_deepseek_provider():
    """Mock DeepSeek Provider fixture."""
    from tests.ai.fixtures import MockDeepSeekProvider

    provider = MockDeepSeekProvider()
    provider.reset_call_history()
    return provider


@pytest.fixture
def mock_claude_provider():
    """Mock Claude Provider fixture."""
    from tests.ai.fixtures import MockClaudeProvider

    provider = MockClaudeProvider()
    provider.reset_call_history()
    return provider


@pytest.fixture
def mock_mimo_provider():
    """Mock MiMo Provider fixture."""
    from tests.ai.fixtures import MockMiMoProvider

    provider = MockMiMoProvider()
    provider.reset_call_history()
    return provider


@pytest.fixture
def all_mock_providers():
    """All mock providers as a dictionary."""
    from tests.ai.fixtures import (
        MockDeepSeekProvider,
        MockClaudeProvider,
        MockMiMoProvider,
    )

    providers = {
        "deepseek": MockDeepSeekProvider(),
        "claude": MockClaudeProvider(),
        "mimo": MockMiMoProvider(),
    }

    # Reset all call histories
    for provider in providers.values():
        provider.reset_call_history()

    return providers


@pytest.fixture
def response_recorder(tmp_path):
    """Response recorder fixture with temp storage."""
    from tests.ai.fixtures import ResponseRecorder

    storage_path = tmp_path / "recordings"
    return ResponseRecorder(storage_path=storage_path)


@pytest.fixture
def response_replayer(response_recorder):
    """Response replayer fixture."""
    from tests.ai.fixtures import ResponseReplayer

    return ResponseReplayer(recorder=response_recorder)


# ============================================================================
# pytest markers
# ============================================================================


def pytest_configure(config):
    """Configure custom pytest markers."""
    config.addinivalue_line(
        "markers", "real_api: Tests that make real API calls (may have cost)"
    )
    config.addinivalue_line(
        "markers", "slow: Tests that take longer to run"
    )
    config.addinivalue_line(
        "markers", "integration: Integration tests"
    )
    config.addinivalue_line(
        "markers", "unit: Unit tests (fast, isolated)"
    )

    # Enable pytest-asyncio mode for async tests
    # This allows async def test functions to work
    try:
        import pytest_asyncio
        config.option.asyncio_mode = "auto"
    except ImportError:
        pass  # pytest-asyncio not installed, async tests will be skipped
