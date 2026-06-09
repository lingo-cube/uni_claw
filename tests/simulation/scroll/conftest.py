"""
Pytest配置文件 - V7.0-SimScroll测试

提供共享fixtures和测试配置
"""

import pytest
from pathlib import Path


@pytest.fixture
def fixtures_path():
    """获取测试fixtures路径"""
    return Path(__file__).parent.parent.parent.parent / "fixtures" / "scroll"


@pytest.fixture
def wifi_list_fixtures(fixtures_path):
    """加载wifi_list测试数据"""
    import json
    with open(fixtures_path / "wifi_list.json") as f:
        return json.load(f)


@pytest.fixture
def empty_list_fixtures(fixtures_path):
    """加载empty_list测试数据"""
    import json
    with open(fixtures_path / "empty_list.json") as f:
        return json.load(f)


@pytest.fixture
def duplicate_elements_fixtures(fixtures_path):
    """加载duplicate_elements测试数据"""
    import json
    with open(fixtures_path / "duplicate_elements.json") as f:
        return json.load(f)


@pytest.fixture
def nested_list_fixtures(fixtures_path):
    """加载nested_list测试数据"""
    import json
    with open(fixtures_path / "nested_list.json") as f:
        return json.load(f)


@pytest.fixture
def mock_vision_service():
    """提供Mock视觉服务的工厂函数"""
    from tests.simulation.scroll.test_scrollable_vision import ScrollableMockVisionService

    def _create(virtual_pages=None, adaptive_scroll=True):
        if virtual_pages is None:
            virtual_pages = {}
        return ScrollableMockVisionService(virtual_pages, adaptive_scroll)

    return _create


@pytest.fixture
def mock_action_executor():
    """提供Mock动作执行器的工厂函数"""
    from tests.simulation.scroll.test_scenarios import MockActionExecutor

    def _create(vision_service=None):
        if vision_service is None:
            # 创建默认vision service
            from tests.simulation.scroll.test_scenarios import ScrollableMockVisionService
            vision_service = ScrollableMockVisionService({})
        return MockActionExecutor(vision_service)

    return _create


def pytest_configure(config):
    """Pytest配置"""
    config.addinivalue_line(
        "markers", "scroll: 标记滚动相关测试"
    )
    config.addinivalue_line(
        "markers", "scenario: 标记场景测试"
    )
    config.addinivalue_line(
        "markers", "unit: 标记单元测试"
    )
    config.addinivalue_line(
        "markers", "integration: 标记集成测试"
    )
