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
# Note: Legacy AI fixtures removed in V6.9.1 refactor
# (mock_provider, mock_deepseek_provider, mock_claude_provider,
#  mock_mimo_provider, all_mock_providers, response_recorder, response_replayer)


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
